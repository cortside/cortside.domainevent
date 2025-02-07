using System;
using System.Collections.Generic;
using Cortside.Common.Validation;
using Cortside.DomainEvent.Handlers;
using Cortside.DomainEvent.Hosting;
using Microsoft.Extensions.Configuration;

namespace Cortside.DomainEvent {
    public class KeyedDomainEventReceiverOptions {
        public KeyedDomainEventReceiverOptions() { }

        public KeyedDomainEventReceiverOptions(IConfiguration configuration, int arrayIndex) {
            UseConfiguration(configuration, arrayIndex);
        }

        public KeyedDomainEventReceiverSettings ReceiverSettings { get; set; }
        public ReceiverHostedServiceSettings HostedServiceSettings { get; set; }
        public Dictionary<string, (Type Event, Type Handler)> Handlers { get; set; } = new Dictionary<string, (Type Event, Type Handler)>();

        /// <summary>
        /// Uses configuration for initial values.  Relies on the following options settings:
        ///     DomainEvent:Connections[]:ReceiverHostedService
        /// </summary>
        /// <param name="configuration"></param>
        public void UseConfiguration(IConfiguration configuration, int arrayIndex) {
            ReceiverSettings = configuration.GetSection($"DomainEvent:Connections:{arrayIndex}").Get<KeyedDomainEventReceiverSettings>();
            Guard.From.Null(ReceiverSettings, nameof(ReceiverSettings));
            if (string.IsNullOrWhiteSpace(ReceiverSettings.Service)) {
                ReceiverSettings.AppName = configuration[$"DomainEvent:Connections:{arrayIndex}:Key"];
                ReceiverSettings.Service = configuration[$"DomainEvent:Connections:{arrayIndex}:Key"];
            }
            HostedServiceSettings = configuration.GetSection("ReceiverHostedService").Get<ReceiverHostedServiceSettings>();
            Guard.From.Null(HostedServiceSettings, nameof(HostedServiceSettings));
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
