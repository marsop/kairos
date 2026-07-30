using System.Text.Json;
using Kairos.Core.Models;
using Microsoft.Extensions.Logging;

namespace Kairos.Application.Services;

public class BudgetSettingsService : IBudgetSettingsService
{
    private readonly IStorageService _storage;
    private readonly ISupabaseBudgetSettingsStore? _supabaseStore;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<BudgetSettingsService> _logger;

    private const string StorageKey = "Kairos_budget_settings";
    private bool _isLoaded;

    public BudgetSettingsService(
        IStorageService storage,
        ISettingsService settingsService,
        ILogger<BudgetSettingsService> logger,
        ISupabaseBudgetSettingsStore? supabaseStore = null)
    {
        _storage = storage;
        _settingsService = settingsService;
        _logger = logger;
        _supabaseStore = supabaseStore;
    }

    private bool _minimumEnabled;
    public bool MinimumEnabled
    {
        get => _minimumEnabled;
        set
        {
            if (_minimumEnabled != value)
            {
                _minimumEnabled = value;
                NotifyStateChanged();
                if (_isLoaded) _ = SaveAsync();
            }
        }
    }

    private int _threshold = 95;
    public int Threshold
    {
        get => _threshold;
        set
        {
            var clamped = Math.Clamp(value, 75, 99);
            if (_threshold != clamped)
            {
                _threshold = clamped;
                NotifyStateChanged();
                if (_isLoaded) _ = SaveAsync();
            }
        }
    }

    private string _colorMinimumNotReached = "#0000ff";
    public string ColorMinimumNotReached
    {
        get => _colorMinimumNotReached;
        set
        {
            if (_colorMinimumNotReached != value)
            {
                _colorMinimumNotReached = value;
                NotifyStateChanged();
                if (_isLoaded) _ = SaveAsync();
            }
        }
    }

    private string _colorMinimumReachedMaxNotReached = "#00ff00";
    public string ColorMinimumReachedMaxNotReached
    {
        get => _colorMinimumReachedMaxNotReached;
        set
        {
            if (_colorMinimumReachedMaxNotReached != value)
            {
                _colorMinimumReachedMaxNotReached = value;
                NotifyStateChanged();
                if (_isLoaded) _ = SaveAsync();
            }
        }
    }

    private string _colorBetweenThresholdMax = "#ffff00";
    public string ColorBetweenThresholdMax
    {
        get => _colorBetweenThresholdMax;
        set
        {
            if (_colorBetweenThresholdMax != value)
            {
                _colorBetweenThresholdMax = value;
                NotifyStateChanged();
                if (_isLoaded) _ = SaveAsync();
            }
        }
    }

    private string _colorOverMax = "#ff0000";
    public string ColorOverMax
    {
        get => _colorOverMax;
        set
        {
            if (_colorOverMax != value)
            {
                _colorOverMax = value;
                NotifyStateChanged();
                if (_isLoaded) _ = SaveAsync();
            }
        }
    }

    private BudgetType _budgetType = BudgetType.Monthly;
    public BudgetType BudgetType
    {
        get => _budgetType;
        set
        {
            if (_budgetType != value)
            {
                _budgetType = value;
                NotifyStateChanged();
                if (_isLoaded) _ = SaveAsync();
            }
        }
    }

    private bool _notificationsEnabled = true;
    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set
        {
            if (_notificationsEnabled != value)
            {
                _notificationsEnabled = value;
                NotifyStateChanged();
                if (_isLoaded) _ = SaveAsync();
            }
        }
    }

    public event Action? OnSettingsChanged;
    private void NotifyStateChanged() => OnSettingsChanged?.Invoke();

    public async Task LoadAsync()
    {
        try
        {
            var json = await _storage.GetItemAsync(StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<BudgetSettingsData>(json);
                if (data != null)
                {
                    ApplyData(data);
                }
            }

            if (_supabaseStore != null)
            {
                try
                {
                    var remoteData = await _supabaseStore.LoadSettingsAsync();
                    if (remoteData != null)
                    {
                        ApplyData(remoteData);
                        await SaveLocalAsync();
                    }
                    else
                    {
                        // No remote data exists yet, seed Supabase with local/default values
                        await _supabaseStore.SaveSettingsAsync(BuildData());
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail if Supabase is unavailable
                    _logger.LogError(ex, "Failed to synchronize budget settings from Supabase.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load budget settings.");
        }
        finally
        {
            _isLoaded = true;
            NotifyStateChanged();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            await SaveLocalAsync();

            if (_supabaseStore != null)
            {
                try
                {
                    var data = BuildData();
                    await _supabaseStore.SaveSettingsAsync(data);
                    _logger.LogInformation("Budget settings synchronized to Supabase successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save budget settings to Supabase. Changes are saved locally but not synchronized to remote.");
                    // Don't rethrow - we've already saved locally
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during budget settings save operation.");
        }
    }

    public async Task InitializeDefaultsIfEmptyAsync()
    {
        if (_supabaseStore != null)
        {
            try
            {
                var remoteData = await _supabaseStore.LoadSettingsAsync();
                if (remoteData == null)
                {
                    await _supabaseStore.SaveSettingsAsync(BuildData());
                    _logger.LogInformation("Budget settings initialized in Supabase with defaults.");
                }
                else
                {
                    // Settings exist in Supabase, ensure they're applied locally
                    ApplyData(remoteData);
                    await SaveLocalAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize default budget settings in Supabase. Settings will use local values.");
            }
        }
    }

    private async Task SaveLocalAsync()
    {
        try
        {
            var data = BuildData();
            var json = JsonSerializer.Serialize(data);
            await _storage.SetItemAsync(StorageKey, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save budget settings locally.");
        }
    }

    private void ApplyData(BudgetSettingsData data)
    {
        _minimumEnabled = data.MinimumEnabled;
        _threshold = Math.Clamp(data.Threshold, 75, 99);
        _colorMinimumNotReached = data.ColorMinimumNotReached ?? "#0000ff";
        _colorMinimumReachedMaxNotReached = data.ColorMinimumReachedMaxNotReached ?? "#00ff00";
        _colorBetweenThresholdMax = data.ColorBetweenThresholdMax ?? "#ffff00";
        _colorOverMax = data.ColorOverMax ?? "#ff0000";
        _budgetType = data.BudgetType;
        _notificationsEnabled = data.NotificationsEnabled;
    }

    private BudgetSettingsData BuildData()
    {
        return new BudgetSettingsData
        {
            MinimumEnabled = _minimumEnabled,
            Threshold = _threshold,
            ColorMinimumNotReached = _colorMinimumNotReached,
            ColorMinimumReachedMaxNotReached = _colorMinimumReachedMaxNotReached,
            ColorBetweenThresholdMax = _colorBetweenThresholdMax,
            ColorOverMax = _colorOverMax,
            BudgetType = _budgetType,
            NotificationsEnabled = _notificationsEnabled
        };
    }
}
