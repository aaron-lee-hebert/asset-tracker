using Dapper;
using AssetTracker.Core.Database;
using AssetTracker.Core.Domain;

namespace AssetTracker.Core.Repositories;

public class AssetRepository(ConnectionFactory factory) : IAssetRepository
{
    private readonly ConnectionFactory _factory = factory;

    public async Task<IEnumerable<Asset>> GetAllActiveAsync()
    {
        using var conn = _factory.Create();

        return await conn.QueryAsync<Asset>(@"
            SELECT Id, Name, Category, Description, CreatedAt, IsActive
            FROM Assets
            WHERE IsActive = 1
            ORDER BY Name
        ");
    }

    public async Task<IEnumerable<Asset>> GetAllActiveWithLatestBalanceAsync()
    {
        using var conn = _factory.Create();

        var sql = @"
            WITH LatestBalances AS (
                SELECT 
                    Id,
                    AssetId, 
                    Balance,
                    RecordedAt,
                    Note,
                    ROW_NUMBER() OVER (PARTITION BY AssetId ORDER BY RecordedAt DESC) AS rn
                FROM BalanceEntries
            )
            SELECT 
                a.Id, a.Name, a.Category, a.Description, a.CreatedAt, a.IsActive,
                lb.Id, lb.AssetId, lb.Balance, lb.RecordedAt, lb.Note
            FROM Assets a
            LEFT JOIN LatestBalances lb ON a.Id = lb.AssetId and lb.rn = 1
            WHERE a.IsActive = 1
            ORDER BY a.Name
        ";

        var assets = await conn.QueryAsync<Asset, BalanceEntry, Asset>(
            sql,
            (asset, balance) =>
            {
                asset.LatestBalance = balance;
                return asset;
            },
            splitOn: "Id"
        );

        return assets;
    }

    public async Task<Asset?> GetByIdAsync(int id)
    {
        using var conn = _factory.Create();

        return await conn.QuerySingleOrDefaultAsync<Asset>(@"
            SELECT Id, Name, Category, Description, CreatedAt, IsActive
            FROM Assets
            WHERE Id = @Id
        ", new { Id = id });
        // ^^^ @Id binds to the anonymous object property. Dapper handles
        // the SqlParameter creation — no SqlCommand boilerplate needed.
    }

    public async Task<int> AddAsync(string name, string category, string? description)
    {
        using var conn = _factory.Create();

        // OUTPUT INSERTED.Id returns the identity value in one round trip.
        // No need for a second SELECT SCOPE_IDENTITY() call.
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO Assets (Name, Category, Description)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Category, @Description)
        ", new { Name = name, Category = category, Description = description });
    }

    public async Task RecordBalanceAsync(int assetId, decimal balance, string? note)
    {
        using var conn = _factory.Create();

        await conn.ExecuteAsync(@"
            INSERT INTO BalanceEntries (AssetId, Balance, Note)
            VALUES (@AssetId, @Balance, @Note)
        ", new { AssetId = assetId, Balance = balance, Note = note });
    }

    public async Task<IEnumerable<BalanceEntry>> GetBalanceHistoryAsync(int assetId)
    {
        using var conn = _factory.Create();

        return await conn.QueryAsync<BalanceEntry>(@"
            SELECT Id, AssetId, Balance, RecordedAt, Note
            FROM BalanceEntries
            WHERE AssetId = @AssetId
            ORDER BY RecordedAt DESC
        ", new { AssetId = assetId });
    }
}