using System;
using System.Threading.Tasks;
using Cortside.DomainEvent.EntityFramework.IntegrationTests.Database;
using Cortside.DomainEvent.EntityFramework.IntegrationTests.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests {
    public class OutboxPublisherTest {
        private readonly IServiceProvider provider;

        public OutboxPublisherTest() {
            var services = new ServiceCollection();

            services.AddLogging();

            var options = new DbContextOptionsBuilder<EntityContext>()
                    .UseInMemoryDatabase("DomainEventOutbox")
                    .Options;
            var context = new EntityContext(options);
            Seed(context);
            services.AddSingleton(context);

            services.AddSingleton(new ServiceBusPublisherSettings() { Address = "topic." });
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
        public async Task CanGetWidgets() {
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
        public async Task ShouldPublishEvent1() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.SendAsync(@event);
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
        }

        [Fact]
        public async Task ShouldPublishEvent2() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.SendAsync(@event, correlationId);
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
        }

        [Fact]
        public async Task ShouldPublishEvent3() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.SendAsync(@event, correlationId, messageId);
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
        }

        [Fact]
        public async Task ShouldPublishEvent4() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.SendAsync(@event, "foo", "bar", correlationId);
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal("foo", messages[0].EventType);
            Assert.Equal("bar", messages[0].Address);
        }

        //[Fact]
        //public async Task ShouldPublishEvent5() {
        //    // arrange
        //    var publisher = provider.GetService<IDomainEventOutboxPublisher>();
        //    var db = provider.GetService<EntityContext>();
        //    var correlationId = Guid.NewGuid().ToString();
        //    var messageId = Guid.NewGuid().ToString();

        //    // act
        //    var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
        //    await publisher.SendAsync<WidgetStateChangedEvent>(@event, "foo", "bar", correlationId, messageId);
        //    await db.SaveChangesAsync();

        //    // assert
        //    var messages = await db.Set<Outbox>().ToListAsync();
        //    Assert.Single(messages);
        //    Assert.Equal(correlationId, messages[0].CorrelationId);
        //    Assert.Equal(messageId, messages[0].MessageId);
        //    Assert.Equal("foo", messages[0].EventType);
        //    Assert.Equal("bar", messages[0].Address);
        //}

        [Fact]
        public async Task ShouldScheduleEvent1() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleMessageAsync(@event, scheduleDate);
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            messages[0].ScheduledDate.Should().BeCloseTo(scheduleDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent2() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleMessageAsync(@event, correlationId, scheduleDate);
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            messages[0].ScheduledDate.Should().BeCloseTo(scheduleDate);
        }

        [Fact]
        public async Task ShouldScheduleEvent3() {
            // arrange
            var publisher = provider.GetService<IDomainEventOutboxPublisher>();
            var db = provider.GetService<EntityContext>();
            var correlationId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var scheduleDate = DateTime.UtcNow.AddDays(1);

            // act
            var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
            await publisher.ScheduleMessageAsync(@event, correlationId, messageId, scheduleDate);
            await db.SaveChangesAsync();

            // assert
            var messages = await db.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Equal(correlationId, messages[0].CorrelationId);
            Assert.Equal(messageId, messages[0].MessageId);
            Assert.Equal(scheduleDate, messages[0].ScheduledDate);
        }

        //[Fact]
        //public async Task ShouldScheduleEvent4() {
        //    // arrange
        //    var publisher = provider.GetService<IDomainEventOutboxPublisher>();
        //    var db = provider.GetService<EntityContext>();
        //    var correlationId = Guid.NewGuid().ToString();
        //    var messageId = Guid.NewGuid().ToString();
        //    var scheduleDate = DateTime.UtcNow.AddDays(1);

        //    // act
        //    var @event = new WidgetStateChangedEvent() { WidgetId = 1, Timestamp = DateTime.UtcNow };
        //    await publisher.ScheduleMessageAsync(@event, "foo", "bar", correlationId, messageId, scheduleDate);
        //    await db.SaveChangesAsync();

        //    // assert
        //    var messages = await db.Set<Outbox>().ToListAsync();
        //    Assert.Single(messages);
        //    Assert.Equal(correlationId, messages[0].CorrelationId);
        //    Assert.Equal(messageId, messages[0].MessageId);
        //    Assert.Equal("foo", messages[0].EventType);
        //    Assert.Equal("bar", messages[0].Address);
        //    Assert.Equal(scheduleDate, messages[0].ScheduledDate);
        //}

    }
}
