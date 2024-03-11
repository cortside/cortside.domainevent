﻿using System;
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
        public Dictionary<string, EventMapping> Handlers { get; set; } = new Dictionary<string, EventMapping>();

        /// <summary>
        /// Uses configuration for initial values.  Relies on the following options settings:
        ///     ReceiverHostedService
        ///     ServiceBus
        ///     Service
        /// </summary>
        /// <param name="configuration"></param>
        public void UseConfiguration(IConfiguration configuration) {
            ReceiverSettings = configuration.GetSection("ServiceBus").Get<DomainEventReceiverSettings>();
            if (string.IsNullOrWhiteSpace(ReceiverSettings.Service)) {
                ReceiverSettings.AppName = configuration["Service:Name"];
            }
            HostedServiceSettings = configuration.GetSection("ReceiverHostedService").Get<ReceiverHostedServiceSettings>();
        }

        /// <summary>
        /// Adds a handler for a given event using default event type name
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        public void AddHandler<TEvent, THandler>() where TEvent : class where THandler : class {
            AddHandler<TEvent, THandler>(typeof(TEvent).FullName);
        }

        /// <summary>
        /// Adds a handler for a given event using specified event type name
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="key"></param>
        public void AddHandler<TEvent, THandler>(string key) where TEvent : class where THandler : class {
            var argumentTypes = new List<Type>();
            foreach (Type intType in typeof(THandler).GetInterfaces()) {
                if (intType.IsGenericType && intType.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)) {
                    argumentTypes.Add(intType.GetGenericArguments()[0]);
                }
            }

            if (argumentTypes.Count == 0) {
                throw new ArgumentException($"{typeof(THandler)} does not implement IDomainEventHandler<>");
            }

            var asType = argumentTypes[0];

            if (!asType.IsAssignableFrom(typeof(TEvent))) {
                throw new ArgumentException($"{typeof(TEvent)} is not assignable from {asType}");
            }

            Handlers.Add(key, new EventMapping(typeof(TEvent), asType, typeof(THandler)));
        }
    }
}
