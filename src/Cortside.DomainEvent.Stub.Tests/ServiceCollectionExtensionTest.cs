using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Tests;
using Shouldly;
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
            provider.GetKeyedService<IStubBroker>(coolKey).ShouldNotBeNull();
            provider.GetKeyedService<IDomainEventReceiver>(coolKey).ShouldNotBeNull();
            provider.GetKeyedService<IDomainEventPublisher>(coolKey).ShouldNotBeNull();

            provider.GetKeyedService<IStubBroker>(hotKey).ShouldNotBeNull();
            provider.GetKeyedService<IDomainEventReceiver>(hotKey).ShouldNotBeNull();
            provider.GetKeyedService<IDomainEventPublisher>(hotKey).ShouldNotBeNull();

        }


    }
}
