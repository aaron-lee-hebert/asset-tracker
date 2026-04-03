using AssetTracker.Core.Repositories;

namespace AssetTracker.Cli;

public static class Commands
{
    public static async Task AddAssetAsync(IAssetRepository repo, string args)
    {
        // Expected Input: "Chase Checking" Checking
        var parts = args.Split('"', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            Console.WriteLine("Invalid format. Use: add \"Asset Name\" Category");
            return;
        }

        var name = parts[0].Trim();
        var category = parts[1].Trim();

        var id = await repo.AddAsync(name, category, null);
        Console.WriteLine($"Asset added (Id: {id})");
    }

    public static async Task UpdateBalanceAsync(IAssetRepository repo, string args)
    {
        var parts = args.Split(' ');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var assetId) || !decimal.TryParse(parts[1], out var balance))
        {
            Console.WriteLine("Usage: update <assetId> <balance>");
            return;
        }

        var asset = await repo.GetByIdAsync(assetId);
        if (asset == null)
        {
            Console.WriteLine("Asset not found.");
            return;
        }

        await repo.RecordBalanceAsync(assetId, balance, null);
        Console.WriteLine($"Balance updated for {asset.Name}");
    }

    public static async Task ShowSummaryAsync(IAssetRepository repo)
    {
        var assets = await repo.GetAllActiveAsync();
        // TODO (Learning Exercise 1): Replace with multi-mapped query
        // that fetches latest balance per asset in one round trip.

        Console.WriteLine($"\nASSET SUMMARY — {DateTime.Now:yyyy-MM-dd}");
        Console.WriteLine(new string('─', 50));

        decimal total = 0;
        foreach (var a in assets)
        {
            Console.WriteLine($"{a.Name,-25} {a.Category,-15} (no balance yet)");
        }

        Console.WriteLine(new string('─', 50));
        Console.WriteLine($"{"Total",-40} {total,9:C}");
        Console.WriteLine();
    }

    public static async Task ShowHistoryAsync(IAssetRepository repo, string args)
    {
        if (!int.TryParse(args.Trim(), out var assetId))
        {
            Console.WriteLine("Usage: history <assetId>");
            return;
        }

        var entries = await repo.GetBalanceHistoryAsync(assetId);
        foreach (var e in entries)
            Console.WriteLine($"  {e.RecordedAt:yyyy-MM-dd HH:mm}  {e.Balance,12:C}  {e.Note}");

        Console.WriteLine();
    }
}