using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cortside.DomainEvent.EntityFramework.Hosting;
using Cortside.DomainEvent.EntityFramework.IntegrationTests.Database;
using Cortside.DomainEvent.EntityFramework.IntegrationTests.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shouldly;
using Xunit;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests {
    public class OutboxPublisherTest {
        private readonly IServiceProvider provider;

        public OutboxPublisherTest() {
            var services = new ServiceCollection();
            services.AddLogging();

            var options = new DbContextOptionsBuilder<EntityContext>()
                    .UseInMemoryDatabase($"DomainEventOutbox-{Guid.NewGuid()}")
                    .Options;
            var context = new EntityContext(options);
            Seed(context);
            services.AddSingleton(context);

            services.AddSingleton(new DomainEventPublisherSettings() { Topic = "topic." });
            services.AddSingleton(new OutboxHostedServiceConfiguration() { MaximumPublishCount = 3 });
            services.AddTransient<IDomainEventOutboxPublisher, DomainEventOutboxPublisher<EntityContext>>();

            provider = services.BuildServiceProvider();
        }

        private void Seed(EntityContext context) {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var one = new Widget() { Text = "one", Height = 1, Width = 1 };
            var two = new Widget() { Text = "two", Height = 2, Width = 2 };

            context.AddRange(one, two);
            context.SaveChanges();
        }

        [Fact]
        public async Task CanGetWidgetsAsync() {
            // arrange
            var context = provider.GetService<EntityContext>();

            // act
            var widgets = await context.Widgets.ToListAsync();

            // assert
            Assert.Equal(2, widgets.Count);
            Assert.Equal("one", widgets[0].Text);
            Assert.Equal("two", widgets[1].Text);
        }

        [Fact]
        public async Task ShouldPublishEvent1Async() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.PublishAsync(@event);
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
        }

        [Fact]
        public async Task ShouldPublishEvent2Async() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.PublishAsync(@event, correlationId);
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
        }

        [Fact]
        public async Task ShouldPublishEvent3Async() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.PublishAsync(@event, new EventProperties() { CorrelationId = correlationId, MessageId = messageId });
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
        }

        [Fact]
        public async Task ShouldPublishEvent4Async() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.PublishAsync(@event, new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId });
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal("foo", messages[0].EventType);
            Assert.Equal("bar", messages[0].Topic);
            Assert.Equal("baz", messages[0].RoutingKey);
        }

        [Fact]
        public async Task ShouldPublishEvent5Async() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.PublishAsync(JsonConvert.SerializeObject(@event), new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId, MessageId = messageId });
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
            Assert.Equal("foo", messages[0].EventType);
            Assert.Equal("bar", messages[0].Topic);
            Assert.Equal("baz", messages[0].RoutingKey);
        }

        [Fact]
        public async Task ShouldScheduleEvent1Async() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleAsync(@event, scheduleDate);
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            messages[0].ScheduledDate.ShouldBe(scheduleDate, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task ShouldScheduleEvent2Async() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleAsync(@event, scheduleDate, new EventProperties() { CorrelationId = correlationId });
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            messages[0].ScheduledDate.ShouldBe(scheduleDate, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task ShouldScheduleEvent3Async() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleAsync(@event, scheduleDate, new EventProperties() { CorrelationId = correlationId, MessageId = messageId });
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
            Assert.Equal(scheduleDate, messages[0].ScheduledDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent4Async() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleAsync(@event, scheduleDate, new EventProperties() { EventType = "foo", Topic = "bar", CorrelationId = correlationId });
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal("foo", messages[0].EventType);
            Assert.Equal("bar", messages[0].Topic);
            Assert.Equal("WidgetStateChangedEvent", messages[0].RoutingKey);
            Assert.Equal(scheduleDate, messages[0].ScheduledDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent5Async() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleAsync(JsonConvert.SerializeObject(@event), scheduleDate, new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId, MessageId = messageId });
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
            Assert.Equal("foo", messages[0].EventType);
            Assert.Equal("bar", messages[0].Topic);
            Assert.Equal("baz", messages[0].RoutingKey);
            Assert.Equal(scheduleDate, messages[0].ScheduledDate);
        }

        [Fact]
        public async Task ShouldPublishEventWithSerializerSettingsAsync() {
            // arrange
            var settings = provider.GetService<DomainEventPublisherSettings>();
            settings.SerializerSettings = new JsonSerializerSettings() {
                Converters = new List<JsonConverter> {
                    new StringEnumConverter(new SnakeCaseNamingStrategy())
                }
            };
            var outboxHostedServiceConfiguration = provider.GetService<OutboxHostedServiceConfiguration>();

            var db = provider.GetService<EntityContext>();
            var publisher = new DomainEventOutboxPublisher<EntityContext>(settings, outboxHostedServiceConfiguration, db, new NullLogger<DomainEventOutboxPublisher<EntityContext>>());

            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow, Status = WidgetStatus.CreatedWidget };
            await publisher.PublishAsync(@event, new EventProperties() { EventType = "foo", Topic = "bar", RoutingKey = "baz", CorrelationId = correlationId, MessageId = messageId });
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
            Assert.Equal("foo", messages[0].EventType);
            Assert.Equal("bar", messages[0].Topic);
            Assert.Equal("baz", messages[0].RoutingKey);
            Assert.Contains("created_widget", messages[0].Body);
        }
    }
}
