using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent {
    public class DomainEventPublisher : BaseDomainEventPublisher, IDisposable {
        private Connection conn;
        private readonly Session sharedSession;

        public new event PublisherClosedCallback Closed;

        public DomainEventPublisher(DomainEventPublisherSettings settings, ILogger<DomainEventPublisher> logger) : base(settings, logger) {
        }

        public DomainEventPublisher(DomainEventPublisherSettings settings, ILogger<DomainEventPublisher> logger, Session session) : base(settings, logger) {
            sharedSession = session;
        }

        public DomainEventPublisher(KeyedDomainEventPublisherSettings settings, ILogger<DomainEventPublisher> logger) : base(settings, logger) {
        }

        public void Connect() {
            if (conn == null) {
                conn = new Connection(new Address(ConnectionString));
            }
        }

        protected override async Task SendAsync(Message message, EventProperties properties) {
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
                var sender = new SenderLink(session, Settings.Service + Guid.NewGuid().ToString(), attach, null);
                sender.Closed += OnClosed;

                try {
                    await sender.SendAsync(message).ConfigureAwait(false);
                    Statistics.Instance.Publish();
                    Logger.LogInformation($"Published message {message.Properties.MessageId}");
                } catch (Exception ex) {
                    Statistics.Instance.Publish(false);
                    Logger.LogError(ex, $"Error publishing message {message.Properties.MessageId}");
                    Error = new DomainEventError {
                        Condition = "Publish",
                        Description = ex.Message,
                        Exception = ex
                    };
                    Closed?.Invoke(this, Error);
                    throw new DomainEventPublisherException($"Error publishing message {message.Properties.MessageId}", ex);
                } finally {
                    if (Error == null && sender.Error != null) {
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
            if (Error == null && sender.Error != null) {
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
