using AssetTracker.Core.Repositories;
using Dapper;

namespace AssetTracker.Tests.Repositoriies;

[TestFixture]
public class AssetRepositoryTests
{
    private AssetRepository _repository;

    [SetUp]
    public async Task Setup()
    {
        _repository = new AssetRepository(TestConnectionFactory.Create());

        // Clean slate before each test.
        using var conn = TestConnectionFactory.Create().Create();
        conn.Open();
        await conn.ExecuteAsync("DELETE FROM BalanceEntries; DELETE From Assets;");
    }

    [Test]
    public async Task AddAsync_ShouldReturnNewId()
    {
        var id = await _repository.AddAsync("Test Checking", "Checking", null);
        Assert.That(id, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetAllActiveAsync_ShouldReturnOnlyActiveAssets()
    {
        // Arranage
        await _repository.AddAsync("Active Account", "Checking", null);
        using var conn = TestConnectionFactory.Create().Create();
        conn.Open();
        await conn.ExecuteAsync("INSERT INTO Assets (Name, Category, Description, CreatedAt, IsActive) VALUES ('Inactive', 'Checking', NULL, GETDATE(), 0);");

        // Act  
        var results = await _repository.GetAllActiveAsync();

        // Assert
        Assert.That(results.Count(), Is.EqualTo(1));
        Assert.That(results.First().Name, Is.EqualTo("Active Account"));
    }

    [Test]
    public async Task RecordBalanceAsync_ShouldAppendEntry()
    {
        // Arrange
        var assetId = await _repository.AddAsync("Savings", "Savings", null);
        await _repository.RecordBalanceAsync(assetId, 1000m, "Initial");
        await _repository.RecordBalanceAsync(assetId, 1250m, "After deposit");

        // Act
        var history = (await _repository.GetBalanceHistoryAsync(assetId)).ToList();

        // Assert
        Assert.That(history.Count, Is.EqualTo(2));
        Assert.That(history.First().Balance, Is.EqualTo(1250m));
    }
}