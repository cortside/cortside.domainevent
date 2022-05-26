using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Amqp;
using Newtonsoft.Json;

namespace Cortside.DomainEvent.Stub {
    public class ConcurrentQueueBroker : IStubBroker {
        private readonly ConcurrentQueue<Message> queue = new ConcurrentQueue<Message>();
        private readonly ConcurrentQueue<Message> accepted = new ConcurrentQueue<Message>();
        private readonly ConcurrentQueue<Message> dlq = new ConcurrentQueue<Message>();
        private readonly ConcurrentQueue<Message> unmapped = new ConcurrentQueue<Message>();
        private int published = 0;

        public bool HasItems { get => queue.Count > 0; }
        public bool HasDeadLetterItems { get => dlq.Count > 0; }

        public ReadOnlyCollection<Message> AcceptedItems { get => accepted.ToArray().ToList().AsReadOnly(); }
        public ReadOnlyCollection<Message> ActiveItems { get => queue.ToArray().ToList().AsReadOnly(); }
        public ReadOnlyCollection<Message> DeadLetterItems { get => dlq.ToArray().ToList().AsReadOnly(); }
        public ReadOnlyCollection<Message> UnmappedItems { get => unmapped.ToArray().ToList().AsReadOnly(); }

        public int Published { get => published; }
        public int Accepted { get => accepted.Count; }

        public void Enqueue(Message message) {
            queue.Enqueue(message);
            published++;
        }
        public void Dequeue() {
            queue.TryDequeue(out _);
        }

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
            accepted.Enqueue(message);
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
        public void EnqueueUnmapped(Message message) {
            unmapped.Enqueue(message);
        }

        public void ResetQueue() {
            queue.Clear();
        }

        public List<T> GetAcceptedMessagesByType<T>(Func<T, bool> predicate = null) {
            return GetMessagesByType<T>(AcceptedItems, predicate);
        }

        public List<T> GetActiveMessagesByType<T>(Func<T, bool> predicate = null) {
            return GetMessagesByType<T>(ActiveItems, predicate);
        }

        public List<T> GetDLQMessagesByType<T>(Func<T, bool> predicate = null) {
            return GetMessagesByType<T>(DeadLetterItems, predicate);
        }

        public List<T> GetUnmappedMessagesByType<T>(Func<T, bool> predicate = null) {
            return GetMessagesByType<T>(UnmappedItems, predicate);
        }

        private List<T> GetMessagesByType<T>(ReadOnlyCollection<Message> collection, Func<T, bool> predicate = null) {
            var name = typeof(T).Name;
            var messages = collection.Where(x => x.ApplicationProperties.Map.Values.Any(y => y.ToString().Contains(name)));
            var result = messages
                .Select(x => JsonConvert.DeserializeObject<T>(x.Body.ToString()))
                .ToList();

            if (predicate != null) {
                result = result.Where(predicate).ToList();
            }

            return result;
        }
    }
}
