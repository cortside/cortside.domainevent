using Microsoft.EntityFrameworkCore;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests {
    public class EntityContext : DbContext {
        public EntityContext(DbContextOptions<EntityContext> options) : base(options) { }

        public DbSet<Widget> Widgets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.AddDomainEventOutbox();
        }
    }
}
