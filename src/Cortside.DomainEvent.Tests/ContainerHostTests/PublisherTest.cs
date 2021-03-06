using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Amqp.Types;
using Cortside.DomainEvent.Tests.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;

namespace Cortside.DomainEvent.Tests.ContainerHostTests {
    public partial class ContainerHostTest : BaseHostTest {
        [Fact]
        public async Task ShouldPublishEvent1() {
            // arrange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            this.host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.PublishAsync(@event);

            // assert
            Assert.Single(processor.Messages);
            var message = processor.Messages[0];
            Assert.Equal(@event.IntValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).IntValue);
            Assert.Equal(@event.StringValue, JsonConvert.DeserializeObject<TestEvent>(message.GetBody<string>()).StringValue);
            Assert.Equal(@event.GetType().FullName, message.ApplicationProperties[Constants.MESSAGE_TYPE_KEY]);
        }

        [Fact]
        public async Task ShouldPublishEvent2() {
            // arange
            string topic = Guid.NewGuid().ToString();
            var processor = new TestMessageProcessor();
            this.host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.PublishAsync(@event, correlationId);

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
            this.host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.PublishAsync(@event, new EventProperties() { CorrelationId = correlationId, MessageId = messageId });

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
            this.host.RegisterMessageProcessor("barTestEvent", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.PublishAsync<TestEvent>(@event, new EventProperties() { EventType = "foo", Topic = "bar", CorrelationId = correlationId });

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
            this.host.RegisterMessageProcessor("barbaz", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.PublishAsync(JsonConvert.SerializeObject(@event), new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId, MessageId = messageId });

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
            this.host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleAsync<TestEvent>(@event, scheduleDate);

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
            this.host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleAsync<TestEvent>(@event, scheduleDate, correlationId);

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
            this.host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleAsync<TestEvent>(@event, scheduleDate, new EventProperties() { CorrelationId = correlationId, MessageId = messageId });

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
            this.host.RegisterMessageProcessor("barbaz", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleAsync<TestEvent>(@event, scheduleDate, new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId });

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
            this.host.RegisterMessageProcessor("barbaz", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleAsync(JsonConvert.SerializeObject(@event), scheduleDate, new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId, MessageId = messageId });

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
            this.host.RegisterMessageProcessor(topic + "TestEvent", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var count = 10;

            // act
            using (var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>())) {
                publisher.Connect();
                for (int i = 0; i < count; i++) {
                    var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
                    await publisher.PublishAsync(@event);
                }
            }

            // assert
            Assert.Equal(count, processor.Messages.Count);
        }
    }
}
