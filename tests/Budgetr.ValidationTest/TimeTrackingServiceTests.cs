using Budgetr.Shared.Models;
using Budgetr.Shared.Services;
using System.Text.Json;

namespace Budgetr.ValidationTest;

public class TimeTrackingServiceTests
{
    [Fact]
    public async Task LoadAsync_NoStoredAccount_LoadsMetersFromConfiguration()
    {
        var defaultMeters = new[]
        {
            new Meter { Name = "Work", Factor = 1, DisplayOrder = 0 },
            new Meter { Name = "Break", Factor = -1, DisplayOrder = 1 }
        };

        var storage = new InMemoryStorageService();
        var config = new StubMeterConfigurationService(defaultMeters);
        var settings = new StubSettingsService();
        var sut = new TimeTrackingService(storage, config, settings, new StubNotificationService(), new StubStringLocalizer());

        await sut.LoadAsync();

        Assert.Equal(2, sut.Account.Meters.Count);
        Assert.Equal(1, config.LoadCalls);
        Assert.Equal(TimeSpan.FromHours(24), sut.TimelinePeriod);
    }

    [Fact]
    public async Task LoadAsync_StoredMetersExist_DoesNotLoadDefaults()
    {
        var storage = new InMemoryStorageService();
        var stored = new TimeAccount
        {
            Meters = new List<Meter>
            {
                new Meter { Name = "Stored", Factor = 2, DisplayOrder = 0 }
            },
            TimelinePeriod = TimeSpan.FromHours(12)
        };
        await storage.SetItemAsync("budgetr_account", JsonSerializer.Serialize(stored));

        var config = new StubMeterConfigurationService(new[]
        {
            new Meter { Name = "Default", Factor = 1, DisplayOrder = 0 }
        });
        var settings = new StubSettingsService();
        var sut = new TimeTrackingService(storage, config, settings, new StubNotificationService(), new StubStringLocalizer());

        await sut.LoadAsync();

        Assert.Single(sut.Account.Meters);
        Assert.Equal("Stored", sut.Account.Meters[0].Name);
        Assert.Equal(0, config.LoadCalls);
        Assert.Equal(TimeSpan.FromHours(12), sut.TimelinePeriod);
    }

    [Fact]
    public async Task AddMeter_Valid_AddsMeterWithNextDisplayOrder()
    {
        var sut = await CreateLoadedServiceAsync();
        var beforeCount = sut.Account.Meters.Count;
        var maxOrder = sut.Account.Meters.Max(m => m.DisplayOrder);

        sut.AddMeter("New Meter", 2.5);

        Assert.Equal(beforeCount + 1, sut.Account.Meters.Count);
        var meter = sut.Account.Meters.Single(m => m.Name == "New Meter");
        Assert.Equal(2.5, meter.Factor);
        Assert.Equal(maxOrder + 1, meter.DisplayOrder);
    }

    [Fact]
    public async Task AddMeter_EmptyName_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        Assert.Throws<ArgumentException>(() => sut.AddMeter("", 1));
    }

    [Fact]
    public async Task AddMeter_FactorOutsideRange_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.AddMeter("Invalid", 100));
    }

    [Fact]
    public async Task AddMeter_AtMaxMeters_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        while (sut.Account.Meters.Count < TimeTrackingService.MaxMeters)
        {
            sut.AddMeter($"Meter {sut.Account.Meters.Count}", 1);
        }

        Assert.Throws<InvalidOperationException>(() => sut.AddMeter("Overflow", 1));
    }

    [Fact]
    public async Task ActivateMeter_NoPreviousActive_CreatesActiveEvent()
    {
        var sut = await CreateLoadedServiceAsync();
        var meter = sut.Account.Meters.First();

        sut.ActivateMeter(meter.Id);

        var active = sut.GetActiveEvent();
        Assert.NotNull(active);
        Assert.Equal(meter.Name, active!.MeterName);
        Assert.Equal(meter.Factor, active.Factor);
    }

    [Fact]
    public async Task ActivateMeter_WithPreviousActive_DeactivatesPreviousEvent()
    {
        var sut = await CreateLoadedServiceAsync();
        var first = sut.Account.Meters[0];
        var second = sut.Account.Meters[1];

        sut.ActivateMeter(first.Id);
        var previous = sut.GetActiveEvent();
        Assert.NotNull(previous);

        sut.ActivateMeter(second.Id);

        Assert.NotNull(previous!.EndTime);
        var current = sut.GetActiveEvent();
        Assert.NotNull(current);
        Assert.Equal(second.Name, current!.MeterName);
    }

    [Fact]
    public async Task DeleteMeter_WhenActive_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        var meter = sut.Account.Meters.First();
        sut.ActivateMeter(meter.Id);

        Assert.Throws<InvalidOperationException>(() => sut.DeleteMeter(meter.Id));
    }

    [Fact]
    public async Task UpdateEventTimes_ActiveEvent_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        var meter = sut.Account.Meters.First();
        sut.ActivateMeter(meter.Id);
        var active = sut.GetActiveEvent();
        Assert.NotNull(active);

        var now = DateTimeOffset.UtcNow;
        Assert.Throws<InvalidOperationException>(() => sut.UpdateEventTimes(active!.Id, now.AddHours(-2), now.AddHours(-1)));
    }

    [Fact]
    public async Task UpdateEventTimes_EndBeforeStart_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        var meter = sut.Account.Meters.First();
        sut.ActivateMeter(meter.Id);
        sut.DeactivateMeter();
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

        var import = new BudgetrExportData
        {
            Language = "",
            TutorialCompleted = true,
            Meters = new List<Meter>
            {
                new Meter { Name = "Imported", Factor = 1.5, DisplayOrder = 50 }
            },
            Events = new List<MeterEvent>()
        };

        await sut.ImportDataAsync(JsonSerializer.Serialize(import));

        Assert.Equal("en", settings.Language);
        Assert.True(settings.TutorialCompleted);
        Assert.Single(sut.Account.Meters);
        Assert.Equal(0, sut.Account.Meters[0].DisplayOrder);
    }

    [Fact]
    public async Task ImportDataAsync_NoMeters_Throws()
    {
        var sut = await CreateLoadedServiceAsync();
        var import = new BudgetrExportData
        {
            Meters = new List<Meter>()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ImportDataAsync(JsonSerializer.Serialize(import)));
    }

    [Fact]
    public async Task ReorderMeters_ValidInput_ReordersAndRewritesDisplayOrder()
    {
        var sut = await CreateLoadedServiceAsync();
        sut.AddMeter("Third", 2);
        var orderedByCurrent = sut.Account.Meters.OrderBy(m => m.DisplayOrder).ToList();
        var reversedIds = orderedByCurrent.Select(m => m.Id).Reverse().ToList();

        sut.ReorderMeters(reversedIds);

        var reordered = sut.Account.Meters;
        Assert.Equal(reversedIds, reordered.Select(m => m.Id).ToList());
        Assert.Equal(new[] { 0, 1, 2 }, reordered.Select(m => m.DisplayOrder).ToArray());
    }

    private static async Task<TimeTrackingService> CreateLoadedServiceAsync(
        StubSettingsService? settingsService = null)
    {
        var storage = new InMemoryStorageService();
        var config = new StubMeterConfigurationService(new[]
        {
            new Meter { Name = "Work", Factor = 1, DisplayOrder = 0 },
            new Meter { Name = "Break", Factor = -1, DisplayOrder = 1 }
        });
        var settings = settingsService ?? new StubSettingsService();
        var notifications = new StubNotificationService();
        var localizer = new StubStringLocalizer();
        var service = new TimeTrackingService(storage, config, settings, notifications, localizer);
        await service.LoadAsync();
        return service;
    }
}
