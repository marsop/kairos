namespace Budgetr.Shared.Services;

/// <summary>
/// Interface for PWA capabilities (installation, offline detection).
/// </summary>
public interface IPwaService
{
    /// <summary>
    /// Whether the app can be installed (browser event fired).
    /// </summary>
    bool IsInstallable { get; }

    /// <summary>
    /// Whether the app currently has network connectivity.
    /// </summary>
    bool IsOnline { get; }

    /// <summary>
    /// Event raised when installability or connectivity changes.
    /// </summary>
    event Action? OnStateChanged;

    /// <summary>
    /// Triggers the browser's install prompt.
    /// </summary>
    Task InstallAppAsync();

    /// <summary>
    /// Initializes the PWA service (e.g. JS Interop).
    /// </summary>
    Task InitializeAsync();
}
