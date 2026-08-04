namespace Kairos.Application.Services;

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
    string ChartType { get; set; }

    /// <summary>
    /// Gets or sets whether browser notifications are enabled.
    /// </summary>
    bool BrowserNotificationsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether sounds are enabled.
    /// </summary>
    bool SoundsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether advanced settings are visible.
    /// </summary>
    bool AdvancedSettingsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether Timeular settings are enabled.
    /// </summary>
    bool TimeularSettingsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether activity groups are enabled.
    /// </summary>
    bool ActivityGroupsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether budgets are enabled.
    /// </summary>
    bool BudgetsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the currently active activity group (0 or 1).
    /// </summary>
    int ActiveActivityGroup { get; set; }

    /// <summary>
    /// Gets or sets how many activity groups are available.
    /// </summary>
    int ActivityGroupCount { get; set; }

    /// <summary>
    /// Gets the custom name for a given activity group, if any.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <returns>The custom group name or null when no custom name is set.</returns>
    string? GetActivityGroupName(int groupId);

    /// <summary>
    /// Sets a custom name for a given activity group.
    /// </summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="name">The new name. Null or empty clears the custom name.</param>
    void SetActivityGroupName(int groupId, string? name);

    /// <summary>
    /// Removes a group name entry and shifts following names left.
    /// </summary>
    /// <param name="groupId">The removed group id.</param>
    void RemoveActivityGroupNameAt(int groupId);

    /// <summary>
    /// Gets the custom color for a given activity group, if any.
    /// </summary>
    string? GetActivityGroupColor(int groupId);

    /// <summary>
    /// Sets a custom color for a given activity group.
    /// </summary>
    void SetActivityGroupColor(int groupId, string? color);

    /// <summary>
    /// Gets the custom icon for a given activity group, if any.
    /// </summary>
    string? GetActivityGroupIcon(int groupId);

    /// <summary>
    /// Sets a custom icon for a given activity group.
    /// </summary>
    void SetActivityGroupIcon(int groupId, string? icon);

    /// <summary>
    /// Gets or sets the threshold in seconds below which completed events are automatically deleted.
    /// </summary>
    int AutoDeleteEventDuration { get; set; }
    int StickyEventsDuration { get; set; }

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
