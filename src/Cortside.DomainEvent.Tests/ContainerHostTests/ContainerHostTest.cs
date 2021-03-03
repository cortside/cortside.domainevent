using System;
using System.Collections.Generic;
using System.Threading;
using System.Transactions;
using Amqp;
using Amqp.Framing;
using Xunit;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {
    [CollectionDefinition("dbcontexttests", DisableParallelization = true)]

    public partial class ContainerHostTest : BaseHostTest {
        [Fact(Skip = "tx error")]
        public void Retry() {
            string name = "Retry";
            List<Message> messages = new List<Message>();
            this.host.RegisterMessageProcessor(name, new TestMessageProcessor(50, messages));
            this.linkProcessor = new TestLinkProcessor();
            this.host.RegisterLinkProcessor(this.linkProcessor);

            int count = 80;
            var connection = new Connection(Address);
            var session = new Session(connection);
            var sender = new SenderLink(session, "send-link", name);

            for (int i = 0; i < count; i++) {
                var m = new Message("msg" + i) { ApplicationProperties = new ApplicationProperties(), Header = new Header() { DeliveryCount = 0 }, Properties = new Properties() { MessageId = name + i, GroupId = name } };
                sender.Send(m, Timeout);
            }
            sender.Close();

            this.host.RegisterMessageSource(name, new TestMessageSource(new Queue<Message>(messages)));
            var receiver = new ReceiverLink(session, "recv-link", name);
            var message = receiver.Receive();
            var deliveryCount = message.Header.DeliveryCount;
            var delay = 10 * deliveryCount;
            var scheduleTime = DateTime.UtcNow.AddSeconds(delay);

            using (var ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled)) {
                var sndr = new SenderLink(session, "retry", "test.queue");
                // create a new message to be queued with scheduled delivery time
                var retry = new Message(message.GetBody<string>()) {
                    Header = message.Header,
                    Footer = message.Footer,
                    Properties = message.Properties,
                    ApplicationProperties = message.ApplicationProperties
                };
                retry.ApplicationProperties[SCHEDULED_ENQUEUE_TIME_UTC] = scheduleTime;
                sndr.Send(retry);
                receiver.Accept(message);
            }

            receiver.Close();
            session.Close();
            connection.Close();
        }

        [Fact]
        public void ContainerHostProcessorOrderTest() {
            string name = "ContainerHostProcessorOrderTest";
            List<Message> messages = new List<Message>();
            this.host.RegisterMessageProcessor(name, new TestMessageProcessor(50, messages));
            this.linkProcessor = new TestLinkProcessor();
            this.host.RegisterLinkProcessor(this.linkProcessor);

            int count = 80;
            var connection = new Connection(Address);
            var session = new Session(connection);
            var sender = new SenderLink(session, "send-link", name);

            for (int i = 0; i < count; i++) {
                var message = new Message("msg" + i);
                message.Properties = new Properties() { GroupId = name };
                sender.Send(message, Timeout);
            }

            sender.Close();

            this.host.RegisterMessageSource(name, new TestMessageSource(new Queue<Message>(messages)));
            var receiver = new ReceiverLink(session, "recv-link", name);
            for (int i = 0; i < count; i++) {
                var message = receiver.Receive();
                receiver.Accept(message);
            }

            receiver.Close();

            sender = new SenderLink(session, "send-link", "any");
            for (int i = 0; i < count; i++) {
                var message = new Message("msg" + i);
                message.Properties = new Properties() { GroupId = name };
                sender.Send(message, Timeout);
            }

            sender.Close();
            session.Close();
            connection.Close();
        }

        [Fact]
        public void ContainerHostMessageProcessorTest() {
            string name = "ContainerHostMessageProcessorTest";
            var processor = new TestMessageProcessor();
            this.host.RegisterMessageProcessor(name, processor);

            int count = 500;
            var connection = new Connection(Address);
            var session = new Session(connection);
            var sender = new SenderLink(session, "send-link", name);

            for (int i = 0; i < count; i++) {
                var message = new Message("msg" + i);
                message.Properties = new Properties() { GroupId = name };
                sender.Send(message, Timeout);
            }

            sender.Close();
            session.Close();
            connection.Close();

            Assert.Equal(count, processor.Messages.Count);
            for (int i = 0; i < count; i++) {
                var message = processor.Messages[i];
                Assert.Equal("msg" + i, message.GetBody<string>());
            }
        }

        [Fact]
        public void ContainerHostMessageSourceTest() {
            string name = "ContainerHostMessageSourceTest";
            int count = 100;
            Queue<Message> messages = new Queue<Message>();
            for (int i = 0; i < count; i++) {
                messages.Enqueue(new Message("test") { Properties = new Properties() { MessageId = name + i } });
            }

            var source = new TestMessageSource(messages);
            this.host.RegisterMessageSource(name, source);

            var connection = new Connection(Address);
            var session = new Session(connection);
            var receiver = new ReceiverLink(session, "receiver0", name);
            receiver.SetCredit(count, CreditMode.Manual);
            int released = 0;
            int rejected = 0;
            int ignored = 0;
            for (int i = 1; i <= count; i++) {
                Message message = receiver.Receive();
                if (i % 5 == 0) {
                    receiver.Reject(message);
                    rejected++;
                } else if (i % 17 == 0) {
                    receiver.Release(message);
                    released++;
                } else if (i % 36 == 0) {
                    ignored++;
                } else {
                    receiver.Accept(message);
                }
            }

            receiver.Close();
            session.Close();
            connection.Close();

            Thread.Sleep(500);
            Assert.Equal(released + ignored, source.Count);
            Assert.Equal(rejected, source.DeadLetterCount);
        }

        [Fact]
        public void ContainerHostRequestProcessorTest() {
            string name = "ContainerHostRequestProcessorTest";
            var processor = new TestRequestProcessor();
            this.host.RegisterRequestProcessor(name, processor);

            int count = 500;
            var connection = new Connection(Address);
            var session = new Session(connection);

            string replyTo = "client-reply-to";
            Attach recvAttach = new Attach() {
                Source = new Source() { Address = name },
                Target = new Target() { Address = replyTo }
            };

            var doneEvent = new ManualResetEvent(false);
            List<string> responses = new List<string>();
            ReceiverLink receiver = new ReceiverLink(session, "request-client-receiver", recvAttach, null);
            receiver.Start(
                20,
                (link, message) => {
                    responses.Add(message.GetBody<string>());
                    link.Accept(message);
                    if (responses.Count == count) {
                        doneEvent.Set();
                    }
                });

            SenderLink sender = new SenderLink(session, "request-client-sender", name);
            for (int i = 0; i < count; i++) {
                Message request = new Message("Hello");
                request.Properties = new Properties() { MessageId = "request" + i, ReplyTo = replyTo };
                sender.Send(request, null, null);
            }

            Assert.True(doneEvent.WaitOne(10000), "Not completed in time");

            receiver.Close();
            sender.Close();
            session.Close();
            connection.Close();

            Assert.Equal(count, processor.TotalCount);
            Assert.Equal(count, responses.Count);
            for (int i = 1; i <= count; i++) {
                Assert.Equal("OK" + i, responses[i - 1]);
            }
        }

    }
}
