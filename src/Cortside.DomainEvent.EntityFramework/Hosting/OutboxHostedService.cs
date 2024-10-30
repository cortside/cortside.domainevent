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
    -- explicitly set the isolation level incase it was set on the connection already or default for read committed snapshot is on
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED

    UPDATE Q
    SET LockId = case when PublishCount>={config.MaximumPublishCount} then null else '{lockId}' end,
        Status=case when PublishCount>={config.MaximumPublishCount} then 'Failed' else 'Publishing' end,
        LastModifiedDate=GETUTCDATE(),
        PublishCount=PublishCount+case when PublishCount>={config.MaximumPublishCount} then 0 else 1 end
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
                logger.LogTrace("Getting DbContext instance");
                var db = scope.ServiceProvider.GetService<T>();
                var isRelational = !db.Database.ProviderName.Contains("InMemory");
                logger.LogTrace($"Obtained DbContext with provider {db.Database.ProviderName} with isRational = {isRelational}");

                int messageCount;
                if (isRelational) {
                    messageCount = await db.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
                } else {
                    // intentionally does not use LastModifiedDate in getting messages with Publishing status so that tests don't have to wait for that
                    var messages = db.Set<Outbox>().Where(o => (o.LockId == null && o.Status == OutboxStatus.Queued && o.ScheduledDate < DateTime.UtcNow) || (o.Status == OutboxStatus.Publishing)).Take(config.BatchSize);
                    foreach (var message in messages) {
                        if (message.PublishCount >= config.MaximumPublishCount) {
                            message.Status = OutboxStatus.Failed;
                            message.LockId = null;
                        } else {
                            message.Status = OutboxStatus.Publishing;
                            message.LockId = lockId;
                            message.PublishCount++;
                        }
                    }
                    await db.SaveChangesAsync().ConfigureAwait(false);
                    messageCount = await messages.CountAsync();
                }
                logger.LogInformation("Messages to publish: {Count}", messageCount);

                try {
                    List<Outbox> messages = await db.Set<Outbox>().Where(x => x.LockId == lockId).ToListAsync().ConfigureAwait(false);
                    logger.LogInformation("Messages claimed: {Count}", messages.Count);

                    foreach (var message in messages) {
                        var properties = new EventProperties() {
                            EventType = message.EventType,
                            Topic = message.Topic,
                            RoutingKey = message.RoutingKey,
                            CorrelationId = message.CorrelationId,
                            MessageId = message.MessageId
                        };

                        logger.LogTrace("Getting IDomainEventPublisher instance");
                        var publisher = scope.ServiceProvider.GetService<IDomainEventPublisher>();
                        logger.LogTrace($"Obtained IDomainEventPublisher instance with type of {publisher.GetType().Name}");

                        try {
                            await publisher.PublishAsync(message.Body, properties).ConfigureAwait(false);
                            message.Status = OutboxStatus.Published;
                            message.PublishedDate = DateTime.UtcNow;
                            message.LockId = null;

                            logger.LogTrace("Saving changes for message {message.MessageId}");
                            await db.SaveChangesAsync().ConfigureAwait(false);
                            logger.LogTrace("Saved changes for message {message.MessageId}");
                        } catch (Exception ex) {
                            // set message back to original locked state -- just in case the exception is from ef
                            db.Entry(message).State = EntityState.Unchanged;

                            logger.LogError(ex, "Exception attempting to publish message {MessageId} from outbox: {Reason}", message.MessageId, ex.Message);
                        }
                    }
                } catch (Exception ex) {
                    logger.LogError(ex, "Exception attempting to publish from outbox: {Reason}", ex.Message);
                }

                if (config.PurgePublished) {
                    try {
                        logger.LogTrace($"Purging published messages with batch size of {config.BatchSize}");
                        if (isRelational) {
                            var query = $"DELETE TOP ({config.BatchSize}) FROM Outbox Where Status = '{OutboxStatus.Published}'";
                            await db.Database.ExecuteSqlRawAsync(query).ConfigureAwait(false);
                        } else {
                            db.RemoveRange(db.Set<Outbox>().Where(o => o.Status == OutboxStatus.Published).Take(config.BatchSize));
                            await db.SaveChangesAsync().ConfigureAwait(false);
                        }
                        logger.LogTrace($"Purged published messages with batch size of {config.BatchSize}");
                    } catch (Exception ex) {
                        logger.LogError(ex, "Exception attempting to purge published from outbox");
                    }
                }
            }
        }
    }
}
