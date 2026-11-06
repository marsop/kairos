using Kairos.Application.Services;
using Microsoft.JSInterop;

namespace Kairos.Infrastructure.Services;

public class PwaService : IPwaService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<PwaService>? _objRef;
    private bool _isInstallable;
    private bool _isOnline = true;
    private bool _isVisible = true;

    public bool IsInstallable => _isInstallable;
    public bool IsOnline => _isOnline;
    public bool IsVisible => _isVisible;

    public event Action? OnStateChanged;
    public event Action<bool>? OnVisibilityChanged;

    public PwaService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        _objRef = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("pwaInterop.init", _objRef);
        _isOnline = await _js.InvokeAsync<bool>("pwaInterop.getOnlineStatus");
        try
        {
            _isVisible = await _js.InvokeAsync<bool>("pwaInterop.getVisibilityStatus");
        }
        catch
        {
            _isVisible = true;
        }
        OnVisibilityChanged?.Invoke(_isVisible);
        OnStateChanged?.Invoke();
    }

    public async Task InstallAppAsync()
    {
        if (_isInstallable)
        {
            await _js.InvokeVoidAsync("pwaInterop.triggerInstall");
            _isInstallable = false; // Usually becomes false after invocation
            OnStateChanged?.Invoke();
        }
    }

    [JSInvokable]
    public void OnInstallable(bool installable)
    {
        _isInstallable = installable;
        OnStateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnConnectionChanged(bool isOnline)
    {
        if (_isOnline != isOnline)
        {
            _isOnline = isOnline;
            OnStateChanged?.Invoke();
        }
    }

    [JSInvokable("OnVisibilityChanged")]
    public void NotifyVisibilityChanged(bool isVisible)
    {
        if (_isVisible != isVisible)
        {
            _isVisible = isVisible;
            OnVisibilityChanged?.Invoke(isVisible);
            OnStateChanged?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_objRef != null)
        {
            _objRef.Dispose();
        }
        await Task.CompletedTask;
    }
}
