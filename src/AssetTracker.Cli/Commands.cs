using AssetTracker.Core.Repositories;

namespace AssetTracker.Cli;

public static class Commands
{
    public static async Task AddAssetAsync(IAssetRepository repo, string args)
    {
        if (!TryExtractQuotedName(args, out var name, out var remainder))
        {
            Console.WriteLine("Invalid format. Use: add <\"Asset Name\"> <category>");
            return;
        }

        var category = remainder.Trim();
        if (string.IsNullOrEmpty(category))
        {
            Console.WriteLine("Invalid format. Use: add <\"Asset Name\"> <category>");
            return;
        }

        var id = await repo.AddAsync(name, category, null);
        Console.WriteLine($"Asset added (Id: {id})");
    }

    public static async Task AddAssetWithBalanceAsync(IAssetRepository repo, string args)
    {
        if (!TryExtractQuotedName(args, out var name, out var remainder))
        {
            Console.WriteLine("Invalid format. Use: addb <\"Asset Name\"> <category> <initialBalance>");
            return;
        }

        var trailing = remainder.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (trailing.Length < 2 || !decimal.TryParse(trailing[1], out var initialBalance))
        {
            Console.WriteLine("Invalid format. Use: addb <\"Asset Name\"> <category> <initialBalance>");
            return;
        }

        var category = trailing[0];

        var id = await repo.AddWithInitialBalanceAsync(name, category, null, initialBalance);
        Console.WriteLine($"Asset added (Id: {id}) with an initial balance of {initialBalance:C}");
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
        var assets = await repo.GetAllActiveWithLatestBalanceAsync();

        Console.WriteLine($"\nASSET SUMMARY — {DateTime.Now:yyyy-MM-dd}");
        Console.WriteLine(new string('─', 75));

        decimal total = 0;
        foreach (var a in assets)
        {
            Console.WriteLine($"{a.Name,-50}{a.Category,-15}{a.LatestBalance?.Balance.ToString("C") ?? "N/A",10}");
            total += a.LatestBalance?.Balance ?? 0;
        }

        Console.WriteLine(new string('─', 75));
        Console.WriteLine($"{"Total",-65} {total,9:C}");
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

    private static bool TryExtractQuotedName(string input, out string name, out string remainder)
    {
        name = string.Empty;
        remainder = string.Empty;

        var open = input.IndexOf('"');
        var close = input.IndexOf('"', open + 1);
        if (open < 0 || close < 0)
            return false;

        name = input[(open + 1)..close];
        remainder = input[(close + 1)..];
        return true;
    }
}