using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Cortside.DomainEvent.EntityFramework {

    public static class DbContextExtensions {

        public static DbSet<Outbox> Outbox(this DbContext context) {
            return context.Set<Outbox>();
        }

        public static async Task<T> ExecuteScalarAsync<T>(this DbContext context, string rawSql, params object[] parameters) {
            var conn = context.Database.GetDbConnection();
            using (var command = conn.CreateCommand()) {
                command.CommandText = rawSql;
                if (parameters != null) {
                    foreach (var p in parameters) {
                        command.Parameters.Add(p);
                    }
                }
                await conn.OpenAsync();
                return (T)await command.ExecuteScalarAsync();
            }
        }
    }
}
