using Kairos.Shared.Models;
using Kairos.Shared.Services;
using System.Text.Json;

namespace Kairos.ValidationTest;

public class TimeTrackingServiceTests
{
    [Fact]
    public async Task LoadAsync_NoStoredAccount_LoadsActivitiesFromConfiguration()
    {
        var defaultActivities = new[]
        {
            new Activity { Name = "Work", Color = "#10B981", Factor = 1, DisplayOrder = 0 },
            new Activity { Name = "Break", Color = "#EF4444", Factor = -1, DisplayOrder = 1 }
        };

        var storage = new InMemoryStorageService();
        var config = new StubActivityConfigurationService(defaultActivities);
        var settings = new StubSettingsService();
        var sut = new TimeTrackingService(
            storage,
            config,
            settings,
            new StubNotificationService(),
            new StubStringLocalizer(),
            new StubSupabaseAuthService(),
            new StubSupabaseActivityStore());

        await sut.LoadAsync();

        Assert.Equal(2, sut.Account.Activities.Count);
        Assert.Equal(1, config.LoadCalls);
        Assert.Equal(TimeSpan.FromHours(24), sut.TimelinePeriod);
        Assert.All(sut.Account.Activities, activity => Assert.Equal(1.0, activity.Factor));
        Assert.Equal("#10B981", sut.Account.Activities[0].Color);
    }

    [Fact]
    public async Task LoadAsync_StoredActivitiesExist_DoesNotLoadDefaults()
    {
        var storage = new InMemoryStorageService();
        var stored = new TimeAccount
        {
            Activities = new List<Activity>
            {
                new Activity { Name = "Stored", Color = "#3B82F6", Factor = 2, DisplayOrder = 0 }
            },
            TimelinePeriod = TimeSpan.FromHours(12)
        };
        await storage.SetItemAsync("Kairos_account", JsonSerializer.Serialize(stored));

        var config = new StubActivityConfigurationService(new[]
        {
            new Activity { Name = "Default", Factor = 1, DisplayOrder = 0 }
        });
        var settings = new StubSettingsService();
        var sut = new TimeTrackingService(
            storage,
            config,
            settings,
            new StubNotificationService(),
            new StubStringLocalizer(),
            new StubSupabaseAuthService(),
            new StubSupabaseActivityStore());

        await sut.LoadAsync();

        Assert.Single(sut.Account.Activities);
        Assert.Equal("Stored", sut.Account.Activities[0].Name);
        Assert.Equal("#3B82F6", sut.Account.Activities[0].Color);
        Assert.Equal(1.0, sut.Account.Activities[0].Factor);
        Assert.Equal(0, config.LoadCalls);
        Assert.Equal(TimeSpan.FromHours(12), sut.TimelinePeriod);
    }

    [Fact]
    public async Task AddActivity_Valid_AddsActivityWithNextDisplayOrder()
    {
        var sut = await CreateLoadedServiceAsync();
        var beforeCount = sut.Account.Activities.Count;
        var maxOrder = sut.Account.Activities.Max(m => m.DisplayOrder);

        sut.AddActivity("New Activity", "#8B5CF6");

        Assert.Equal(beforeCount + 1, sut.Account.Activities.Count);
        var activity = sut.Account.Activities.Single(m => m.Name == "New Activity");
        Assert.Equal("#8B5CF6", activity.Color);
        Assert.Equal(1.0, activity.Factor);
        Assert.Equal(maxOrder + 1, activity.DisplayOrder);
    }

    [Fact]
    public async Task AddActivity_EmptyName_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        Assert.Throws<ArgumentException>(() => sut.AddActivity(""));
    }

    [Fact]
    public async Task AddActivity_AlwaysUsesFactorOne()
    {
        var sut = await CreateLoadedServiceAsync();
        sut.AddActivity("Invalid", "#F59E0B");
        Assert.Equal(1.0, sut.Account.Activities.Single(m => m.Name == "Invalid").Factor);
    }

    [Fact]
    public async Task AddActivity_InvalidColor_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        Assert.Throws<ArgumentException>(() => sut.AddActivity("Invalid Color", "green"));
    }

    [Fact]
    public async Task AddActivity_AtMaxActivities_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        while (sut.Account.Activities.Count < TimeTrackingService.MaxActivities)
        {
            sut.AddActivity($"Activity {sut.Account.Activities.Count}");
        }

        Assert.Throws<InvalidOperationException>(() => sut.AddActivity("Overflow"));
    }

    [Fact]
    public async Task ActivateActivity_NoPreviousActive_CreatesActiveEvent()
    {
        var sut = await CreateLoadedServiceAsync();
        var activity = sut.Account.Activities.First();

        sut.ActivateActivity(activity.Id, "Deep work");

        var active = sut.GetActiveEvent();
        Assert.NotNull(active);
        Assert.Equal(activity.Name, active!.ActivityName);
        Assert.Equal(activity.Color, active.ActivityColor);
        Assert.Equal(activity.Factor, active.Factor);
        Assert.Equal("Deep work", active.Comment);
    }

    [Fact]
    public async Task ActivateActivity_WithPreviousActive_DeactivatesPreviousEvent()
    {
        var sut = await CreateLoadedServiceAsync();
        var first = sut.Account.Activities[0];
        var second = sut.Account.Activities[1];

        sut.ActivateActivity(first.Id, "First");
        var previous = sut.GetActiveEvent();
        Assert.NotNull(previous);

        sut.ActivateActivity(second.Id, "Second");

        Assert.NotNull(previous!.EndTime);
        var current = sut.GetActiveEvent();
        Assert.NotNull(current);
        Assert.Equal(second.Name, current!.ActivityName);
    }

    [Fact]
    public async Task DeleteActivity_WhenActive_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        var activity = sut.Account.Activities.First();
        sut.ActivateActivity(activity.Id, "Active activity");

        Assert.Throws<InvalidOperationException>(() => sut.DeleteActivity(activity.Id));
    }

    [Fact]
    public async Task UpdateEventTimes_ActiveEvent_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        var activity = sut.Account.Activities.First();
        sut.ActivateActivity(activity.Id, "Active activity");
        var active = sut.GetActiveEvent();
        Assert.NotNull(active);

        var now = DateTimeOffset.UtcNow;
        Assert.Throws<InvalidOperationException>(() => sut.UpdateEventTimes(active!.Id, now.AddHours(-2), now.AddHours(-1)));
    }

    [Fact]
    public async Task UpdateEventTimes_EndBeforeStart_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        var activity = sut.Account.Activities.First();
        sut.ActivateActivity(activity.Id, "Active activity");
        sut.DeactivateActivity();
        var evt = sut.Account.Events.Single();

        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var end = DateTimeOffset.UtcNow.AddHours(-2);
        Assert.Throws<ArgumentException>(() => sut.UpdateEventTimes(evt.Id, start, end));
    }

    [Fact]
    public async Task ImportDataAsync_WithoutLanguage_DefaultsToEnglish()
    {
        var settings = new StubSettingsService { Language = "de" };
        var sut = await CreateLoadedServiceAsync(settingsService: settings);

        var import = new KairosExportData
        {
            Language = "",
            TutorialCompleted = true,
            Activities = new List<Activity>
            {
                new Activity { Name = "Imported", Factor = 1.5, DisplayOrder = 50 }
            },
            Events = new List<ActivityEvent>()
        };

        await sut.ImportDataAsync(JsonSerializer.Serialize(import));

        Assert.Equal("en", settings.Language);
        Assert.True(settings.TutorialCompleted);
        Assert.Single(sut.Account.Activities);
        Assert.Equal(0, sut.Account.Activities[0].DisplayOrder);
        Assert.Equal(Activity.DefaultColor, sut.Account.Activities[0].Color);
    }

    [Fact]
    public async Task ImportDataAsync_NoActivities_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        var import = new KairosExportData
        {
            Activities = new List<Activity>()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ImportDataAsync(JsonSerializer.Serialize(import)));
    }

    [Fact]
    public async Task ReorderActivities_ValidInput_ReordersAndRewritesDisplayOrder()
    {
        var sut = await CreateLoadedServiceAsync();
        sut.AddActivity("Third", "#14B8A6");
        var orderedByCurrent = sut.Account.Activities.OrderBy(m => m.DisplayOrder).ToList();
        var reversedIds = orderedByCurrent.Select(m => m.Id).Reverse().ToList();

        sut.ReorderActivities(reversedIds);

        var reordered = sut.Account.Activities;
        Assert.Equal(reversedIds, reordered.Select(m => m.Id).ToList());
        Assert.Equal(new[] { 0, 1, 2 }, reordered.Select(m => m.DisplayOrder).ToArray());
    }

    [Fact]
    public async Task ActivateActivity_EmptyComment_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        var activity = sut.Account.Activities.First();

        Assert.Throws<ArgumentException>(() => sut.ActivateActivity(activity.Id, ""));
    }

    [Fact]
    public async Task ActivateActivity_CommentOver250Chars_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        var activity = sut.Account.Activities.First();
        var comment = new string('a', 251);

        Assert.Throws<ArgumentException>(() => sut.ActivateActivity(activity.Id, comment));
    }

    [Fact]
    public async Task ExportDayAsCsv_SelectedDay_ExportsOnlyMatchingEventsInOrder()
    {
        var sut = await CreateLoadedServiceAsync();
        var offset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 3, 30));
        sut.Account.Events.Clear();
        sut.Account.Events.Add(new ActivityEvent
        {
            ActivityName = "Deep Work",
            Comment = "Finish roadmap",
            StartTime = new DateTimeOffset(2026, 3, 30, 9, 15, 0, offset),
            EndTime = new DateTimeOffset(2026, 3, 30, 10, 45, 0, offset)
        });
        sut.Account.Events.Add(new ActivityEvent
        {
            ActivityName = "Break",
            Comment = "Coffee, outside",
            StartTime = new DateTimeOffset(2026, 3, 30, 11, 0, 0, offset),
            EndTime = new DateTimeOffset(2026, 3, 30, 11, 15, 0, offset)
        });
        sut.Account.Events.Add(new ActivityEvent
        {
            ActivityName = "Other Day",
            Comment = "Ignore me",
            StartTime = new DateTimeOffset(2026, 3, 29, 16, 0, 0, offset),
            EndTime = new DateTimeOffset(2026, 3, 29, 17, 0, 0, offset)
        });

        var csv = sut.ExportDayAsCsv(new DateOnly(2026, 3, 30));
        var rows = csv.Trim().Split(Environment.NewLine);

        Assert.Equal(3, rows.Length);
        Assert.Equal("Activity,Comment,Start,End,DurationMinutes,DurationHours,Status", rows[0]);
        Assert.Contains("Deep Work,Finish roadmap,2026-03-30 09:15:00,2026-03-30 10:45:00,90,1.5,Completed", rows[1]);
        Assert.Contains("Break,\"Coffee, outside\",2026-03-30 11:00:00,2026-03-30 11:15:00,15,0.25,Completed", rows[2]);
    }

    [Fact]
    public async Task ExportDayAsCsv_EscapesQuotesAndLeavesBlankEndForActiveEvent()
    {
        var sut = await CreateLoadedServiceAsync();
        var offset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 3, 31));
        sut.Account.Events.Clear();
        sut.Account.Events.Add(new ActivityEvent
        {
            ActivityName = "Review",
            Comment = "Discuss \"Phase 2\"",
            StartTime = new DateTimeOffset(2026, 3, 31, 14, 0, 0, offset)
        });

        var csv = sut.ExportDayAsCsv(new DateOnly(2026, 3, 31));
        var row = csv.Trim().Split(Environment.NewLine).Last();

        Assert.Contains("\"Discuss \"\"Phase 2\"\"\"", row);
        Assert.Contains("2026-03-31 14:00:00,,", row);
        Assert.EndsWith("Active", row);
    }

    [Fact]
    public async Task ActivateActivity_WhenStateListenerThrows_StillStartsPersistence()
    {
        var storage = new InMemoryStorageService();
        var config = new StubActivityConfigurationService(new[]
        {
            new Activity { Name = "Work", Color = "#10B981", Factor = 1, DisplayOrder = 0 }
        });
        var sut = new TimeTrackingService(
            storage,
            config,
            new StubSettingsService(),
            new StubNotificationService(),
            new StubStringLocalizer(),
            new StubSupabaseAuthService(),
            new StubSupabaseActivityStore());

        await sut.LoadAsync();
        var previousSetCalls = storage.SetCalls;
        sut.OnStateChanged += () => throw new InvalidOperationException("UI update failed");

        Assert.Throws<InvalidOperationException>(() => sut.ActivateActivity(sut.Account.Activities[0].Id, "Deep work"));

        Assert.True(storage.SetCalls > previousSetCalls);
        var persisted = await storage.GetItemAsync("Kairos_account");
        Assert.Contains("Deep work", persisted);
    }

    [Fact]
    public async Task RealtimeReload_WithOlderRemoteSnapshot_DoesNotOverwriteRecentLocalActivation()
    {
        var storage = new InMemoryStorageService();
        var config = new StubActivityConfigurationService(new[]
        {
            new Activity { Name = "Work", Color = "#10B981", Factor = 1, DisplayOrder = 0 },
            new Activity { Name = "Break", Color = "#EF4444", Factor = 1, DisplayOrder = 1 }
        });
        var settings = new StubSettingsService();
        var notifications = new StubNotificationService();
        var auth = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentUserId = "user-1",
            CurrentAccessToken = "token"
        };
        var activityStore = new StubSupabaseActivityStore();
        var accountStore = new StubSupabaseTimeAccountStore();
        var realtime = new StubSupabaseRealtimeService();
        var sut = new TimeTrackingService(
            storage,
            config,
            settings,
            notifications,
            new StubStringLocalizer(),
            auth,
            activityStore,
            accountStore,
            realtime);

        await sut.LoadAsync();
        sut.ActivateActivity(sut.Account.Activities[0].Id, "Deep work");
        var localModifiedAt = sut.Account.LastModifiedAtUtc;

        accountStore.LoadedAccount = new TimeAccount
        {
            Events = new List<ActivityEvent>(),
            TimelinePeriod = sut.TimelinePeriod,
            LastModifiedAtUtc = localModifiedAt.AddSeconds(-10)
        };

        realtime.RaiseTableChanged("time_accounts");
        await Task.Delay(100);

        var active = sut.GetActiveEvent();
        Assert.NotNull(active);
        Assert.Equal("Deep work", active!.Comment);
        Assert.Single(sut.Account.Events);
    }

    private static async Task<TimeTrackingService> CreateLoadedServiceAsync(
        StubSettingsService? settingsService = null)
    {
        var storage = new InMemoryStorageService();
        var config = new StubActivityConfigurationService(new[]
        {
            new Activity { Name = "Work", Color = "#10B981", Factor = 1, DisplayOrder = 0 },
            new Activity { Name = "Break", Color = "#EF4444", Factor = -1, DisplayOrder = 1 }
        });
        var settings = settingsService ?? new StubSettingsService();
        var notifications = new StubNotificationService();
        var localizer = new StubStringLocalizer();
        var service = new TimeTrackingService(
            storage,
            config,
            settings,
            notifications,
            localizer,
            new StubSupabaseAuthService(),
            new StubSupabaseActivityStore());
        await service.LoadAsync();
        return service;
    }
}
