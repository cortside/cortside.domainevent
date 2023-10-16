using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cortside.Common.Testing.Logging;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cortside.DomainEvent.IntegrationTests {
    public class ManualTest {
        protected readonly IServiceProvider serviceProvider;
        protected readonly Dictionary<string, Type> eventTypes;
        protected readonly Random r;
        protected DomainEventPublisher publisher;
        protected readonly LogEventLogger<DomainEventPublisher> mockLogger;
        protected readonly DomainEventReceiverSettings receiverSettings;
        protected readonly DomainEventPublisherSettings publisherSettings;
        protected readonly bool enabled;

        public ManualTest() {
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

            mockLogger = new LogEventLogger<DomainEventPublisher>();

            receiverSettings = configRoot.GetSection("ServiceBus").Get<DomainEventReceiverSettings>();
            publisherSettings = configRoot.GetSection("ServiceBus").Get<DomainEventPublisherSettings>();
            publisher = new DomainEventPublisher(publisherSettings, mockLogger);

            enabled = configRoot.GetValue<bool>("EnableE2ETests");
        }

        [Fact(Skip = "for manual testing")]
        public async Task ReceiveOne() {
            EventMessage message;
            var logger = new LogEventLogger<DomainEventReceiver>();

            do {
                using (var receiver = new DomainEventReceiver(receiverSettings, serviceProvider, logger)) {
                    receiver.Start(eventTypes);
                    message = await receiver.ReceiveAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    message?.Accept();
                }
                Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
            } while (message != null);
        }
    }
}
