using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cortside.DomainEvent.Stub.Tests {
    public class ServiceCollectionExtensionTest {
        private readonly ServiceCollection services;

        public ServiceCollectionExtensionTest() {
            services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDomainEventHandler<TestEvent>, TestEventHandler>();
        }

        [Fact]
        public void ShouldRegisterKeyedStubs() {
            // arrange
            var coolKey = "coolKey";
            var hotKey = "hotKey";

            // act
            services.AddKeyedDomainEventStubs(coolKey);
            services.AddKeyedDomainEventStubs(hotKey);
            var provider = services.BuildServiceProvider();

            // assert
            provider.GetKeyedService<IStubBroker>(coolKey).Should().NotBeNull();
            provider.GetKeyedService<IDomainEventReceiver>(coolKey).Should().NotBeNull();
            provider.GetKeyedService<IDomainEventPublisher>(coolKey).Should().NotBeNull();

            provider.GetKeyedService<IStubBroker>(hotKey).Should().NotBeNull();
            provider.GetKeyedService<IDomainEventReceiver>(hotKey).Should().NotBeNull();
            provider.GetKeyedService<IDomainEventPublisher>(hotKey).Should().NotBeNull();

        }


    }
}
