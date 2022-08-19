using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Amqp;

namespace Cortside.DomainEvent.Stub
{
    public interface IStubBroker
    {
        bool HasItems { get; }
        bool HasDeadLetterItems { get; }

        ReadOnlyCollection<Message> AcceptedItems { get; }
        ReadOnlyCollection<Message> ActiveItems { get; }
        ReadOnlyCollection<Message> DeadLetterItems { get; }
        ReadOnlyCollection<Message> UnmappedItems { get; }

        public int Published { get; }
        public int Accepted { get; }

        void Enqueue(Message message);
        void Dequeue();
        Message Peek();
        void ResetQueue();

        void Accept(Message message);
        void Reject(Message message);
        void Release(Message message);

        void Shovel();

        void EnqueueUnmapped(Message message);

        List<T> GetAcceptedMessagesByType<T>(Func<T, bool> predicate = null);
        List<T> GetActiveMessagesByType<T>(Func<T, bool> predicate = null);
        List<T> GetDLQMessagesByType<T>(Func<T, bool> predicate = null);
        List<T> GetUnmappedMessagesByType<T>(Func<T, bool> predicate = null);

        // WaitUntilConsumed()
    }
}
