using Cortside.DomainEvent.EntityFramework.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cortside.DomainEvent.EntityFramework {
    public static class ServiceCollectionExtensions {
        /// <summary>
        /// Registers an outbox publisher hosted service using T DbContext class.  Relies on the following options settings:
        ///     OutboxHostedService
        ///     ServiceBus
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddDomainEventOutboxPublisher<T>(this IServiceCollection services, IConfiguration configuration) where T : DbContext {
            services.AddDomainEventPublisher(configuration);

            // Register publisher
            services.AddScoped<IDomainEventOutboxPublisher, DomainEventOutboxPublisher<T>>();

            // outbox hosted service
            var outboxConfiguration = configuration.GetSection("OutboxHostedService").Get<OutboxHostedServiceConfiguration>();
            services.AddSingleton(outboxConfiguration);
            services.AddHostedService<OutboxHostedService<T>>();

            return services;
        }
    }
}
