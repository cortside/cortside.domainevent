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

namespace Cortside.DomainEvent.EntityFramework {
    public class OutboxHostedService<T> : TimedHostedService where T : DbContext {
        private readonly IServiceProvider serviceProvider;
        private readonly OutboxHostedServiceConfiguration config;

        public OutboxHostedService(ILogger<OutboxHostedService<T>> logger, OutboxHostedServiceConfiguration config, IServiceProvider serviceProvider) : base(logger, config.Enabled, config.Interval, true) {
            this.serviceProvider = serviceProvider;
            this.config = config;
        }

        public override async Task StartAsync(CancellationToken cancellationToken) {
            logger.LogInformation($"OutboxHostedService StartAsync() entered.");
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteIntervalAsync() {
            await Task.Yield();

            var correlationId = CorrelationContext.GetCorrelationId();

            using (var scope = serviceProvider.CreateScope()) {
                var db = serviceProvider.GetService<T>();
                var publisher = serviceProvider.GetService<IDomainEventPublisher>();

                var isRelational = !db.Database.ProviderName.Contains("InMemory");
                await using (var tx = isRelational ? await db.Database.BeginTransactionAsync() : null) {
                    try {
                        List<Outbox> messages;
                        if (isRelational) {
                            var sql = $";with cte as (select top ({config.Interval}) * from Outbox where LockId is null and Status='{OutboxStatus.Queued}' and ScheduledDate<GETUTCDATE() order by ScheduledDate) update cte WITH (XLOCK) set LockId = '{correlationId}', Status='{OutboxStatus.Publishing}'";
                            await db.Database.ExecuteSqlRawAsync(sql);
                            messages = await db.Set<Outbox>().Where(o => o.LockId == correlationId).ToListAsync();
                        } else {
                            messages = await db.Set<Outbox>().ToListAsync();
                        }

                        logger.LogInformation($"message count: {messages.Count}");
                        foreach (var message in messages) {
                            await publisher.SendAsync(message.EventType, message.Address, message.Body, message.CorrelationId, message.MessageId);
                            message.Status = OutboxStatus.Published;
                            message.PublishedDate = DateTime.UtcNow;
                            message.LockId = null;
                        }

                        await db.SaveChangesAsync();
                        await tx?.CommitAsync();
                    } catch (Exception ex) {
                        logger.LogError(ex, "Exception attempting to publish from outbox");
                        await tx?.RollbackAsync();
                    }
                }
            }
        }
    }
}
