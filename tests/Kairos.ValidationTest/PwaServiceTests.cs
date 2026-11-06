using Kairos.Infrastructure.Services;

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
    [Fact]
    public async Task InitializeAsync_LoadsVisibilityStatus_False()
    {
        var js = new TestJsRuntime();
        js.SetResult("pwaInterop.getOnlineStatus", true);
        js.SetResult("pwaInterop.getVisibilityStatus", false);
        var sut = new PwaService(js);
        bool? receivedVisibility = null;
        sut.OnVisibilityChanged += v => receivedVisibility = v;

        await sut.InitializeAsync();

        Assert.False(sut.IsVisible);
        Assert.False(receivedVisibility);
        Assert.Contains(js.Invocations, x => x.Identifier == "pwaInterop.getVisibilityStatus");
    }

    [Fact]
    public async Task InitializeAsync_WhenVisibilityCallFails_GracefullyDefaultsToTrue()
    {
        var js = new TestJsRuntime();
        js.SetResult("pwaInterop.getOnlineStatus", true);
        js.SetException("pwaInterop.getVisibilityStatus", new InvalidOperationException("Not supported"));
        var sut = new PwaService(js);

        await sut.InitializeAsync();

        Assert.True(sut.IsVisible);
    }

    [Fact]
    public void NotifyVisibilityChanged_WhenChanged_FiresVisibilityAndStateChangedEvents()
    {
        var sut = new PwaService(new TestJsRuntime());
        var stateNotifications = 0;
        bool? receivedVisibility = null;
        sut.OnStateChanged += () => stateNotifications++;
        sut.OnVisibilityChanged += v => receivedVisibility = v;

        // Transition from default true to false
        sut.NotifyVisibilityChanged(false);

        Assert.False(sut.IsVisible);
        Assert.False(receivedVisibility);
        Assert.Equal(1, stateNotifications);

        // Transition from false to true
        sut.NotifyVisibilityChanged(true);

        Assert.True(sut.IsVisible);
        Assert.True(receivedVisibility);
        Assert.Equal(2, stateNotifications);
    }

    [Fact]
    public void NotifyVisibilityChanged_WhenUnchanged_DoesNotFireEvents()
    {
        var sut = new PwaService(new TestJsRuntime());
        var stateNotifications = 0;
        var visibilityNotifications = 0;
        sut.OnStateChanged += () => stateNotifications++;
        sut.OnVisibilityChanged += _ => visibilityNotifications++;

        // Already true by default, calling with true again
        sut.NotifyVisibilityChanged(true);

        Assert.Equal(0, stateNotifications);
        Assert.Equal(0, visibilityNotifications);
    }
}
