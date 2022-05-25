using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {

    public partial class ContainerHostTest : BaseHostTest {

        [Fact]
        public async Task ShouldReceiveMessage_AcceptAsync() {
            receiverSettings.Queue = Guid.NewGuid().ToString();

            List<Message> messages = new List<Message>();
            host.RegisterMessageProcessor(receiverSettings.Queue + "TestEvent", new TestMessageProcessor(50, messages));
            linkProcessor = new TestLinkProcessor();
            host.RegisterLinkProcessor(linkProcessor);

            const int count = 1;
            publisterSettings.Topic = receiverSettings.Queue;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventPublisher>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.PublishAsync(@event).ConfigureAwait(false);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            host.RegisterMessageSource(receiverSettings.Queue, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventReceiver>())) {
                receiver.Start(eventTypes);
                for (int i = 0; i < count; i++) {
                    var message = await receiver.ReceiveAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    Assert.NotNull(message.GetData<TestEvent>());
                    message.Accept();
                }
            }

            Assert.Equal(0, source.DeadLetterCount);
            Assert.Equal(0, source.Count);
        }

        [Fact]
        public async Task ShouldReceiveMessage_RejectAsync() {
            receiverSettings.Queue = Guid.NewGuid().ToString();

            List<Message> messages = new List<Message>();
            host.RegisterMessageProcessor(receiverSettings.Queue + "TestEvent", new TestMessageProcessor(50, messages));
            linkProcessor = new TestLinkProcessor();
            host.RegisterLinkProcessor(linkProcessor);

            const int count = 1;
            publisterSettings.Topic = receiverSettings.Queue;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventPublisher>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.PublishAsync(@event).ConfigureAwait(false);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            host.RegisterMessageSource(receiverSettings.Queue, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventReceiver>())) {
                receiver.Start(eventTypes);
                int waits = 0;
                do {
                    await Task.Delay(1000).ConfigureAwait(false);
                    if (receiver.Link.LinkState == LinkState.Attached) {
                        break;
                    }
                    waits++;
                }
                while (waits < 20);

                for (int i = 0; i < count; i++) {
                    var message = await receiver.ReceiveAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                    message.Reject();
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }

            Assert.Equal(count, source.DeadLetterCount);
            Assert.Equal(0, source.Count);
        }

        [Fact(Skip = "concurrency issue")]
        public async Task ShouldReceiveMessage_ReleaseAsync() {
            receiverSettings.Queue = Guid.NewGuid().ToString();

            List<Message> messages = new List<Message>();
            host.RegisterMessageProcessor(receiverSettings.Queue + "TestEvent", new TestMessageProcessor(50, messages));
            linkProcessor = new TestLinkProcessor();
            host.RegisterLinkProcessor(linkProcessor);

            const int count = 1;
            publisterSettings.Topic = receiverSettings.Queue;
            var publisher = new DomainEventPublisher(publisterSettings, new NullLogger<DomainEventPublisher>());

            for (int i = 0; i < count; i++) {
                var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                await publisher.PublishAsync(@event).ConfigureAwait(false);
            }

            var source = new TestMessageSource(new Queue<Message>(messages));
            host.RegisterMessageSource(receiverSettings.Queue, source);
            using (var receiver = new DomainEventReceiver(receiverSettings, provider, new NullLogger<DomainEventReceiver>())) {
                receiver.Start(eventTypes);
                for (int i = 0; i < count; i++) {
                    var message = await receiver.ReceiveAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    message.Release();
                }
            }

            Assert.Equal(0, source.DeadLetterCount);
            Assert.Equal(count, source.Count);
        }
    }
}
