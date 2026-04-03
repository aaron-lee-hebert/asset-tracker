using AssetTracker.Core.Database;

namespace AssetTracker.Tests;

public static class TestConnectionFactory
{
    public static ConnectionFactory Create() =>
        new("Server=localhost\\sql;Database=AssetTracker_Test;Trusted_Connection=True;TrustServerCertificate=True;");
}