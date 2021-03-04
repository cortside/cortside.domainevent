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
            receiverSettings.Queue = Guid.NewGuid().ToString();

            List<Message> messages = new List<Message>();
            this.host.RegisterMessageProcessor(receiverSettings.Queue + "TestEvent", new TestMessageProcessor(50, messages));
            this.linkProcessor = new TestLinkProcessor();
            this.host.RegisterLinkProcessor(this.linkProcessor);

            int count = 1;
            publisterSettings.Topic = receiverSettings.Queue;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventPublisher>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.SendAsync(@event);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            this.host.RegisterMessageSource(receiverSettings.Queue, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventReceiver>())) {
                receiver.Start(eventTypes);
                for (int i = 0; i < count; i++) {
                    var message = receiver.Receive(TimeSpan.FromSeconds(1));
                    message.Accept();
                }
            }

            Assert.Equal(0, source.DeadLetterCount);
            Assert.Equal(0, source.Count);
        }

        [Fact(Skip = "flake")]
        public async Task ShouldReceiveMessage_Reject() {
            receiverSettings.Queue = Guid.NewGuid().ToString();

            List<Message> messages = new List<Message>();
            this.host.RegisterMessageProcessor(receiverSettings.Queue + "TestEvent", new TestMessageProcessor(50, messages));
            this.linkProcessor = new TestLinkProcessor();
            this.host.RegisterLinkProcessor(this.linkProcessor);

            int count = 1;
            publisterSettings.Topic = receiverSettings.Queue;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventPublisher>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.SendAsync(@event);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            this.host.RegisterMessageSource(receiverSettings.Queue, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventReceiver>())) {
                receiver.Start(eventTypes);
                for (int i = 0; i < count; i++) {
                    var message = receiver.Receive(TimeSpan.FromSeconds(1));
                    message.Reject();
                }
            }

            Assert.Equal(count, source.DeadLetterCount);
            Assert.Equal(0, source.Count);
        }

        [Fact(Skip = "flake")]
        public async Task ShouldReceiveMessage_Release() {
            receiverSettings.Queue = Guid.NewGuid().ToString();

            List<Message> messages = new List<Message>();
            this.host.RegisterMessageProcessor(receiverSettings.Queue + "TestEvent", new TestMessageProcessor(50, messages));
            this.linkProcessor = new TestLinkProcessor();
            this.host.RegisterLinkProcessor(this.linkProcessor);

            int count = 1;
            publisterSettings.Topic = receiverSettings.Queue;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventPublisher>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.SendAsync(@event);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            this.host.RegisterMessageSource(receiverSettings.Queue, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventReceiver>())) {
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
