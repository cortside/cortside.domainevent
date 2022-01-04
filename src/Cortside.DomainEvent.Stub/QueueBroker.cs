using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using Amqp;

namespace Cortside.DomainEvent.Stub {
    public class QueueBroker : IStubBroker {
        private readonly ConcurrentQueue<Message> queue = new ConcurrentQueue<Message>();
        private readonly ConcurrentQueue<Message> dlq = new ConcurrentQueue<Message>();
        private int published = 0;
        private int accepted = 0;

        public bool HasItems { get => queue.Count > 0; }
        public bool HasDeadLetterItems { get => dlq.Count > 0; }

        public ReadOnlyCollection<Message> Items { get => queue.ToArray().ToList().AsReadOnly(); }
        public ReadOnlyCollection<Message> DeadLetterItems { get => dlq.ToArray().ToList().AsReadOnly(); }

        public int Published { get => published; }
        public int Accepted { get => accepted; }

        public Message Peek() {
            Message message;
            queue.TryPeek(out message);

            return message;
        }

        public void Reject(Message message) {
            queue.TryDequeue(out _);
            dlq.Enqueue(message);
        }

        public void Accept(Message message) {
            queue.TryDequeue(out _);
            accepted++;
        }

        public void Enqueue(Message message) {
            queue.Enqueue(message);
            published++;
        }

        public void Release(Message message) {
            // leave item in queue
        }

        public void Shovel() {
            while (dlq.Count > 0) {
                Message message;
                dlq.TryDequeue(out message);
                queue.Enqueue(message);
            }
        }
    }
}
