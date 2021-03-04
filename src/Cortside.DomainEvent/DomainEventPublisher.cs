using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Amqp.Types;
using Cortside.DomainEvent.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Cortside.DomainEvent {
    public class DomainEventPublisher : IDomainEventPublisher {
        public event PublisherClosedCallback Closed;

        public DomainEventPublisher(DomainEventPublisherSettings settings, ILogger<DomainEventPublisher> logger) {
            Settings = settings;
            Logger = logger;
        }

        public DomainEventError Error { get; set; }

        protected DomainEventPublisherSettings Settings { get; }

        protected ILogger<DomainEventPublisher> Logger { get; }

        private Session CreateSession() {
            var conn = new Connection(new Address(Settings.ConnectionString));
            return new Session(conn);
        }

        public async Task PublishAsync<T>(T @event) where T : class {
            var properties = new EventProperties();
            var message = CreateMessage(@event, properties);
            await InnerSendAsync(message, properties);
        }

        public async Task PublishAsync<T>(T @event, string correlationId) where T : class {
            var properties = new EventProperties() { CorrelationId = correlationId };
            var message = CreateMessage(@event, properties);
            await InnerSendAsync(message, properties);
        }

        public async Task PublishAsync<T>(T @event, EventProperties properties) where T : class {
            var message = CreateMessage(@event, properties);
            await InnerSendAsync(message, properties);
        }

        public async Task PublishAsync(string body, EventProperties properties) {
            var message = CreateMessage(body, properties);
            await InnerSendAsync(message, properties);
        }

        public async Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc) where T : class {
            var properties = new EventProperties();
            var message = CreateMessage(@event, properties, scheduledEnqueueTimeUtc);
            await InnerSendAsync(message, properties);
        }

        public async Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, string correlationId) where T : class {
            var properties = new EventProperties() { CorrelationId = correlationId };
            var message = CreateMessage(@event, properties, scheduledEnqueueTimeUtc);
            await InnerSendAsync(message, properties);
        }

        public async Task ScheduleAsync<T>(T @event, DateTime scheduledEnqueueTimeUtc, EventProperties properties) where T : class {
            var message = CreateMessage(@event, properties, scheduledEnqueueTimeUtc);
            await InnerSendAsync(message, properties);
        }

        public async Task ScheduleAsync(string body, DateTime scheduledEnqueueTimeUtc, EventProperties properties) {
            var message = CreateMessage(body, properties, scheduledEnqueueTimeUtc);
            await InnerSendAsync(message, properties);
        }

        private Message CreateMessage(object @event, EventProperties properties, DateTime? scheduledEnqueueTimeUtc = null) {
            var data = JsonConvert.SerializeObject(@event);
            properties.EventType = properties.EventType ?? @event.GetType().FullName;
            properties.Topic = properties.Topic ?? Settings.Topic;
            properties.RoutingKey = properties.RoutingKey ?? @event.GetType().Name;

            return CreateMessage(data, properties, scheduledEnqueueTimeUtc);
        }

        private Message CreateMessage(string data, EventProperties properties, DateTime? scheduledEnqueueTimeUtc = null) {
            Guard.Against(() => properties.EventType == null, () => new ArgumentNullException(nameof(properties.EventType), "EventType is a required argument"));
            Guard.Against(() => properties.Topic == null, () => new ArgumentNullException(nameof(properties.Topic), "Topic is a required argument"));
            Guard.Against(() => properties.RoutingKey == null, () => new ArgumentNullException(nameof(properties.RoutingKey), "RoutingKey is a required argument"));

            properties.MessageId = properties.MessageId ?? Guid.NewGuid().ToString();

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
                Logger.LogTrace($"Publishing message {message.Properties.MessageId} to {properties.Address} with body: {message.Body}");
                var session = CreateSession();
                var attach = new Attach() {
                    Target = new Target() { Address = properties.Address, Durable = Settings.Durable },
                    Source = new Source()
                };
                var sender = new SenderLink(session, Settings.AppName, attach, null);
                sender.Closed += OnClosed;

                try {
                    await sender.SendAsync(message);
                    Logger.LogInformation($"Published message {message.Properties.MessageId}");
                } finally {
                    if (sender.Error != null) {
                        Error = new DomainEventError();
                        Error.Condition = sender.Error.Condition.ToString();
                        Error.Description = sender.Error.Description;
                        Closed?.Invoke(this, Error);
                    }
                    if (!sender.IsClosed) {
                        await sender.CloseAsync(TimeSpan.FromSeconds(5));
                    }
                    await session.CloseAsync();
                    await session.Connection.CloseAsync();
                }
            }
        }

        private void OnClosed(IAmqpObject sender, Error error) {
            if (sender.Error != null) {
                Error = new DomainEventError();
                Error.Condition = sender.Error.Condition.ToString();
                Error.Description = sender.Error.Description;
            }
            Closed?.Invoke(this, Error);
        }
    }
}
