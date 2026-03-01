using Kairos.Shared.Services;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Kairos.ValidationTest;

public class ActivityConfigurationServiceTests
{
    [Fact]
    public async Task LoadActivitiesAsync_ValidConfig_MapsNamesAndDisplayOrder()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"activities":[{"name":"Work"},{"name":"Break"}]}""",
                Encoding.UTF8,
                "application/json")
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var sut = new ActivityConfigurationService(client);

        var activities = await sut.LoadActivitiesAsync();

        Assert.Equal(2, activities.Count);
        Assert.Equal("Work", activities[0].Name);
        Assert.Equal("Break", activities[1].Name);
        Assert.Equal(0, activities[0].DisplayOrder);
        Assert.Equal(1, activities[1].DisplayOrder);
        Assert.All(activities, activity => Assert.Equal(1.0, activity.Factor));
    }

    [Fact]
    public async Task LoadActivitiesAsync_NoActivities_Throws()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"activities":[]}""", Encoding.UTF8, "application/json")
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var sut = new ActivityConfigurationService(client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadActivitiesAsync());
    }
}
