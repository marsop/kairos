using Kairos.Application.Services;

namespace Kairos.ValidationTest;

public class SyncConflictNotifierTests
{
    [Fact]
    public void HasListeners_ReturnsFalseWhenNoSubscribers()
    {
        var sut = new SyncConflictNotifier();
        Assert.False(sut.HasListeners);
    }

    [Fact]
    public void HasListeners_ReturnsTrueWhenSubscribed()
    {
        var sut = new SyncConflictNotifier();
        sut.OnConflictDetected += _ => { };
        Assert.True(sut.HasListeners);
    }

    [Fact]
    public async Task ResolveConflictAsync_WhenNoListeners_DefaultsToTrue()
    {
        var sut = new SyncConflictNotifier();
        var result = await sut.ResolveConflictAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task ResolveConflictAsync_WhenListenerChoosesFalse_ReturnsFalse()
    {
        var sut = new SyncConflictNotifier();
        sut.OnConflictDetected += callback => callback(false);

        var result = await sut.ResolveConflictAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task ResolveConflictAsync_WhenListenerChoosesTrue_ReturnsTrue()
    {
        var sut = new SyncConflictNotifier();
        sut.OnConflictDetected += callback => callback(true);

        var result = await sut.ResolveConflictAsync();
        Assert.True(result);
    }
}
