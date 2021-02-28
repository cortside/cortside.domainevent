using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortside.DomainEvent.EntityFramework {
    public static class ModelBuilderExtensions {
        public static void AddDomainEventOutbox(this ModelBuilder builder) {
            builder.ApplyConfiguration(new OutboxMessageEntityConfiguration());
        }
    }

    internal class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<Outbox> {
        public void Configure(EntityTypeBuilder<Outbox> builder) {
            builder.HasIndex(p => new { p.ScheduledDate, p.Status })
                .IncludeProperties(p => new { p.EventType })
                .HasName("IX_ScheduleDate_Status");
        }
    }
}
