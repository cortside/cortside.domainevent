using System;
using System.Collections.Generic;
using System.Threading;
using Amqp;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests;
using Cortside.DomainEvent.Tests.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true)]

namespace Cortside.DomainEvent.IntegrationTests
{
    [CollectionDefinition("e2etests", DisableParallelization = true)]
    public class E2EBase
    {
        protected readonly IServiceProvider serviceProvider;
        protected readonly Dictionary<string, Type> eventTypes;
        protected readonly Random r;
        protected DomainEventPublisher publisher;
        protected readonly MockLogger<DomainEventPublisher> mockLogger;
        protected readonly DomainEventReceiverSettings receiverSettings;
        protected readonly DomainEventPublisherSettings publisherSettings;
        protected bool enabled;
        protected readonly string queue;
        protected readonly Address address;

        public E2EBase()
        {
            r = new Random();

            //Config
            var config = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .AddJsonFile("config.user.json", true);
            IConfiguration configRoot = config.Build();

            //IoC
            var collection = new ServiceCollection();
            collection.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
            collection.AddLogging();
            serviceProvider = collection.BuildServiceProvider();

            eventTypes = new Dictionary<string, Type> {
                { typeof(TestEvent).FullName, typeof(TestEvent) }
            };

            mockLogger = new MockLogger<DomainEventPublisher>();

            var domainEventSection = configRoot.GetSection("DomainEventSettings");
            receiverSettings = GetSettings<DomainEventReceiverSettings>(domainEventSection);
            receiverSettings.Queue = domainEventSection["Queue"];

            publisherSettings = GetSettings<DomainEventPublisherSettings>(domainEventSection);
            publisherSettings.Topic = domainEventSection["Topic"];

            publisher = new DomainEventPublisher(publisherSettings, mockLogger);

            enabled = configRoot.GetValue<bool>("EnableE2ETests");

            queue = receiverSettings.Queue;
            address = new Address(receiverSettings.ConnectionString);
        }

        protected T GetSettings<T>(IConfigurationSection section) where T : DomainEventSettings, new()
        {
            return new T
            {
                AppName = section["AppName"],
                Key = section["Key"],
                Namespace = section["Namespace"],
                PolicyName = section["Policy"],
                Protocol = section["Protocol"],
                Durable = Convert.ToUInt32(section["Durable"])
            };
        }

        protected TestEvent NewTestEvent()
        {
            var @event = new TestEvent
            {
                IntValue = r.Next(),
                StringValue = Guid.NewGuid().ToString()
            };
            return @event;
        }

        protected TimeSpan ReceiveAndWait(string correlationId, Amqp.Types.Map filter = null)
        {
            var tokenSource = new CancellationTokenSource();
            var start = DateTime.Now;

            using (var receiver = new DomainEventReceiver(receiverSettings, serviceProvider, new NullLogger<DomainEventReceiver>()))
            {
                receiver.Closed += (r, e) => tokenSource.Cancel();
                receiver.StartAndListen(eventTypes, filter);

                while (!TestEvent.Instances.ContainsKey(correlationId) && (DateTime.Now - start) < new TimeSpan(0, 0, 60))
                {
                    if (tokenSource.Token.IsCancellationRequested)
                    {
                        if (receiver.Error != null)
                        {
                            Assert.Equal(string.Empty, receiver.Error.Description);
                            Assert.Equal(string.Empty, receiver.Error.Condition);
                        }
                        Assert.True(receiver.Error == null);
                    }
                    Thread.Sleep(1000);
                } // run for 30 seconds
            }

            return DateTime.Now.Subtract(start);
        }
    }
}
