﻿using Amqp;

namespace Cortside.DomainEvent.Stub {
    public interface IQueueBroker {
        bool HasItems { get; }

        void Accept(Message message);
        void Enqueue(Message message);
        Message Peek();
        void Reject(Message message);
        void Release(Message message);
        void Shovel();
    }
}