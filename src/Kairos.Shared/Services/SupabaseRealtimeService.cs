using Microsoft.JSInterop;
using Microsoft.Extensions.Options;

namespace Kairos.Shared.Services;

public sealed class SupabaseRealtimeService : ISupabaseRealtimeService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ISupabaseAuthService _authService;
    private readonly SupabaseAuthOptions _options;

    private DotNetObjectReference<SupabaseRealtimeService>? _dotNetRef;
    private bool _isInitialized;

    public event Action<string>? OnTableChanged;

    public SupabaseRealtimeService(IJSRuntime jsRuntime, ISupabaseAuthService authService, IOptions<SupabaseAuthOptions> options)
    {
        _jsRuntime = jsRuntime;
        _authService = authService;
        _options = options.Value;
        _authService.OnAuthStateChanged += HandleAuthStateChanged;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _dotNetRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("kairosSupabaseRealtime.initialize", _dotNetRef);
        _isInitialized = true;
        await UpdateConnectionAsync();
    }

    [JSInvokable]
    public Task NotifyTableChanged(string table)
    {
        if (!string.IsNullOrWhiteSpace(table))
        {
            OnTableChanged?.Invoke(table);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _authService.OnAuthStateChanged -= HandleAuthStateChanged;

        if (_isInitialized)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("kairosSupabaseRealtime.disconnect");
            }
            catch
            {
                // Ignore teardown issues during disposal.
            }
        }

        _dotNetRef?.Dispose();
    }

    private void HandleAuthStateChanged()
    {
        _ = UpdateConnectionAsync();
    }

    private async Task UpdateConnectionAsync()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (!_authService.IsAuthenticated
            || string.IsNullOrWhiteSpace(_options.Url)
            || string.IsNullOrWhiteSpace(_options.AnonKey)
            || string.IsNullOrWhiteSpace(_authService.CurrentAccessToken)
            || string.IsNullOrWhiteSpace(_authService.CurrentUserId))
        {
            await _jsRuntime.InvokeVoidAsync("kairosSupabaseRealtime.disconnect");
            return;
        }

        await _jsRuntime.InvokeVoidAsync("kairosSupabaseRealtime.connect", new
        {
            url = _options.Url,
            anonKey = _options.AnonKey,
            accessToken = _authService.CurrentAccessToken,
            userId = _authService.CurrentUserId
        });
    }
}
