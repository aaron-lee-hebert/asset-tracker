namespace AssetTracker.Core.Domain;

public class Asset
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsActive { get; set; }

    // Not a DB column — populated by a JOIN or second query
    public BalanceEntry? LatestBalance { get; set; }
}