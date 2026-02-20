namespace Budgetr.Shared.Services;

/// <summary>
/// Interface for cloud synchronization providers.
/// </summary>
public interface ISyncProvider
{
    /// <summary>
    /// Display name of the provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether the user is currently authenticated with the provider.
    /// </summary>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Downloads the latest backup data from the provider.
    /// </summary>
    /// <returns>JSON string of the backup data, or null if no backup exists.</returns>
    Task<string?> DownloadDataAsync();

    /// <summary>
    /// Uploads data to the provider as a backup.
    /// </summary>
    /// <param name="jsonData">JSON string to backup.</param>
    Task UploadDataAsync(string jsonData);

    /// <summary>
    /// Gets the timestamp of the last backup, if available.
    /// </summary>
    Task<DateTimeOffset?> GetLastBackupTimeAsync();
}
