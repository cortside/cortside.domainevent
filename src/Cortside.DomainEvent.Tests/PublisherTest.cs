using System;
using System.Threading.Tasks;
using Amqp;
using Cortside.DomainEvent.Tests.ContainerHostTests;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cortside.DomainEvent.Tests {
    public class PublisherTest : BaseHostTest {

        [Fact]
        public async Task ContainerHostMessageProcessorTest() {
            string name = "ContainerHostMessageProcessorTest";
            var processor = new TestMessageProcessor();
            this.host.RegisterMessageProcessor(name + "String", processor);

            var settings = this.settings.Copy();
            settings.Address = name;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventComms>());

            int count = 500;

            for (int i = 0; i < count; i++) {
                await publisher.SendAsync("msg" + i);
            }

            Assert.Equal(count, processor.Messages.Count);
            for (int i = 0; i < count; i++) {
                var message = processor.Messages[i];
                Assert.Equal($"\"msg{i}\"", message.GetBody<string>());
            }
        }
    }
}
