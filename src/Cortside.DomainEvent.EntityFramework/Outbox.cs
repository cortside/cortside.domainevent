using System;
using System.ComponentModel;
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

        public Guid? LockId { get; set; }

        [Required]
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

        public int PublishCount { get; set; }

        [StringLength(50)]
        public string Key { get; set; }

        [Required]
        [DefaultValue(10)]
        public int RemainingAttempts { get; set; }
    }
}
