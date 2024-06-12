using System;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Cortside.Common.Testing.Logging.LogEvent;
using Cortside.DomainEvent.Hosting;
using Cortside.DomainEvent.Tests.Events;
using Cortside.DomainEvent.Tests.Handlers;
using Cortside.DomainEvent.Tests.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Cortside.DomainEvent.Tests {
    public class BaseDomainEventReceiverTest {
        private readonly IServiceProvider serviceProvider;
        private readonly DomainEventReceiverSettings settings;
        private readonly LogEventLogger<DomainEventReceiver> logger;
        private readonly MockReceiver receiver;
        private readonly Mock<IReceiverLink> receiverLink;

        public BaseDomainEventReceiverTest() {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var services = new ServiceCollection();
            services.AddLogging(o => o.AddConsole());

            // add domain event receiver with handlers
            services.AddDomainEventReceiver(o => {
                o.UseConfiguration(config);
                o.AddHandler<ActionEvent, ResourceIdEventHandler>();
                o.AddHandler<TestEvent, TestEventHandler>();
                o.AddHandler<ResourceIdEvent, ResourceIdEventHandler>();
            });

            serviceProvider = services.BuildServiceProvider();
            var receiverSettings = serviceProvider.GetRequiredService<ReceiverHostedServiceSettings>();

            settings = new DomainEventReceiverSettings();

            logger = new LogEventLogger<DomainEventReceiver>();
            receiver = new MockReceiver(settings, serviceProvider, logger);
            receiver.Setup(receiverSettings.MessageTypes);

            receiverLink = new Mock<IReceiverLink>();
        }

        [Fact]
        public async Task ShouldHandleByBaseTypeAsync() {
            // arrange
            var @event = new ActionEvent() {
                ResourceId = Guid.NewGuid(),
                Quantity = 1
            };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            var message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Accept(message));

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
            receiverLink.VerifyAll();
        }

        private Message CreateMessage(string eventType, object body) {
            var message = new Message(body) {
                ApplicationProperties = new ApplicationProperties(),
                Properties = new Properties() {
                    CorrelationId = Guid.NewGuid().ToString(),
                    MessageId = Guid.NewGuid().ToString()
                },
                Header = new Header() {
                    DeliveryCount = 1
                }
            };
            message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY] = eventType;
            return message;
        }
    }
}
