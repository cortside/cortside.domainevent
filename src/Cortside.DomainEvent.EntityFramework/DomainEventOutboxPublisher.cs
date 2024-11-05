#pragma warning disable CS0067

using System;
using System.Threading.Tasks;
using Cortside.Common.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cortside.DomainEvent.EntityFramework {
    public class DomainEventOutboxPublisher<TDbContext> : IDomainEventOutboxPublisher where TDbContext : DbContext {
        protected DomainEventPublisherSettings Settings { get; }

        private readonly TDbContext context;

        protected ILogger<DomainEventOutboxPublisher<TDbContext>> Logger { get; }

        public DomainEventOutboxPublisher(DomainEventPublisherSettings settings, TDbContext context, ILogger<DomainEventOutboxPublisher<TDbContext>> logger) {
            Settings = settings;
            this.context = context;
            Logger = logger;
        }

        public DomainEventError Error { get; set; }

        public event PublisherClosedCallback Closed;

        public Task PublishAsync<T>(T @event) where T : class {
            var properties = new EventProperties();
            return InnerSendAsync(@event, properties);
        }

        public Task PublishAsync<T>(T @event, string correlationId) where T : class {
            var properties = new EventProperties() { CorrelationId = correlationId };
            return InnerSendAsync(@event, properties);
        }

        public Task PublishAsync<T>(T @event, EventProperties properties) where T : class {
            return InnerSendAsync(@event, properties);
        }

        public Task PublishAsync(string body, EventProperties properties) {
            return InnerSendAsync(body, properties);
        }

        public Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc) where T : class {
            var properties = new EventProperties();
            return InnerSendAsync(@event, properties, scheduledEnqueueTimeUtc);
        }

        public Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, string correlationId) where T : class {
            var properties = new EventProperties() { CorrelationId = correlationId };
            return InnerSendAsync(@event, properties, scheduledEnqueueTimeUtc);
        }
        public Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, EventProperties properties) where T : class {
            return InnerSendAsync(@event, properties, scheduledEnqueueTimeUtc);
        }

        public Task ScheduleAsync(string body, DateTime scheduledEnqueueTimeUtc, EventProperties properties) {
            return InnerSendAsync(body, properties, scheduledEnqueueTimeUtc);
        }

        private Task InnerSendAsync(object @event, EventProperties properties, DateTime? scheduledEnqueueTimeUtc = null) {
            string body;
            if (Settings.SerializerSettings != null) {
                body = JsonConvert.SerializeObject(@event, Settings.SerializerSettings);
            } else {
                body = JsonConvert.SerializeObject(@event);
            }
            properties.EventType ??= @event.GetType().FullName;
            properties.Topic ??= Settings.Topic;
            properties.RoutingKey ??= @event.GetType().Name;

            return InnerSendAsync(body, properties, scheduledEnqueueTimeUtc);
        }

        private async Task InnerSendAsync(string body, EventProperties properties, DateTime? scheduledEnqueueTimeUtc = null) {
            Guard.Against(() => properties.EventType == null, () => new ArgumentException("EventType is a required argument"));
            Guard.Against(() => properties.Topic == null, () => new ArgumentException("Topic is a required argument"));
            Guard.Against(() => properties.RoutingKey == null, () => new ArgumentException("RoutingKey is a required argument"));

            var date = DateTime.UtcNow;
            properties.MessageId ??= Guid.NewGuid().ToString();

            Logger.LogDebug("Queueing message {MessageId} with body: {MessageBody}", properties.MessageId, body);
            await context.Set<Outbox>().AddAsync(new Outbox() {
                MessageId = properties.MessageId,
                CorrelationId = properties.CorrelationId,
                EventType = properties.EventType,
                Topic = properties.Topic,
                RoutingKey = properties.RoutingKey,
                Body = body,
                CreatedDate = date,
                ScheduledDate = scheduledEnqueueTimeUtc ?? date,
                Status = OutboxStatus.Queued
            }).ConfigureAwait(false);

            // this statistic is not aware of transactions and will be off if the transaction is rolled back
            Statistics.Instance.Queue();
        }
    }
}
