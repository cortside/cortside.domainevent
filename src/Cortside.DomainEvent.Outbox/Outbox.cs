using System;

namespace Cortside.DomainEvent.EntityFramework {
    public class Outbox {
        public string MessageId { get; set; }
        public string CorrelationId { get; set; }
        public string EventType { get; set; }
        public string Address { get; set; }
        public string Body { get; set; }

        public OutboxStatus Status { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime ScheduledDate { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedDate { get; set; }

        public string LockId { get; set; }
    }
}
