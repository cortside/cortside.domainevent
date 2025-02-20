using System;
using System.Collections.Generic;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Hosting;
using Microsoft.Extensions.Configuration;

namespace Cortside.DomainEvent {
    public class DomainEventReceiverOptions {
        public DomainEventReceiverOptions() { }

        public DomainEventReceiverOptions(IConfiguration configuration) {
            UseConfiguration(configuration);
        }

        public DomainEventReceiverSettings ReceiverSettings { get; set; }
        public ReceiverHostedServiceSettings HostedServiceSettings { get; set; }
        public Dictionary<string, (Type Event, Type Handler)> Handlers { get; set; } = new Dictionary<string, (Type Event, Type Handler)>();

        /// <summary>
        /// Uses configuration for initial values.  Relies on the following options settings:
        ///     DomainEvent
        ///     Service
        ///
        /// Or to be obsoleted:
        ///     ReceiverHostedService
        ///     ServiceBus
        /// </summary>
        /// <param name="configuration"></param>
        public void UseConfiguration(IConfiguration configuration) {
            if (configuration.GetSection("DomainEvent:Connections").Exists()) {
                var connections = configuration.GetSection("DomainEvent:Connections").Get<IList<KeyedDomainEventReceiverSettings>>();

                if (connections.Count == 0) {
                    throw new InvalidOperationException("No connections found in configuration");
                }
                if (connections.Count > 1) {
                    // TODO: better state X
                    throw new InvalidOperationException("Multiple connections not supported for this extension method, please use X instead");
                }

                var connection = connections[0];
                ReceiverSettings = new DomainEventReceiverSettings() {
                    Protocol = connection.Protocol,
                    Namespace = connection.Server,
                    Policy = connection.Username,
                    Key = connection.Password,
                    Queue = connection.Queue,
                    Service = connection.Service,
                    Credits = connection.Credits,
                    Durable = connection.Durable,
                    UndefinedTypeName = connection.UndefinedTypeName
                };
                if (string.IsNullOrWhiteSpace(ReceiverSettings.Service)) {
                    ReceiverSettings.Service = configuration["Service:Name"];
                }

                HostedServiceSettings = configuration.GetSection("DomainEvent:Connections:0:ReceiverHostedService")
                    .Get<ReceiverHostedServiceSettings>();
            } else {
                ReceiverSettings = configuration.GetSection("ServiceBus").Get<DomainEventReceiverSettings>();
                if (string.IsNullOrWhiteSpace(ReceiverSettings.Service)) {
                    ReceiverSettings.Service = configuration["Service:Name"];
                }

                HostedServiceSettings = configuration.GetSection("ReceiverHostedService").Get<ReceiverHostedServiceSettings>();
            }
        }

        /// <summary>
        /// Adds a handler for a given event using default event type name
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        public void AddHandler<TEvent, THandler>() where TEvent : class where THandler : IDomainEventHandler<TEvent> {
            AddHandler<TEvent, THandler>(typeof(TEvent).FullName);
        }

        /// <summary>
        /// Adds a handler for a given event using specified event type name
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="key"></param>
        public void AddHandler<TEvent, THandler>(string key) where TEvent : class where THandler : IDomainEventHandler<TEvent> {
            Handlers.Add(key, (typeof(TEvent), typeof(THandler)));
        }
    }
}
