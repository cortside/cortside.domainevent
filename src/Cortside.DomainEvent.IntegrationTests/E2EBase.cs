using System;
using System.Collections.Generic;
using Cortside.Common.Testing.Logging.LogEvent;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests.Events;
using Cortside.DomainEvent.Tests.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true)]

namespace Cortside.DomainEvent.IntegrationTests {
    [CollectionDefinition("e2etests", DisableParallelization = true)]
    public class E2EBase {
        protected readonly IServiceProvider serviceProvider;
        protected readonly Dictionary<string, EventMapping> eventTypes;
        protected readonly Random r;
        protected DomainEventPublisher publisher;
        protected readonly LogEventLogger<DomainEventPublisher> mockLogger;
        protected readonly DomainEventReceiverSettings receiverSettings;
        protected readonly DomainEventPublisherSettings publisherSettings;
        protected readonly bool enabled;

        public E2EBase() {
            r = new Random();

            //Config
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.local.json", true);
            IConfiguration configRoot = config.Build();

            //IoC
            var collection = new ServiceCollection();
            collection.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            collection.AddLogging();
            serviceProvider = collection.BuildServiceProvider();

            eventTypes = new Dictionary<string, EventMapping> {
                { typeof(TestEvent).FullName, new EventMapping(typeof(TestEvent),typeof(TestEvent), typeof(TestEventHandler)) }
            };

            mockLogger = new LogEventLogger<DomainEventPublisher>();

            receiverSettings = configRoot.GetSection("ServiceBus").Get<DomainEventReceiverSettings>();
            publisherSettings = configRoot.GetSection("ServiceBus").Get<DomainEventPublisherSettings>();
            publisher = new DomainEventPublisher(publisherSettings, mockLogger);

            enabled = configRoot.GetValue<bool>("EnableE2ETests");
        }

        protected TestEvent NewTestEvent() {
            var @event = new TestEvent {
                IntValue = r.Next(),
                StringValue = Guid.NewGuid().ToString()
            };
            return @event;
        }
    }
}
