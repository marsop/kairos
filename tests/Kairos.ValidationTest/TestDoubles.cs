using Kairos.Core.Models;
using Kairos.Application.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using System.Net;
using System.Net.Http;

namespace Kairos.ValidationTest;

internal sealed class InMemoryStorageService : IStorageService
{
    private readonly Dictionary<string, string> _store = new();
    public int SetCalls { get; private set; }
    public List<string> RemovedKeys { get; } = new();

    public Task<string?> GetItemAsync(string key)
    {
        _store.TryGetValue(key, out var value);
        return Task.FromResult<string?>(value);
    }

    public Task SetItemAsync(string key, string value)
    {
        _store[key] = value;
        SetCalls++;
        return Task.CompletedTask;
    }

    public Task RemoveItemAsync(string key)
    {
        _store.Remove(key);
        RemovedKeys.Add(key);
        return Task.CompletedTask;
    }
}

internal sealed class StubActivityConfigurationService : IActivityConfigurationService
{
    private readonly List<Activity> _activities;

    public int LoadCalls { get; private set; }

    public StubActivityConfigurationService(IEnumerable<Activity> activities)
    {
        _activities = activities.ToList();
    }

    public Task<List<Activity>> LoadActivitiesAsync()
    {
        LoadCalls++;
        return Task.FromResult(_activities.Select(CloneActivity).ToList());
    }

    private static Activity CloneActivity(Activity activity)
    {
        return new Activity
        {
            Id = activity.Id,
            Name = activity.Name,
            Color = activity.Color,

            DisplayOrder = activity.DisplayOrder
        };
    }
}

internal sealed class StubSettingsService : ISettingsService
{
    private readonly List<string> _activityGroupNames = [string.Empty, string.Empty];
    private readonly List<string> _activityGroupColors = ["#10B981", "#10B981"];
    private readonly List<string> _activityGroupIcons = ["🗂️", "🗂️"];

    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "en";
    public bool TutorialCompleted { get; set; }
    public string HistoryView { get; set; } = "list";
    public string ChartType { get; set; } = "line";
    public bool BrowserNotificationsEnabled { get; set; }
    public bool SoundsEnabled { get; set; }
    public bool AdvancedSettingsEnabled { get; set; } = true;
    public bool TimeularSettingsEnabled { get; set; }
    public bool ActivityGroupsEnabled { get; set; }
    public bool BudgetsEnabled { get; set; } = true;
    public int ActiveActivityGroup { get; set; } = 0;
    public int ActivityGroupCount { get; set; } = 2;
    public int AutoDeleteEventDuration { get; set; }
    public int StickyEventsDuration { get; set; } = 0;
    public DateTimeOffset? LastSupabaseSync { get; private set; }
    public event Action? OnSettingsChanged;

    public Task LoadAsync() => Task.CompletedTask;
    public Task SaveAsync() => Task.CompletedTask;

    public Task SetLanguageAsync(string language)
    {
        Language = language;
        OnSettingsChanged?.Invoke();
        return Task.CompletedTask;
    }

    public string? GetActivityGroupName(int groupId)
    {
        if (groupId < 0 || groupId >= _activityGroupNames.Count)
        {
            return null;
        }

        var value = _activityGroupNames[groupId]?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void SetActivityGroupName(int groupId, string? name)
    {
        if (groupId < 0)
        {
            return;
        }

        while (_activityGroupNames.Count <= groupId)
        {
            _activityGroupNames.Add(string.Empty);
        }

        _activityGroupNames[groupId] = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        OnSettingsChanged?.Invoke();
    }

    public void RemoveActivityGroupNameAt(int groupId)
    {
        if (groupId < 0 || groupId >= _activityGroupNames.Count)
        {
            return;
        }

        _activityGroupNames.RemoveAt(groupId);
        if (groupId < _activityGroupColors.Count)
        {
            _activityGroupColors.RemoveAt(groupId);
        }
        if (groupId < _activityGroupIcons.Count)
        {
            _activityGroupIcons.RemoveAt(groupId);
        }
        OnSettingsChanged?.Invoke();
    }

    public string? GetActivityGroupColor(int groupId)
    {
        if (groupId < 0 || groupId >= _activityGroupColors.Count)
        {
            return null;
        }

        return _activityGroupColors[groupId];
    }

    public void SetActivityGroupColor(int groupId, string? color)
    {
        if (groupId < 0)
        {
            return;
        }

        while (_activityGroupColors.Count <= groupId)
        {
            _activityGroupColors.Add("#10B981");
        }

        _activityGroupColors[groupId] = string.IsNullOrWhiteSpace(color) ? "#10B981" : color.Trim();
        OnSettingsChanged?.Invoke();
    }

    public string? GetActivityGroupIcon(int groupId)
    {
        if (groupId < 0 || groupId >= _activityGroupIcons.Count)
        {
            return null;
        }

        return _activityGroupIcons[groupId];
    }

    public void SetActivityGroupIcon(int groupId, string? icon)
    {
        if (groupId < 0)
        {
            return;
        }

        while (_activityGroupIcons.Count <= groupId)
        {
            _activityGroupIcons.Add("🗂️");
        }

        _activityGroupIcons[groupId] = string.IsNullOrWhiteSpace(icon) ? "🗂️" : icon.Trim();
        OnSettingsChanged?.Invoke();
    }

    public void UpdateLastSupabaseSync()
    {
        LastSupabaseSync = DateTimeOffset.UtcNow;
        OnSettingsChanged?.Invoke();
    }
}

internal sealed class StubNotificationService : INotificationService
{
    public List<(string Title, string Body)> SentNotifications { get; } = new();
    public event Action<ToastMessage>? OnToastReceived;

    public Task NotifyAsync(string title, string body)
    {
        SentNotifications.Add((title, body));
        OnToastReceived?.Invoke(new ToastMessage(title, body, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task<string> GetBrowserPermissionStateAsync() => Task.FromResult("default");
    public Task<string> RequestBrowserPermissionAsync() => Task.FromResult("default");
}

internal sealed class StubStringLocalizer : IStringLocalizer<Kairos.Application.Resources.Strings>
{
    public LocalizedString this[string name] => new LocalizedString(name, name);

    public LocalizedString this[string name, params object[] arguments] => new LocalizedString(name, string.Format(name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
}

internal sealed class TestNavigationManager : NavigationManager
{
    public List<string> Navigations { get; } = new();

    public TestNavigationManager()
    {
        Initialize("http://localhost/", "http://localhost/");
    }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        var absolute = ToAbsoluteUri(uri).ToString();
        Navigations.Add(absolute);
        Uri = absolute;
    }
}

internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}

internal sealed class TestJsRuntime : IJSRuntime
{
    private readonly Dictionary<string, Func<object?[]?, object?>> _handlers = new();
    private readonly Dictionary<string, Exception> _exceptions = new();

    public List<(string Identifier, object?[] Arguments)> Invocations { get; } = new();

    public void SetResult(string identifier, object? result)
    {
        _handlers[identifier] = _ => result;
    }

    public void SetException(string identifier, Exception exception)
    {
        _exceptions[identifier] = exception;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var arguments = args ?? Array.Empty<object?>();
        Invocations.Add((identifier, arguments));

        if (_exceptions.TryGetValue(identifier, out var exception))
        {
            return ValueTask.FromException<TValue>(exception);
        }

        if (_handlers.TryGetValue(identifier, out var handler))
        {
            return ValueTask.FromResult((TValue)handler(arguments)!);
        }

        return ValueTask.FromResult(default(TValue)!);
    }
}

internal sealed class StubSupabaseAuthService : ISupabaseAuthService
{
    public bool IsInitialized { get; set; } = true;
    public bool IsConfigured { get; set; } = true;
    public bool IsAuthenticated { get; set; }
    public string? CurrentUserEmail { get; set; }
    public string? CurrentUserId { get; set; }
    public string? CurrentAccessToken { get; set; }
    public event Action? OnAuthStateChanged;

    public Task InitializeAsync() => Task.CompletedTask;
    public Task<SupabaseAuthResult> SignInAsync(string email, string password) => Task.FromResult(new SupabaseAuthResult { Succeeded = true });
    public Task<SupabaseAuthResult> SignUpAsync(string email, string password) => Task.FromResult(new SupabaseAuthResult { Succeeded = true });
    public Task SignOutAsync() => Task.CompletedTask;

    public void RaiseAuthStateChanged() => OnAuthStateChanged?.Invoke();
}

internal sealed class StubSupabaseActivityStore : ISupabaseActivityStore
{
    public List<Activity> Activities { get; set; } = new();
    public int LoadCalls { get; private set; }
    public int SaveCalls { get; private set; }

    public Task<IReadOnlyList<Activity>> LoadActivitiesAsync()
    {
        LoadCalls++;
        return Task.FromResult<IReadOnlyList<Activity>>(Activities.Select(a => new Activity
        {
            Id = a.Id,
            Name = a.Name,
            Color = a.Color,

            DisplayOrder = a.DisplayOrder
        }).ToList());
    }

    public Task SaveActivitiesAsync(IReadOnlyList<Activity> activities)
    {
        SaveCalls++;
        Activities = activities.Select(a => new Activity
        {
            Id = a.Id,
            Name = a.Name,
            Color = a.Color,

            DisplayOrder = a.DisplayOrder
        }).ToList();
        return Task.CompletedTask;
    }
}

internal sealed class StubSupabaseTimeAccountStore : ISupabaseTimeAccountStore
{
    public TimeAccount? LoadedAccount { get; set; }
    public int LoadCalls { get; private set; }
    public int SaveCalls { get; private set; }

    public Task<TimeAccount?> LoadAccountAsync()
    {
        LoadCalls++;
        return Task.FromResult(CloneAccount(LoadedAccount));
    }

    public Task SaveAccountAsync(TimeAccount account)
    {
        SaveCalls++;
        LoadedAccount = CloneAccount(account);
        return Task.CompletedTask;
    }

    private static TimeAccount? CloneAccount(TimeAccount? account)
    {
        if (account is null)
        {
            return null;
        }

        return new TimeAccount
        {
            Events = account.Events.Select(e => new ActivityEvent
            {
                Id = e.Id,
                StartTime = e.StartTime,
                EndTime = e.EndTime,

                ActivityName = e.ActivityName,
                ActivityColor = e.ActivityColor,
                Comment = e.Comment
            }).ToList(),
            Activities = account.Activities.Select(a => new Activity
            {
                Id = a.Id,
                Name = a.Name,
                Color = a.Color,

                DisplayOrder = a.DisplayOrder
            }).ToList(),
            TimelinePeriod = account.TimelinePeriod,
            LastModifiedAtUtc = account.LastModifiedAtUtc
        };
    }
}

internal sealed class StubSupabaseRealtimeService : ISupabaseRealtimeService
{
    public event Action<string>? OnTableChanged;
    public event Action? OnConnected;

    public Task InitializeAsync() => Task.CompletedTask;

    public void RaiseTableChanged(string table)
    {
        OnTableChanged?.Invoke(table);
    }

    public void TriggerConnected()
    {
        OnConnected?.Invoke();
    }
}

public class StubBudgetSettingsService : IBudgetSettingsService
{
    public bool MinimumEnabled { get; set; } = false;
    public int Threshold { get; set; } = 95;
    public string ColorMinimumNotReached { get; set; } = "#0000ff";
    public string ColorMinimumReachedMaxNotReached { get; set; } = "#00ff00";
    public string ColorBetweenThresholdMax { get; set; } = "#ffff00";
    public string ColorOverMax { get; set; } = "#ff0000";
    public BudgetType BudgetType { get; set; } = BudgetType.Monthly;
    public bool NotificationsEnabled { get; set; } = true;

#pragma warning disable CS0067
    public event Action? OnSettingsChanged;
#pragma warning restore CS0067
    public Task LoadAsync() => Task.CompletedTask;
    public Task SaveAsync() => Task.CompletedTask;
    public Task InitializeDefaultsIfEmptyAsync() => Task.CompletedTask;
}
