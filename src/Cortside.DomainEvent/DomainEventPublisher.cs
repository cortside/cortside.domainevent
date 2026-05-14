using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent {
    public class DomainEventPublisher : BaseDomainEventPublisher, IDomainEventPublisherSession {
        private Connection conn;
        private Session sharedSession;

        public new event PublisherClosedCallback Closed;

        public DomainEventPublisher(DomainEventPublisherSettings settings, ILogger<DomainEventPublisher> logger) : base(settings, logger) {
        }

        public DomainEventPublisher(DomainEventPublisherSettings settings, ILogger<DomainEventPublisher> logger, Session session) : base(settings, logger) {
            sharedSession = session;
        }

        public DomainEventPublisher(KeyedDomainEventPublisherSettings settings, ILogger<DomainEventPublisher> logger) : base(settings, logger) {
        }

        public void Connect() {
            if (conn == null || conn.ConnectionState == ConnectionState.End) {
                conn = new Connection(new Address(ConnectionString));
            }
        }

        public override IDomainEventPublisherSession BeginSession() {
            Connect();
            sharedSession = new Session(conn);

            return this;
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
                        Logger.LogTrace("Using non shared session null connection. Creating new connection.");
                        Connect();
                        disconnectAfter = true;
                    }
                    Logger.LogTrace("Using non shared session with connection state: {State}", conn?.ConnectionState);

                    session = new Session(conn);
                    attach = new Attach() {
                        Target = new Target() { Address = properties.Address, Durable = Settings.Durable },
                        Source = new Source()
                    };
                } else {
                    Logger.LogTrace("Using shared session.");

                    session = sharedSession;
                    attach = new Attach() {
                        Target = new Target() { Address = properties.Address, Durable = Settings.Durable },
                        Source = new Source()
                    };
                }
                var sender = new SenderLink(session, Settings.Service + Guid.NewGuid().ToString(), attach, null);
                sender.Closed += OnClosed;
                Logger.LogTrace("SenderLink established");

                try {
                    await sender.SendAsync(message).ConfigureAwait(false);
                    Statistics.Instance.Publish();
                    Logger.LogInformation("Published message {MessageId}", message.Properties.MessageId);
                } catch (Exception ex) {
                    Statistics.Instance.Publish(false);
                    Logger.LogError(ex, "Error publishing message {MessageId}", message.Properties.MessageId);
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
                        if (Error != null) {
                            Logger.LogTrace(Error.Exception,
                                "Publisher closed. Error description {Description}, Condition {Condition}.",
                                Error.Description, Error.Condition);
                        }
                    }

                    Logger.LogTrace("Send complete. disconnectAfter: {DisconnectAfter}, null shared session: {NullSharedSession}, sender State {SenderState}, sender is closed {IsClosed}",
                        disconnectAfter, sharedSession == null, sender.LinkState, sender.IsClosed);
                    if (disconnectAfter && sharedSession != null) {
                        if (!sender.IsClosed) {
                            await sender.CloseAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        }
                        await session.CloseAsync().ConfigureAwait(false);
                        await session.Connection.CloseAsync().ConfigureAwait(false);
                        conn = null;
                    }

                    Logger.LogTrace("End of SendAsync");
                }
            }
        }

        private void OnClosed(IAmqpObject sender, Error error) {
            Logger.LogTrace("OnClosed called");
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
