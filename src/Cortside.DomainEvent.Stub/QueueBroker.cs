using System.Collections.Generic;
using Amqp;

namespace Cortside.DomainEvent.Stub {
    public class QueueBroker : IQueueBroker {
        private readonly Queue<Message> queue = new Queue<Message>();
        private readonly Queue<Message> dlq = new Queue<Message>();

        public bool HasItems { get => queue.Count > 0; }

        public Message Peek() {
            return queue.Peek();
        }

        public void Reject(Message message) {
            queue.Dequeue();
            dlq.Enqueue(message);
        }

        public void Accept(Message message) {
            queue.Dequeue();
        }

        public void Enqueue(Message message) {
            queue.Enqueue(message);
        }

        public void Release(Message message) {
            // leave item in queue
        }

        public void Shovel() {
            while (dlq.Count > 0) {
                var message = dlq.Dequeue();
                queue.Enqueue(message);
            }
        }
    }
}
