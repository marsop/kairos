using Kairos.Shared.Models;
using Kairos.Shared.Services;
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
            Factor = activity.Factor,
            DisplayOrder = activity.DisplayOrder
        };
    }
}

internal sealed class StubSettingsService : ISettingsService
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "en";
    public bool TutorialCompleted { get; set; }
    public bool BrowserNotificationsEnabled { get; set; }
    public event Action? OnSettingsChanged;

    public Task LoadAsync() => Task.CompletedTask;
    public Task SaveAsync() => Task.CompletedTask;

    public Task SetLanguageAsync(string language)
    {
        Language = language;
        OnSettingsChanged?.Invoke();
        return Task.CompletedTask;
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

internal sealed class StubStringLocalizer : IStringLocalizer<Kairos.Shared.Resources.Strings>
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
