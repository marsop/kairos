namespace Budgetr.Shared.Services;

/// <summary>
/// Interface for managing a Timeular device connection and orientation events.
/// </summary>
public interface ITimeularService
{
    bool IsInitialized { get; }
    bool IsConnecting { get; }
    bool IsConnected { get; }
    bool HasConnectedBefore { get; }
    string? DeviceName { get; }
    string? StatusMessage { get; }
    string StatusClass { get; }
    string? AutoReconnectMessage { get; }
    string AutoReconnectClass { get; }
    IReadOnlyList<TimeularLogEntry> ChangeLog { get; }

    event Action? OnStateChanged;

    Task InitializeAsync();
    Task ConnectAsync();
    Task DisconnectAsync();
}

public sealed record TimeularLogEntry(DateTimeOffset At, string Message);
