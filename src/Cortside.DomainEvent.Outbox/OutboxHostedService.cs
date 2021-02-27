using System;
using System.Linq;
using System.Threading.Tasks;
using Cortside.Common.Correlation;
using Cortside.Common.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.EntityFramework {
    public class OutboxHostedService<T> : TimedHostedService where T : DbContext {
        private readonly IServiceProvider serviceProvider;

        public OutboxHostedService(ILogger<OutboxHostedService<T>> logger, OutboxHostedServiceConfiguration config, IServiceProvider serviceProvider) : base(logger, config.Enabled, config.Interval, true) {
            this.serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteIntervalAsync() {
            var correlationId = CorrelationContext.GetCorrelationId();

            using (var scope = serviceProvider.CreateScope()) {
                var db = serviceProvider.GetService<T>();
                var publisher = serviceProvider.GetService<IDomainEventPublisher>();

                await using (var tx = await db.Database.BeginTransactionAsync()) {
                    try {
                        // TODO: top needs to be configurable
                        await db.Database.ExecuteSqlRawAsync($"UPDATE top (5) Outbox WITH (XLOCK) SET LockId = '{correlationId}', Status='{OutboxStatus.Publishing}'  WHERE LockId is null and Status='{OutboxStatus.Queued}' and ScheduledDate<GETUTCDATE() order by ScheduledDate");

                        var messages = await db.Set<Outbox>().Where(o => o.LockId == correlationId).ToListAsync();
                        logger.LogInformation($"message count: {messages.Count}");
                        foreach (var message in messages) {
                            await publisher.SendAsync(message.EventType, message.EventType, message.Body, message.CorrelationId, message.MessageId);
                            message.Status = OutboxStatus.Published;
                            message.PublishedDate = DateTime.UtcNow;
                            message.LockId = null;
                        }

                        await db.SaveChangesAsync();
                        await tx.CommitAsync();
                    } catch (Exception ex) {
                        logger.LogError(ex, "Exception attempting to publish from outbox");
                        await tx.RollbackAsync();
                    }
                }
            }
        }
    }
}
