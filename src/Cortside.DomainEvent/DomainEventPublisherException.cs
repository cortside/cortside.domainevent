using System;

namespace Cortside.DomainEvent {
    public class DomainEventPublisherException : Exception {
        public DomainEventPublisherException() {
        }

        public DomainEventPublisherException(string message) : base(message) {
        }

        public DomainEventPublisherException(string message, Exception inner) : base(message, inner) {
        }
    }
}
