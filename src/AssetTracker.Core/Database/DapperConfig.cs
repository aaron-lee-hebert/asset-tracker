using Dapper;

namespace AssetTracker.Core.Database;

public static class DapperConfig
{
    /// <summary>
    /// Configures Dapper to map snake_case database columns to PascalCase
    /// C# properties (e.g., asset_id → AssetId). Must be called once at
    /// application startup before any Dapper query. The underlying assignment
    /// is idempotent, so calling this more than once is harmless.
    /// </summary>
    public static void Configure()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
