using System.Data;
using Microsoft.Data.SqlClient;

namespace AssetTracker.Core.Database
{
    public class ConnectionFactory(string connectionString)
    {
        private readonly string _connectionString = connectionString;

        // Returns IDbConnection, not SqlConnection.
        // This keeps callers decoupled from the provider.
        public IDbConnection Create()
        {
            return new SqlConnection(_connectionString);
        }
    }
}