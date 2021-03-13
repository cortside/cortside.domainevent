using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Cortside.DomainEvent.EntityFramework.IntegrationTests.Database {
    public class EntityContext : DbContext {
        public EntityContext(DbContextOptions<EntityContext> options) : base(options) { }

        public DbSet<Widget> Widgets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.AddDomainEventOutbox();
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync() {
            var supportsTransactions = !Database.ProviderName.Contains("InMemory");
            if (supportsTransactions) {
                return await Database.BeginTransactionAsync().ConfigureAwait(false);
            }
            return null;
        }
    }
}
