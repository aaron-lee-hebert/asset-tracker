namespace AssetTracker.Core.Domain;

public class BalanceEntry
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public decimal Balance { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? Note { get; set; }
}