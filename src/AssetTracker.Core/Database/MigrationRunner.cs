using System.Data;
using Dapper;

namespace AssetTracker.Core.Database
{
    public class MigrationRunner(ConnectionFactory connectionFactory, string migrationsPath) : IMigrationRunner
    {
        public async Task RunAsync()
        {
            using var conn = connectionFactory.Create();
            conn.Open(); // Open once - all private methods will have the connection.

            await EnsureMigrationsTableAsync(conn);

            var applied = await GetAppliedMigrationsAsync(conn);
            var pending = GetPendingMigrations(applied);

            foreach (var script in pending)
                await ApplyMigrationAsync(conn, script);
        }

        private static async Task EnsureMigrationsTableAsync(IDbConnection conn)
        {
            // CREATE TABLE IF NOT EXISTS is the idiomatic Postgres equivalent
            // of the OBJECT_ID check we had under SQL Server.
            await conn.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS __migrations (
                    id          SERIAL PRIMARY KEY,
                    file_name   VARCHAR(255) NOT NULL,
                    applied_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
                )
            ");
        }

        private static async Task<IEnumerable<string>> GetAppliedMigrationsAsync(IDbConnection conn)
        {
            return await conn.QueryAsync<string>("SELECT file_name FROM __migrations");
        }

        private IEnumerable<string> GetPendingMigrations(IEnumerable<string> applied)
        {
            var appliedSet = new HashSet<string>(applied, StringComparer.OrdinalIgnoreCase);

            return Directory
                .GetFiles(migrationsPath, "*.sql")
                .OrderBy(Path.GetFileName)
                .Where(path => !appliedSet.Contains(Path.GetFileName(path)));
        }

        private static async Task ApplyMigrationAsync(IDbConnection conn, string scriptPath)
        {
            var fileName = Path.GetFileName(scriptPath);
            var sql = await File.ReadAllTextAsync(scriptPath);

            await conn.ExecuteAsync(sql);
            await conn.ExecuteAsync(
                "INSERT INTO __migrations (file_name) VALUES (@FileName)",
                new { FileName = fileName }
            );
        }
    }
}