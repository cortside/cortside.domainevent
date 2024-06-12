using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Amqp;
using Amqp.Framing;
using Cortside.Common.Testing.Logging.LogEvent;
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
    public class DomainEventReceiverTest {
        private readonly IServiceProvider serviceProvider;
        private readonly DomainEventReceiverSettings settings;
        private readonly LogEventLogger<DomainEventReceiver> logger;
        private readonly MockReceiver receiver;
        private readonly Mock<IReceiverLink> receiverLink;

        public DomainEventReceiverTest() {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TestEventHandler>();
            serviceProvider = services.BuildServiceProvider();

            settings = new DomainEventReceiverSettings();

            logger = new LogEventLogger<DomainEventReceiver>();
            receiver = new MockReceiver(settings, serviceProvider, logger);
            receiver.Setup(new Dictionary<string, EventMapping> {
                { typeof(TestEvent).FullName, new EventMapping(typeof(TestEvent), typeof(TestEvent), typeof(TestEventHandler)) }
            });

            receiverLink = new Mock<IReceiverLink>();
        }

        [Theory]
        [InlineData("ServiceBus:Service")]
        [InlineData("ServiceBus:AppName")]
        public void ShouldParseService(string key) {
            // arrange
            var value = Guid.NewGuid().ToString();
            var dictionary = new Dictionary<string, string> {
                {key, value},
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(dictionary)
                .Build();

            // act
            var model = configuration.GetSection("ServiceBus").Get<DomainEventReceiverSettings>();

            // assert
            Assert.Equal(value, model.Service);
        }

        [Theory]
        [InlineData("ServiceBus:Policy")]
        [InlineData("ServiceBus:PolicyName")]
        public void ShouldParsePolicy(string key) {
            // arrange
            var value = Guid.NewGuid().ToString();
            var dictionary = new Dictionary<string, string> {
                {key, value},
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(dictionary)
                .Build();

            // act
            var model = configuration.GetSection("ServiceBus").Get<DomainEventReceiverSettings>();

            // assert
            Assert.Equal(value, model.Policy);
        }

        [Fact]
        public async Task ShouldHandleWelformedJsonAsync() {
            // arrange
            var @event = new TestEvent() { IntValue = 1 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Accept(message));

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            receiverLink.VerifyAll();
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
        }

        [Theory]
        [InlineData("{")]
        [InlineData("{ \"contractorId\": \"6677\", \"contractorNumber\": \"1037\" \"sponsorNumber\": \"2910\" }")]
        public async Task ShouldHandleMalformedJsonAsync(string body) {
            // arrange
            var @event = new TestEvent();
            var eventType = @event.GetType().FullName;
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            receiverLink.VerifyAll();
            Assert.Contains(logger.LogEvents, x => x.LogLevel == LogLevel.Error && x.Message.Contains("errors deserializing messsage body"));
        }

        [Fact]
        public async Task ShouldHandleInvalidTypeAsync() {
            // arrange
            var @event = new TestEvent();
            var eventType = @event.GetType().FullName;
            const int body = 1;
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            receiverLink.VerifyAll();
            Assert.Contains(logger.LogEvents, x => x.LogLevel == LogLevel.Error && x.Message.Contains("invalid type"));
        }

        [Fact]
        public async Task ShouldHandleXmlDictionaryByteArrayAsync() {
            // arrange
            var @event = new TestEvent() { IntValue = 1 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, GetByteArray(body));

            receiverLink.Setup(x => x.Accept(message));

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
            receiverLink.VerifyAll();
        }

        [Fact]
        public async Task ShouldHandleUtf8StringByteArrayAsync() {
            // arrange
            var @event = new TestEvent() { IntValue = 1 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, Encoding.UTF8.GetBytes(body));

            receiverLink.Setup(x => x.Accept(message));

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
            receiverLink.VerifyAll();
        }

        [Fact]
        public async Task ShouldHandleMessageTypeNotFoundAsync() {
            // arrange
            var @event = new TestEvent();
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));
            receiver.Setup(new Dictionary<string, EventMapping>());

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            receiverLink.VerifyAll();
            Assert.Contains(logger.LogEvents, x => x.LogLevel == LogLevel.Error && x.Message.Contains("message type was not registered for type"));
        }

        [Fact]
        public async Task ShouldHandleHandlerNotFoundAsync() {
            // arrange
            var @event = new TestEvent();
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));
            var provider = new ServiceCollection().BuildServiceProvider();
            receiver.SetProvider(provider);

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            receiverLink.VerifyAll();
            Assert.Contains(logger.LogEvents, x => x.LogLevel == LogLevel.Error && x.Message.Contains("handler was not found for type"));
        }

        [Fact]
        public async Task ShouldHandleSuccessResultAsync() {
            // arrange
            var @event = new TestEvent() { IntValue = 2 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Accept(message));

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            receiverLink.VerifyAll();
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
        }

        [Fact]
        public async Task ShouldHandleFailedResultAsync() {
            // arrange
            var @event = new TestEvent() { IntValue = -1 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            receiverLink.VerifyAll();
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
        }

        [Fact(Skip = "no tx handling with containerhost")]
        public async Task ShouldHandleRetryResultAsync() {
            // arrange
            var @event = new TestEvent() { IntValue = 0 };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Accept(message));

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            receiverLink.VerifyAll();
            Assert.DoesNotContain(logger.LogEvents, x => x.LogLevel == LogLevel.Error);
        }

        [Fact]
        public async Task ShouldHandleUnhandledExceptionAsync() {
            // arrange
            var @event = new TestEvent() { IntValue = int.MinValue };
            var eventType = @event.GetType().FullName;
            var body = JsonConvert.SerializeObject(@event);
            Message message = CreateMessage(eventType, body);

            receiverLink.Setup(x => x.Reject(message, null));

            // act
            await receiver.MessageCallbackAsync(receiverLink.Object, message);

            // assert
            receiverLink.VerifyAll();
            Assert.Contains(logger.LogEvents, x => x.LogLevel == LogLevel.Error && x.Message.Contains("caught unhandled exception"));
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

        private byte[] GetByteArray(string body) {
            MemoryStream stream = new MemoryStream();
            DataContractSerializer s = new DataContractSerializer(typeof(string));
            XmlDictionaryWriter writer = XmlDictionaryWriter.CreateBinaryWriter(stream);
            writer.WriteStartDocument();
            s.WriteStartObject(writer, body);
            s.WriteObjectContent(writer, body);
            s.WriteEndObject(writer);
            writer.Flush();
            stream.Position = 0;

            return stream.ToArray();
        }
    }
}
