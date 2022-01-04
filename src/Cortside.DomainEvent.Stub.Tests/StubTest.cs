using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cortside.DomainEvent.Stub.Tests {
    public class StubTest {
        [Fact]
        public async Task ShouldPublishAndHandleMessageAsync() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            var provider = services.BuildServiceProvider();

            var eventTypeLookup = new Dictionary<string, Type> {
                {typeof(TestEvent).FullName, typeof(TestEvent)},
            };

            IStubBroker broker = new ConcurrentQueueBroker();
            var psettings = new DomainEventPublisherSettings() { Topic = "topic" };
            var rsettings = new DomainEventReceiverSettings();
            var publisher = new DomainEventPublisherStub(psettings, new NullLogger<DomainEventPublisherStub>(), broker);
            var receiver = new DomainEventReceiverStub(rsettings, provider, new NullLogger<DomainEventReceiverStub>(), broker);

            receiver.StartAndListen(eventTypeLookup);
            await publisher.PublishAsync(new TestEvent() { IntValue = 1 }).ConfigureAwait(false);

            await Task.Delay(2000).ConfigureAwait(false);
            Assert.False(broker.HasItems);
            Assert.False(broker.HasDeadLetterItems);
            var messages = broker.GetAcceptedMessagesByType<TestEvent>();
            Assert.NotNull(messages);
            Assert.Equal(1, messages.First().IntValue);

            // test the filter
            messages = broker.GetAcceptedMessagesByType<TestEvent>(x => x.IntValue == 1);
            Assert.NotNull(messages);
            Assert.Equal(1, messages.First().IntValue);
        }
    }
}
