using Kairos.Web.Services;

namespace Kairos.ValidationTest;

public class BrowserStorageServiceTests
{
    [Fact]
    public async Task GetItemAsync_UsesLocalStorageGetItem()
    {
        var js = new TestJsRuntime();
        js.SetResult("localStorage.getItem", "value-1");
        var sut = new BrowserStorageService(js);

        var value = await sut.GetItemAsync("k1");

        Assert.Equal("value-1", value);
        Assert.Contains(js.Invocations, x => x.Identifier == "localStorage.getItem" && (string)x.Arguments[0]! == "k1");
    }

    [Fact]
    public async Task SetAndRemoveItemAsync_UseLocalStorageMethods()
    {
        var js = new TestJsRuntime();
        var sut = new BrowserStorageService(js);

        await sut.SetItemAsync("k2", "v2");
        await sut.RemoveItemAsync("k2");

        Assert.Contains(js.Invocations, x => x.Identifier == "localStorage.setItem");
        Assert.Contains(js.Invocations, x => x.Identifier == "localStorage.removeItem");
    }
}
