using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Amqp;
using Amqp.Framing;
using Cortside.DomainEvent.Tests;
using Xunit;

namespace Cortside.DomainEvent.IntegrationTests {
    public class RetryTest : E2EBase {

        [Fact]
        public async Task ShouldBeAbleHaveHandlerRetry() {
            if (enabled) {
                var @event = new TestEvent {
                    IntValue = 0,  // 0=retry
                    StringValue = Guid.NewGuid().ToString()
                };

                var messageId = Guid.NewGuid().ToString();
                try {
                    await publisher.PublishAsync(@event, new EventProperties() { MessageId = messageId }).ConfigureAwait(false);
                } finally {
                    Assert.Null(publisher.Error);
                }

                ReceiveSuccessAndWait(messageId, 180);

                Assert.True(TestEvent.Instances.Any());
                Assert.Contains(TestEvent.Instances, x => x.Value.MessageId == messageId);
                var received = TestEvent.Instances.Single(x => x.Value.MessageId == messageId).Value;
                Assert.Equal(@event.StringValue, received.Data.StringValue);
                Assert.Equal(@event.IntValue, received.Data.IntValue);

                Assert.False(TestEvent.Fail.ContainsKey(messageId));
                Assert.Equal(4, TestEvent.Retry[messageId]);
                Assert.Equal(1, TestEvent.Success[messageId]);
            }
        }

        [Fact]
        public async Task ShouldReceiveSend() {
            if (enabled) {
                InternalStart();

                // consume all queued messages before tests starts
                var message = Link.Receive(TimeSpan.Zero);
                while (message != null) {
                    Link.Accept(message);
                }
                Thread.Sleep(10000);
                // shouldn't be any messages left
                var empty = Link.Receive(TimeSpan.Zero);
                Assert.Null(empty);

                var @event = new TestEvent {
                    IntValue = 0,  // 0=retry
                    StringValue = Guid.NewGuid().ToString()
                };

                var messageId = Guid.NewGuid().ToString();
                try {
                    await publisher.PublishAsync(@event, new EventProperties() { MessageId = messageId }).ConfigureAwait(false);
                } finally {
                    Assert.Null(publisher.Error);
                }

                message = Link.Receive();
                Assert.NotNull(message);
                Assert.Equal(messageId, message.Properties.MessageId);

                var linkName = receiverSettings.AppName + "-" + Guid.NewGuid().ToString();
                var sender = new SenderLink(Link.Session, linkName, receiverSettings.Queue);

                string body = DomainEventMessage.GetBody(message);
                var retry = new Message(body) {
                    Header = message.Header,
                    Footer = message.Footer,
                    Properties = message.Properties,
                    ApplicationProperties = message.ApplicationProperties
                };
                retry.Header.DeliveryCount = message.Header.DeliveryCount + 1;
                retry.ApplicationProperties[Constants.SCHEDULED_ENQUEUE_TIME_UTC] = DateTime.UtcNow.AddSeconds(10);

                using (var tx = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled)) {
                    sender.Send(retry);
                    Link.Accept(message);
                    tx.Complete();
                }
                Assert.Null(Error);

                // shouldn't be any messages left
                empty = Link.Receive(TimeSpan.Zero);
                Assert.Null(empty);

                message = Link.Receive();
                Assert.NotNull(message);
                Assert.Equal(messageId, message.Properties.MessageId);
                Assert.Equal(retry.Header.DeliveryCount, message.Header.DeliveryCount);
                Link.Accept(message);
            }
        }

        public ReceiverLink Link { get; protected set; }
        public DomainEventError Error { get; set; }
        //public event ReceiverClosedCallback Closed;


        private Session CreateSession() {
            var conn = new Connection(new Address(receiverSettings.ConnectionString));
            return new Session(conn);
        }

        private void OnClosed(IAmqpObject sender, Error error) {
            if (sender.Error != null) {
                Error = new DomainEventError();
                Error.Condition = sender.Error.Condition.ToString();
                Error.Description = sender.Error.Description;
            }
            Link.Close(TimeSpan.Zero, error);
            //Closed?.Invoke(this, Error);
        }

        private void InternalStart() {
            if (Link != null) {
                throw new InvalidOperationException("Already receiving.");
            }

            Error = null;
            var session = CreateSession();
            var attach = new Attach() {
                Source = new Source() {
                    Address = receiverSettings.Queue,
                    Durable = receiverSettings.Durable
                },
                Target = new Target() {
                    Address = null
                }
            };
            Link = new ReceiverLink(session, receiverSettings.AppName, attach, null);
            Link.Closed += OnClosed;

            // moved from Erik's test to help know that receiver link is up
            int waits = 0;
            do {
                Thread.Sleep(1000);
                if (Link.LinkState == LinkState.Attached) {
                    break;
                }
                waits++;
            }
            while (waits < 15);  // TODO: needs configuration value
        }

        public void Close(TimeSpan? timeout = null) {
            timeout = timeout ?? TimeSpan.Zero;
            Link?.Session.Close(timeout.Value);
            Link?.Session.Connection.Close(timeout.Value);
            Link?.Close(timeout.Value);
            Link = null;
            Error = null;
            //EventTypeLookup = null;
        }
    }
}
