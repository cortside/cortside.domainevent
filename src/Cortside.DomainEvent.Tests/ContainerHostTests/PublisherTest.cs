using System;
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
            await publisher.SendAsync(@event);

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
            await publisher.SendAsync(@event, correlationId);

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
            await publisher.SendAsync(@event, correlationId, messageId);

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
            this.host.RegisterMessageProcessor("bar", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.SendAsync<TestEvent>(@event, "foo", "bar", correlationId);

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
            this.host.RegisterMessageProcessor("bar", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.SendAsync("foo", "bar", JsonConvert.SerializeObject(@event), correlationId, messageId);

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
            await publisher.ScheduleMessageAsync<TestEvent>(@event, scheduleDate);

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
            await publisher.ScheduleMessageAsync<TestEvent>(@event, correlationId, scheduleDate);

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
            await publisher.ScheduleMessageAsync<TestEvent>(@event, correlationId, messageId, scheduleDate);

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
            this.host.RegisterMessageProcessor("bar", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleMessageAsync<TestEvent>(@event, "foo", "bar", correlationId, scheduleDate);

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
            this.host.RegisterMessageProcessor("bar", processor);

            var settings = this.publisterSettings.Copy();
            settings.Topic = topic;
            var publisher = new DomainEventPublisher(settings, new NullLogger<DomainEventPublisher>());
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new TestEvent() { IntValue = random.Next(), StringValue = Guid.NewGuid().ToString() };
            await publisher.ScheduleMessageAsync(JsonConvert.SerializeObject(@event), "foo", "bar", correlationId, messageId, scheduleDate);

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
    }
}
