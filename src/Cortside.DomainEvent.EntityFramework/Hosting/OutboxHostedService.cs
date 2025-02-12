using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cortside.Common.Correlation;
using Cortside.Common.Hosting;
using Cortside.Common.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.EntityFramework.Hosting {
    public class OutboxHostedService<T> : TimedHostedService where T : DbContext {
        private readonly IServiceProvider serviceProvider;
        private readonly OutboxHostedServiceConfiguration config;
        private readonly string Key;

        public OutboxHostedService(ILogger<OutboxHostedService<T>> logger, OutboxHostedServiceConfiguration config, IServiceProvider serviceProvider) : base(logger, config.Enabled, config.Interval, true) {
            this.serviceProvider = serviceProvider;
            this.config = config;
        }

        public OutboxHostedService(ILogger<OutboxHostedService<T>> logger, OutboxHostedServiceConfiguration config, IServiceProvider serviceProvider, KeyedDomainEventPublisherSettings keyedSettings) : base(logger, config.Enabled, config.Interval, true) {
            this.serviceProvider = serviceProvider;
            this.config = config;
            this.Key = keyedSettings.Key;
        }

        public override Task StartAsync(CancellationToken cancellationToken) {
            logger.LogInformation("{Key} OutboxHostedService StartAsync() entered.", Key);
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteIntervalAsync() {
            logger.LogDebug("{Key} OutboxHostedService ExecuteIntervalAsync() entered.", Key);
            await Task.Yield();
            var keySql = (!string.IsNullOrWhiteSpace(Key)) ? $"and [Key]='{Key}'" : "";

            var lockId = CorrelationContext.GetCorrelationId();
            var sql = $@"
declare @rows int
select @rows = count(*) from Outbox with (nolock) where ((LockId is null and Status='Queued' and ScheduledDate<GETUTCDATE())
            or (status='Publishing' and LastModifiedDate<(dateadd(second, -{config.PublishRetryInterval}, GETUTCDATE())))
            {keySql}
            )

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
            where ((LockId is null and Status='Queued' and ScheduledDate<GETUTCDATE())
                or (status='Publishing' and LastModifiedDate<(dateadd(second, -{config.PublishRetryInterval}, GETUTCDATE())))
                {keySql}
                )
            order by ScheduledDate
    ) Q
  END
";

            using (logger.PushProperty("Key", Key))
            using (var scope = serviceProvider.CreateScope()) {
                var db = scope.ServiceProvider.GetService<T>();
                var isRelational = !db.Database.ProviderName.Contains("InMemory");

                logger.LogDebug("Obtained DbContext with provider {ProviderName}, IsRelational = {IsRelational}", db.Database.ProviderName, isRelational);

                int messageCount;
                if (isRelational) {
                    messageCount = await db.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
                } else {
                    // intentionally does not use LastModifiedDate in getting messages with Publishing status so that tests don't have to wait for that
                    var messages = db.Set<Outbox>().Where(o => (string.IsNullOrWhiteSpace(Key) || o.Key == Key)
                        && ((o.LockId == null && o.Status == OutboxStatus.Queued && o.ScheduledDate < DateTime.UtcNow) || (o.Status == OutboxStatus.Publishing))).Take(config.BatchSize);
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
                logger.LogInformation("{Key} Messages to publish: {Count}", Key, messageCount);

                try {
                    List<Outbox> messages = await db.Set<Outbox>().Where(x => x.LockId == lockId).ToListAsync().ConfigureAwait(false);
                    logger.LogInformation("{Key} Messages claimed: {Count}", Key, messages.Count);

                    var i = 1;
                    foreach (var message in messages) {
                        logger.LogDebug("{Key} Publishing message {MessageId} [OutboxId: {OutboxId}] ({Index} of {Count})", Key, message.MessageId, message.OutboxId, i, messages.Count);
                        var properties = new EventProperties() {
                            EventType = message.EventType,
                            Topic = message.Topic,
                            RoutingKey = message.RoutingKey,
                            CorrelationId = message.CorrelationId,
                            MessageId = message.MessageId
                        };

                        var publisher = (!string.IsNullOrWhiteSpace(Key)) ? scope.ServiceProvider.GetKeyedService<IDomainEventPublisher>(Key) : scope.ServiceProvider.GetService<IDomainEventPublisher>();

                        try {
                            await publisher.PublishAsync(message.Body, properties).ConfigureAwait(false);
                            message.Status = OutboxStatus.Published;
                            message.PublishedDate = DateTime.UtcNow;
                            message.LockId = null;

                            await db.SaveChangesAsync().ConfigureAwait(false);
                        } catch (Exception ex) {
                            // set message back to original locked state -- just in case the exception is from ef
                            db.Entry(message).State = EntityState.Unchanged;

                            logger.LogError(ex, "Exception attempting to publish message {MessageId} from outbox: {Reason}", message.MessageId, ex.Message);
                        }

                        logger.LogDebug("{Key} Published message {MessageId} [OutboxId: {OutboxId}] ({Index} of {Count})", Key, message.MessageId, message.OutboxId, i, messages.Count);
                        i++;
                    }
                } catch (Exception ex) {
                    logger.LogError(ex, "Exception attempting to publish from outbox: {Reason}", ex.Message);
                }

                if (config.PurgePublished) {
                    try {
                        if (isRelational) {
                            // TODO: keyed? probably
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

                logger.LogDebug("OutboxHostedService ExecuteIntervalAsync() completed.");
            }
        }
    }
}
