using System;
using System.Collections.Generic;
using Cortside.DomainEvent.Tests;
using Cortside.DomainEvent.Tests.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cortside.DomainEvent.IntegrationTests {
    public class E2EBase {
        protected readonly IServiceProvider serviceProvider;
        protected readonly Dictionary<string, Type> eventTypes;
        protected readonly Random r;
        protected readonly DomainEventPublisher publisher;
        protected readonly MockLogger<DomainEventPublisher> mockLogger;
        protected readonly MessageBrokerReceiverSettings receiverSettings;
        protected readonly MessageBrokerPublisherSettings publisherSettings;
        protected readonly bool enabled;

        public E2EBase() {
            r = new Random();

            //Config
            var config = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .AddJsonFile("config.user.json", true);
            var configRoot = config.Build();

            //IoC
            var collection = new ServiceCollection();
            collection.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            serviceProvider = collection.BuildServiceProvider();

            eventTypes = new Dictionary<string, Type> {
                { typeof(TestEvent).FullName, typeof(TestEvent) }
            };

            mockLogger = new MockLogger<DomainEventPublisher>();

            var publisherSection = configRoot.GetSection("Publisher.Settings");
            publisherSettings = GetSettings<MessageBrokerPublisherSettings>(publisherSection);
            publisherSettings.Topic = publisherSection["Address"];
            publisher = new DomainEventPublisher(publisherSettings, mockLogger);

            var receiverSection = configRoot.GetSection("Receiver.Settings");
            receiverSettings = GetSettings<MessageBrokerReceiverSettings>(receiverSection);
            receiverSettings.Queue = publisherSection["Address"];
            enabled = configRoot.GetValue<bool>("EnableE2ETests");
        }

        protected T GetSettings<T>(IConfigurationSection section) where T : MessageBrokerSettings, new() {
            return new T {
                AppName = section["AppName"],
                Key = section["Key"],
                Namespace = section["Namespace"],
                PolicyName = section["Policy"],
                Protocol = section["Protocol"],
                Durable = Convert.ToUInt32(section["Durable"])
            };
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
