using System.Text.Json;
using System.Globalization;

namespace Budgetr.Shared.Services;

/// <summary>
/// Implementation of settings service with local storage persistence.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IStorageService _storage;
    private const string StorageKey = "budgetr_settings";
    private const string DefaultLanguage = "en";

    private bool _tutorialCompleted;
    private bool _browserNotificationsEnabled;
    private string _language = DefaultLanguage;

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

    public SettingsService(IStorageService storage)
    {
        _storage = storage;
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
                    _language = string.IsNullOrEmpty(data.Language) ? DefaultLanguage : data.Language;
                    _tutorialCompleted = data.TutorialCompleted;
                    _browserNotificationsEnabled = data.BrowserNotificationsEnabled;
                }
            }
            catch
            {
                // If deserialization fails, keep defaults
                _language = DefaultLanguage;
                _tutorialCompleted = false;
                _browserNotificationsEnabled = false;
            }
        }
        
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
    public string Language { get; set; } = "en";
    public bool TutorialCompleted { get; set; }
    public bool BrowserNotificationsEnabled { get; set; }
}
