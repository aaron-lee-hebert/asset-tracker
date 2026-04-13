using Dapper;

namespace AssetTracker.Core.Database;

public static class DapperConfig
{
    private static bool _configured;

    /// <summary>
    /// Configures Dapper to map snake_case database columns to PascalCase
    /// C# properties (e.g., asset_id → AssetId). Must be called once at
    /// application startup before any Dapper query.
    /// </summary>
    public static void Configure()
    {
        if (_configured) return;
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        _configured = true;
    }
}
