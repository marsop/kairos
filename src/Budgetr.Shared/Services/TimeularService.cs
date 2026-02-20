using Budgetr.Shared.Models;
using Budgetr.Shared.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Budgetr.Shared.Services;

/// <summary>
/// App-level Timeular integration service. Keeps listener active across page navigation.
/// </summary>
public sealed class TimeularService : ITimeularService, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ITimeTrackingService _timeService;
    private readonly INotificationService _notificationService;
    private readonly IStringLocalizer<Strings> _localizer;
    private readonly List<TimeularLogEntry> _changeLog = new();
    private DotNetObjectReference<TimeularService>? _interopRef;

    public bool IsInitialized { get; private set; }
    public bool IsConnecting { get; private set; }
    public bool IsConnected { get; private set; }
    public bool HasConnectedBefore { get; private set; }
    public string? DeviceName { get; private set; }
    public string? StatusMessage { get; private set; }
    public string StatusClass { get; private set; } = string.Empty;
    public string? AutoReconnectMessage { get; private set; }
    public string AutoReconnectClass { get; private set; } = string.Empty;
    public IReadOnlyList<TimeularLogEntry> ChangeLog => _changeLog;

    public event Action? OnStateChanged;

    public TimeularService(IJSRuntime jsRuntime, ITimeTrackingService timeService, INotificationService notificationService, IStringLocalizer<Strings> localizer)
    {
        _jsRuntime = jsRuntime;
        _timeService = timeService;
        _notificationService = notificationService;
        _localizer = localizer;
    }

    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        _interopRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("timeularInterop.registerListener", _interopRef);
        await LoadStateAsync();
        await TryReconnectSavedDeviceAsync();
        IsInitialized = true;
        NotifyStateChanged();
    }

    public async Task ConnectAsync()
    {
        await InitializeAsync();

        IsConnecting = true;
        StatusMessage = null;
        NotifyStateChanged();

        try
        {
            var result = await _jsRuntime.InvokeAsync<TimeularConnectResult>("timeularInterop.requestAndConnect");
            if (result.Success)
            {
                IsConnected = true;
                HasConnectedBefore = true;
                DeviceName = result.DeviceName;
                StatusMessage = $"Connected to {result.DeviceName}.";
                StatusClass = "success";
                AddTimeularChange($"Connected to {result.DeviceName ?? "Timeular"}");

                _ = _notificationService.NotifyAsync(
                    _localizer["NotificationTimeularConnectedTitle"],
                    _localizer["NotificationTimeularConnectedBody"]);
            }
            else
            {
                IsConnected = false;
                StatusMessage = result.Message ?? "Could not connect to the Timeular device.";
                StatusClass = "error";
                AddTimeularChange(StatusMessage);
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = $"Could not connect to Timeular: {ex.Message}";
            StatusClass = "error";
            AddTimeularChange(StatusMessage);
        }
        finally
        {
            IsConnecting = false;
            NotifyStateChanged();
        }
    }

    public async Task DisconnectAsync()
    {
        await InitializeAsync();

        try
        {
            await _jsRuntime.InvokeVoidAsync("timeularInterop.disconnect");
            IsConnected = false;
            StatusMessage = "Timeular disconnected.";
            StatusClass = "success";
            AddTimeularChange("Disconnected from Timeular");

            _ = _notificationService.NotifyAsync(
                _localizer["NotificationTimeularDisconnectedTitle"],
                _localizer["NotificationTimeularDisconnectedBody"]);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not disconnect from Timeular: {ex.Message}";
            StatusClass = "error";
            AddTimeularChange(StatusMessage);
        }

        NotifyStateChanged();
    }

    [JSInvokable]
    public Task OnTimeularChange(TimeularChangeEvent change)
    {
        if (change.EventType == "disconnected")
        {
            IsConnected = false;
            StatusMessage = "Timeular disconnected.";
            StatusClass = "error";
            AddTimeularChange("Device disconnected");

            _ = _notificationService.NotifyAsync(
                _localizer["NotificationTimeularDisconnectedTitle"],
                _localizer["NotificationTimeularDisconnectedBody"]);
        }
        else if (change.EventType == "orientation")
        {
            var faceLabel = change.Face.HasValue ? $"Face {change.Face.Value}" : "Face ?";
            var rawLabel = string.IsNullOrWhiteSpace(change.RawHex) ? string.Empty : $" ({change.RawHex})";
            var mappingAction = ApplyTimeularFaceMapping(change.Face);
            AddTimeularChange($"{faceLabel}{rawLabel}{mappingAction}", change.TimestampUtc);
        }
        else
        {
            AddTimeularChange("Received a device event", change.TimestampUtc);
        }

        NotifyStateChanged();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _ = _jsRuntime.InvokeVoidAsync("timeularInterop.unregisterListener");
        _interopRef?.Dispose();
    }

    private async Task LoadStateAsync()
    {
        try
        {
            var savedState = await _jsRuntime.InvokeAsync<TimeularSavedState?>("timeularInterop.getSavedState");
            if (savedState is not null)
            {
                DeviceName = savedState.DeviceName;
                HasConnectedBefore = !string.IsNullOrWhiteSpace(savedState.DeviceId)
                    || !string.IsNullOrWhiteSpace(savedState.DeviceName)
                    || !string.IsNullOrWhiteSpace(savedState.ConnectedAtUtc);
                IsConnected = false;
                AddTimeularChange($"Known device: {DeviceName ?? "Timeular"}");
            }
        }
        catch
        {
            // Ignore state restoration errors to avoid blocking startup
        }
    }

    private async Task TryReconnectSavedDeviceAsync()
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<TimeularReconnectResult?>("timeularInterop.reconnectSavedDevice");
            if (result is null)
            {
                return;
            }

            if (result.Success)
            {
                IsConnected = true;
                HasConnectedBefore = true;
                DeviceName = result.DeviceName ?? DeviceName;
                StatusMessage = $"Reconnected to {DeviceName ?? "Timeular"}.";
                StatusClass = "success";
                AutoReconnectMessage = $"Auto-reconnect succeeded: {DeviceName ?? "Timeular"} is connected.";
                AutoReconnectClass = "success";
                AddTimeularChange($"Reconnected to {DeviceName ?? "Timeular"}");

                _ = _notificationService.NotifyAsync(
                    _localizer["NotificationTimeularConnectedTitle"],
                    _localizer["NotificationTimeularConnectedBody"]);
                return;
            }

            var reason = string.IsNullOrWhiteSpace(result.Message) ? "No details were provided." : result.Message;
            AutoReconnectClass = result.Attempted ? "error" : "info";
            AutoReconnectMessage = result.Attempted
                ? $"Auto-reconnect failed: {reason}"
                : $"Auto-reconnect skipped: {reason}";
        }
        catch
        {
            AutoReconnectClass = "error";
            AutoReconnectMessage = "Auto-reconnect failed due to an unexpected startup error.";
        }
    }

    private string ApplyTimeularFaceMapping(int? face)
    {
        var orderedMeters = _timeService.Account.Meters
            .OrderBy(m => m.DisplayOrder)
            .ToList();

        Meter? targetMeter = null;
        if (face.HasValue && face.Value > 0)
        {
            targetMeter = orderedMeters.ElementAtOrDefault(face.Value - 1);
        }

        var activeEvent = _timeService.GetActiveEvent();
        if (targetMeter is null)
        {
            if (activeEvent is not null)
            {
                _timeService.DeactivateMeter();
                return " -> deactivated";
            }

            return string.Empty;
        }

        var isTargetAlreadyActive = activeEvent is not null
            && activeEvent.MeterName == targetMeter.Name
            && Math.Abs(activeEvent.Factor - targetMeter.Factor) < 0.0001;

        if (isTargetAlreadyActive)
        {
            return " -> already active";
        }

        _timeService.ActivateMeter(targetMeter.Id);
        var mappedIndex = orderedMeters.FindIndex(m => m.Id == targetMeter.Id) + 1;
        return $" -> activated #{mappedIndex}";
    }

    private void AddTimeularChange(string message, string? timestampUtc = null)
    {
        var timestamp = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(timestampUtc) && DateTimeOffset.TryParse(timestampUtc, out var parsed))
        {
            timestamp = parsed;
        }

        _changeLog.Insert(0, new TimeularLogEntry(timestamp, message));
        if (_changeLog.Count > 4)
        {
            _changeLog.RemoveRange(4, _changeLog.Count - 4);
        }
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }

    public sealed class TimeularSavedState
    {
        public string? DeviceName { get; set; }
        public string? DeviceId { get; set; }
        public string? ConnectedAtUtc { get; set; }
    }

    public sealed class TimeularConnectResult
    {
        public bool Success { get; set; }
        public string? DeviceName { get; set; }
        public string? DeviceId { get; set; }
        public string? Message { get; set; }
    }

    public sealed class TimeularReconnectResult
    {
        public bool Attempted { get; set; }
        public bool Success { get; set; }
        public string? DeviceName { get; set; }
        public string? DeviceId { get; set; }
        public string? Message { get; set; }
    }

    public sealed class TimeularChangeEvent
    {
        public string? EventType { get; set; }
        public int? Face { get; set; }
        public string? RawHex { get; set; }
        public string? TimestampUtc { get; set; }
    }
}
