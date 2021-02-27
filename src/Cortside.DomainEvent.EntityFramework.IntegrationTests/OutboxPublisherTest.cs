using System;
using System.Threading.Tasks;
using Cortside.Common.DomainEvent;
using Cortside.DomainEvent.EntityFramework.IntegrationTests.Events;
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
        public async Task ShouldPublishEvent() {
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
    }
}
