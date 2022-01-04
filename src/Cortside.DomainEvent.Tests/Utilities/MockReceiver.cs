using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Tests {
    public class MockReceiver : DomainEventReceiver {

        public MockReceiver(DomainEventReceiverSettings settings, IServiceProvider provider, ILogger<DomainEventReceiver> logger) : base(settings, provider, logger) {
        }

        public void Setup(IDictionary<string, Type> eventTypeLookup) {
            EventTypeLookup = eventTypeLookup;
        }

        public Task MessageCallbackAsync(IReceiverLink receiver, Message message) {
            return OnMessageCallbackAsync(receiver, message);
        }

        internal void SetProvider(ServiceProvider provider) {
            base.Provider = provider;
        }
    }
}
