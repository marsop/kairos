using Microsoft.Extensions.Logging.Abstractions;
using Kairos.Application.Services;
using System.Text.Json;

namespace Kairos.ValidationTest;

public class SettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_InvalidJson_KeepsDefaultsAndNotifies()
    {
        var storage = new InMemoryStorageService();
        await storage.SetItemAsync("Kairos_settings", "{ invalid");
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        await sut.LoadAsync();

        Assert.Equal("light", sut.Theme);
        Assert.Equal("en", sut.Language);
        Assert.False(sut.TutorialCompleted);
        Assert.False(sut.BrowserNotificationsEnabled);
        Assert.False(sut.ActivityGroupsEnabled);
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task SetLanguageAsync_NewLanguage_PersistsAndNotifies()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        await sut.SetLanguageAsync("de");

        Assert.Equal("de", sut.Language);
        Assert.Equal(1, events);
        var savedJson = await storage.GetItemAsync("Kairos_settings");
        Assert.NotNull(savedJson);
        using var doc = JsonDocument.Parse(savedJson!);
        Assert.Equal("de", doc.RootElement.GetProperty("Language").GetString());
    }

    [Fact]
    public async Task Theme_WhenChanged_PersistsAndNotifies()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        sut.Theme = "dark";

        Assert.Equal("dark", sut.Theme);
        Assert.Equal(1, events);
        var savedJson = await storage.GetItemAsync("Kairos_settings");
        Assert.NotNull(savedJson);
        using var doc = JsonDocument.Parse(savedJson!);
        Assert.Equal("dark", doc.RootElement.GetProperty("Theme").GetString());
    }

    [Fact]
    public void TutorialCompleted_WhenChanged_SavesAndNotifies()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        sut.TutorialCompleted = true;

        Assert.True(sut.TutorialCompleted);
        Assert.Equal(1, events);
        Assert.True(storage.SetCalls > 0);
    }

    [Fact]
    public async Task ActivityGroupsEnabled_WhenChanged_PersistsAndNotifies()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        // Setting a new value triggers save synchronously (even though the task is fire-and-forget)
        sut.ActivityGroupsEnabled = true;
        // Wait a tiny bit to allow the fire-and-forget SaveAsync to complete execution in tests
        await Task.Delay(50);

        Assert.True(sut.ActivityGroupsEnabled);
        Assert.Equal(1, events);
        var savedJson = await storage.GetItemAsync("Kairos_settings");
        Assert.NotNull(savedJson);
        using var doc = JsonDocument.Parse(savedJson!);
        Assert.True(doc.RootElement.GetProperty("ActivityGroupsEnabled").GetBoolean());
    }

    [Fact]
    public async Task SetActivityGroupName_ValidName_UpdatesAndNotifies()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        sut.SetActivityGroupName(0, "Work");

        Assert.Equal("Work", sut.GetActivityGroupName(0));
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task SetActivityGroupName_NameTooLong_TruncatesTo40Chars()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();

        var longName = new string('A', 50);
        sut.SetActivityGroupName(0, longName);

        Assert.Equal(new string('A', 40), sut.GetActivityGroupName(0));
    }

    [Fact]
    public async Task SetActivityGroupName_InvalidGroupId_DoesNothing()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        sut.SetActivityGroupName(-1, "Work");
        sut.SetActivityGroupName(999, "Work");

        Assert.Null(sut.GetActivityGroupName(-1));
        Assert.Equal(0, events);
    }

    [Fact]
    public async Task RemoveActivityGroupNameAt_ValidId_RemovesAndShifts()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();
        sut.ActivityGroupCount = 3;
        sut.SetActivityGroupName(0, "G0");
        sut.SetActivityGroupName(1, "G1");
        sut.SetActivityGroupName(2, "G2");

        var events = 0;
        sut.OnSettingsChanged += () => events++;

        sut.RemoveActivityGroupNameAt(1);

        // the list shifts left, but then EnsureCapacity replenishes the end with string.Empty
        Assert.Equal("G0", sut.GetActivityGroupName(0));
        Assert.Equal("G2", sut.GetActivityGroupName(1));
        Assert.Null(sut.GetActivityGroupName(2)); // It becomes string.Empty which GetActivityGroupName returns as null
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task SetActivityGroupColor_ValidColor_UpdatesAndNotifies()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        sut.SetActivityGroupColor(0, "#FF0000");

        Assert.Equal("#FF0000", sut.GetActivityGroupColor(0));
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task SetActivityGroupColor_InvalidColor_UsesDefault()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();

        sut.SetActivityGroupColor(0, "invalid");

        // The default color in SettingsService is "#10B981"
        Assert.Equal("#10B981", sut.GetActivityGroupColor(0));
    }

    [Fact]
    public async Task SetActivityGroupIcon_ValidIcon_UpdatesAndNotifies()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        sut.SetActivityGroupIcon(0, "⭐");

        Assert.Equal("⭐", sut.GetActivityGroupIcon(0));
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task SetActivityGroupIcon_IconTooLong_TruncatesTo8Chars()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();

        sut.SetActivityGroupIcon(0, "123456789");

        Assert.Equal("12345678", sut.GetActivityGroupIcon(0));
    }

    [Fact]
    public async Task Properties_WhenChanged_PersistAndNotify()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();

        var changeEvents = 0;
        sut.OnSettingsChanged += () => changeEvents++;

        sut.BudgetsEnabled = false;
        sut.StickyEventsDuration = 25;
        sut.AutoDeleteEventDuration = 45;
        sut.SoundsEnabled = true;
        sut.AdvancedSettingsEnabled = false;
        sut.TimeularSettingsEnabled = true;
        sut.BrowserNotificationsEnabled = true;
        sut.ChartType = "bar";
        sut.HistoryView = "stats";

        Assert.False(sut.BudgetsEnabled);
        Assert.Equal(25, sut.StickyEventsDuration);
        Assert.Equal(45, sut.AutoDeleteEventDuration);
        Assert.True(sut.SoundsEnabled);
        Assert.False(sut.AdvancedSettingsEnabled);
        Assert.True(sut.TimeularSettingsEnabled);
        Assert.True(sut.BrowserNotificationsEnabled);
        Assert.Equal("bar", sut.ChartType);
        Assert.Equal("stats", sut.HistoryView);
        Assert.True(changeEvents > 0);
    }

    [Fact]
    public async Task ActiveActivityGroup_And_ActivityGroupCount_ClampingBehavior()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();

        sut.ActivityGroupCount = 3;
        Assert.Equal(3, sut.ActivityGroupCount);

        sut.ActiveActivityGroup = 2;
        Assert.Equal(2, sut.ActiveActivityGroup);

        // Clamping to max index when count shrinks
        sut.ActivityGroupCount = 2;
        Assert.Equal(2, sut.ActivityGroupCount);
        Assert.Equal(1, sut.ActiveActivityGroup);

        // Setting active group beyond bounds clamps
        sut.ActiveActivityGroup = 10;
        Assert.Equal(1, sut.ActiveActivityGroup);

        // Disabling activity groups resets active to 0
        sut.ActivityGroupsEnabled = true;
        sut.ActiveActivityGroup = 1;
        sut.ActivityGroupsEnabled = false;
        Assert.Equal(0, sut.ActiveActivityGroup);
    }

    [Fact]
    public async Task LoadAsync_WithCustomGroupIdsAndNames_RestoresCorrectly()
    {
        var storage = new InMemoryStorageService();
        var customData = new
        {
            Theme = "dark",
            Language = "de",
            ActivityGroupCount = 3,
            ActivityGroupNames = new[] { "One", "Two", "Three" },
            ActivityGroupColors = new[] { "#111111", "#222222", "#333333" },
            ActivityGroupIcons = new[] { "1️⃣", "2️⃣", "3️⃣" }
        };
        await storage.SetItemAsync("Kairos_settings", System.Text.Json.JsonSerializer.Serialize(customData));

        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);
        await sut.LoadAsync();

        Assert.Equal("dark", sut.Theme);
        Assert.Equal("de", sut.Language);
        Assert.Equal(3, sut.ActivityGroupCount);
        Assert.Equal("One", sut.GetActivityGroupName(0));
        Assert.Equal("Two", sut.GetActivityGroupName(1));
        Assert.Equal("Three", sut.GetActivityGroupName(2));
        Assert.Equal("#111111", sut.GetActivityGroupColor(0));
        Assert.Equal("1️⃣", sut.GetActivityGroupIcon(0));
    }

    [Fact]
    public async Task SetLanguageAsync_InvalidCulture_FallsBackToDefault()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance);

        await sut.SetLanguageAsync("invalid-not-a-real-culture-xyz");

        Assert.Equal("invalid-not-a-real-culture-xyz", sut.Language);
    }

    [Fact]
    public async Task SupabaseSync_WhenAuthenticatedAndRemoteSettingsExist_AppliesRemote()
    {
        var storage = new InMemoryStorageService();
        var authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "token",
            CurrentUserId = "user-sync"
        };
        var settingsStore = new StubSupabaseSettingsStore
        {
            LoadedSettings = new SyncedSettingsData
            {
                Theme = "dark",
                Language = "gl",
                TutorialCompleted = true,
                SoundsEnabled = true,
                AdvancedSettingsEnabled = true,
                TimeularSettingsEnabled = true,
                ActivityGroupsEnabled = true,
                BudgetsEnabled = false,
                ActiveActivityGroup = 1,
                AutoDeleteEventDuration = 50,
                StickyEventsDuration = 30
            }
        };
        var groupStore = new StubSupabaseActivityGroupsStore
        {
            LoadedGroups = new List<SyncedActivityGroupData>
            {
                new() { GroupId = Guid.NewGuid(), GroupOrder = 0, Name = "Remote Work", Color = "#EF4444", Icon = "💼" },
                new() { GroupId = Guid.NewGuid(), GroupOrder = 1, Name = "Remote Life", Color = "#10B981", Icon = "🏡" }
            }
        };
        var realtime = new StubSupabaseRealtimeService();

        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance, authService, settingsStore, groupStore, realtime);

        await sut.LoadAsync();

        Assert.Equal("dark", sut.Theme);
        Assert.Equal("gl", sut.Language);
        Assert.True(sut.TutorialCompleted);
        Assert.True(sut.SoundsEnabled);
        Assert.False(sut.BudgetsEnabled);
        Assert.Equal(1, sut.ActiveActivityGroup);
        Assert.Equal("Remote Work", sut.GetActivityGroupName(0));
        Assert.Equal("Remote Life", sut.GetActivityGroupName(1));
        Assert.NotNull(sut.LastSupabaseSync);
    }

    [Fact]
    public async Task SupabaseSync_WhenRemoteSettingsNull_SeedsDefaultsToSupabase()
    {
        var storage = new InMemoryStorageService();
        var authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "token",
            CurrentUserId = "user-seed"
        };
        var settingsStore = new StubSupabaseSettingsStore
        {
            LoadedSettings = null
        };
        var groupStore = new StubSupabaseActivityGroupsStore
        {
            LoadedGroups = null
        };

        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance, authService, settingsStore, groupStore);

        await sut.LoadAsync();

        Assert.Equal(1, settingsStore.SaveCalls);
        Assert.NotNull(settingsStore.SavedSettings);
        Assert.Equal(1, groupStore.SaveCalls);
        Assert.NotNull(groupStore.SavedGroups);
        Assert.NotNull(sut.LastSupabaseSync);
    }

    [Fact]
    public async Task SupabaseSync_RemoteTableChanged_TriggersPull()
    {
        var storage = new InMemoryStorageService();
        var authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "token",
            CurrentUserId = "user-rt"
        };
        var settingsStore = new StubSupabaseSettingsStore
        {
            LoadedSettings = new SyncedSettingsData { Theme = "light" }
        };
        var realtime = new StubSupabaseRealtimeService();

        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance, authService, settingsStore, null, realtime);
        await sut.LoadAsync();

        var initialLoadCalls = settingsStore.LoadCalls;

        // Change remote settings and trigger realtime table change
        settingsStore.LoadedSettings = new SyncedSettingsData { Theme = "dark" };
        realtime.RaiseTableChanged("user_settings");

        await Task.Delay(50);

        Assert.True(settingsStore.LoadCalls > initialLoadCalls);
        Assert.Equal("dark", sut.Theme);
    }

    [Fact]
    public async Task SaveAsync_WhenAuthenticated_PersistsToSupabase()
    {
        var storage = new InMemoryStorageService();
        var authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "token",
            CurrentUserId = "user-save"
        };
        var settingsStore = new StubSupabaseSettingsStore();
        var groupStore = new StubSupabaseActivityGroupsStore();

        var sut = new SettingsService(storage, NullLogger<SettingsService>.Instance, authService, settingsStore, groupStore);
        await sut.LoadAsync();

        sut.Theme = "dark";
        await Task.Delay(50);

        Assert.True(settingsStore.SaveCalls > 0);
        Assert.True(groupStore.SaveCalls > 0);
    }
}
