using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cortside.DomainEvent.EntityFramework.Hosting;
using Cortside.DomainEvent.EntityFramework.IntegrationTests.Database;
using Cortside.DomainEvent.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests {
    public class OutboxHostedReceiverTest {
        private readonly ServiceCollection services;
        private readonly Mock<IDomainEventPublisher> publisher;
        private readonly Outbox outbox;
        private readonly EntityContext context;

        public OutboxHostedReceiverTest() {
            services = new ServiceCollection();
            services.AddLogging();
            services.AddHostedService<OutboxHostedService<EntityContext>>();

            var options = new DbContextOptionsBuilder<EntityContext>()
                .UseInMemoryDatabase($"OutboxHostedService-{Guid.NewGuid()}")
                .Options;
            context = new EntityContext(options);
            outbox = new Outbox() { EventType = "foo", Topic = "bar", RoutingKey = "baz", Body = "{}", CorrelationId = Guid.NewGuid().ToString(), MessageId = Guid.NewGuid().ToString(), LockId = null };
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
            services.AddSingleton(new OutboxHostedServiceConfiguration() { Enabled = true, Interval = 1, BatchSize = 1000, PurgePublished = false });

            var serviceProvider = services.BuildServiceProvider();

            var service = serviceProvider.GetService<IHostedService>() as OutboxHostedService<EntityContext>;

            var source = new CancellationTokenSource();
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
        public async Task ShouldPurgePublishedMessagesAsync() {
            publisher.Setup(x => x.PublishAsync(outbox.Body, It.IsAny<EventProperties>()));
            services.AddSingleton<IDomainEventPublisher>(publisher.Object);
            services.AddSingleton(new ReceiverHostedServiceSettings() { Enabled = true, MessageTypes = new Dictionary<string, Type>() });
            services.AddSingleton(new OutboxHostedServiceConfiguration() { Enabled = true, Interval = 1, BatchSize = 1000, PurgePublished = true });

            var serviceProvider = services.BuildServiceProvider();

            var service = serviceProvider.GetRequiredService<IHostedService>() as OutboxHostedService<EntityContext>;

            CancellationTokenSource source = new CancellationTokenSource();
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

            var service = serviceProvider.GetService<IHostedService>() as OutboxHostedService<EntityContext>;

            var source = new CancellationTokenSource();
            await service.StartAsync(source.Token);

            await Task.Delay(3000);

            var messages = await context.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Null(messages[0].LockId);
            Assert.Equal(OutboxStatus.Published, messages[0].Status);
            Assert.NotNull(messages[0].PublishedDate);
            Assert.Equal(2, messages[0].PublishCount);
            publisher.VerifyAll();

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

            var source = new CancellationTokenSource();
            await service.StartAsync(source.Token);

            await Task.Delay(6000);

            var messages = await context.Set<Outbox>().ToListAsync();
            Assert.Single(messages);
            Assert.Null(messages[0].LockId);
            Assert.Equal(OutboxStatus.Failed, messages[0].Status);
            Assert.Null(messages[0].PublishedDate);
            Assert.Equal(3, messages[0].PublishCount);
            publisher.VerifyAll();

            await service.StopAsync(source.Token);
        }
    }
}
