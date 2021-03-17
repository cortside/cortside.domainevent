using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Amqp.Types;
using Cortside.DomainEvent.Tests.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {
    public partial class ContainerHostTest : BaseHostTest {
        [Fact]
        public async Task ShouldPublishEvent1() {
            // arrange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString(), Status = StatusEnum.PendingWork };
            await publisher.PublishAsync(@event).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            var body = message.GetBody<string>();
            Assert.Contains("\"Status\":1", body);
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(body).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(body).StringValue);
            Assert.Equal(@event.Status, JsonConvert.DeserializeObject<TestEvent>(body).Status);
            Assert.Equal(@event.GetType().FullName, message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
        }

        [Fact]
        public async Task ShouldPublishEvent1WithSerializationSettings() {
            // arrange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            settings.SerializerSettings = new JsonSerializerSettings() {
                Converters = new List<JsonConverter> {
                    new StringEnumConverter(new CamelCaseNamingStrategy())
                }
            };
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString(), Status = StatusEnum.PendingWork };
            await publisher.PublishAsync(@event).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            var body = message.GetBody<string>();
            Assert.Contains("\"Status\":\"pendingWork\"", body);
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(body).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(body).StringValue);
            Assert.Equal(@event.Status, JsonConvert.DeserializeObject<TestEvent>(body).Status);
            Assert.Equal(@event.GetType().FullName, message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
        }

        [Fact]
        public async Task ShouldPublishEvent2() {
            // arange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.PublishAsync(@event, correlationId).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            Assert.Equal(@event.GetType().FullName, message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
            Assert.Equal(correlationId, message.Properties.CorrelationId);
        }

        [Fact]
        public async Task ShouldPublishEvent3() {
            // arange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.PublishAsync(@event, new EventProperties() { CorrelationId = correlationId, MessageId = messageId }).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            Assert.Equal(@event.GetType().FullName, message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
            Assert.Equal(correlationId, message.Properties.CorrelationId);
            Assert.Equal(messageId, message.Properties.MessageId);
        }

        [Fact]
        public async Task ShouldPublishEvent4() {
            // arange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor("barTestEvent", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.PublishAsync<TestEvent>(@event, new EventProperties() { EventType = "foo", Topic = "bar", CorrelationId = correlationId }).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            Assert.Equal("foo", message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
            Assert.Equal(correlationId, message.Properties.CorrelationId);
        }

        [Fact]
        public async Task ShouldPublishEvent5() {
            // arange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor("barbaz", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.PublishAsync(JsonConvert.SerializeObject(@event), new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId, MessageId = messageId }).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            Assert.Equal("foo", message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
            Assert.Equal(correlationId, message.Properties.CorrelationId);
            Assert.Equal(messageId, message.Properties.MessageId);
        }

        [Fact]
        public async Task ShouldScheduleEvent1() {
            // arrange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleAsync<TestEvent>(@event, scheduleDate).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            Assert.Equal(@event.GetType().FullName, message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
            ((DateTime)message.MessageAnnotations[new Symbol(Constants.SCHEDULED_ENQUEUE_TIME_UTC)]).Should().BeCloseTo(scheduleDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent2() {
            // arrange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleAsync<TestEvent>(@event, scheduleDate, correlationId).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            Assert.Equal(@event.GetType().FullName, message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
            Assert.Equal(correlationId, message.Properties.CorrelationId);
            ((DateTime)message.MessageAnnotations[new Symbol(Constants.SCHEDULED_ENQUEUE_TIME_UTC)]).Should().BeCloseTo(scheduleDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent3() {
            // arrange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleAsync<TestEvent>(@event, scheduleDate, new EventProperties() { CorrelationId = correlationId, MessageId = messageId }).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            Assert.Equal(@event.GetType().FullName, message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
            Assert.Equal(correlationId, message.Properties.CorrelationId);
            Assert.Equal(messageId, message.Properties.MessageId);
            ((DateTime)message.MessageAnnotations[new Symbol(Constants.SCHEDULED_ENQUEUE_TIME_UTC)]).Should().BeCloseTo(scheduleDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent4() {
            // arrange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor("barbaz", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleAsync<TestEvent>(@event, scheduleDate, new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId }).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            Assert.Equal("foo", message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
            Assert.Equal(correlationId, message.Properties.CorrelationId);
            ((DateTime)message.MessageAnnotations[new Symbol(Constants.SCHEDULED_ENQUEUE_TIME_UTC)]).Should().BeCloseTo(scheduleDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent5() {
            // arrange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            host.RegisterMessageProcessor("barbaz", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleAsync(JsonConvert.SerializeObject(@event), scheduleDate, new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId, MessageId = messageId }).ConfigureAwait(false);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            Assert.Equal("foo", message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
            Assert.Equal(correlationId, message.Properties.CorrelationId);
            Assert.Equal(messageId, message.Properties.MessageId);
            ((DateTime)message.MessageAnnotations[new Symbol(Constants.SCHEDULED_ENQUEUE_TIME_UTC)]).Should().BeCloseTo(scheduleDate);
        }

        [Fact]
        public async Task ShouldPublishWithUsing() {
            // arrange
            string topic = Guid.NewGuid().ToString();
            List<Message> messages = new List<Message>();
            var processor = new TestMessageProcessor(50, messages);
            host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = publisterSettings.Copy();
            settings.Topic = topic;
            var count = 10;

            // act
            using (var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>())) {
                publisher.Connect();
                for (int i = 0; i < count; i++) {
                    var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                    await publisher.PublishAsync(@event).ConfigureAwait(false);
                }
            }

            // assert
            Assert.Equal(count, processor.Messages.Count);
        }
    }
}
