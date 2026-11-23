using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Kairos.ValidationTest;

public class SupabaseRealtimeServiceTests
{
    private readonly TestJsRuntime _jsRuntime;
    private readonly StubSupabaseAuthService _authService;
    private readonly IOptions<SupabaseAuthOptions> _options;

    public SupabaseRealtimeServiceTests()
    {
        _jsRuntime = new TestJsRuntime();
        _authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "access-token-1",
            CurrentUserId = "user-1"
        };
        _options = Options.Create(new SupabaseAuthOptions
        {
            Url = "https://example.supabase.co",
            AnonKey = "anon-key"
        });
    }

    [Fact]
    public async Task InitializeAsync_WhenAuthenticated_ConnectsViaJs()
    {
        var sut = new SupabaseRealtimeService(_jsRuntime, _authService, _options);

        await sut.InitializeAsync();

        Assert.Contains(_jsRuntime.Invocations, x => x.Identifier == "kairosSupabaseRealtime.initialize");
        Assert.Contains(_jsRuntime.Invocations, x => x.Identifier == "kairosSupabaseRealtime.connect");
    }

    [Fact]
    public async Task InitializeAsync_WhenUnauthenticated_DisconnectsViaJs()
    {
        _authService.IsAuthenticated = false;
        var sut = new SupabaseRealtimeService(_jsRuntime, _authService, _options);

        await sut.InitializeAsync();

        Assert.Contains(_jsRuntime.Invocations, x => x.Identifier == "kairosSupabaseRealtime.initialize");
        Assert.Contains(_jsRuntime.Invocations, x => x.Identifier == "kairosSupabaseRealtime.disconnect");
    }

    [Fact]
    public async Task NotifyTableChanged_FiresEvent()
    {
        var sut = new SupabaseRealtimeService(_jsRuntime, _authService, _options);
        string? changedTable = null;
        sut.OnTableChanged += table => changedTable = table;

        await sut.NotifyTableChanged("activities");
        Assert.Equal("activities", changedTable);

        // Whitespace should not fire
        changedTable = null;
        await sut.NotifyTableChanged("  ");
        Assert.Null(changedTable);
    }

    [Fact]
    public async Task NotifyConnected_FiresEvent()
    {
        var sut = new SupabaseRealtimeService(_jsRuntime, _authService, _options);
        var connectedFired = false;
        sut.OnConnected += () => connectedFired = true;

        await sut.NotifyConnected();

        Assert.True(connectedFired);
    }

    [Fact]
    public async Task DisposeAsync_DisconnectsAndUnregisters()
    {
        var sut = new SupabaseRealtimeService(_jsRuntime, _authService, _options);
        await sut.InitializeAsync();

        await sut.DisposeAsync();

        Assert.Contains(_jsRuntime.Invocations, x => x.Identifier == "kairosSupabaseRealtime.disconnect");
    }

    [Fact]
    public async Task AuthStateChanged_UpdatesConnection()
    {
        _authService.IsAuthenticated = false;
        var sut = new SupabaseRealtimeService(_jsRuntime, _authService, _options);
        await sut.InitializeAsync();

        _jsRuntime.Invocations.Clear();

        // Now authenticate and raise auth changed
        _authService.IsAuthenticated = true;
        _authService.CurrentAccessToken = "new-token";
        _authService.CurrentUserId = "user-2";
        _authService.RaiseAuthStateChanged();

        // Allow task to run
        await Task.Delay(50);

        Assert.Contains(_jsRuntime.Invocations, x => x.Identifier == "kairosSupabaseRealtime.connect");
    }
}
