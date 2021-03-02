using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {
    public partial class ContainerHostTest : BaseHostTest {

        [Fact]
        public async Task ShouldReceiveMessage_Accept() {
            List<Message> messages = new List<Message>();
            this.host.RegisterMessageProcessor(receiverSettings.Address + "TestEvent", new TestMessageProcessor(50, messages));
            this.linkProcessor = new TestLinkProcessor();
            this.host.RegisterLinkProcessor(this.linkProcessor);

            int count = 80;
            publisterSettings.Address = receiverSettings.Address;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventComms>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.SendAsync(@event);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            this.host.RegisterMessageSource(receiverSettings.Address, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventComms>())) {
                receiver.Start(eventTypes);
                for (int i = 0; i < count; i++) {
                    var message = receiver.Receive(TimeSpan.FromSeconds(1));
                    message.Accept();
                }
            }

            Assert.Equal(0, source.DeadLetterCount);
            Assert.Equal(0, source.Count);
        }

        [Fact]
        public async Task ShouldReceiveMessage_Reject() {
            List<Message> messages = new List<Message>();
            this.host.RegisterMessageProcessor(receiverSettings.Address + "TestEvent", new TestMessageProcessor(50, messages));
            this.linkProcessor = new TestLinkProcessor();
            this.host.RegisterLinkProcessor(this.linkProcessor);

            int count = 80;
            publisterSettings.Address = receiverSettings.Address;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventComms>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.SendAsync(@event);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            this.host.RegisterMessageSource(receiverSettings.Address, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventComms>())) {
                receiver.Start(eventTypes);
                for (int i = 0; i < count; i++) {
                    var message = receiver.Receive(TimeSpan.FromSeconds(1));
                    message.Reject();
                }
            }

            Assert.Equal(count, source.DeadLetterCount);
            Assert.Equal(0, source.Count);
        }

        [Fact]
        public async Task ShouldReceiveMessage_Release() {
            List<Message> messages = new List<Message>();
            this.host.RegisterMessageProcessor(receiverSettings.Address + "TestEvent", new TestMessageProcessor(50, messages));
            this.linkProcessor = new TestLinkProcessor();
            this.host.RegisterLinkProcessor(this.linkProcessor);

            int count = 80;
            publisterSettings.Address = receiverSettings.Address;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventComms>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.SendAsync(@event);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            this.host.RegisterMessageSource(receiverSettings.Address, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventComms>())) {
                receiver.Start(eventTypes);
                for (int i = 0; i < count; i++) {
                    var message = receiver.Receive(TimeSpan.FromSeconds(1));
                    message.Release();
                }
            }

            Assert.Equal(0, source.DeadLetterCount);
            Assert.Equal(count, source.Count);
        }
    }
}
