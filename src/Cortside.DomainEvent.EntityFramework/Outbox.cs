using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cortside.DomainEvent.EntityFramework {
    [Table("Outbox", Schema = "dbo")]
    public class Outbox {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public int OutboxId { get; set; }

        [Required]
        [StringLength(36)]
        public string MessageId { get; set; }

        [StringLength(250)]
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

        [Required]
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

        [Required]
        public byte Priority { get; set; }
    }
}
