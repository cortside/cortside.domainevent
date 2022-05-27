using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cortside.Common.Correlation;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cortside.DomainEvent.Stub.Tests {
    public class StubTest {
        private readonly DomainEventPublisherStub publisher;
        private readonly DomainEventReceiverStub receiver;
        private readonly IStubBroker broker;

        public StubTest() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            var provider = services.BuildServiceProvider();

            var eventTypeLookup = new Dictionary<string, Type> {
                {typeof(TestEvent).FullName, typeof(TestEvent)},
            };

            this.broker = new ConcurrentQueueBroker();
            var psettings = new DomainEventPublisherSettings() { Topic = "topic" };
            var rsettings = new DomainEventReceiverSettings();
            this.publisher = new DomainEventPublisherStub(psettings, new NullLogger<DomainEventPublisherStub>(), broker);
            this.receiver = new DomainEventReceiverStub(rsettings, provider, new NullLogger<DomainEventReceiverStub>(), broker);

            this.receiver.StartAndListen(eventTypeLookup);
        }

        [Fact]
        public async Task ShouldPublishAndHandleMessageAsync() {
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

        [Fact]
        public async Task ShouldSetCorrelationIdAsync() {
            var correlationId = Guid.NewGuid().ToString();
            CorrelationContext.SetCorrelationId(Guid.NewGuid().ToString());

            var intValue = int.MaxValue;
            await publisher.PublishAsync(new TestEvent() { IntValue = intValue }, correlationId).ConfigureAwait(false);

            await Task.Delay(2000).ConfigureAwait(false);
            Assert.False(broker.HasItems);
            var messages = broker.GetAcceptedMessagesByType<TestEvent>();
            Assert.NotNull(messages);
            Assert.Equal(intValue, messages.First().IntValue);

            // test the filter
            messages = broker.GetAcceptedMessagesByType<TestEvent>(x => x.IntValue == intValue);
            Assert.NotNull(messages);
            Assert.Equal(intValue, messages.First().IntValue);
        }
    }
}
