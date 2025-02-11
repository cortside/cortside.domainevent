using System;
using System.Collections.Generic;
using Cortside.Common.Validation;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        public static IServiceCollection AddKeyedDomainEventReceiver(this IServiceCollection services, Action<KeyedDomainEventReceiverOptions> options) {
            var o = new KeyedDomainEventReceiverOptions();
            options?.Invoke(o);

            return services.AddKeyedDomainEventReceiver(o);
        }

        public static IServiceCollection AddKeyedDomainEventReceiver(this IServiceCollection services, KeyedDomainEventReceiverOptions options) {
            Guard.From.Null(options, nameof(options));
            Guard.From.Null(options.ReceiverSettings, nameof(options.ReceiverSettings));
            Guard.From.Null(options.HostedServiceSettings, nameof(options.HostedServiceSettings));
            Guard.From.NullOrWhitespace(options.ReceiverSettings.Key, nameof(options.ReceiverSettings.Key));
            Guard.From.NullOrWhitespace(options.ReceiverSettings.Server, nameof(options.ReceiverSettings.Server));
            Guard.From.NullOrWhitespace(options.ReceiverSettings.Username, nameof(options.ReceiverSettings.Username));
            Guard.From.NullOrWhitespace(options.ReceiverSettings.Password, nameof(options.ReceiverSettings.Password));

            // register and setup settings for handlers
            options.HostedServiceSettings.MessageTypes = new Dictionary<string, Type>();
            foreach (var handler in options.Handlers) {
                var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(handler.Value.Event);
                services.AddTransient(handlerType, handler.Value.Handler);
                options.HostedServiceSettings.MessageTypes.Add(handler.Key, handler.Value.Event);
            }

            // Register Hosted Services
            //services.AddKeyedSingleton(settings.ReceiverSettings.Key, settings.ReceiverSettings); // do we actually need to do this if we use ctor with specific settings??
            //services.AddKeyedSingleton(settings.ReceiverSettings.Key, settings.HostedServiceSettings); // needed??
            services.AddKeyedSingleton(options.ReceiverSettings.Key, (sp, IDomainEventReceiver) => {
                var loggerFactory = sp.GetService<ILoggerFactory>();
                return new DomainEventReceiver(options.ReceiverSettings, sp, loggerFactory.CreateLogger<DomainEventReceiver>());
            });

            // do not use AddHostedService extension: https://github.com/dotnet/runtime/issues/38751#issuecomment-1967830195
            services.AddSingleton<IHostedService, ReceiverHostedService>(sp => {
                var loggerFactory = sp.GetService<ILoggerFactory>();
                return new ReceiverHostedService(loggerFactory.CreateLogger<ReceiverHostedService>(), sp, options.HostedServiceSettings, options.ReceiverSettings);
            });

            return services;
        }

        public static IServiceCollection AddKeyedDomainEventReceivers(this IServiceCollection services, IConfiguration configuration) {
            var optionsList = configuration.GetSection("DomainEvent:Connections").Get<IList<KeyedDomainEventReceiverOptions>>();
            Guard.From.Null(optionsList, nameof(optionsList));

            foreach (var options in optionsList) {
                var index = optionsList.IndexOf(options);
                options.HostedServiceSettings = configuration.GetSection($"DomainEvent:Connections:{index}:ReceiverHostedService").Get<ReceiverHostedServiceSettings>();
                options.ReceiverSettings = configuration.GetSection($"DomainEvent:Connections:{index}").Get<KeyedDomainEventReceiverSettings>();
                Guard.From.NullOrWhitespace(options.ReceiverSettings.Key, nameof(options.ReceiverSettings.Key));
                // tech debt
                options.ReceiverSettings.Service = configuration[$"DomainEvent:Connections:{index}:Key"];
                options.ReceiverSettings.AppName = configuration[$"DomainEvent:Connections:{index}:Key"];

                services = services.AddKeyedDomainEventReceiver(options);
            }

            return services;
        }

        /// <summary>
        /// Registers an outbox publisher with T DbContext class.  Relies on the following settings settings:
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

        public static IServiceCollection AddDomainEventPublishers(this IServiceCollection services, IConfiguration configuration) {
            var settingsList = configuration.GetSection("DomainEvent:Connections").Get<IList<KeyedDomainEventPublisherSettings>>();
            Guard.From.Null(settingsList, nameof(settingsList));

            foreach (var settings in settingsList) {
                var index = settingsList.IndexOf(settings);
                Guard.From.NullOrWhitespace(settings.Key, nameof(settings.Key));
                // tech debt
                settings.Service = configuration[$"DomainEvent:Connections:{index}:Key"]; // needed for SenderLink
                settings.AppName = configuration[$"DomainEvent:Connections:{index}:Key"];

                services = services.AddKeyedDomainEventPublisher(settings);
            }

            return services;
        }

        public static IServiceCollection AddKeyedDomainEventPublisher(this IServiceCollection services, KeyedDomainEventPublisherSettings settings) {
            Guard.From.Null(settings, nameof(settings));
            Guard.From.NullOrWhitespace(settings.Key, nameof(settings.Key));
            Guard.From.NullOrWhitespace(settings.Server, nameof(settings.Server));
            Guard.From.NullOrWhitespace(settings.Username, nameof(settings.Username));
            Guard.From.NullOrWhitespace(settings.Password, nameof(settings.Password));

            // Register publisher
            services.AddKeyedTransient<IDomainEventPublisher, DomainEventPublisher>(settings.Key, (sp, IDomainEventPublisher) => {
                var loggerFactory = sp.GetService<ILoggerFactory>();
                return new DomainEventPublisher(settings, loggerFactory.CreateLogger<DomainEventPublisher>());
            });



            return services;
        }
    }
}
