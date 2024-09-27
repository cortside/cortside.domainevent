using Cortside.DomainEvent.EntityFramework;
using Microsoft.Extensions.DependencyInjection;

namespace Cortside.DomainEvent.Stub {
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
            services.AddTransient<IDomainEventOutboxPublisher, DomainEventOutboxPublisherStub>();


            return services;
        }
    }
}
