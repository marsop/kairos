namespace Kairos.Shared.Services;

/// <summary>
/// Interface for application settings management.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets or sets the current application theme ("light" or "dark").
    /// </summary>
    string Theme { get; set; }

    /// <summary>
    /// Gets or sets the current language code (e.g., "en", "de", "es", "gl", "gsw").
    /// </summary>
    string Language { get; set; }

    /// <summary>
    /// Gets or sets whether the tutorial has been completed.
    /// </summary>
    bool TutorialCompleted { get; set; }

    string HistoryView { get; set; }

    /// <summary>
    /// Gets or sets whether browser notifications are enabled.
    /// </summary>
    bool BrowserNotificationsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether activity groups are enabled.
    /// </summary>
    bool ActivityGroupsEnabled { get; set; }

    /// <summary>
    /// Gets the last time Supabase was synchronized (in-memory only).
    /// </summary>
    DateTimeOffset? LastSupabaseSync { get; }

    /// <summary>
    /// Event raised when any setting changes.
    /// </summary>
    event Action? OnSettingsChanged;

    /// <summary>
    /// Loads settings from persistent storage.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Saves current settings to persistent storage.
    /// </summary>
    Task SaveAsync();

    Task SetLanguageAsync(string language);

    /// <summary>
    /// Updates the last Supabase synchronization time to the current UTC time.
    /// </summary>
    void UpdateLastSupabaseSync();
}
