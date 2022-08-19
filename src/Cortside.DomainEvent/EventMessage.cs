using Amqp;
using Amqp.Types;

namespace Cortside.DomainEvent
{
    public class EventMessage
    {
        private readonly Message message;
        private readonly IReceiverLink link;

        internal EventMessage(DomainEventMessage domainEvent, Message message, IReceiverLink link)
        {
            this.message = message;
            this.link = link;
            Message = domainEvent;
        }

        public DomainEventMessage Message { get; }

        public void Reject()
        {
            link.Reject(message);
        }

        public void Release()
        {
            link.Release(message);
        }

        public void Accept()
        {
            link.Accept(message);
        }

        public void Modify(bool deliveryFailed, bool undeliverableHere = false, Fields messageAnnotations = null)
        {
            link.Modify(message, deliveryFailed, undeliverableHere, messageAnnotations);
        }

        public T GetData<T>()
        {
            return (T)Message.Data;
        }
    }
}
