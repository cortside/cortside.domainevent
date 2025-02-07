using System;
using System.Collections.Generic;
using Cortside.Common.Validation;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

            services.AddSingleton(options.ReceiverSettings);

            // register and setup settings for handlers
            options.HostedServiceSettings.MessageTypes = new Dictionary<string, Type>();
            //foreach (var handler in options.Handlers) {
            //    var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(handler.Value.Event);
            //    services.AddTransient(handlerType, handler.Value.Handler);
            //    options.HostedServiceSettings.MessageTypes.Add(handler.Key, handler.Value.Event);
            //}

            //// Register Hosted Services
            //services.AddSingleton(options.HostedServiceSettings);
            //services.AddSingleton<IDomainEventReceiver, DomainEventReceiver>();
            //services.AddHostedService<ReceiverHostedService>();
            foreach (var handler in options.Handlers) {
                var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(handler.Value.Event);
                services.AddTransient(handlerType, handler.Value.Handler);
                options.HostedServiceSettings.MessageTypes.Add(handler.Key, handler.Value.Event);
            }

            // Register Hosted Services
            //services.AddKeyedSingleton(options.ReceiverSettings.Key, options.HostedServiceSettings); // needed??
            services.AddKeyedSingleton(options.ReceiverSettings.Key, (sp, IDomainEventReceiver) => {
                var loggerFactory = sp.GetService<ILoggerFactory>();
                var receiver = new DomainEventReceiver(options.ReceiverSettings, sp, loggerFactory.CreateLogger<DomainEventReceiver>());
                return receiver;
            });

            services.AddHostedService<ReceiverHostedService>((sp) => {
                var loggerFactory = sp.GetService<ILoggerFactory>();
                //var receiver = sp.GetKeyedService<IDomainEventReceiver>(options.ReceiverSettings.Key);
                var service = new ReceiverHostedService(loggerFactory.CreateLogger<ReceiverHostedService>(), sp, options.HostedServiceSettings, options.ReceiverSettings);

                return service;
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
                // tech debt
                options.ReceiverSettings.Service = configuration[$"DomainEvent:Connections:{index}:Key"];
                options.ReceiverSettings.AppName = configuration[$"DomainEvent:Connections:{index}:Key"];
                Guard.From.NullOrWhitespace(options.ReceiverSettings.Key, nameof(options.ReceiverSettings.Key));

                //services.AddKeyedSingleton(options.ReceiverSettings.Key, options.ReceiverSettings); // do we actually need to do this if we use ctor with specific settings??

                // register and setup settings for handlers
                options.HostedServiceSettings.MessageTypes = new Dictionary<string, Type>();
                foreach (var handler in options.Handlers) {
                    var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(handler.Value.Event);
                    services.AddTransient(handlerType, handler.Value.Handler);
                    options.HostedServiceSettings.MessageTypes.Add(handler.Key, handler.Value.Event);
                }

                // Register Hosted Services
                //services.AddKeyedSingleton(options.ReceiverSettings.Key, options.HostedServiceSettings); // needed??
                services.AddKeyedSingleton(options.ReceiverSettings.Key, (sp, IDomainEventReceiver) => {
                    var loggerFactory = sp.GetService<ILoggerFactory>();
                    var receiver = new DomainEventReceiver(options.ReceiverSettings, sp, loggerFactory.CreateLogger<DomainEventReceiver>());
                    return receiver;
                });

                // doesn't work, only one gets registered
                //services.AddHostedService((sp) => {
                //    var loggerFactory = sp.GetService<ILoggerFactory>();
                //    //var receiver = sp.GetKeyedService<IDomainEventReceiver>(options.ReceiverSettings.Key);
                //    return new ReceiverHostedService(loggerFactory.CreateLogger<ReceiverHostedService>(), sp, options.HostedServiceSettings, options.ReceiverSettings);
                //});

                // workaround to AddHostedService extension: https://github.com/dotnet/runtime/issues/38751#issuecomment-1967830195
                services.AddSingleton<IHostedService, ReceiverHostedService>(sp => {
                    var loggerFactory = sp.GetService<ILoggerFactory>();
                    return new ReceiverHostedService(loggerFactory.CreateLogger<ReceiverHostedService>(), sp, options.HostedServiceSettings, options.ReceiverSettings);
                });

                // does not work - both get registered but code underneath does not resolve them and start them up per usual... possibly start them explicitly another way??
                //services.AddKeyedHostedService(options.ReceiverSettings.Key, (sp, ReceiverHostedService) => {
                //    var loggerFactory = sp.GetService<ILoggerFactory>();
                //    //var receiver = sp.GetKeyedService<IDomainEventReceiver>(options.ReceiverSettings.Key);
                //    var service = new ReceiverHostedService(loggerFactory.CreateLogger<ReceiverHostedService>(), sp, options.HostedServiceSettings, options.ReceiverSettings);

                //    return service;
                //});
            }


            return services;
        }

        public static IServiceCollection AddKeyedHostedService<THostedService>(this IServiceCollection services, object? serviceKey, Func<IServiceProvider, object?, THostedService> implementationFactory)
            where THostedService : class, IHostedService {
            services.TryAddEnumerable(ServiceDescriptor.KeyedSingleton<IHostedService>(serviceKey, implementationFactory));

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
