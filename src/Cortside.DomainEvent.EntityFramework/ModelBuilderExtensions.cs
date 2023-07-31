using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortside.DomainEvent.EntityFramework {
    public static class ModelBuilderExtensions {
        public static void AddDomainEventOutbox(this ModelBuilder builder, string schema = "dbo") {
            builder.ApplyConfiguration(new OutboxMessageEntityConfiguration(schema));
        }
    }

    internal class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<Outbox> {
        private readonly string schema;

        internal OutboxMessageEntityConfiguration(string schema) {
            this.schema = schema;
        }

        public void Configure(EntityTypeBuilder<Outbox> builder) {
            builder.ToTable("Outbox", schema);
            builder.HasKey(t => t.OutboxId);

            builder.HasIndex(t => t.MessageId)
                .IsUnique(true);

            builder.Property(t => t.MessageId)
                .HasMaxLength(36)
                .IsRequired()
                .ValueGeneratedNever();

            builder.Property(t => t.CorrelationId)
                .HasMaxLength(250);

            builder.Property(t => t.EventType)
                .HasMaxLength(250)
                .IsRequired();

            builder.Property(t => t.Topic)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(t => t.RoutingKey)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(t => t.Body)
                .IsRequired();

            builder.Property(t => t.Status)
                .HasMaxLength(10)
                .IsRequired()
                .HasConversion(
                    v => v.ToString(),
                    v => (OutboxStatus)Enum.Parse(typeof(OutboxStatus), v)
                );

            builder.Property(t => t.LockId)
                    .HasMaxLength(36);

            builder.HasIndex(p => new { p.ScheduledDate, p.Status })
                .IncludeProperties(p => new { p.EventType })
                .HasName("IX_ScheduleDate_Status");

            builder.HasIndex(p => new { p.Status, p.LockId, p.ScheduledDate })
                .HasName("IX_Status_LockId_ScheduleDate");
        }
    }
}
