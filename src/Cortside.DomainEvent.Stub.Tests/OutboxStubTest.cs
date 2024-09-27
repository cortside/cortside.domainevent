using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cortside.DomainEvent.Stub.Tests {
    public class OutboxStubTest {
        private readonly DomainEventOutboxPublisherStub publisher;
        private readonly DomainEventReceiverStub receiver;
        private readonly IStubBroker broker;

        public OutboxStubTest() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            var provider = services.BuildServiceProvider();

            var eventTypeLookup = new Dictionary<string, Type> {
                {typeof(TestEvent).FullName, typeof(TestEvent)},
            };

            broker = new ConcurrentQueueBroker();
            var psettings = new DomainEventPublisherSettings() { Topic = "topic" };
            var rsettings = new DomainEventReceiverSettings();
            publisher = new DomainEventOutboxPublisherStub(psettings, new NullLogger<DomainEventOutboxPublisherStub>(), broker);
            receiver = new DomainEventReceiverStub(rsettings, provider, new NullLogger<DomainEventReceiverStub>(), broker);

            receiver.StartAndListen(eventTypeLookup);
        }

        [Fact]
        public async Task ShouldPublishAndHandleMessageAsync() {
            await publisher.PublishAsync(new TestEvent() { IntValue = 1 });

            await Task.Delay(2000);
            Assert.False(broker.HasItems);
            Assert.False(broker.HasDeadLetterItems);
            var messages = broker.GetAcceptedMessagesByType<TestEvent>();
            Assert.NotNull(messages);
            Assert.Equal(1, messages[0].IntValue);

            // test the filter
            messages = broker.GetAcceptedMessagesByType<TestEvent>(x => x.IntValue == 1);
            Assert.NotNull(messages);
            Assert.Equal(1, messages[0].IntValue);
        }


    }
}
