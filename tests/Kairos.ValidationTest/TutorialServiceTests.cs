using Kairos.Shared.Services;

namespace Kairos.ValidationTest;

public class TutorialServiceTests
{
    [Fact]
    public async Task InitializeAsync_FirstLaunch_StartsTutorial()
    {
        var storage = new InMemoryStorageService();
        var settings = new StubSettingsService { TutorialCompleted = false };
        var navigation = new TestNavigationManager();
        var sut = new TutorialService(storage, settings, navigation, new StubStringLocalizer());

        await sut.InitializeAsync();

        Assert.True(sut.IsActive);
        Assert.NotNull(sut.CurrentStep);
        Assert.NotEmpty(navigation.Navigations);
    }

    [Fact]
    public async Task InitializeAsync_LegacyCompletion_MigratesToSettingsAndRemovesLegacyKey()
    {
        var storage = new InMemoryStorageService();
        await storage.SetItemAsync("tutorial_completed_v1", "true");
        var settings = new StubSettingsService { TutorialCompleted = false };
        var sut = new TutorialService(storage, settings, new TestNavigationManager(), new StubStringLocalizer());

        await sut.InitializeAsync();

        Assert.True(settings.TutorialCompleted);
        Assert.False(sut.IsActive);
        Assert.Contains("tutorial_completed_v1", storage.RemovedKeys);
    }

    [Fact]
    public async Task SetAvatarAsync_ValidAvatar_UpdatesAvatarAndStepAssets()
    {
        var sut = new TutorialService(
            new InMemoryStorageService(),
            new StubSettingsService(),
            new TestNavigationManager(),
            new StubStringLocalizer());

        sut.StartTutorial();
        await sut.SetAvatarAsync("zarzaparrilla");

        Assert.Equal("zarzaparrilla", sut.CurrentAvatar.Id);
        Assert.Contains("zarzaparrilla", sut.CurrentStep!.ImageUrl);
    }

    [Fact]
    public void NextStep_AfterLastStep_CompletesTutorial()
    {
        var settings = new StubSettingsService();
        var sut = new TutorialService(
            new InMemoryStorageService(),
            settings,
            new TestNavigationManager(),
            new StubStringLocalizer());
        sut.StartTutorial();

        while (sut.IsActive)
        {
            sut.NextStep();
        }

        Assert.True(settings.TutorialCompleted);
        Assert.False(sut.IsActive);
    }
}
