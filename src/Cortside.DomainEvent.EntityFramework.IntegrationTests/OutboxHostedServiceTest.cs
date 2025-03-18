using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cortside.DomainEvent.EntityFramework.Hosting;
using Cortside.DomainEvent.EntityFramework.IntegrationTests.Database;
using Cortside.DomainEvent.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests {
    public class OutboxHostedServiceTest {
        private readonly ServiceCollection services;
        private readonly Mock<IDomainEventPublisher> publisher;
        private readonly Outbox outbox;
        private readonly EntityContext context;

        public OutboxHostedServiceTest() {
            services = new ServiceCollection();
            services.AddLogging();
            services.AddHostedService<OutboxHostedService<EntityContext>>();

            var options = new DbContextOptionsBuilder<EntityContext>()
                .UseInMemoryDatabase($"OutboxHostedService-{Guid.NewGuid()}")
                .Options;
            context = new EntityContext(options);
            outbox = new Outbox() { EventType = "foo", Topic = "bar", RoutingKey = "baz", Body = "{}", CorrelationId = Guid.NewGuid().ToString(), MessageId = Guid.NewGuid().ToString(), LockId = null, RemainingAttempts = 3 };
            context.Set<Outbox>().Add(outbox);
            context.SaveChanges();
            services.AddSingleton(context);
            publisher = new Mock<IDomainEventPublisher>();
        }

        [Fact]
        public async Task ShouldSuccessfullyPublishMessageAsync() {
            publisher.Setup(x => x.PublishAsync(outbox.Body, It.IsAny<EventProperties>()));
            services.AddSingleton<IDomainEventPublisher>(publisher.Object);
            services.AddSingleton(new ReceiverHostedServiceSettings() { Enabled = true, MessageTypes = [] });
            services.AddSingleton(new OutboxHostedServiceConfiguration() { Enabled = true, Interval = 1, BatchSize = 1000, PurgePublished = false, MaximumPublishCount = 3 });

            var serviceProvider = services.BuildServiceProvider();

            var service = serviceProvider.GetService<IHostedService>() as OutboxHostedService<EntityContext>;

            using var source = new CancellationTokenSource();
            await service.StartAsync(source.Token);

            await Task.Delay(1000);

            var messages = await context.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Null(messages[0].LockId);
            Assert.Equal(OutboxStatus.Published, messages[0].Status);
            Assert.NotNull(messages[0].PublishedDate);
            publisher.VerifyAll();

            await service.StopAsync(source.Token);
        }

        [Fact]
        public async Task ShouldSuccessfullyPublishKeyedMessageAsync() {
            var key = "connectionKey";
            var keyedoutbox = new Outbox() { Key = key, EventType = "foo", Topic = "bar", RoutingKey = "baz", Body = "{}", CorrelationId = Guid.NewGuid().ToString(), MessageId = Guid.NewGuid().ToString(), LockId = null, RemainingAttempts = 10 };
            context.Set<Outbox>().Add(keyedoutbox);
            await context.SaveChangesAsync();
            publisher.Setup(x => x.PublishAsync(outbox.Body, It.IsAny<EventProperties>()));
            services.AddKeyedSingleton<IDomainEventPublisher>(key, publisher.Object);
            var outboxConfiguration = new OutboxHostedServiceConfiguration() { Enabled = true, Interval = 1, BatchSize = 1000, PurgePublished = false };
            var settings = new KeyedDomainEventPublisherSettings { Key = key };
            services.AddSingleton<IHostedService, OutboxHostedService<EntityContext>>(sp => {
                var loggerFactory = sp.GetService<ILoggerFactory>();
                return new OutboxHostedService<EntityContext>(loggerFactory.CreateLogger<OutboxHostedService<EntityContext>>(), outboxConfiguration, sp, settings);
            });

            var serviceProvider = services.BuildServiceProvider();

            var service = serviceProvider.GetService<IHostedService>() as OutboxHostedService<EntityContext>;

            using var source = new CancellationTokenSource();
            await service.StartAsync(source.Token);

            await Task.Delay(1000);

            var messages = await context.Set<Outbox>().Where(x => x.Key == key).ToListAsync();
            Assert.Single(messages);
            Assert.Null(messages[0].LockId);
            Assert.Equal(OutboxStatus.Published, messages[0].Status);
            Assert.NotNull(messages[0].PublishedDate);
            publisher.VerifyAll();

            await service.StopAsync(source.Token);
        }

        [Fact]
        public async Task ShouldPurgePublishedMessagesAsync() {
            publisher.Setup(x => x.PublishAsync(outbox.Body, It.IsAny<EventProperties>()));
            services.AddSingleton<IDomainEventPublisher>(publisher.Object);
            services.AddSingleton(new ReceiverHostedServiceSettings() { Enabled = true, MessageTypes = [] });
            services.AddSingleton(new OutboxHostedServiceConfiguration() { Enabled = true, Interval = 1, BatchSize = 1000, PurgePublished = true });

            var serviceProvider = services.BuildServiceProvider();

            var service = serviceProvider.GetRequiredService<IHostedService>() as OutboxHostedService<EntityContext>;

            using var source = new CancellationTokenSource();
            await service.StartAsync(source.Token);

            await Task.Delay(1000);

            var messages = await context.Set<Outbox>().ToListAsync();
            Assert.Empty(messages);
            publisher.VerifyAll();

            await service.StopAsync(source.Token);
        }

        [Fact]
        public async Task ShouldRetryPublisherExceptionAsync() {
            publisher.SetupSequence(x => x.PublishAsync(outbox.Body, It.IsAny<EventProperties>()))
                .Throws<DomainEventPublisherException>()
                .Returns(Task.CompletedTask);
            services.AddSingleton<IDomainEventPublisher>(publisher.Object);
            services.AddSingleton(new ReceiverHostedServiceSettings() { Enabled = true, MessageTypes = [] });
            services.AddSingleton(new OutboxHostedServiceConfiguration() { Enabled = true, Interval = 1, BatchSize = 1000, PurgePublished = false });

            var serviceProvider = services.BuildServiceProvider();

            var service = serviceProvider.GetRequiredService<IHostedService>() as OutboxHostedService<EntityContext>;

            using var source = new CancellationTokenSource();
            await service.StartAsync(source.Token);

            await Task.Delay(3000);

            using (var scope = serviceProvider.CreateScope()) {
                var db = scope.ServiceProvider.GetService<EntityContext>();
                var messages = await db.Set<Outbox>().ToListAsync();
                Assert.Single(messages);
                Assert.Null(messages[0].LockId);
                Assert.Equal(OutboxStatus.Published, messages[0].Status);
                Assert.NotNull(messages[0].PublishedDate);
                Assert.Equal(2, messages[0].PublishCount);
                publisher.VerifyAll();
            }

            await service.StopAsync(source.Token);
        }

        [Fact]
        public async Task ShouldFailAfterMaximumPublishCountAsync() {
            publisher.Setup(x => x.PublishAsync(outbox.Body, It.IsAny<EventProperties>()))
                .Throws<DomainEventPublisherException>();
            services.AddSingleton<IDomainEventPublisher>(publisher.Object);
            services.AddSingleton(new ReceiverHostedServiceSettings() { Enabled = true, MessageTypes = [] });
            services.AddSingleton(new OutboxHostedServiceConfiguration() { Enabled = true, Interval = 1, BatchSize = 1000, PurgePublished = false, MaximumPublishCount = 3 });

            var serviceProvider = services.BuildServiceProvider();
            var service = serviceProvider.GetService<IHostedService>() as OutboxHostedService<EntityContext>;

            using var source = new CancellationTokenSource();
            await service.StartAsync(source.Token);

            await Task.Delay(6000);

            using (var scope = serviceProvider.CreateScope()) {
                var db = scope.ServiceProvider.GetService<EntityContext>();
                var messages = await db.Set<Outbox>().ToListAsync();
                Assert.Single(messages);
                Assert.Null(messages[0].LockId);
                Assert.Equal(OutboxStatus.Failed, messages[0].Status);
                Assert.Null(messages[0].PublishedDate);
                Assert.Equal(3, messages[0].PublishCount);
                publisher.VerifyAll();
            }

            await service.StopAsync(source.Token);
        }
    }
}
