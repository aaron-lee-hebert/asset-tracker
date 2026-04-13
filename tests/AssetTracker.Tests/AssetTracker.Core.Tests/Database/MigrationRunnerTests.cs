using AssetTracker.Core.Database;
using Dapper;

namespace AssetTracker.Tests;

[TestFixture]
public class MigrationRunnerTests
{
    [OneTimeSetUp]
    public void ConfigureDapper() => AssetTracker.Core.Database.DapperConfig.Configure();

    private string _migrationsPath;
    private ConnectionFactory _factory;

    [SetUp]
    public async Task Setup()
    {
        _factory = TestConnectionFactory.Create();

        _migrationsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_migrationsPath);
    }

    [TearDown]
    public async Task Teardown()
    {
        Directory.Delete(_migrationsPath, recursive: true);

        using var conn = _factory.Create();
        await conn.ExecuteAsync(@"
            DROP TABLE IF EXISTS TestAssets;
            DROP TABLE IF EXISTS __Migrations;
        ");
    }

    [Test]
    public async Task RunAsync_WhenOneMigrationExists_AppliesItAndRecordsIt()
    {
        // Arrange

        // Write a real .sql file into the temp folder.
        // This is the "migration" the runner will discover and apply.
        // The table name is intentionally unique to this test so it doesn't
        // collide with your real Assets table.
        var scriptName = "001_CreateTestAssets.sql";
        var scriptPath = Path.Combine(_migrationsPath, scriptName);
        await File.WriteAllTextAsync(scriptPath, @"
            CREATE TABLE TestAssets (
                Id   INT IDENTITY PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL
            );
        ");

        var runner = new MigrationRunner(_factory, _migrationsPath);

        // Act
        await runner.RunAsync();

        // Assert
        using var conn = _factory.Create();

        // Assertion 1: The migration was recorded in __Migrations.
        // This verifies the runner is tracking what it applied.
        var appliedMigrations = await conn.QueryAsync<string>(
            "SELECT FileName FROM __Migrations"
        );
        Assert.That(appliedMigrations, Contains.Item(scriptName));

        // Assertion 2: The schema change actually happened.
        // Recording the migration without applying it would pass Assertion 1
        // but fail here — so both assertions are necessary.
        var tableExists = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = 'TestAssets'
        ");
        Assert.That(tableExists, Is.EqualTo(1));
    }
}