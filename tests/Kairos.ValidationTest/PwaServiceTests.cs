using Kairos.Web.Services;

namespace Kairos.ValidationTest;

public class PwaServiceTests
{
    [Fact]
    public async Task InitializeAsync_LoadsOnlineStatusAndNotifies()
    {
        var js = new TestJsRuntime();
        js.SetResult("pwaInterop.getOnlineStatus", false);
        var sut = new PwaService(js);
        var notifications = 0;
        sut.OnStateChanged += () => notifications++;

        await sut.InitializeAsync();

        Assert.False(sut.IsOnline);
        Assert.True(notifications > 0);
        Assert.Contains(js.Invocations, x => x.Identifier == "pwaInterop.init");
    }

    [Fact]
    public async Task InstallAppAsync_WhenInstallable_TriggersInstallAndResetsFlag()
    {
        var js = new TestJsRuntime();
        var sut = new PwaService(js);
        sut.OnInstallable(true);

        await sut.InstallAppAsync();

        Assert.False(sut.IsInstallable);
        Assert.Contains(js.Invocations, x => x.Identifier == "pwaInterop.triggerInstall");
    }

    [Fact]
    public void OnConnectionChanged_OnlyNotifiesWhenValueChanges()
    {
        var sut = new PwaService(new TestJsRuntime());
        var notifications = 0;
        sut.OnStateChanged += () => notifications++;

        sut.OnConnectionChanged(true);
        sut.OnConnectionChanged(false);

        Assert.Equal(1, notifications);
        Assert.False(sut.IsOnline);
    }
}
