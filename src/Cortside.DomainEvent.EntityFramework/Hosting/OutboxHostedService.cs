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

            var lockId = CorrelationContext.GetCorrelationId();
            var sql = $@"
declare @rows int
select @rows = count(*) from Outbox with (nolock) where (LockId is null and Status='Queued' and ScheduledDate<GETUTCDATE())
			or (status='Publishing' and LastModifiedDate<(dateadd(second, -60, GETUTCDATE())))

if (@rows > 0)
  BEGIN
	UPDATE Q
	SET LockId = '{lockId}', Status='Publishing', LastModifiedDate=GETUTCDATE()
	FROM (
			select top ({config.BatchSize}) * from Outbox
			WITH (ROWLOCK, READPAST)
			where (LockId is null and Status='Queued' and ScheduledDate<GETUTCDATE())
				or (status='Publishing' and LastModifiedDate<(dateadd(second, -60, GETUTCDATE())))
			order by ScheduledDate
	) Q
  END
";

            using (var scope = serviceProvider.CreateScope()) {
                var db = scope.ServiceProvider.GetService<T>();
                var isRelational = !db.Database.ProviderName.Contains("InMemory");

                var messageCount = 0;
                if (isRelational) {
                    messageCount = await db.Database.ExecuteSqlRawAsync(sql);
                } else {
                    var messages = db.Set<Outbox>().Where(o => o.Status == OutboxStatus.Queued && o.ScheduledDate < DateTime.UtcNow).Take(config.BatchSize);
                    foreach (var message in messages) {
                        message.Status = OutboxStatus.Publishing;
                        message.LockId = lockId;
                        message.LastModifiedDate = DateTime.UtcNow;
                    }
                    await db.SaveChangesAsync().ConfigureAwait(false);
                    messageCount = messages.Count();
                }
                logger.LogInformation($"messages to publish: {messageCount}");

                try {
                    List<Outbox> messages = await db.Set<Outbox>().Where(x => x.LockId == lockId).ToListAsync().ConfigureAwait(false);
                    logger.LogInformation($"messages claimed: {messages.Count}");

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
                } catch (Exception ex) {
                    logger.LogError(ex, "Exception attempting to publish from outbox");
                }

                if (config.PurgePublished) {
                    try {
                        if (isRelational) {
                            var query = $"DELETE TOP ({config.BatchSize}) FROM Outbox Where Status = '{OutboxStatus.Published}'";
                            await db.Database.ExecuteSqlRawAsync(query).ConfigureAwait(false);
                        } else {
                            db.RemoveRange(db.Set<Outbox>().Where(o => o.Status == OutboxStatus.Published).Take(config.BatchSize));
                            await db.SaveChangesAsync().ConfigureAwait(false);
                        }
                    } catch (Exception ex) {
                        logger.LogError(ex, "Exception attempting to purge published from outbox");
                    }
                }
            }
        }
    }
}
