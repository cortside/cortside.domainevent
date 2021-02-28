using System;
using System.Threading.Tasks;
using Amqp;
using Cortside.DomainEvent.Tests.ContainerHostTests;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;

namespace Cortside.DomainEvent.Tests {
    public class PublisherTest : BaseHostTest {

        [Fact]
        public async Task ShouldPublishEventWithObject() {
            string name = "ShouldPublishEventWithObject";
            var processor = new TestMessageProcessor();
            this.host.RegisterMessageProcessor(name + "TestEvent", processor);

            var settings = this.settings.Copy();
            settings.Address = name;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventComms>());

            int count = 500;

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = i, StringValue = i.ToString() };
                await publisher.SendAsync(@event);
            }

            Assert.Equal(count, processor.Messages.Count);
            for (int i = 0; i < count; i++) {
                var message = processor.Messages[i];
                Assert.Equal(i, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
                Assert.Equal(i.ToString(), JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            }
        }
    }
}
