using System;
using System.Collections.Generic;
using Amqp;

namespace Cortside.DomainEvent {
    public delegate void ReceiverClosedCallback(IDomainEventReceiver receiver, DomainEventError error);
    public interface IDomainEventReceiver {
        event ReceiverClosedCallback Closed;
        void StartAndListen(IDictionary<string, EventMapping> eventTypeLookup);
        void Close(TimeSpan? timeout = null);
        ReceiverLink Link { get; }
    }
}
