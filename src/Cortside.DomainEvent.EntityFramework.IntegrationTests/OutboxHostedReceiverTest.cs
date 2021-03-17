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

        [Fact]
        public async Task StartOutboxHostedService() {
            IServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.AddHostedService<OutboxHostedService<EntityContext>>();

            var options = new DbContextOptionsBuilder<EntityContext>()
                .UseInMemoryDatabase($"OutboxHostedService-{Guid.NewGuid()}")
                .Options;
            var context = new EntityContext(options);
            var outbox = new Outbox() { EventType = "foo", Topic = "bar", RoutingKey = "baz", Body = "{}", CorrelationId = Guid.NewGuid().ToString(), MessageId = Guid.NewGuid().ToString(), LockId = Guid.NewGuid().ToString() };
            context.Set<Outbox>().Add(outbox);
            await context.SaveChangesAsync().ConfigureAwait(false);
            services.AddSingleton(context);
            var publisher = new Mock<IDomainEventPublisher>();

            publisher.Setup(x => x.PublishAsync(outbox.Body, It.IsAny<EventProperties>()));
            services.AddSingleton<IDomainEventPublisher>(publisher.Object);
            services.AddSingleton(new ReceiverHostedServiceSettings() { Enabled = true, MessageTypes = new Dictionary<string, Type>() });
            services.AddSingleton(new OutboxHostedServiceConfiguration() { Enabled = true, Interval = 5 });

            var serviceProvider = services.BuildServiceProvider();

            var service = serviceProvider.GetService<IHostedService>() as OutboxHostedService<EntityContext>;

            CancellationTokenSource source = new CancellationTokenSource();
            await service.StartAsync(source.Token).ConfigureAwait(false);

            await Task.Delay(1000).ConfigureAwait(false);

            var messages = await context.Set<Outbox>().ToListAsync().ConfigureAwait(false);
            Assert.Single(messages);
            Assert.Null(messages[0].LockId);
            Assert.Equal(OutboxStatus.Published, messages[0].Status);
            Assert.NotNull(messages[0].PublishedDate);
            publisher.VerifyAll();

            source.Cancel();
            await service.StopAsync(source.Token).ConfigureAwait(false);
        }

    }
}
