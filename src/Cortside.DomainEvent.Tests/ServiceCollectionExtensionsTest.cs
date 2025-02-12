using System.Collections.Generic;
using Cortside.DomainEvent.Health;
using Cortside.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Cortside.DomainEvent.Tests {
    public class ServiceCollectionExtensionsTest {
        [Fact]
        public void AddDomainEventReceiver() {
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
                        ["ReceiverHostedService:Enabled"] = "true",
                        ["ReceiverHostedService:TimedInterval"] = "60",
                        ["Build:Version"] = "1.0.0.0",
                        ["Build:Timestamp"] = "2024-08-30 15:01:19Z",
                        ["Build:Tag"] = "",
                        ["Build:Suffix"] = "",
                    })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddDomainEventReceiver(o => {
                o.UseConfiguration(configuration);
                o.AddHandler<TestEvent, TestEventHandler>();
            });
            var serviceProvider = services.BuildServiceProvider();
            var receiver = serviceProvider.GetService<IDomainEventReceiver>();

            // Assert
            Assert.NotNull(receiver);
        }

        [Fact]
        public void AddDomainEventHealthCheck() {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string> {
                        ["Service:Name"] = "test-api",
                        ["HealthCheckHostedService:Name"] = "test-api",
                        ["HealthCheckHostedService:Checks:0:Name"] = "domainevent",
                        ["HealthCheckHostedService:Checks:0:Type"] = "domainevent",
                        ["HealthCheckHostedService:Checks:0:Required"] = "true",
                        ["HealthCheckHostedService:Checks:0:Interval"] = "30",
                        ["HealthCheckHostedService:Checks:0:Timeout"] = "5",
                        ["Build:Version"] = "1.0.0.0",
                        ["Build:Timestamp"] = "2024-08-30 15:01:19Z",
                        ["Build:Tag"] = "",
                        ["Build:Suffix"] = "",
                    })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMemoryCache();
            var reciever = new Mock<IDomainEventReceiver>();
            services.AddSingleton(reciever.Object);

            // Act
            services.AddHealth(o => {
                o.UseConfiguration(configuration);
                o.AddCustomCheck("domainevent", typeof(DomainEventCheck));
            });

            var serviceProvider = services.BuildServiceProvider();
            var check = serviceProvider.GetService<DomainEventCheck>();

            // Assert
            Assert.NotNull(check);
        }

        [Fact]
        public void ShouldAddKeyedDomainEventReceiver() {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string> {
                        ["DomainEvent:Connections:0:Key"] = "test-api",
                        ["DomainEvent:Connections:0:Protocol"] = "amqp",
                        ["DomainEvent:Connections:0:Server"] = "localhost",
                        ["DomainEvent:Connections:0:Username"] = "localhost",
                        ["DomainEvent:Connections:0:Password"] = "localhost",
                        ["DomainEvent:Connections:0:Queue"] = "localhost",
                        ["DomainEvent:Connections:0:Topic"] = "localhost",
                        ["DomainEvent:Connections:0:ReceiverHostedService:Enabled"] = "true",
                        ["DomainEvent:Connections:0:ReceiverHostedService:TimedInterval"] = "60",
                    })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddKeyedDomainEventReceiver(o => {
                o.UseConfiguration(configuration, 0);
                o.AddHandler<TestEvent, TestEventHandler>();
            });
            var serviceProvider = services.BuildServiceProvider();
            var receiver = serviceProvider.GetKeyedService<IDomainEventReceiver>("test-api");

            // Assert
            Assert.NotNull(receiver);
        }

        [Fact]
        public void ShouldAddDomainEventReceivers() {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string> {
                        ["DomainEvent:Connections:0:Key"] = "test-api",
                        ["DomainEvent:Connections:0:Protocol"] = "amqp",
                        ["DomainEvent:Connections:0:Server"] = "localhost",
                        ["DomainEvent:Connections:0:Username"] = "localhost",
                        ["DomainEvent:Connections:0:Password"] = "localhost",
                        ["DomainEvent:Connections:0:Queue"] = "localhost",
                        ["DomainEvent:Connections:0:Topic"] = "localhost",
                        ["DomainEvent:Connections:0:ReceiverHostedService:Enabled"] = "true",
                        ["DomainEvent:Connections:0:ReceiverHostedService:TimedInterval"] = "60",
                    })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddDomainEventReceivers(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var receiver = serviceProvider.GetKeyedService<IDomainEventReceiver>("test-api");

            // Assert
            Assert.NotNull(receiver);
        }

        [Fact]
        public void ShouldAddDomainEventPublishers() {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string> {
                        ["DomainEvent:Connections:0:Key"] = "test-api",
                        ["DomainEvent:Connections:0:Protocol"] = "amqp",
                        ["DomainEvent:Connections:0:Server"] = "localhost",
                        ["DomainEvent:Connections:0:Username"] = "localhost",
                        ["DomainEvent:Connections:0:Password"] = "localhost",
                        ["DomainEvent:Connections:0:Queue"] = "localhost",
                        ["DomainEvent:Connections:0:Topic"] = "localhost",
                    })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();

            // Act
            services.AddDomainEventPublishers(configuration);
            var serviceProvider = services.BuildServiceProvider();
            var publisher = serviceProvider.GetKeyedService<IDomainEventPublisher>("test-api");

            // Assert
            Assert.NotNull(publisher);
        }
    }
}
