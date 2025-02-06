using System;
using System.Collections.Generic;
using Cortside.Common.Testing.Logging.LogEvent;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true)]

namespace Cortside.DomainEvent.IntegrationTests {
    [CollectionDefinition("e2etests", DisableParallelization = true)]
    public class E2EBase {
        protected readonly IServiceProvider serviceProvider;
        protected readonly Dictionary<string, Type> eventTypes;
        private readonly ILoggerFactory loggerFactory;
        protected readonly Random r;
        protected DomainEventPublisher publisher;
        protected readonly ILogger<DomainEventPublisher> publisherLogger;
        protected readonly ILogger<DomainEventReceiver> receiverLogger;
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

            eventTypes = new Dictionary<string, Type> {
                { typeof(TestEvent).FullName, typeof(TestEvent) }
            };

            loggerFactory = LoggerFactory.Create(builder => {
                builder
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("Cortside.Common", LogLevel.Trace)
                    .AddLogEvent();
            });

            publisherLogger = loggerFactory.CreateLogger<DomainEventPublisher>();
            receiverLogger = loggerFactory.CreateLogger<DomainEventReceiver>();

            receiverSettings = configRoot.GetSection("ServiceBus").Get<DomainEventReceiverSettings>();
            publisherSettings = configRoot.GetSection("ServiceBus").Get<DomainEventPublisherSettings>();
            publisher = new DomainEventPublisher(publisherSettings, publisherLogger);

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
