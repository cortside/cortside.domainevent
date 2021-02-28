using Microsoft.EntityFrameworkCore;

namespace Cortside.DomainEvent.EntityFramework {
    public static class DbContextExtensions {
        public static DbSet<Outbox> Outbox(this DbContext context) {
            return context.Set<Outbox>();
        }
    }
}
