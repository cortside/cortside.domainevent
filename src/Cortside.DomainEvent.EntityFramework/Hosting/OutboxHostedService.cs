using System;
using System.Collections.Generic;
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

        public override async Task StartAsync(CancellationToken cancellationToken) {
            logger.LogInformation("OutboxHostedService StartAsync() entered.");
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        protected override async Task ExecuteIntervalAsync() {
            await Task.Yield();

            var correlationId = CorrelationContext.GetCorrelationId();

            using (var scope = serviceProvider.CreateScope()) {
                var db = scope.ServiceProvider.GetService<T>();
                var publisher = scope.ServiceProvider.GetService<IDomainEventPublisher>();

                var isRelational = !db.Database.ProviderName.Contains("InMemory");
                var strategy = db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () => {
                    await using (var tx = isRelational ? await db.Database.BeginTransactionAsync().ConfigureAwait(false) : null) {
                        try {
                            List<Outbox> messages;
                            if (isRelational) {
                                var sql = $";with cte as (select top ({config.Interval}) * from Outbox where LockId is null and Status='{OutboxStatus.Queued}' and ScheduledDate<GETUTCDATE() order by ScheduledDate) update cte WITH (XLOCK) set LockId = '{correlationId}', Status='{OutboxStatus.Publishing}'";
                                await db.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
                                messages = await db.Set<Outbox>().Where(o => o.LockId == correlationId).ToListAsync().ConfigureAwait(false);
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
                                await publisher.PublishAsync(message.Body, properties).ConfigureAwait(false);
                                message.Status = OutboxStatus.Published;
                                message.PublishedDate = DateTime.UtcNow;
                                message.LockId = null;
                            }

                            await db.SaveChangesAsync().ConfigureAwait(false);
                            await (tx?.CommitAsync()).ConfigureAwait(false);
                        } catch (Exception ex) {
                            logger.LogError(ex, "Exception attempting to publish from outbox");
                            await (tx?.RollbackAsync()).ConfigureAwait(false);
                        }
                    }
                }).ConfigureAwait(false);
            }
        }
    }
}

