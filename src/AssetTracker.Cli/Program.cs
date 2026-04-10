using Microsoft.Extensions.Configuration;
using AssetTracker.Core.Database;
using AssetTracker.Core.Repositories;
using static AssetTracker.Cli.Commands;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.local.json", optional: true) // gitignored overrides
    .Build();

var connectionString = config.GetConnectionString("AssetTracker")
    ?? throw new InvalidOperationException("Connection string 'AssetTracker' not found.");

var migrationsPath = config["Paths:Migrations"]
    ?? throw new InvalidOperationException("Migrations path not configuted");

var factory = new ConnectionFactory(connectionString);

var migrationRunner = new MigrationRunner(factory, migrationsPath);
await migrationRunner.RunAsync();

var repo = new AssetRepository(factory);

Console.WriteLine("Asset Tracker. Command: add, addb, update, summary, history, quit");
Console.WriteLine();

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine()?.Trim() ?? string.Empty;
    var parts = input.Split(' ', 2);
    var command = parts[0].ToLower();
    var cmdArgs = parts.Length > 1 ? parts[1] : string.Empty;

    try
    {
        switch (command)
        {
            case "add":
                await AddAssetAsync(repo, cmdArgs);
                break;
            case "addb":
                await AddAssetWithBalanceAsync(repo, cmdArgs);
                break;
            case "update":
                await UpdateBalanceAsync(repo, cmdArgs);
                break;
            case "summary":
                await ShowSummaryAsync(repo);
                break;
            case "history":
                await ShowHistoryAsync(repo, cmdArgs);
                break;
            case "quit":
                Console.WriteLine("Goodbye!");
                return;
            default:
                Console.WriteLine("Unknown command.");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
