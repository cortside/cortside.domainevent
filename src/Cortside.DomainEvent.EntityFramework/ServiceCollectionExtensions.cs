using System.Collections.Generic;
using Cortside.Common.Validation;
using Cortside.DomainEvent.EntityFramework.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        /// <summary>
        /// Registers multiple outbox publishers and hosted services.  Relies on settings sections:
        ///     DomainEvent:Connections[]
        ///     DomainEvent:Connections:0:OutboxHostedService
        ///
        ///     ServiceBus and root level OutboxHostedService are not used and are obsolete
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddDomainEventOutboxPublishers<T>(this IServiceCollection services, IConfiguration configuration) where T : DbContext {

            var settingsList = configuration.GetSection("DomainEvent:Connections").Get<IList<KeyedDomainEventPublisherSettings>>();
            Guard.From.Null(settingsList, nameof(settingsList));

            // TODO: if only one, only register one (non-keyed) so consumers don't have to use FromKeyedServicesAttribute everywhere IDomainEventOutboxPublisher is injected??

            foreach (var settings in settingsList) {
                var index = settingsList.IndexOf(settings);
                Guard.From.NullOrWhitespace(settings.Key, nameof(settings.Key));
                // tech debt
                settings.Service = configuration[$"DomainEvent:Connections:{index}:Key"]; // needed for SenderLink
                settings.AppName = configuration[$"DomainEvent:Connections:{index}:Key"];

                services.AddKeyedDomainEventPublisher(settings);
                // register publisher
                services.AddKeyedScoped<IDomainEventOutboxPublisher, DomainEventOutboxPublisher<T>>(settings.Key, (sp, obj) => {
                    var loggerFactory = sp.GetService<ILoggerFactory>();
                    var context = sp.GetService<T>();
                    Guard.From.Null(context, nameof(context));
                    return new DomainEventOutboxPublisher<T>(settings, context, loggerFactory.CreateLogger<DomainEventOutboxPublisher<T>>());
                });

                // outbox hosted service
                var outboxConfiguration = configuration.GetSection($"DomainEvent:Connections:{index}:OutboxHostedService").Get<OutboxHostedServiceConfiguration>();
                //services.AddKeyedSingleton(settings.Key, outboxConfiguration); // needed?
                // do not use AddHostedService extension: https://github.com/dotnet/runtime/issues/38751#issuecomment-1967830195
                services.AddSingleton<IHostedService, OutboxHostedService<T>>(sp => {
                    var loggerFactory = sp.GetService<ILoggerFactory>();
                    return new OutboxHostedService<T>(loggerFactory.CreateLogger<OutboxHostedService<T>>(), outboxConfiguration, sp, settings);
                });


            }

            return services;
        }
    }
}
