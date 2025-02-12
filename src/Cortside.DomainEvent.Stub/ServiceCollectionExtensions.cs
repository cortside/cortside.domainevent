using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

            return services;
        }

        /// <summary>
        /// Registers keyed in memory stub publisher and receiver, for when a service has multiple DomainEvent Connections
        /// </summary>
        /// <param name="services"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static IServiceCollection AddKeyedDomainEventStubs(this IServiceCollection services, string key) {
            services.AddKeyedSingleton<IStubBroker, ConcurrentQueueBroker>(key);
            services.AddKeyedTransient<IDomainEventPublisher, DomainEventPublisherStub>(key, (sp, obj) => {
                var loggerFactory = sp.GetService<ILoggerFactory>();
                var broker = sp.GetKeyedService<IStubBroker>(key);
                return new DomainEventPublisherStub(new DomainEventPublisherSettings { Topic = key, Service = key }, loggerFactory.CreateLogger<DomainEventPublisherStub>(), broker);
            });
            services.AddKeyedTransient<IDomainEventReceiver, DomainEventReceiverStub>(key, (sp, obj) => {
                var loggerFactory = sp.GetService<ILoggerFactory>();
                var broker = sp.GetKeyedService<IStubBroker>(key);
                return new DomainEventReceiverStub(new DomainEventReceiverSettings(), sp, loggerFactory.CreateLogger<DomainEventReceiverStub>(), broker);
            });
            return services;
        }
    }
}
