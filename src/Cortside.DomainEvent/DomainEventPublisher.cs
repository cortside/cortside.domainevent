using System;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent {
    public class DomainEventPublisher : BaseDomainEventPublisher, IDisposable {
        private Connection conn;
        public new event PublisherClosedCallback Closed;

        public DomainEventPublisher(DomainEventPublisherSettings settings, ILogger<DomainEventPublisher> logger) : base(settings, logger) {
        }

        public void Connect() {
            if (conn == null) {
                conn = new Connection(new Address(Settings.ConnectionString));
            }
        }

        protected override async Task SendAsync(Message message, EventProperties properties) {
            var disconnectAfter = false;
            if (conn == null) {
                Connect();
                disconnectAfter = true;
            }

            var session = new Session(conn);
            var attach = new Attach() {
                Target = new Target() { Address = properties.Address, Durable = Settings.Durable },
                Source = new Source()
            };
            var sender = new SenderLink(session, Settings.AppName + Guid.NewGuid().ToString(), attach, null);
            sender.Closed += OnClosed;

            try {
                await sender.SendAsync(message).ConfigureAwait(false);
                Logger.LogInformation($"Published message {message.Properties.MessageId}");
            } finally {
                if (sender.Error != null) {
                    Error = new DomainEventError();
                    Error.Condition = sender.Error.Condition.ToString();
                    Error.Description = sender.Error.Description;
                    Closed?.Invoke(this, Error);
                }

                if (disconnectAfter) {
                    if (!sender.IsClosed) {
                        await sender.CloseAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    }
                    await session.CloseAsync().ConfigureAwait(false);
                    await session.Connection.CloseAsync().ConfigureAwait(false);
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

        public void Close(TimeSpan? timeout = null) {
            timeout = timeout ?? TimeSpan.Zero;
            conn?.Close(timeout.Value);
            conn = null;
            Error = null;
        }

        public void Dispose() {
            Close();
        }
    }
}
