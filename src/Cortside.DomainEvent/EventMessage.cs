using Amqp;

namespace Cortside.DomainEvent {
    public class EventMessage {
        private readonly Message message;
        private readonly IReceiverLink link;

        internal EventMessage(DomainEventMessage domainEvent, Message message, IReceiverLink link) {
            this.message = message;
            this.link = link;
            Message = domainEvent;
        }

        public DomainEventMessage Message { get; }

        public void Reject() {
            link.Reject(message);
            Statistics.Instance.Reject();
        }

        public void Release() {
            link.Release(message);
            Statistics.Instance.Release();
        }

        public void Accept() {
            link.Accept(message);
            Statistics.Instance.Accept();
        }

        //public void Modify(bool deliveryFailed, bool undeliverableHere = false, Fields messageAnnotations = null) {
        //    link.Modify(message, deliveryFailed, undeliverableHere, messageAnnotations);
        //}

        public T GetData<T>() {
            return (T)Message.Data;
        }
    }
}
