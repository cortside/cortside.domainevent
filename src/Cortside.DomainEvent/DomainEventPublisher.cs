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
    public class DomainEventPublisher : IDomainEventPublisher, IDisposable {
        private Connection conn;
        private readonly Session sharedSession;

        public event PublisherClosedCallback Closed;

        public DomainEventPublisher(DomainEventPublisherSettings settings, ILogger<DomainEventPublisher> logger) {
            Settings = settings;
            Logger = logger;
        }

        public DomainEventPublisher(DomainEventPublisherSettings settings, ILogger<DomainEventPublisher> logger, Session session) {
            Settings = settings;
            Logger = logger;
            sharedSession = session;
        }

        public DomainEventError Error { get; set; }

        protected DomainEventPublisherSettings Settings { get; }

        protected ILogger<DomainEventPublisher> Logger { get; }

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

        public void Connect() {
            if (conn == null) {
                conn = new Connection(new Address(Settings.ConnectionString));
            }
        }

        private async Task InnerSendAsync(Message message, EventProperties properties) {
            using (Logger.BeginScope(new Dictionary<string, object> {
                ["CorrelationId"] = message.Properties.CorrelationId,
                ["MessageId"] = message.Properties.MessageId,
                ["MessageType"] = message.Properties.GroupId
            })) {
                Logger.LogTrace("Publishing message {MessageId} to {Address} with body: {MessageBody}", message.Properties.MessageId, properties.Address, message.Body);

                var disconnectAfter = false;
                Attach attach;
                Session session;

                if (sharedSession == null) {
                    if (conn == null) {
                        Connect();
                        disconnectAfter = true;
                    }

                    session = new Session(conn);
                    attach = new Attach() {
                        Target = new Target() { Address = properties.Address, Durable = Settings.Durable },
                        Source = new Source()
                    };
                } else {
                    session = sharedSession;
                    attach = new Attach() {
                        Target = new Target() { Address = properties.Address, Durable = Settings.Durable },
                        Source = new Source()
                    };
                }
                var sender = new SenderLink(session, Settings.AppName + Guid.NewGuid().ToString(), attach, null);
                sender.Closed += OnClosed;

                try {
                    await sender.SendAsync(message).ConfigureAwait(false);
                    Logger.LogInformation($"Published message {message.Properties.MessageId}");
                } finally {
                    if (sender.Error != null) {
                        Error = new DomainEventError {
                            Condition = sender.Error.Condition.ToString(),
                            Description = sender.Error.Description
                        };
                        Closed?.Invoke(this, Error);
                    }

                    if (disconnectAfter && sharedSession != null) {
                        if (!sender.IsClosed) {
                            await sender.CloseAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        }
                        await session.CloseAsync().ConfigureAwait(false);
                        await session.Connection.CloseAsync().ConfigureAwait(false);
                        conn = null;
                    }
                }
            }
        }

        private void OnClosed(IAmqpObject sender, Error error) {
            if (sender.Error != null) {
                Error = new DomainEventError {
                    Condition = sender.Error.Condition.ToString(),
                    Description = sender.Error.Description
                };
            }
            Closed?.Invoke(this, Error);
        }

        public void Close(TimeSpan? timeout = null) {
            timeout ??= TimeSpan.Zero;
            conn?.Close(timeout.Value);
            conn = null;
            Error = null;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            Close();
        }
    }
}
