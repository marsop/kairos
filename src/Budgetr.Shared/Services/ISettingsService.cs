namespace Budgetr.Shared.Services;

/// <summary>
/// Interface for application settings management.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets or sets the current language code (e.g., "en", "de", "es", "gl", "gsw").
    /// </summary>
    string Language { get; set; }

    /// <summary>
    /// Gets or sets whether the tutorial has been completed.
    /// </summary>
    bool TutorialCompleted { get; set; }

    /// <summary>
    /// Gets or sets whether browser notifications are enabled.
    /// </summary>
    bool BrowserNotificationsEnabled { get; set; }

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
}
