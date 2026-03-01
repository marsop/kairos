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
        var sut = new SettingsService(storage);
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        await sut.LoadAsync();

        Assert.Equal("en", sut.Language);
        Assert.False(sut.TutorialCompleted);
        Assert.False(sut.BrowserNotificationsEnabled);
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task SetLanguageAsync_NewLanguage_PersistsAndNotifies()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage);
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
    public void TutorialCompleted_WhenChanged_SavesAndNotifies()
    {
        var storage = new InMemoryStorageService();
        var sut = new SettingsService(storage);
        var events = 0;
        sut.OnSettingsChanged += () => events++;

        sut.TutorialCompleted = true;

        Assert.True(sut.TutorialCompleted);
        Assert.Equal(1, events);
        Assert.True(storage.SetCalls > 0);
    }
}
