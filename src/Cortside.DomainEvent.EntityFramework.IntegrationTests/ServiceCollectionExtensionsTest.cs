using System.Collections.Generic;
using Cortside.DomainEvent.EntityFramework;
using Cortside.DomainEvent.EntityFramework.Hosting;
using Cortside.DomainEvent.EntityFramework.IntegrationTests.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests {
    public class ServiceCollectionExtensionsTest {
        [Fact]
        public void AddDomainEventOutboxPublisherLegacy() {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string> {
                        ["Service:Name"] = "test-api",
                        ["ServiceBus:Protocol"] = "amqp",
                        ["ServiceBus:Namespace"] = "localhost",
                        ["ServiceBus:Policy"] = "localhost",
                        ["ServiceBus:Key"] = "localhost",
                        ["ServiceBus:Queue"] = "localhost",
                        ["ServiceBus:Topic"] = "localhost",
                        ["OutboxHostedService:Enabled"] = "true",
                        ["OutboxHostedService:BatchSize"] = "5",
                        ["OutboxHostedService:Interval"] = "5",
                        ["OutboxHostedService:PurgePublished"] = "false"
                    })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddDbContext<EntityContext>(opt => {
                opt.UseSqlServer("Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;");
            });

            services.AddDomainEventOutboxPublisher<EntityContext>(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var publisher = serviceProvider.GetService<IDomainEventPublisher>();
            var outboxPublisher = serviceProvider.GetService<IDomainEventOutboxPublisher>();
            var outbox = serviceProvider.GetService<IHostedService>();

            // Assert
            Assert.NotNull(publisher);
            Assert.NotNull(outboxPublisher);
            Assert.NotNull(outbox);
            Assert.IsAssignableFrom<OutboxHostedService<EntityContext>>(outbox);
        }

        [Fact]
        public void AddDomainEventOutboxPublisher() {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string> {
                        ["Service:Name"] = "test-api",
                        ["DomainEvent:Connections:0:Protocol"] = "amqp",
                        ["DomainEvent:Connections:0:Server"] = "localhost",
                        ["DomainEvent:Connections:0:Username"] = "localhost",
                        ["DomainEvent:Connections:0:Password"] = "localhost",
                        ["DomainEvent:Connections:0:Queue"] = "localhost",
                        ["DomainEvent:Connections:0:Topic"] = "localhost",
                        ["DomainEvent:Connections:0:OutboxHostedService:Enabled"] = "true",
                        ["DomainEvent:Connections:0:OutboxHostedService:BatchSize"] = "5",
                        ["DomainEvent:Connections:0:OutboxHostedService:Interval"] = "5",
                        ["DomainEvent:Connections:0:OutboxHostedService:PurgePublished"] = "false"
                    })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddDbContext<EntityContext>(opt => {
                opt.UseSqlServer("Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;");
            });

            services.AddDomainEventOutboxPublisher<EntityContext>(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var publisher = serviceProvider.GetService<IDomainEventPublisher>();
            var outboxPublisher = serviceProvider.GetService<IDomainEventOutboxPublisher>();
            var outbox = serviceProvider.GetService<IHostedService>();

            // Assert
            Assert.NotNull(publisher);
            Assert.NotNull(outboxPublisher);
            Assert.NotNull(outbox);
            Assert.IsAssignableFrom<OutboxHostedService<EntityContext>>(outbox);
        }
    }
}
