using Cortside.DomainEvent.Stub;
using Microsoft.Extensions.DependencyInjection;

namespace Cortside.DomainEvent.EntityFramework {
    public static class ServiceCollectionExtensions {
        /// <summary>
        /// Registers in memory stub publisher and receiver
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddDomainEventStubs(this IServiceCollection services) {
            services.AddSingleton<IStubBroker, ConcurrentQueueBroker>();
            services.AddTransient<IDomainEventPublisher, DomainEventPublisherStub>();
            services.AddTransient<IDomainEventReceiver, DomainEventReceiverStub>();

            return services;
        }
    }
}
