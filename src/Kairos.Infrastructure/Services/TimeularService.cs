using Kairos.Core.Models;
using Kairos.Application.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Kairos.Infrastructure.Services;

/// <summary>
/// App-level Timeular integration service. Keeps listener active across page navigation.
/// </summary>
public sealed class TimeularService : ITimeularService, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ITimeTrackingService _timeService;
    private readonly IActivityStartPromptService _activityStartPromptService;
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;
    private readonly IStringLocalizer<Strings> _localizer;
    private readonly ILogger<TimeularService> _logger;
    private readonly List<TimeularLogEntry> _changeLog = new();
    private DotNetObjectReference<TimeularService>? _interopRef;

    public bool IsInitialized { get; private set; }
    public bool IsConnecting { get; private set; }
    public bool IsConnected { get; private set; }
    public bool HasConnectedBefore { get; private set; }
    public string? DeviceName { get; private set; }
    public string? StatusMessage { get; private set; }
    public string StatusClass { get; private set; } = string.Empty;
    public IReadOnlyList<TimeularLogEntry> ChangeLog => _changeLog;

    public event Action? OnStateChanged;

    public TimeularService(
        IJSRuntime jsRuntime,
        ITimeTrackingService timeService,
        IActivityStartPromptService activityStartPromptService,
        INotificationService notificationService,
        ISettingsService settingsService,
        IStringLocalizer<Strings> localizer,
        ILogger<TimeularService> logger)
    {
        _jsRuntime = jsRuntime;
        _timeService = timeService;
        _activityStartPromptService = activityStartPromptService;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _localizer = localizer;
        _logger = logger;
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

    public Task ConnectAsync() => ConnectInternalAsync(autoConnect: false);

    public Task AutoConnectAsync() => ConnectInternalAsync(autoConnect: true);

    private async Task ConnectInternalAsync(bool autoConnect)
    {
        await InitializeAsync();

        IsConnecting = true;
        StatusMessage = null;
        NotifyStateChanged();

        try
        {
            if (autoConnect)
            {
                try
                {
                    _logger.LogInformation("Attempting auto-reconnect to Timeular device '{DeviceName}'...", DeviceName ?? "Unknown");
                    var result = await _jsRuntime.InvokeAsync<TimeularReconnectResult?>("timeularInterop.reconnectSavedDevice");
                    if (result != null && result.Success)
                    {
                        IsConnected = true;
                        HasConnectedBefore = true;
                        DeviceName = result.DeviceName ?? DeviceName;
                        StatusMessage = $"Reconnected to {DeviceName ?? "Timeular"}.";
                        StatusClass = "success";
                        AddTimeularChange($"Reconnected to {DeviceName ?? "Timeular"}");

                        _ = _notificationService.NotifyAsync(
                            _localizer["NotificationTimeularConnectedTitle"],
                            _localizer["NotificationTimeularConnectedBody"]);

                        return; // Successfully reconnected, skip the picker
                    }
                    else
                    {
                        var failMessage = result?.Message ?? "No result from reconnect.";
                        _logger.LogWarning("Auto-reconnect to Timeular device '{DeviceName}' failed. Reason: {Message}", DeviceName ?? "Unknown", failMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Auto-reconnect to Timeular device '{DeviceName}' failed with an exception.", DeviceName ?? "Unknown");
                    // Ignore auto-reconnect errors and fall back to the picker
                }
            }

            // Fallback to picker
            var pickerResult = await _jsRuntime.InvokeAsync<TimeularConnectResult>("timeularInterop.requestAndConnect");
            if (pickerResult.Success)
            {
                IsConnected = true;
                HasConnectedBefore = true;
                DeviceName = pickerResult.DeviceName;
                StatusMessage = $"Connected to {pickerResult.DeviceName}.";
                StatusClass = "success";
                AddTimeularChange($"Connected to {pickerResult.DeviceName ?? "Timeular"}");

                _ = _notificationService.NotifyAsync(
                    _localizer["NotificationTimeularConnectedTitle"],
                    _localizer["NotificationTimeularConnectedBody"]);
            }
            else
            {
                IsConnected = false;
                StatusMessage = pickerResult.Message ?? "Could not connect to the Timeular device.";
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
            _logger.LogInformation("Startup auto-reconnect attempting for Timeular device '{DeviceName}'...", DeviceName ?? "Unknown");
            var result = await _jsRuntime.InvokeAsync<TimeularReconnectResult?>("timeularInterop.reconnectSavedDevice");
            if (result is null)
            {
                _logger.LogWarning("Startup auto-reconnect failed for Timeular device '{DeviceName}'. No result from reconnect.", DeviceName ?? "Unknown");
                return;
            }

            if (result.Success)
            {
                IsConnected = true;
                HasConnectedBefore = true;
                DeviceName = result.DeviceName ?? DeviceName;
                StatusMessage = $"Reconnected to {DeviceName ?? "Timeular"}.";
                StatusClass = "success";
                AddTimeularChange($"Reconnected to {DeviceName ?? "Timeular"}");

                _ = _notificationService.NotifyAsync(
                    _localizer["NotificationTimeularConnectedTitle"],
                    _localizer["NotificationTimeularConnectedBody"]);
            }
            else
            {
                var failMessage = result.Message ?? "Unknown reason.";
                _logger.LogWarning("Startup auto-reconnect failed for Timeular device '{DeviceName}'. Reason: {Message}", DeviceName ?? "Unknown", failMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup auto-reconnect failed for Timeular device '{DeviceName}' with an exception.", DeviceName ?? "Unknown");
            // Ignore unexpected startup errors during auto-reconnect
        }
    }

    private string ApplyTimeularFaceMapping(int? face)
    {
        var orderedActivities = _timeService.Account.Activities
            .Where(a => a.ActivityGroupId == _settingsService.ActiveActivityGroup)
            .OrderBy(m => m.DisplayOrder)
            .ToList();

        Activity? targetActivity = null;
        if (face.HasValue && face.Value > 0)
        {
            targetActivity = orderedActivities.ElementAtOrDefault(face.Value - 1);
        }

        var activeEvent = _timeService.GetActiveEvent();
        if (targetActivity is null)
        {
            if (activeEvent is not null)
            {
                _timeService.DeactivateActivity();
                return " -> deactivated";
            }

            return string.Empty;
        }

        var isTargetAlreadyActive = activeEvent is not null
            && activeEvent.ActivityName == targetActivity.Name;

        if (isTargetAlreadyActive)
        {
            return " -> already active";
        }

        var mappedIndex = orderedActivities.FindIndex(m => m.Id == targetActivity.Id) + 1;
        _activityStartPromptService.RequestStart(targetActivity.Id);
        return $" -> comment requested for #{mappedIndex}";
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
