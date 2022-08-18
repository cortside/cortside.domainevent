using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Amqp.Types;
using Cortside.DomainEvent.Tests;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Cortside.DomainEvent.IntegrationTests {
    public class FilterTest : E2EBase {
        public FilterTest(ITestOutputHelper output) {
            Trace.TraceLevel = TraceLevel.Frame;
            Trace.TraceListener = (l, f, a) => output.WriteLine(DateTime.Now.ToString("[hh:mm:ss.fff]") + " " + string.Format(f, a));

            enabled = true;
        }

        [Fact]
        public async Task ShouldFilterByCorrelationIdAsync() {
            // https://github.com/Azure/amqpnetlite/issues/524

            // https://www.ibm.com/docs/en/ibm-mq/9.2?topic=applications-mapping-amqp-mq-message-fields
            // https://people.apache.org/~rgodfrey/amqp-1.0/apache-filters.html#type-jms-selector-filter

            ////https://www.eurex.com/resource/blob/2559044/8d3a36bdbfcceba6e56a182c824bbc00/data/eurex-clearing-messaging-connectivity-B-v.7.1.pdf
            //Map filters = new Map();
            //filters.Add(new Symbol("apache.org:selector-filter:string"), new
            //DescribedValue(new Symbol("apache.org:selector-filter:string"),
            //"amqp.correlation_id='123456'"));

            if (enabled) {
                var properties = new EventProperties() {
                    CorrelationId = Guid.NewGuid().ToString(),
                    ApplicationProperties = new Dictionary<string, object> {
                            { "sn", 100 }
                        }
                };

                var filter = new Map {
                    { new Symbol("f1"), new DescribedValue(new Symbol("jms-selector"), $"correlation-id = '{properties.CorrelationId}'") }
                };

                DomainEventMessage<TestEvent> received = await PublishAndAssertAsync(properties, filter).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task ShouldFilterByEventTypeAsync() {
            if (enabled) {
                var properties = new EventProperties() {
                    CorrelationId = Guid.NewGuid().ToString()
                };

                var filter = new Map {
                    { new Symbol("f1"), new DescribedValue(new Symbol("jms-selector"), $"event-type = '{typeof(TestEvent).FullName}'") }
                };

                DomainEventMessage<TestEvent> received = await PublishAndAssertAsync(properties, filter).ConfigureAwait(false);
                Assert.Equal(typeof(TestEvent).FullName, received.EventType);
            }
        }

        [Fact]
        public async Task ShouldFilterByGroupIdAsync() {
            if (enabled) {
                var properties = new EventProperties() {
                    CorrelationId = Guid.NewGuid().ToString()
                };

                var filter = new Map {
                    { new Symbol("f1"), new DescribedValue(new Symbol("jms-selector"), $"group-id = '{typeof(TestEvent).FullName}'") }
                };

                DomainEventMessage<TestEvent> received = await PublishAndAssertAsync(properties, filter).ConfigureAwait(false);
                Assert.Equal(typeof(TestEvent).FullName, received.EventType);
            }
        }

        [Fact]
        public async Task ShouldReceiveNoneBecauseOfFilterAsync() {
            if (enabled) {
                var properties = new EventProperties() {
                    CorrelationId = Guid.NewGuid().ToString()
                };

                var filter = new Map {
                    { new Symbol("f1"), new DescribedValue(new Symbol("jms-selector"), "foo-bar = 'baz'") }
                };

                var @event = new TestEvent {
                    IntValue = r.Next() + 1,
                    StringValue = Guid.NewGuid().ToString()
                };

                try {
                    await publisher.PublishAsync(@event, properties).ConfigureAwait(false);
                } finally {
                    Assert.Null(publisher.Error);
                }

                ReceiveAndWait(properties.CorrelationId, filter);

                Assert.DoesNotContain(mockLogger.LogEvents, x => x.LogLevel == LogLevel.Error);
                Assert.True(TestEvent.Instances.Count == 0);
            }
        }

        [Fact]
        public void AmqpNetLite_ReceiveWithFilter() {
            if (enabled) {
                string testName = "ReceiveWithFilter";
                Connection connection = new Connection(address);
                Session session = new Session(connection);

                Message message = new Message("I can match a filter");
                message.Properties = new Properties() { GroupId = "abcdefg" };
                message.ApplicationProperties = new ApplicationProperties();
                message.ApplicationProperties["sn"] = 100;

                SenderLink sender = new SenderLink(session, "sender-" + testName, queue);
                sender.Send(message, null, null);

                // update the filter descriptor and expression according to the broker
                Map filters = new Map();
                // JMS selector filter: code = 0x0000468C00000004L, symbol="apache.org:selector-filter:string", symbol="jms-selector"
                filters.Add(new Symbol("f1"), new DescribedValue(0x0000468C00000004UL, "sn = 100"));
                ReceiverLink receiver = new ReceiverLink(session, "receiver-" + testName, new Source() { Address = queue, FilterSet = filters }, null);
                Message message2 = receiver.Receive();
                Assert.Equal(message.ApplicationProperties["sn"], message2.ApplicationProperties["sn"]);
                receiver.Accept(message2);

                sender.Close();
                receiver.Close();
                session.Close();
                connection.Close();
            }
        }

        [Fact]
        public async Task DomainEvent_ReceiveWithFilterAsync() {
            if (enabled) {
                var properties = new EventProperties() {
                    CorrelationId = Guid.NewGuid().ToString(),
                    ApplicationProperties = new Dictionary<string, object> {
                            { "sn", 100 }
                        }
                };

                var filter = new Map {
                    { new Symbol("f1"), new DescribedValue(new Symbol("jms-selector"), "sn = 100") }
                };

                DomainEventMessage<TestEvent> received = await PublishAndAssertAsync(properties, filter).ConfigureAwait(false);
                //Assert.Equal(100, received.ApplicationProperties["sn"]);
            }
        }

        private async Task<DomainEventMessage<TestEvent>> PublishAndAssertAsync(EventProperties properties, Map filter) {
            var @event = new TestEvent {
                IntValue = r.Next() + 1,
                StringValue = Guid.NewGuid().ToString()
            };

            try {
                await publisher.PublishAsync(@event, properties).ConfigureAwait(false);
            } finally {
                Assert.Null(publisher.Error);
            }

            ReceiveAndWait(properties.CorrelationId, filter);

            Assert.DoesNotContain(mockLogger.LogEvents, x => x.LogLevel == LogLevel.Error);

            Assert.True(TestEvent.Instances.Count > 0);
            Assert.Contains(TestEvent.Instances, x => x.Value.CorrelationId == properties.CorrelationId);
            var received = TestEvent.Instances.Single(x => x.Value.CorrelationId == properties.CorrelationId).Value;
            Assert.Equal(@event.StringValue, received.Data.StringValue);
            Assert.Equal(@event.IntValue, received.Data.IntValue);
            return received;
        }
    }
}
