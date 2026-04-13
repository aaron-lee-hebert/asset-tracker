using System.Data;
using Npgsql;

namespace AssetTracker.Core.Database
{
    public class ConnectionFactory(string connectionString)
    {
        private readonly string _connectionString = connectionString;

        // Returns IDbConnection, not NpgsqlConnection.
        // This keeps callers decoupled from the provider.
        public IDbConnection Create()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}
