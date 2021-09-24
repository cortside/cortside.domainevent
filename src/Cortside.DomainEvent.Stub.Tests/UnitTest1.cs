using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cortside.DomainEvent.Stub.Tests {
    public class UnitTest1 {
        [Fact]
        public async Task ShouldPublishAndHandleMessage() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            var provider = services.BuildServiceProvider();

            var eventTypeLookup = new Dictionary<string, Type> {
                {typeof(TestEvent).FullName, typeof(TestEvent)},
            };

            var broker = new QueueBroker();
            var psettings = new DomainEventPublisherSettings() { Topic = "topic" };
            var rsettings = new DomainEventReceiverSettings();
            var publisher = new DomainEventPublisherStub(psettings, new NullLogger<DomainEventPublisherStub>(), broker);
            var receiver = new DomainEventReceiverStub(rsettings, provider, new NullLogger<DomainEventReceiverStub>(), broker);

            receiver.StartAndListen(eventTypeLookup);
            await publisher.PublishAsync<TestEvent>(new TestEvent() { IntValue = 1 });

            await Task.Delay(12000);
            Assert.False(broker.HasItems);
            Assert.False(broker.HasDeadLetterItems);
        }
    }
}
