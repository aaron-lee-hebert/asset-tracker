namespace AssetTracker.Core.Database
{
    public interface IMigrationRunner
    {
        Task RunAsync();
    }
}