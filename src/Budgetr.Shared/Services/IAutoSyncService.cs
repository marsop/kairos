namespace Budgetr.Shared.Services;

/// <summary>
/// Interface for managing automatic synchronization to cloud providers.
/// </summary>
public interface IAutoSyncService : IDisposable
{
    /// <summary>
    /// Whether auto-sync is currently enabled.
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// The name of the currently active sync provider (e.g. "GoogleDrive", "Supabase"), or null if disabled.
    /// </summary>
    string? ActiveProviderName { get; }
    
    /// <summary>
    /// Enables auto-sync with the specified provider. The service will subscribe to data changes
    /// and automatically backup to the specified cloud provider.
    /// If another provider is already active, it will be disabled first.
    /// </summary>
    /// <param name="providerName">Name of the sync provider to use.</param>
    Task EnableAsync(string providerName);
    
    /// <summary>
    /// Disables auto-sync.
    /// </summary>
    Task DisableAsync();
    
    /// <summary>
    /// Gets the last successful auto-sync time.
    /// </summary>
    DateTimeOffset? LastSyncTime { get; }
    
    /// <summary>
    /// Gets the current sync status.
    /// </summary>
    AutoSyncStatus Status { get; }
    
    /// <summary>
    /// Event raised when sync status changes (syncing, completed, failed).
    /// </summary>
    event Action<AutoSyncStatus>? OnStatusChanged;
}

/// <summary>
/// Represents the current status of auto-sync.
/// </summary>
public enum AutoSyncStatus
{
    /// <summary>Auto-sync is idle, waiting for changes.</summary>
    Idle,
    
    /// <summary>Currently syncing data.</summary>
    Syncing,
    
    /// <summary>Last sync completed successfully.</summary>
    Success,
    
    /// <summary>Last sync failed.</summary>
    Failed
}
