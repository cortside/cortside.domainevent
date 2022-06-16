using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Amqp.Types;
using Cortside.Common.Correlation;
using Cortside.Common.Validation;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cortside.DomainEvent {
    public abstract class BaseDomainEventPublisher : IDomainEventPublisher {
        public event PublisherClosedCallback Closed;

        protected BaseDomainEventPublisher(DomainEventPublisherSettings settings, ILogger logger) {
            Settings = settings;
            Logger = logger;
        }

        public DomainEventError Error { get; set; }

        protected DomainEventPublisherSettings Settings { get; }

        protected ILogger Logger { get; }

        public Task PublishAsync<T>(T @event) where T : class {
            var properties = new EventProperties();
            var message = CreateMessage(@event, properties);
            return InnerSendAsync(message, properties);
        }

        public Task PublishAsync<T>(T @event, string correlationId) where T : class {
            var properties = new EventProperties() { CorrelationId = correlationId };
            var message = CreateMessage(@event, properties);
            return InnerSendAsync(message, properties);
        }

        public Task PublishAsync<T>(T @event, EventProperties properties) where T : class {
            var message = CreateMessage(@event, properties);
            return InnerSendAsync(message, properties);
        }

        public Task PublishAsync(string body, EventProperties properties) {
            var message = CreateMessage(body, properties);
            return InnerSendAsync(message, properties);
        }

        public Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc) where T : class {
            var properties = new EventProperties();
            var message = CreateMessage(@event, properties, scheduledEnqueueTimeUtc);
            return InnerSendAsync(message, properties);
        }

        public Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, string correlationId) where T : class {
            var properties = new EventProperties() { CorrelationId = correlationId };
            var message = CreateMessage(@event, properties, scheduledEnqueueTimeUtc);
            return InnerSendAsync(message, properties);
        }

        public Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, EventProperties properties) where T : class {
            var message = CreateMessage(@event, properties, scheduledEnqueueTimeUtc);
            return InnerSendAsync(message, properties);
        }

        public Task ScheduleAsync(string body, DateTime scheduledEnqueueTimeUtc, EventProperties properties) {
            var message = CreateMessage(body, properties, scheduledEnqueueTimeUtc);
            return InnerSendAsync(message, properties);
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

            // if the correlationId is not set, set one from the current context
            if (string.IsNullOrEmpty(properties.CorrelationId)) {
                properties.CorrelationId = CorrelationContext.GetCorrelationId(true);
            }

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
                await SendAsync(message, properties);
            }
        }

        protected abstract Task SendAsync(Message message, EventProperties properties);
    }
}
