using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Globalization;
using System.Threading;

namespace Kairos.Application.Services;

/// <summary>
/// Implementation of settings service with local storage persistence.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IStorageService _storage;
    private readonly ISupabaseAuthService? _authService;
    private readonly ISupabaseSettingsStore? _supabaseSettingsStore;
    private readonly ISupabaseActivityGroupsStore? _supabaseActivityGroupsStore;
    private readonly ISupabaseRealtimeService? _realtimeService;
    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _supabaseSyncLock = new(1, 1);
    private const string StorageKey = "Kairos_settings";
    private const string DefaultLanguage = "en";
    private const string DefaultTheme = "light";
    private const int DefaultActivityGroupCount = 2;
    private const string DefaultGroupColor = "#10B981";
    private const string DefaultGroupIcon = "🗂️";

    private bool _tutorialCompleted;
    private bool _browserNotificationsEnabled;
    private bool _soundsEnabled;
    private bool _advancedSettingsEnabled = true;
    private bool _timeularSettingsEnabled;
    private bool _activityGroupsEnabled;
    private bool _budgetsEnabled = true;
    private int _activeActivityGroup;
    private int _activityGroupCount = DefaultActivityGroupCount;
    private List<string> _activityGroupNames = [];
    private List<Guid> _activityGroupIds = [];
    private List<string> _activityGroupColors = [];
    private List<string> _activityGroupIcons = [];
    private int _autoDeleteEventDuration;
    private int _stickyEventsDuration;
    private string _language = DefaultLanguage;
    private string _theme = DefaultTheme;
    private DateTimeOffset? _lastSupabaseSync;

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

    public bool BudgetsEnabled
    {
        get => _budgetsEnabled;
        set
        {
            if (_budgetsEnabled != value)
            {
                _budgetsEnabled = value;
                _ = SaveAsync();
            }
        }
    }

    public int StickyEventsDuration
    {
        get => _stickyEventsDuration;
        set
        {
            if (_stickyEventsDuration != value)
            {
                _stickyEventsDuration = value;
                _ = SaveAsync();
                OnSettingsChanged?.Invoke();
            }
        }
    }

    public int AutoDeleteEventDuration
    {
        get => _autoDeleteEventDuration;
        set
        {
            if (_autoDeleteEventDuration != value)
            {
                _autoDeleteEventDuration = value;
                OnSettingsChanged?.Invoke();
                _ = SaveAsync();
            }
        }
    }

    public bool SoundsEnabled
    {
        get => _soundsEnabled;
        set
        {
            if (_soundsEnabled != value)
            {
                _soundsEnabled = value;
                _ = SaveAsync();
                OnSettingsChanged?.Invoke();
            }
        }
    }

    public bool AdvancedSettingsEnabled
    {
        get => _advancedSettingsEnabled;
        set
        {
            if (_advancedSettingsEnabled != value)
            {
                _advancedSettingsEnabled = value;
                OnSettingsChanged?.Invoke();
                _ = SaveAsync();
            }
        }
    }

    public int ActiveActivityGroup
    {
        get => _activeActivityGroup;
        set
        {
            var normalized = Math.Clamp(value, 0, _activityGroupCount - 1);
            if (_activeActivityGroup != normalized)
            {
                _activeActivityGroup = normalized;
                _ = SaveAsync();
                OnSettingsChanged?.Invoke();
            }
        }
    }

    public int ActivityGroupCount
    {
        get => _activityGroupCount;
        set
        {
            var normalized = Math.Max(value, 1);
            if (_activityGroupCount != normalized)
            {
                _activityGroupCount = normalized;
                if (_activeActivityGroup >= _activityGroupCount)
                {
                    _activeActivityGroup = _activityGroupCount - 1;
                }

                EnsureActivityGroupNamesCapacity(_activityGroupCount);
                EnsureActivityGroupIdsCapacity(_activityGroupCount);
                EnsureActivityGroupColorsCapacity(_activityGroupCount);
                EnsureActivityGroupIconsCapacity(_activityGroupCount);

                _ = SaveAsync();
                OnSettingsChanged?.Invoke();
            }
        }
    }

    public bool TimeularSettingsEnabled
    {
        get => _timeularSettingsEnabled;
        set
        {
            if (_timeularSettingsEnabled != value)
            {
                _timeularSettingsEnabled = value;
                _ = SaveAsync();
            }
        }
    }

    public bool ActivityGroupsEnabled
    {
        get => _activityGroupsEnabled;
        set
        {
            if (_activityGroupsEnabled != value)
            {
                _activityGroupsEnabled = value;
                if (!_activityGroupsEnabled && _activeActivityGroup != 0)
                {
                    ActiveActivityGroup = 0; // This will also save and notify
                }
                else
                {
                    _ = SaveAsync();
                    OnSettingsChanged?.Invoke();
                }
            }
        }
    }

    public string? GetActivityGroupName(int groupId)
    {
        if (groupId < 0 || groupId >= _activityGroupNames.Count)
        {
            return null;
        }

        var name = _activityGroupNames[groupId]?.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    public void SetActivityGroupName(int groupId, string? name)
    {
        if (groupId < 0 || groupId >= _activityGroupCount)
        {
            return;
        }

        EnsureActivityGroupNamesCapacity(_activityGroupCount);
        EnsureActivityGroupIdsCapacity(_activityGroupCount);
        EnsureActivityGroupColorsCapacity(_activityGroupCount);
        EnsureActivityGroupIconsCapacity(_activityGroupCount);

        var normalized = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        if (normalized.Length > 40)
        {
            normalized = normalized[..40];
        }

        if (string.Equals(_activityGroupNames[groupId], normalized, StringComparison.Ordinal))
        {
            return;
        }

        _activityGroupNames[groupId] = normalized;
        OnSettingsChanged?.Invoke();
        _ = SaveAsync();
    }

    public void RemoveActivityGroupNameAt(int groupId)
    {
        if (groupId < 0 || groupId >= _activityGroupNames.Count)
        {
            return;
        }

        _activityGroupNames.RemoveAt(groupId);
        if (groupId < _activityGroupIds.Count)
        {
            _activityGroupIds.RemoveAt(groupId);
        }
        if (groupId < _activityGroupColors.Count)
        {
            _activityGroupColors.RemoveAt(groupId);
        }
        if (groupId < _activityGroupIcons.Count)
        {
            _activityGroupIcons.RemoveAt(groupId);
        }
        EnsureActivityGroupNamesCapacity(_activityGroupCount);
        EnsureActivityGroupIdsCapacity(_activityGroupCount);
        EnsureActivityGroupColorsCapacity(_activityGroupCount);
        EnsureActivityGroupIconsCapacity(_activityGroupCount);
        OnSettingsChanged?.Invoke();
        _ = SaveAsync();
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
        if (groupId < 0 || groupId >= _activityGroupCount)
        {
            return;
        }

        EnsureActivityGroupColorsCapacity(_activityGroupCount);
        var normalized = NormalizeGroupColor(color);
        if (string.Equals(_activityGroupColors[groupId], normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _activityGroupColors[groupId] = normalized;
        OnSettingsChanged?.Invoke();
        _ = SaveAsync();
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
        if (groupId < 0 || groupId >= _activityGroupCount)
        {
            return;
        }

        EnsureActivityGroupIconsCapacity(_activityGroupCount);
        var normalized = NormalizeGroupIcon(icon);
        if (string.Equals(_activityGroupIcons[groupId], normalized, StringComparison.Ordinal))
        {
            return;
        }

        _activityGroupIcons[groupId] = normalized;
        OnSettingsChanged?.Invoke();
        _ = SaveAsync();
    }

    public DateTimeOffset? LastSupabaseSync => _lastSupabaseSync;

    public void UpdateLastSupabaseSync()
    {
        _lastSupabaseSync = DateTimeOffset.UtcNow;
        OnSettingsChanged?.Invoke();
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

    private string _chartType = "line";
    public string ChartType
    {
        get => _chartType;
        set
        {
            if (_chartType != value)
            {
                _chartType = value;
                OnSettingsChanged?.Invoke();
                _ = SaveAsync();
            }
        }
    }

    private string _historyView = "list";
    public string HistoryView
    {
        get => _historyView;
        set
        {
            if (_historyView != value)
            {
                _historyView = value;
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
        ILogger<SettingsService> logger,
        ISupabaseAuthService? authService = null,
        ISupabaseSettingsStore? supabaseSettingsStore = null,
        ISupabaseActivityGroupsStore? supabaseActivityGroupsStore = null,
        ISupabaseRealtimeService? realtimeService = null)
    {
        _storage = storage;
        _logger = logger;
        _authService = authService;
        _supabaseSettingsStore = supabaseSettingsStore;
        _supabaseActivityGroupsStore = supabaseActivityGroupsStore;
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
                    _soundsEnabled = data.SoundsEnabled;
                    _advancedSettingsEnabled = data.AdvancedSettingsEnabled;
                    _timeularSettingsEnabled = data.TimeularSettingsEnabled;
                    _activityGroupsEnabled = data.ActivityGroupsEnabled;
                    _budgetsEnabled = data.BudgetsEnabled;
                    _activeActivityGroup = data.ActiveActivityGroup;
                    _activityGroupCount = Math.Max(data.ActivityGroupCount <= 0 ? DefaultActivityGroupCount : data.ActivityGroupCount, 1);
                    _activityGroupNames = NormalizeActivityGroupNames(data.ActivityGroupNames, _activityGroupCount);
                    _activityGroupIds = NormalizeActivityGroupIds(data.ActivityGroupIds, _activityGroupCount);
                    _activityGroupColors = NormalizeActivityGroupColors(data.ActivityGroupColors, _activityGroupCount);
                    _activityGroupIcons = NormalizeActivityGroupIcons(data.ActivityGroupIcons, _activityGroupCount);
                    if (_activeActivityGroup >= _activityGroupCount)
                    {
                        _activeActivityGroup = _activityGroupCount - 1;
                    }
                    _autoDeleteEventDuration = data.AutoDeleteEventDuration;
                    _stickyEventsDuration = data.StickyEventsDuration;
                    _historyView = data.HistoryView ?? "list";
                    _chartType = data.ChartType ?? "line";
                }
            }
            catch
            {
                // If deserialization fails, keep defaults
                _theme = DefaultTheme;
                _language = DefaultLanguage;
                _tutorialCompleted = false;
                _browserNotificationsEnabled = false;
                _soundsEnabled = false;
                _activityGroupNames = [];
                _activityGroupIds = [];
                _activityGroupColors = [];
                _activityGroupIcons = [];
            }
        }

        EnsureActivityGroupNamesCapacity(_activityGroupCount);
        EnsureActivityGroupIdsCapacity(_activityGroupCount);
        EnsureActivityGroupColorsCapacity(_activityGroupCount);
        EnsureActivityGroupIconsCapacity(_activityGroupCount);

        await PullSettingsFromSupabaseOrSeedAsync(seedWhenMissing: true);
        UpdateCulture(_language);
        OnSettingsChanged?.Invoke();
    }

    private void UpdateCulture(string languageCode)
    {
        try
        {
            _logger.LogInformation("Updating culture to '{LanguageCode}'", languageCode);
            var culture = new CultureInfo(languageCode);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            _logger.LogInformation("Culture set to: {Culture}, UI: {UICulture}", CultureInfo.CurrentCulture.Name, CultureInfo.CurrentUICulture.Name);
        }
        catch (CultureNotFoundException ex)
        {
            _logger.LogWarning(ex, "Culture '{LanguageCode}' not found. Falling back to default.", languageCode);
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
            BrowserNotificationsEnabled = _browserNotificationsEnabled,
            SoundsEnabled = _soundsEnabled,
            AdvancedSettingsEnabled = _advancedSettingsEnabled,
            TimeularSettingsEnabled = _timeularSettingsEnabled,
            ActivityGroupsEnabled = _activityGroupsEnabled,
            BudgetsEnabled = _budgetsEnabled,
            ActiveActivityGroup = _activeActivityGroup,
            ActivityGroupCount = _activityGroupCount,
            ActivityGroupNames = _activityGroupNames,
            ActivityGroupIds = _activityGroupIds.Select(id => id.ToString()).ToList(),
            ActivityGroupColors = _activityGroupColors,
            ActivityGroupIcons = _activityGroupIcons,
            AutoDeleteEventDuration = _autoDeleteEventDuration,
            StickyEventsDuration = StickyEventsDuration,
            HistoryView = _historyView,
            ChartType = _chartType
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
        if (string.Equals(table, "user_settings", StringComparison.Ordinal)
            || string.Equals(table, "user_activity_groups", StringComparison.Ordinal))
        {
            _ = PullSettingsFromSupabaseOrSeedAsync(seedWhenMissing: false);
        }
    }

    private async Task PullSettingsFromSupabaseOrSeedAsync(bool seedWhenMissing)
    {
        if (_authService is null || _supabaseSettingsStore is null || !await _authService.EnsureAuthenticatedAsync())
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

                if (_supabaseActivityGroupsStore is not null)
                {
                    var remoteGroups = await _supabaseActivityGroupsStore.LoadGroupsAsync();
                    if (remoteGroups is not null && remoteGroups.Count > 0)
                    {
                        ApplySyncedActivityGroups(remoteGroups);
                    }
                    else if (seedWhenMissing)
                    {
                        await _supabaseActivityGroupsStore.SaveGroupsAsync(BuildSyncedActivityGroups());
                    }
                }

                await SaveLocalAsync();
                UpdateLastSupabaseSync();
                OnSettingsChanged?.Invoke();
                return;
            }

            if (seedWhenMissing)
            {
                await _supabaseSettingsStore.SaveSettingsAsync(BuildSyncedSettings());
                if (_supabaseActivityGroupsStore is not null)
                {
                    await _supabaseActivityGroupsStore.SaveGroupsAsync(BuildSyncedActivityGroups());
                }
                UpdateLastSupabaseSync();
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
        if (_authService is null || _supabaseSettingsStore is null || !await _authService.EnsureAuthenticatedAsync())
        {
            return;
        }

        await _supabaseSyncLock.WaitAsync();
        try
        {
            await _supabaseSettingsStore.SaveSettingsAsync(BuildSyncedSettings());
            if (_supabaseActivityGroupsStore is not null)
            {
                await _supabaseActivityGroupsStore.SaveGroupsAsync(BuildSyncedActivityGroups());
            }
            UpdateLastSupabaseSync();
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
            TutorialCompleted = _tutorialCompleted,
            SoundsEnabled = _soundsEnabled,
            AdvancedSettingsEnabled = _advancedSettingsEnabled,
            TimeularSettingsEnabled = _timeularSettingsEnabled,
            ActivityGroupsEnabled = _activityGroupsEnabled,
            BudgetsEnabled = _budgetsEnabled,
            ActiveActivityGroup = _activeActivityGroup,
            AutoDeleteEventDuration = _autoDeleteEventDuration,
            StickyEventsDuration = _stickyEventsDuration
        };
    }

    private void ApplySyncedSettings(SyncedSettingsData settings)
    {
        _theme = SanitizeTheme(settings.Theme);
        _language = string.IsNullOrWhiteSpace(settings.Language) ? DefaultLanguage : settings.Language;
        _tutorialCompleted = settings.TutorialCompleted;
        _soundsEnabled = settings.SoundsEnabled;
        _advancedSettingsEnabled = settings.AdvancedSettingsEnabled;
        _timeularSettingsEnabled = settings.TimeularSettingsEnabled;
        _activityGroupsEnabled = settings.ActivityGroupsEnabled;
        _budgetsEnabled = settings.BudgetsEnabled;
        if (settings.ActiveActivityGroup >= _activityGroupCount)
        {
            _activityGroupCount = Math.Max(settings.ActiveActivityGroup + 1, 1);
        }
        EnsureActivityGroupNamesCapacity(_activityGroupCount);
        EnsureActivityGroupIdsCapacity(_activityGroupCount);
        _activeActivityGroup = Math.Clamp(settings.ActiveActivityGroup, 0, _activityGroupCount - 1);
        _autoDeleteEventDuration = settings.AutoDeleteEventDuration;
        _stickyEventsDuration = settings.StickyEventsDuration;
        UpdateCulture(_language);
    }

    private async Task SaveLocalAsync()
    {
        var data = new SettingsData
        {
            Theme = _theme,
            Language = _language,
            TutorialCompleted = _tutorialCompleted,
            BrowserNotificationsEnabled = _browserNotificationsEnabled,
            SoundsEnabled = _soundsEnabled,
            AdvancedSettingsEnabled = _advancedSettingsEnabled,
            TimeularSettingsEnabled = _timeularSettingsEnabled,
            ActivityGroupsEnabled = _activityGroupsEnabled,
            BudgetsEnabled = _budgetsEnabled,
            ActiveActivityGroup = _activeActivityGroup,
            ActivityGroupCount = _activityGroupCount,
            ActivityGroupNames = _activityGroupNames,
            ActivityGroupIds = _activityGroupIds.Select(id => id.ToString()).ToList(),
            ActivityGroupColors = _activityGroupColors,
            ActivityGroupIcons = _activityGroupIcons,
            AutoDeleteEventDuration = _autoDeleteEventDuration,
            StickyEventsDuration = StickyEventsDuration,
            HistoryView = _historyView,
            ChartType = _chartType
        };

        var json = JsonSerializer.Serialize(data);
        await _storage.SetItemAsync(StorageKey, json);
    }

    private void EnsureActivityGroupNamesCapacity(int groupCount)
    {
        if (_activityGroupNames.Count > groupCount)
        {
            _activityGroupNames.RemoveRange(groupCount, _activityGroupNames.Count - groupCount);
            return;
        }

        while (_activityGroupNames.Count < groupCount)
        {
            _activityGroupNames.Add(string.Empty);
        }
    }

    private void EnsureActivityGroupIdsCapacity(int groupCount)
    {
        if (_activityGroupIds.Count > groupCount)
        {
            _activityGroupIds.RemoveRange(groupCount, _activityGroupIds.Count - groupCount);
            return;
        }

        while (_activityGroupIds.Count < groupCount)
        {
            _activityGroupIds.Add(Guid.NewGuid());
        }
    }

    private void EnsureActivityGroupColorsCapacity(int groupCount)
    {
        if (_activityGroupColors.Count > groupCount)
        {
            _activityGroupColors.RemoveRange(groupCount, _activityGroupColors.Count - groupCount);
            return;
        }

        while (_activityGroupColors.Count < groupCount)
        {
            _activityGroupColors.Add(DefaultGroupColor);
        }
    }

    private void EnsureActivityGroupIconsCapacity(int groupCount)
    {
        if (_activityGroupIcons.Count > groupCount)
        {
            _activityGroupIcons.RemoveRange(groupCount, _activityGroupIcons.Count - groupCount);
            return;
        }

        while (_activityGroupIcons.Count < groupCount)
        {
            _activityGroupIcons.Add(DefaultGroupIcon);
        }
    }

    private static List<string> NormalizeActivityGroupNames(List<string>? names, int groupCount)
    {
        var normalized = names?
            .Select(name => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim())
            .Take(groupCount)
            .ToList() ?? [];

        while (normalized.Count < groupCount)
        {
            normalized.Add(string.Empty);
        }

        return normalized;
    }

    private static List<Guid> NormalizeActivityGroupIds(List<string>? ids, int groupCount)
    {
        var normalized = ids?
            .Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.NewGuid())
            .Take(groupCount)
            .ToList() ?? [];

        while (normalized.Count < groupCount)
        {
            normalized.Add(Guid.NewGuid());
        }

        return normalized;
    }

    private static List<string> NormalizeActivityGroupColors(List<string>? colors, int groupCount)
    {
        var normalized = colors?
            .Select(NormalizeGroupColor)
            .Take(groupCount)
            .ToList() ?? [];

        while (normalized.Count < groupCount)
        {
            normalized.Add(DefaultGroupColor);
        }

        return normalized;
    }

    private static List<string> NormalizeActivityGroupIcons(List<string>? icons, int groupCount)
    {
        var normalized = icons?
            .Select(NormalizeGroupIcon)
            .Take(groupCount)
            .ToList() ?? [];

        while (normalized.Count < groupCount)
        {
            normalized.Add(DefaultGroupIcon);
        }

        return normalized;
    }

    private static string NormalizeGroupColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return DefaultGroupColor;
        }

        var trimmed = color.Trim();
        if (trimmed.Length != 7 || trimmed[0] != '#')
        {
            return DefaultGroupColor;
        }

        for (var i = 1; i < trimmed.Length; i++)
        {
            if (!Uri.IsHexDigit(trimmed[i]))
            {
                return DefaultGroupColor;
            }
        }

        return trimmed.ToUpperInvariant();
    }

    private static string NormalizeGroupIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return DefaultGroupIcon;
        }

        var trimmed = icon.Trim();
        return trimmed.Length > 8 ? trimmed[..8] : trimmed;
    }

    private List<SyncedActivityGroupData> BuildSyncedActivityGroups()
    {
        EnsureActivityGroupNamesCapacity(_activityGroupCount);
        EnsureActivityGroupIdsCapacity(_activityGroupCount);
        EnsureActivityGroupColorsCapacity(_activityGroupCount);
        EnsureActivityGroupIconsCapacity(_activityGroupCount);

        var result = new List<SyncedActivityGroupData>(_activityGroupCount);
        for (var i = 0; i < _activityGroupCount; i++)
        {
            result.Add(new SyncedActivityGroupData
            {
                GroupId = _activityGroupIds[i],
                GroupOrder = i,
                Name = _activityGroupNames[i],
                Color = _activityGroupColors[i],
                Icon = _activityGroupIcons[i]
            });
        }

        return result;
    }

    private void ApplySyncedActivityGroups(IReadOnlyList<SyncedActivityGroupData> groups)
    {
        var ordered = groups
            .OrderBy(g => g.GroupOrder)
            .ToList();

        if (ordered.Count > 0)
        {
            _activityGroupCount = ordered.Count;
            if (_activeActivityGroup >= _activityGroupCount)
            {
                _activeActivityGroup = _activityGroupCount - 1;
            }
        }

        var names = ordered
            .Select(g => string.IsNullOrWhiteSpace(g.Name) ? string.Empty : g.Name.Trim())
            .ToList();

        var ids = ordered
            .Select(g => g.GroupId == Guid.Empty ? Guid.NewGuid() : g.GroupId)
            .ToList();

        var colors = ordered
            .Select(g => NormalizeGroupColor(g.Color))
            .ToList();

        var icons = ordered
            .Select(g => NormalizeGroupIcon(g.Icon))
            .ToList();

        _activityGroupNames = NormalizeActivityGroupNames(names, _activityGroupCount);
        _activityGroupIds = ids.Take(_activityGroupCount).ToList();
        _activityGroupColors = NormalizeActivityGroupColors(colors, _activityGroupCount);
        _activityGroupIcons = NormalizeActivityGroupIcons(icons, _activityGroupCount);
        EnsureActivityGroupIdsCapacity(_activityGroupCount);
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
    public bool SoundsEnabled { get; set; }
    public bool AdvancedSettingsEnabled { get; set; } = true;
    public bool TimeularSettingsEnabled { get; set; } = false;
    public bool ActivityGroupsEnabled { get; set; }
    public bool BudgetsEnabled { get; set; } = true;
    public int ActiveActivityGroup { get; set; }
    public int ActivityGroupCount { get; set; } = 2;
    public List<string> ActivityGroupNames { get; set; } = [];
    public List<string> ActivityGroupIds { get; set; } = [];
    public List<string> ActivityGroupColors { get; set; } = [];
    public List<string> ActivityGroupIcons { get; set; } = [];
    public int AutoDeleteEventDuration { get; set; }
    public int StickyEventsDuration { get; set; } = 0;
    public string HistoryView { get; set; } = "list";
    public string ChartType { get; set; } = "line";
}

public class SyncedSettingsData
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "en";
    public bool TutorialCompleted { get; set; }
    public bool SoundsEnabled { get; set; }
    public bool AdvancedSettingsEnabled { get; set; } = true;
    public bool TimeularSettingsEnabled { get; set; } = false;
    public bool ActivityGroupsEnabled { get; set; }
    public bool BudgetsEnabled { get; set; } = true;
    public int ActiveActivityGroup { get; set; }
    public int AutoDeleteEventDuration { get; set; }
    public int StickyEventsDuration { get; set; } = 0;
}
