using Budgetr.Shared.Models;
using Budgetr.Shared.Services;
using Microsoft.Extensions.Localization;

namespace Budgetr.ValidationTest;

internal sealed class InMemoryStorageService : IStorageService
{
    private readonly Dictionary<string, string> _store = new();

    public Task<string?> GetItemAsync(string key)
    {
        _store.TryGetValue(key, out var value);
        return Task.FromResult<string?>(value);
    }

    public Task SetItemAsync(string key, string value)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveItemAsync(string key)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }
}

internal sealed class StubMeterConfigurationService : IMeterConfigurationService
{
    private readonly List<Meter> _meters;

    public int LoadCalls { get; private set; }

    public StubMeterConfigurationService(IEnumerable<Meter> meters)
    {
        _meters = meters.ToList();
    }

    public Task<List<Meter>> LoadMetersAsync()
    {
        LoadCalls++;
        return Task.FromResult(_meters.Select(CloneMeter).ToList());
    }

    private static Meter CloneMeter(Meter meter)
    {
        return new Meter
        {
            Id = meter.Id,
            Name = meter.Name,
            Factor = meter.Factor,
            DisplayOrder = meter.DisplayOrder
        };
    }
}

internal sealed class StubSettingsService : ISettingsService
{
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

internal sealed class StubStringLocalizer : IStringLocalizer<Budgetr.Shared.Resources.Strings>
{
    public LocalizedString this[string name] => new LocalizedString(name, name);

    public LocalizedString this[string name, params object[] arguments] => new LocalizedString(name, string.Format(name, arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
}
