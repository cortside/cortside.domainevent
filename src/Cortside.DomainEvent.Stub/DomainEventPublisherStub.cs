using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Amqp.Types;
using Cortside.DomainEvent.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cortside.DomainEvent.Stub {
    public class DomainEventPublisherStub : IDomainEventPublisher {
        private readonly QueueBroker queue;

        public DomainEventPublisherStub(DomainEventPublisherSettings settings, ILogger<DomainEventPublisherStub> logger, QueueBroker queue) {
            Settings = settings;
            Logger = logger;
            this.queue = queue;
        }

        protected DomainEventPublisherSettings Settings { get; }

        protected ILogger<DomainEventPublisherStub> Logger { get; }

        public event PublisherClosedCallback Closed;

        public DomainEventError Error { get; set; }

        public async Task PublishAsync<T>(T @event) where T : class {
            var properties = new EventProperties();
            var message = CreateMessage(@event, properties);
            await InnerSendAsync(message, properties).ConfigureAwait(false);
        }

        public async Task PublishAsync<T>(T @event, string correlationId) where T : class {
            var properties = new EventProperties() { CorrelationId = correlationId };
            var message = CreateMessage(@event, properties);
            await InnerSendAsync(message, properties).ConfigureAwait(false);
        }

        public async Task PublishAsync<T>(T @event, EventProperties properties) where T : class {
            var message = CreateMessage(@event, properties);
            await InnerSendAsync(message, properties).ConfigureAwait(false);
        }

        public async Task PublishAsync(string body, EventProperties properties) {
            var message = CreateMessage(body, properties);
            await InnerSendAsync(message, properties).ConfigureAwait(false);
        }

        public async Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc) where T : class {
            var properties = new EventProperties();
            var message = CreateMessage(@event, properties, scheduledEnqueueTimeUtc);
            await InnerSendAsync(message, properties).ConfigureAwait(false);
        }

        public async Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, string correlationId) where T : class {
            var properties = new EventProperties() { CorrelationId = correlationId };
            var message = CreateMessage(@event, properties, scheduledEnqueueTimeUtc);
            await InnerSendAsync(message, properties).ConfigureAwait(false);
        }

        public async Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, EventProperties properties) where T : class {
            var message = CreateMessage(@event, properties, scheduledEnqueueTimeUtc);
            await InnerSendAsync(message, properties).ConfigureAwait(false);
        }

        public async Task ScheduleAsync(string body, DateTime scheduledEnqueueTimeUtc, EventProperties properties) {
            var message = CreateMessage(body, properties, scheduledEnqueueTimeUtc);
            await InnerSendAsync(message, properties).ConfigureAwait(false);
        }

        private Message CreateMessage(object @event, EventProperties properties, DateTime? scheduledEnqueueTimeUtc = null) {
            string data;
            if (Settings.SerializerSettings != null) {
                data = JsonConvert.SerializeObject(@event, Settings.SerializerSettings);
            } else {
                data = JsonConvert.SerializeObject(@event);
            }
            properties.EventType ??= @event.GetType().FullName;
            properties.Topic ??= Settings.Topic;
            properties.RoutingKey ??= @event.GetType().Name;

            return CreateMessage(data, properties, scheduledEnqueueTimeUtc);
        }

        private Message CreateMessage(string data, EventProperties properties, DateTime? scheduledEnqueueTimeUtc = null) {
            Guard.Against(() => properties.EventType == null, () => new ArgumentException("EventType is a required argument"));
            Guard.Against(() => properties.Topic == null, () => new ArgumentException("Topic is a required argument"));
            Guard.Against(() => properties.RoutingKey == null, () => new ArgumentException("RoutingKey is a required argument"));

            properties.MessageId ??= Guid.NewGuid().ToString();

            var message = new Message(data) {
                Header = new Header {
                    Durable = (Settings.Durable == 2)
                },
                ApplicationProperties = new ApplicationProperties(),
                MessageAnnotations = new MessageAnnotations(),
                Properties = new Properties {
                    MessageId = properties.MessageId,
                    GroupId = properties.EventType,
                    CorrelationId = properties.CorrelationId
                }
            };
            message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY] = properties.EventType;
            if (scheduledEnqueueTimeUtc.HasValue) {
                message.MessageAnnotations[new Symbol(Constants.SCHEDULED_ENQUEUE_TIME_UTC)] = scheduledEnqueueTimeUtc;
            }
            return message;
        }

        private async Task InnerSendAsync(Message message, EventProperties properties) {
            using (Logger.BeginScope(new Dictionary<string, object> {
                ["CorrelationId"] = message.Properties.CorrelationId,
                ["MessageId"] = message.Properties.MessageId,
                ["MessageType"] = message.Properties.GroupId
            })) {
                Logger.LogTrace("Publishing message {MessageId} to {Address} with body: {MessageBody}", message.Properties.MessageId, properties.Address, message.Body);
                queue.Enqueue(message);
                Logger.LogInformation($"Published message {message.Properties.MessageId}");
            }
        }
    }
}
