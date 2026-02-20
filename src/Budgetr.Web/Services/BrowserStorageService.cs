using Budgetr.Shared.Services;
using Microsoft.JSInterop;

namespace Budgetr.Web.Services;

/// <summary>
/// Browser localStorage implementation of IStorageService.
/// </summary>
public class BrowserStorageService : IStorageService
{
    private readonly IJSRuntime _js;

    public BrowserStorageService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string?> GetItemAsync(string key)
    {
        return await _js.InvokeAsync<string?>("localStorage.getItem", key);
    }

    public async Task SetItemAsync(string key, string value)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", key, value);
    }

    public async Task RemoveItemAsync(string key)
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", key);
    }
}
