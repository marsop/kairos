using Microsoft.Extensions.Logging.Abstractions;
using Kairos.Shared.Services;
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
}
