using AssetTracker.Core.Database;

namespace AssetTracker.Tests;

public static class TestConnectionFactory
{
    private const string EnvVarName = "ASSETTRACKER_TEST_CONNECTION_STRING";

    public static ConnectionFactory Create()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVarName)
            ?? throw new InvalidOperationException(
                $"Environment variable '{EnvVarName}' is not set. " +
                "Example: Host=localhost;Database=asset_tracker_test;Username=postgres;Password=***");
        return new ConnectionFactory(connectionString);
    }
}
