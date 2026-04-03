using AssetTracker.Core.Domain;

namespace AssetTracker.Core.Repositories;

public interface IAssetRepository
{
    Task<IEnumerable<Asset>> GetAllActiveAsync();
    Task<IEnumerable<Asset>> GetAllActiveWithLatestBalanceAsync();
    Task<Asset?> GetByIdAsync(int id);
    Task<int> AddAsync(string name, string category, string? description);
    Task<int> AddWithInitialBalanceAsync(string name, string category, string? description, decimal initialBalance);
    Task RecordBalanceAsync(int assetId, decimal balance, string? note);
    Task<IEnumerable<BalanceEntry>> GetBalanceHistoryAsync(int assetId);
}