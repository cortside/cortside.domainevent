using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amqp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Tests {
    public class TestReceiver : DomainEventReceiver {

        public TestReceiver(ServiceBusReceiverSettings settings, IServiceProvider provider, ILogger<DomainEventComms> logger) : base(settings, provider, logger) {
            //var connStr = "amqp://user:pass@127.0.0.1/";
            //var address = new Address(connStr);
            //var conn = new Mock<Connection>(address);
            //var session = new Mock<Session>(conn.Object);
            //Link = new Mock<ReceiverLink>(session.Object, String.Empty, String.Empty).Object;
        }

        public void Setup(IDictionary<string, Type> eventTypeLookup) {
            EventTypeLookup = eventTypeLookup;
        }

        public async Task MessageCallback(IReceiverLink receiver, Message message) {
            await OnMessageCallback(receiver, message);
        }

        internal void SetProvider(ServiceProvider provider) {
            base.Provider = provider;
        }
    }
}
