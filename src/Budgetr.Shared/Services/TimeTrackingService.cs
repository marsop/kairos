using Budgetr.Shared.Models;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace Budgetr.Shared.Services;

/// <summary>
/// Implementation of time tracking service with local storage persistence.
/// </summary>
public class TimeTrackingService : ITimeTrackingService
{
    private readonly IStorageService _storage;
    private readonly IMeterConfigurationService _meterConfig;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly IStringLocalizer<Budgetr.Shared.Resources.Strings> _localizer;
    private TimeAccount _account = new TimeAccount();
    private const string StorageKey = "budgetr_account";
    public const int MaxMeters = 8;
    
    public TimeAccount Account => _account;

    public TimeSpan TimelinePeriod
    {
        get => _account.TimelinePeriod;
        set
        {
            if (_account.TimelinePeriod != value)
            {
                _account.TimelinePeriod = value;
                OnStateChanged?.Invoke();
                _ = SaveAsync();
            }
        }
    }
    
    public event Action? OnStateChanged;

    public TimeTrackingService(IStorageService storage, IMeterConfigurationService meterConfig, ISettingsService settingsService, INotificationService notificationService, IStringLocalizer<Budgetr.Shared.Resources.Strings> localizer)
    {
        _storage = storage;
        _meterConfig = meterConfig;
        _settingsService = settingsService;
        _notificationService = notificationService;
        _localizer = localizer;
    }

    public TimeSpan GetCurrentBalance()
    {
        return TimeSpan.FromTicks(_account.Events.Sum(e => e.TimeContribution.Ticks));
    }

    public MeterEvent? GetActiveEvent()
    {
        return _account.Events.FirstOrDefault(e => e.IsActive);
    }

    public void ActivateMeter(Guid meterId)
    {
        // Deactivate any active meter silently (no notification since we're switching)
        DeactivateInternal();
        
        var meter = _account.Meters.FirstOrDefault(m => m.Id == meterId);
        if (meter == null) return;
        
        var newEvent = new MeterEvent
        {
            StartTime = DateTimeOffset.UtcNow,
            Factor = meter.Factor,
            MeterName = meter.Name
        };
        
        _account.Events.Add(newEvent);
        OnStateChanged?.Invoke();
        _ = SaveAsync();
        _ = _notificationService.NotifyAsync(
            _localizer["NotificationMeterStartedTitle"],
            string.Format(_localizer["NotificationMeterStartedBody"], meter.Name)
        );
    }

    public void DeactivateMeter()
    {
        var activeEvent = GetActiveEvent();
        if (activeEvent != null)
        {
            var meterName = activeEvent.MeterName;
            DeactivateInternal();
            _ = _notificationService.NotifyAsync(
                _localizer["NotificationMeterStoppedTitle"],
                string.Format(_localizer["NotificationMeterStoppedBody"], meterName)
            );
        }
    }

    private void DeactivateInternal()
    {
        var activeEvent = GetActiveEvent();
        if (activeEvent != null)
        {
            activeEvent.EndTime = DateTimeOffset.UtcNow;
            OnStateChanged?.Invoke();
            _ = SaveAsync();
        }
    }

    public void DeleteEvent(Guid eventId)
    {
        var eventToDelete = _account.Events.FirstOrDefault(e => e.Id == eventId);
        if (eventToDelete != null)
        {
            _account.Events.Remove(eventToDelete);
            OnStateChanged?.Invoke();
            _ = SaveAsync();
        }
    }

    public void UpdateEventTimes(Guid eventId, DateTimeOffset newStartTime, DateTimeOffset newEndTime)
    {
        var meterEvent = _account.Events.FirstOrDefault(e => e.Id == eventId);
        if (meterEvent == null) return;

        if (meterEvent.IsActive)
        {
            throw new InvalidOperationException("Cannot edit times of an active event.");
        }

        if (newStartTime >= newEndTime)
        {
            throw new ArgumentException("Start time must be before end time.");
        }

        if (newEndTime > DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("End time cannot be in the future.");
        }

        meterEvent.StartTime = newStartTime;
        meterEvent.EndTime = newEndTime;
        OnStateChanged?.Invoke();
        _ = SaveAsync();
    }

    public List<TimelineDataPoint> GetTimelineData(TimeSpan period)
    {
        var points = new List<TimelineDataPoint>();
        var endTime = DateTimeOffset.UtcNow;
        var startTime = endTime - period;
        
        // Get all events that overlap with the period
        var relevantEvents = _account.Events
            .Where(e => e.StartTime <= endTime && (e.EndTime ?? endTime) >= startTime)
            .OrderBy(e => e.StartTime)
            .ToList();
        
        // Calculate balance before the period starts
        double runningBalance = 0;
        var eventsBefore = _account.Events
            .Where(e => e.StartTime < startTime)
            .ToList();
        
        foreach (var evt in eventsBefore)
        {
            var effectiveEnd = evt.EndTime ?? startTime;
            if (effectiveEnd > startTime) effectiveEnd = startTime;
            var duration = effectiveEnd - evt.StartTime;
            runningBalance += duration.TotalHours * evt.Factor;
        }
        
        // Always add start point
        points.Add(new TimelineDataPoint { Timestamp = startTime, BalanceHours = runningBalance });
        
        // Add points for each event transition in the period
        foreach (var evt in relevantEvents)
        {
            // Point at start of event (before contribution)
            if (evt.StartTime >= startTime)
            {
                points.Add(new TimelineDataPoint 
                { 
                    Timestamp = evt.StartTime, 
                    BalanceHours = runningBalance 
                });
            }
            
            // Calculate contribution up to end or now
            var effectiveStart = evt.StartTime < startTime ? startTime : evt.StartTime;
            var effectiveEnd = evt.EndTime ?? endTime;
            if (effectiveEnd > endTime) effectiveEnd = endTime;
            
            var contribution = (effectiveEnd - effectiveStart).TotalHours * evt.Factor;
            runningBalance += contribution;
            
            // Point at end of event (or now)
            if (evt.EndTime.HasValue && evt.EndTime.Value <= endTime)
            {
                points.Add(new TimelineDataPoint 
                { 
                    Timestamp = evt.EndTime.Value, 
                    BalanceHours = runningBalance 
                });
            }
        }
        
        // Always add current point
        points.Add(new TimelineDataPoint { Timestamp = endTime, BalanceHours = runningBalance });
        
        return points.OrderBy(p => p.Timestamp).ToList();
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_account);
        await _storage.SetItemAsync(StorageKey, json);
    }

    public async Task LoadAsync()
    {
        var json = await _storage.GetItemAsync(StorageKey);
        if (!string.IsNullOrEmpty(json))
        {
            var loaded = JsonSerializer.Deserialize<TimeAccount>(json);
            if (loaded != null)
            {
                _account.Events = loaded.Events;
                _account.Meters = loaded.Meters;

                // Enforce maximum meter limit
                if (_account.Meters.Count > MaxMeters)
                {
                    _account.Meters = _account.Meters
                        .OrderBy(m => m.DisplayOrder)
                        .Take(MaxMeters)
                        .ToList();
                }
                
                // Ensure TimelinePeriod is valid (handle migration from versions without it)
                if (loaded.TimelinePeriod != TimeSpan.Zero)
                {
                    _account.TimelinePeriod = loaded.TimelinePeriod;
                }
                else
                {
                    _account.TimelinePeriod = TimeSpan.FromHours(24);
                }
            }
        }

        // Only load default meters if none were loaded from storage
        if (_account.Meters == null || _account.Meters.Count == 0)
        {
            _account.Meters = await _meterConfig.LoadMetersAsync();
        }
        
        // Auto-stop any active events whose meter factor no longer exists
        var availableFactors = _account.Meters.Select(m => m.Factor).ToHashSet();
        foreach (var activeEvent in _account.Events.Where(e => e.IsActive))
        {
            if (!availableFactors.Contains(activeEvent.Factor))
            {
                activeEvent.EndTime = DateTimeOffset.UtcNow;
            }
        }
        
        OnStateChanged?.Invoke();
        await SaveAsync();
    }

    public void RenameMeter(Guid meterId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.Length < 1 || newName.Length > 40)
        {
            throw new ArgumentException("Meter name must be between 1 and 40 characters.");
        }

        var meter = _account.Meters.FirstOrDefault(m => m.Id == meterId);
        if (meter == null) return;

        meter.Name = newName.Trim();
        
        // Update active event if this meter is currently running
        var activeEvent = GetActiveEvent();
        if (activeEvent != null && activeEvent.Factor == meter.Factor)
        {
            activeEvent.MeterName = meter.Name;
        }
        
        OnStateChanged?.Invoke();
        _ = SaveAsync();
    }

    public string ExportData()
    {
        var exportData = new BudgetrExportData
        {
            ExportedAt = DateTimeOffset.UtcNow,
            Language = _settingsService.Language,
            TutorialCompleted = _settingsService.TutorialCompleted,
            Meters = _account.Meters,
            Events = _account.Events
        };
        
        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }

    public async Task ImportDataAsync(string json)
    {
        var importData = JsonSerializer.Deserialize<BudgetrExportData>(json);
        
        if (importData == null)
        {
            throw new InvalidOperationException("Invalid import data format.");
        }

        if (importData.Meters == null || importData.Meters.Count == 0)
        {
            throw new InvalidOperationException("Import data must contain at least one meter.");
        }

        // Apply language setting (defaults to "en" if not present in import data)
        _settingsService.Language = string.IsNullOrEmpty(importData.Language) ? "en" : importData.Language;
        
        // Apply tutorial completion setting
        _settingsService.TutorialCompleted = importData.TutorialCompleted;

        // Assign display order based on definition order
        for (int i = 0; i < importData.Meters.Count; i++)
        {
            importData.Meters[i].DisplayOrder = i;
        }

        // Enforce maximum meter limit
        if (importData.Meters.Count > MaxMeters)
        {
            importData.Meters = importData.Meters.Take(MaxMeters).ToList();
        }

        // Replace current data
        _account.Meters = importData.Meters;
        _account.Events = importData.Events ?? new List<MeterEvent>();

        // Auto-stop any active events whose meter factor no longer exists
        var availableFactors = _account.Meters.Select(m => m.Factor).ToHashSet();
        foreach (var activeEvent in _account.Events.Where(e => e.IsActive))
        {
            if (!availableFactors.Contains(activeEvent.Factor))
            {
                activeEvent.EndTime = DateTimeOffset.UtcNow;
            }
        }

        OnStateChanged?.Invoke();
        await SaveAsync();
    }

    public void DeleteMeter(Guid meterId)
    {
        var meter = _account.Meters.FirstOrDefault(m => m.Id == meterId);
        if (meter == null) return;

        var activeEvent = GetActiveEvent();
        if (activeEvent != null && activeEvent.MeterName == meter.Name) // Matching by name as names are unique per definition (though factor is the strict key, UI uses name) - actually `ActivateMeter` stores name. Let's check logic.
        {
             // Checking by ID in account meters is safer if we had ID in event, but event stores name/factor.
             // Let's rely on the meter we just found. 
             // IF the currently active event corresponds to this meter.
             // GetActiveEvent returns an event. Event has MeterName and Factor.
             // Meter has Name and Factor.
             // The most reliable check for "Is Active" as used in Meters.razor is:
             // activeEvent.MeterName == meter.Name
             
             if (activeEvent.MeterName == meter.Name)
             {
                 throw new InvalidOperationException("Cannot delete the currently active meter.");
             }
        }
        
        _account.Meters.Remove(meter);
        OnStateChanged?.Invoke();
        _ = SaveAsync();
    }

    public void AddMeter(string name, double factor)
    {
        if (_account.Meters.Count >= MaxMeters)
        {
            throw new InvalidOperationException($"Cannot add more than {MaxMeters} meters.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length < 1 || name.Length > 40)
        {
            throw new ArgumentException("Meter name must be between 1 and 40 characters.");
        }

        // Check for duplicate factor - REMOVED per user request
        // if (_account.Meters.Any(m => Math.Abs(m.Factor - factor) < 0.001))
        // {
        //     throw new ArgumentException($"A meter with factor {factor} already exists.");
        // }

        var newMeter = new Meter
        {
            Name = name.Trim(),
            Factor = factor,
            DisplayOrder = _account.Meters.Count > 0 ? _account.Meters.Max(m => m.DisplayOrder) + 1 : 0
        };

        _account.Meters.Add(newMeter);
        OnStateChanged?.Invoke();
        _ = SaveAsync();
    }
    public async Task ResetDataAsync()
    {
        _account.Events.Clear();
        _account.Meters = await _meterConfig.LoadMetersAsync();
        
        // Reset timeline period to default
        _account.TimelinePeriod = TimeSpan.FromHours(24);
        
        OnStateChanged?.Invoke();
        await SaveAsync();
    }

    public void ReorderMeters(List<Guid> orderedMeterIds)
    {
        if (orderedMeterIds == null || orderedMeterIds.Count != _account.Meters.Count)
        {
            return;
        }

        var newMetersList = new List<Meter>();
        int order = 0;

        foreach (var id in orderedMeterIds)
        {
            var meter = _account.Meters.FirstOrDefault(m => m.Id == id);
            if (meter != null)
            {
                meter.DisplayOrder = order++;
                newMetersList.Add(meter);
            }
        }

        // Only apply if we found all meters (integrity check)
        if (newMetersList.Count == _account.Meters.Count)
        {
            _account.Meters = newMetersList;
            OnStateChanged?.Invoke();
            _ = SaveAsync();
        }
    }
}

/// <summary>
/// Data structure for import/export operations.
/// </summary>
public class BudgetrExportData
{
    public DateTimeOffset ExportedAt { get; set; }
    public string Language { get; set; } = "en";
    public bool TutorialCompleted { get; set; }
    public List<Meter> Meters { get; set; } = new();
    public List<MeterEvent> Events { get; set; } = new();
}
