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
}
