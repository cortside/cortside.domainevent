using System.Collections.Concurrent;
using Amqp;

namespace Cortside.DomainEvent.Stub {
    public class QueueBroker : IQueueBroker {
        private readonly ConcurrentQueue<Message> queue = new ConcurrentQueue<Message>();
        private readonly ConcurrentQueue<Message> dlq = new ConcurrentQueue<Message>();

        public bool HasItems { get => queue.Count > 0; }
        public bool HasDeadLetterItems { get => dlq.Count > 0; }

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
        }

        public void Enqueue(Message message) {
            queue.Enqueue(message);
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
