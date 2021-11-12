using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cortside.Common.Correlation;
using Cortside.Common.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.EntityFramework.Hosting {

    public class OutboxHostedService<T> : TimedHostedService where T : DbContext {
        private readonly IServiceProvider serviceProvider;
        private readonly OutboxHostedServiceConfiguration config;

        public OutboxHostedService(ILogger<OutboxHostedService<T>> logger, OutboxHostedServiceConfiguration config, IServiceProvider serviceProvider) : base(logger, config.Enabled, config.Interval, true) {
            this.serviceProvider = serviceProvider;
            this.config = config;
        }

        public override Task StartAsync(CancellationToken cancellationToken) {
            logger.LogInformation("OutboxHostedService StartAsync() entered.");
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteIntervalAsync() {
            await Task.Yield();

            var correlationId = CorrelationContext.GetCorrelationId();

            using (var scope = serviceProvider.CreateScope()) {
                var db = scope.ServiceProvider.GetService<T>();
                var isRelational = !db.Database.ProviderName.Contains("InMemory");

                var messageCount = 0;
                if (isRelational) {
                    const string sql = "select count(*) from Outbox with (nolock) where ScheduledDate<GETUTCDATE()";
                    messageCount = await db.ExecuteScalarAsync<int>(sql).ConfigureAwait(false);
                }

                var strategy = db.Database.CreateExecutionStrategy();
                if (!isRelational || messageCount > 0) {
                    await strategy.ExecuteAsync(async () => {
                        await using (var tx = isRelational ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted).ConfigureAwait(false) : null) {
                            try {
                                List<Outbox> messages;
                                if (isRelational) {
                                    var sql = $@";with cte as (select top ({config.BatchSize}) * from Outbox
                                                WITH (UPDLOCK)
                                                where LockId is null and Status='{OutboxStatus.Queued}' and ScheduledDate<GETUTCDATE() order by ScheduledDate)
                                            update cte set LockId = '{correlationId}', Status='{OutboxStatus.Publishing}'
                                            ;select * from outbox where LockId = '{correlationId}'";
                                    messages = await db.Set<Outbox>().FromSqlRaw(sql).ToListAsync().ConfigureAwait(false);
                                } else {
                                    messages = await db.Set<Outbox>().ToListAsync().ConfigureAwait(false);
                                }

                                logger.LogInformation($"message count: {messages.Count}");

                                foreach (var message in messages) {
                                    var properties = new EventProperties() {
                                        EventType = message.EventType,
                                        Topic = message.Topic,
                                        RoutingKey = message.RoutingKey,
                                        CorrelationId = message.CorrelationId,
                                        MessageId = message.MessageId
                                    };

                                    var publisher = scope.ServiceProvider.GetService<IDomainEventPublisher>();
                                    await publisher.PublishAsync(message.Body, properties).ConfigureAwait(false);
                                    message.Status = OutboxStatus.Published;
                                    message.PublishedDate = DateTime.UtcNow;
                                    message.LockId = null;
                                }

                                await db.SaveChangesAsync().ConfigureAwait(false);
                                if (isRelational) {
                                    await (tx?.CommitAsync()).ConfigureAwait(false);
                                }
                            } catch (Exception ex) {
                                logger.LogError(ex, "Exception attempting to publish from outbox");
                                await (tx?.RollbackAsync()).ConfigureAwait(false);
                            }
                        }
                    }).ConfigureAwait(false);
                }

                if (config.PurgePublished) {
                    await strategy.ExecuteAsync(async () => {
                        try {
                            if (isRelational) {
                                var query = $@"DELETE TOP ({config.BatchSize}) FROM Outbox
                                                Where Status = '{OutboxStatus.Published}'";
                                await db.Database.ExecuteSqlRawAsync(query).ConfigureAwait(false);
                            } else {
                                db.RemoveRange(db.Set<Outbox>().Where(o => o.Status == OutboxStatus.Published).Take(config.BatchSize));
                            }
                            await db.SaveChangesAsync().ConfigureAwait(false);
                        } catch (Exception ex) {
                            logger.LogError(ex, "Exception attempting to purge published from outbox");
                        }
                    }).ConfigureAwait(false);
                }
            }
        }
    }
}
