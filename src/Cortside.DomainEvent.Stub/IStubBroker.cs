using System.Collections.ObjectModel;
using Amqp;

namespace Cortside.DomainEvent.Stub {
    public interface IStubBroker {
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

        void Accept(Message message);
        void Reject(Message message);
        void Release(Message message);

        void Shovel();

        void EnqueueUnmapped(Message message);

        // WaitUntilConsumed()
    }
}
