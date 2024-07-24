using System;

namespace Cortside.DomainEvent {
    public class DomainEventError {
        public string Condition { get; set; }
        public string Description { get; set; }
        public Exception Exception { get; set; }
    }
}
