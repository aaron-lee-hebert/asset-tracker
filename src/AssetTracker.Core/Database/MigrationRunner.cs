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
            // OBJECT_ID is a safer way to check if an object exists, instead
            // of using IF NOT EXISTS.
            await conn.ExecuteAsync(@"
                IF OBJECT_ID('__Migrations', 'U') IS NULL
                CREATE TABLE __Migrations (
                    Id          INT IDENTITY PRIMARY KEY,
                    FileName    NVARCHAR(255) NOT NULL,
                    AppliedAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                )
            ");
        }

        private static async Task<IEnumerable<string>> GetAppliedMigrationsAsync(IDbConnection conn)
        {
            return await conn.QueryAsync<string>("SELECT FileName FROM __Migrations");
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
                "INSERT INTO __Migrations (FileName) VALUES (@FileName)",
                new { FileName = fileName }
            );
        }
    }
}