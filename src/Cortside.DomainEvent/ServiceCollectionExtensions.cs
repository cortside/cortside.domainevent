using System;
using System.Collections.Generic;
using Cortside.Common.Validation;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cortside.DomainEvent {
    public static class ServiceCollectionExtensions {

        public static IServiceCollection AddDomainEventReceiver(this IServiceCollection services, Action<DomainEventReceiverOptions> options) {
            var o = new DomainEventReceiverOptions();
            options?.Invoke(o);

            return services.AddDomainEventReceiver(o);
        }


        public static IServiceCollection AddDomainEventReceiver(this IServiceCollection services, DomainEventReceiverOptions options) {
            Guard.From.Null(options, nameof(options));
            Guard.From.Null(options.ReceiverSettings, nameof(options.ReceiverSettings));
            Guard.From.Null(options.HostedServiceSettings, nameof(options.HostedServiceSettings));

            services.AddSingleton(options.ReceiverSettings);

            // register and setup settings for handlers
            options.HostedServiceSettings.MessageTypes = new Dictionary<string, Type>();
            foreach (var handler in options.Handlers) {
                var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(handler.Value.Event);
                services.AddTransient(handlerType, handler.Value.Handler);
                options.HostedServiceSettings.MessageTypes.Add(handler.Key, handler.Value.Event);
            }

            // Register Hosted Services
            services.AddSingleton(options.HostedServiceSettings);
            services.AddSingleton<IDomainEventReceiver, DomainEventReceiver>();
            services.AddHostedService<ReceiverHostedService>();

            return services;
        }

        /// <summary>
        /// Registers an outbox publisher with T DbContext class.  Relies on the following options settings:
        ///     ServiceBus
        ///     Service
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddDomainEventPublisher(this IServiceCollection services, IConfiguration configuration) {
            var settings = configuration.GetSection("ServiceBus").Get<DomainEventPublisherSettings>();
            if (string.IsNullOrWhiteSpace(settings.Service)) {
                settings.AppName = configuration["Service:Name"];
            }

            services.AddSingleton(settings);

            // Register publisher
            services.AddTransient<IDomainEventPublisher, DomainEventPublisher>();

            return services;
        }
    }
}
