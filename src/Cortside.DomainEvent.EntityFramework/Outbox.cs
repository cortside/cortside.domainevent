using System;
using System.ComponentModel.DataAnnotations;

namespace Cortside.DomainEvent.EntityFramework {
    public class Outbox {
        [Required]
        [StringLength(36)]
        public string MessageId { get; set; }
        [StringLength(36)]
        public string CorrelationId { get; set; }
        [Required]
        [StringLength(250)]
        public string EventType { get; set; }

        [Required]
        [StringLength(100)]
        public string Topic { get; set; }

        [Required]
        [StringLength(100)]
        public string RoutingKey { get; set; }

        [Required]
        public string Body { get; set; }

        [Required]
        [StringLength(10)]
        public OutboxStatus Status { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [Required]
        public DateTime ScheduledDate { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedDate { get; set; }

        [StringLength(36)]
        public string LockId { get; set; }
    }
}
