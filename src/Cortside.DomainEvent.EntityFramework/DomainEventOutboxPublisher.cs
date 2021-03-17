#pragma warning disable CS0067

using System;
using System.Threading.Tasks;
using Cortside.DomainEvent.Common;
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

        public async Task PublishAsync<T>(T @event) where T : class {
            var properties = new EventProperties();
            await InnerSendAsync(@event, properties).ConfigureAwait(false);
        }

        public async Task PublishAsync<T>(T @event, string correlationId) where T : class {
            var properties = new EventProperties() { CorrelationId = correlationId };
            await InnerSendAsync(@event, properties).ConfigureAwait(false);
        }

        public async Task PublishAsync<T>(T @event, EventProperties properties) where T : class {
            await InnerSendAsync(@event, properties).ConfigureAwait(false);
        }

        public async Task PublishAsync(string body, EventProperties properties) {
            await InnerSendAsync(body, properties).ConfigureAwait(false);
        }

        public async Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc) where T : class {
            var properties = new EventProperties();
            await InnerSendAsync(@event, properties, scheduledEnqueueTimeUtc).ConfigureAwait(false);
        }

        public async Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, string correlationId) where T : class {
            var properties = new EventProperties() { CorrelationId = correlationId };
            await InnerSendAsync(@event, properties, scheduledEnqueueTimeUtc).ConfigureAwait(false);
        }
        public async Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, EventProperties properties) where T : class {
            await InnerSendAsync(@event, properties, scheduledEnqueueTimeUtc).ConfigureAwait(false);
        }

        public async Task ScheduleAsync(string body, DateTime scheduledEnqueueTimeUtc, EventProperties properties) {
            await InnerSendAsync(body, properties, scheduledEnqueueTimeUtc).ConfigureAwait(false);
        }

        private async Task InnerSendAsync(object @event, EventProperties properties, DateTime? scheduledEnqueueTimeUtc = null) {
            var body = JsonConvert.SerializeObject(@event);
            properties.EventType ??= @event.GetType().FullName;
            properties.Topic ??= Settings.Topic;
            properties.RoutingKey ??= @event.GetType().Name;

            await InnerSendAsync(body, properties, scheduledEnqueueTimeUtc).ConfigureAwait(false);
        }

        private async Task InnerSendAsync(string body, EventProperties properties, DateTime? scheduledEnqueueTimeUtc = null) {
            Guard.Against(() => properties.EventType == null, () => new ArgumentException("EventType is a required argument"));
            Guard.Against(() => properties.Topic == null, () => new ArgumentException("Topic is a required argument"));
            Guard.Against(() => properties.RoutingKey == null, () => new ArgumentException("RoutingKey is a required argument"));

            var date = DateTime.UtcNow;
            properties.MessageId ??= Guid.NewGuid().ToString();

            Logger.LogDebug($"Queueing message {properties.MessageId} with body: {body}");
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
        }
    }
}
