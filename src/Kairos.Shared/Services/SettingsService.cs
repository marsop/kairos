using System.Text.Json;
using System.Globalization;
using System.Threading;

namespace Kairos.Shared.Services;

/// <summary>
/// Implementation of settings service with local storage persistence.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IStorageService _storage;
    private readonly ISupabaseAuthService? _authService;
    private readonly ISupabaseSettingsStore? _supabaseSettingsStore;
    private readonly ISupabaseRealtimeService? _realtimeService;
    private readonly SemaphoreSlim _supabaseSyncLock = new(1, 1);
    private const string StorageKey = "Kairos_settings";
    private const string DefaultLanguage = "en";
    private const string DefaultTheme = "light";

    private bool _tutorialCompleted;
    private bool _browserNotificationsEnabled;
    private string _language = DefaultLanguage;
    private string _theme = DefaultTheme;

    public string Theme
    {
        get => _theme;
        set
        {
            var sanitizedTheme = SanitizeTheme(value);
            if (_theme != sanitizedTheme)
            {
                _theme = sanitizedTheme;
                OnSettingsChanged?.Invoke();
                _ = SaveAsync();
            }
        }
    }

    public bool TutorialCompleted
    {
        get => _tutorialCompleted;
        set
        {
            if (_tutorialCompleted != value)
            {
                _tutorialCompleted = value;
                OnSettingsChanged?.Invoke();
                _ = SaveAsync();
            }
        }
    }

    public bool BrowserNotificationsEnabled
    {
        get => _browserNotificationsEnabled;
        set
        {
            if (_browserNotificationsEnabled != value)
            {
                _browserNotificationsEnabled = value;
                OnSettingsChanged?.Invoke();
                _ = SaveAsync();
            }
        }
    }

    public async Task SetLanguageAsync(string language)
    {
        if (_language != language)
        {
            _language = language;
            UpdateCulture(language);
            OnSettingsChanged?.Invoke();
            await SaveAsync();
        }
    }

    public string Language
    {
        get => _language;
        set
        {
            if (_language != value)
            {
                _ = SetLanguageAsync(value);
            }
        }
    }

    public event Action? OnSettingsChanged;

    public SettingsService(
        IStorageService storage,
        ISupabaseAuthService? authService = null,
        ISupabaseSettingsStore? supabaseSettingsStore = null,
        ISupabaseRealtimeService? realtimeService = null)
    {
        _storage = storage;
        _authService = authService;
        _supabaseSettingsStore = supabaseSettingsStore;
        _realtimeService = realtimeService;
        if (_authService is not null)
        {
            _authService.OnAuthStateChanged += HandleAuthStateChanged;
        }
        if (_realtimeService is not null)
        {
            _realtimeService.OnTableChanged += HandleRemoteTableChanged;
        }
    }

    public async Task LoadAsync()
    {
        var json = await _storage.GetItemAsync(StorageKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                if (data != null)
                {
                    _theme = SanitizeTheme(data.Theme);
                    _language = string.IsNullOrEmpty(data.Language) ? DefaultLanguage : data.Language;
                    _tutorialCompleted = data.TutorialCompleted;
                    _browserNotificationsEnabled = data.BrowserNotificationsEnabled;
                }
            }
            catch
            {
                // If deserialization fails, keep defaults
                _theme = DefaultTheme;
                _language = DefaultLanguage;
                _tutorialCompleted = false;
                _browserNotificationsEnabled = false;
            }
        }
        
        await PullSettingsFromSupabaseOrSeedAsync(seedWhenMissing: true);
        UpdateCulture(_language);
        OnSettingsChanged?.Invoke();
    }

    private void UpdateCulture(string languageCode)
    {
        try
        {
            Console.WriteLine($"[SettingsService] Updating culture to '{languageCode}'");
            var culture = new CultureInfo(languageCode);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Console.WriteLine($"[SettingsService] Culture set to: {CultureInfo.CurrentCulture.Name}, UI: {CultureInfo.CurrentUICulture.Name}");
        }
        catch (CultureNotFoundException ex)
        {
            Console.WriteLine($"[SettingsService] Culture '{languageCode}' not found: {ex.Message}. Falling back to default.");
            // Fallback to default if culture code is invalid
            var defaultCulture = new CultureInfo(DefaultLanguage);
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;
        }
    }

    public async Task SaveAsync()
    {
        var data = new SettingsData 
        { 
            Theme = _theme,
            Language = _language,
            TutorialCompleted = _tutorialCompleted,
            BrowserNotificationsEnabled = _browserNotificationsEnabled
        };
        var json = JsonSerializer.Serialize(data);
        await _storage.SetItemAsync(StorageKey, json);
        await PersistSettingsToSupabaseAsync();
    }

    private static string SanitizeTheme(string? theme)
    {
        return string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : DefaultTheme;
    }

    private void HandleAuthStateChanged()
    {
        _ = PullSettingsFromSupabaseOrSeedAsync(seedWhenMissing: true);
    }

    private void HandleRemoteTableChanged(string table)
    {
        if (string.Equals(table, "user_settings", StringComparison.Ordinal))
        {
            _ = PullSettingsFromSupabaseOrSeedAsync(seedWhenMissing: false);
        }
    }

    private async Task PullSettingsFromSupabaseOrSeedAsync(bool seedWhenMissing)
    {
        if (_authService is null || _supabaseSettingsStore is null || !_authService.IsAuthenticated)
        {
            return;
        }

        await _supabaseSyncLock.WaitAsync();
        try
        {
            var remote = await _supabaseSettingsStore.LoadSettingsAsync();
            if (remote is not null)
            {
                ApplySyncedSettings(remote);
                await SaveLocalAsync();
                OnSettingsChanged?.Invoke();
                return;
            }

            if (seedWhenMissing)
            {
                await _supabaseSettingsStore.SaveSettingsAsync(BuildSyncedSettings());
            }
        }
        catch
        {
            // Keep local storage as fallback if cloud sync fails.
        }
        finally
        {
            _supabaseSyncLock.Release();
        }
    }

    private async Task PersistSettingsToSupabaseAsync()
    {
        if (_authService is null || _supabaseSettingsStore is null || !_authService.IsAuthenticated)
        {
            return;
        }

        await _supabaseSyncLock.WaitAsync();
        try
        {
            await _supabaseSettingsStore.SaveSettingsAsync(BuildSyncedSettings());
        }
        catch
        {
            // Keep local storage as fallback if cloud sync fails.
        }
        finally
        {
            _supabaseSyncLock.Release();
        }
    }

    private SyncedSettingsData BuildSyncedSettings()
    {
        return new SyncedSettingsData
        {
            Theme = _theme,
            Language = _language,
            TutorialCompleted = _tutorialCompleted
        };
    }

    private void ApplySyncedSettings(SyncedSettingsData settings)
    {
        _theme = SanitizeTheme(settings.Theme);
        _language = string.IsNullOrWhiteSpace(settings.Language) ? DefaultLanguage : settings.Language;
        _tutorialCompleted = settings.TutorialCompleted;
        UpdateCulture(_language);
    }

    private async Task SaveLocalAsync()
    {
        var data = new SettingsData
        {
            Theme = _theme,
            Language = _language,
            TutorialCompleted = _tutorialCompleted,
            BrowserNotificationsEnabled = _browserNotificationsEnabled
        };

        var json = JsonSerializer.Serialize(data);
        await _storage.SetItemAsync(StorageKey, json);
    }
}

/// <summary>
/// Data structure for settings persistence.
/// </summary>
internal class SettingsData
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "en";
    public bool TutorialCompleted { get; set; }
    public bool BrowserNotificationsEnabled { get; set; }
}

public class SyncedSettingsData
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "en";
    public bool TutorialCompleted { get; set; }
}
