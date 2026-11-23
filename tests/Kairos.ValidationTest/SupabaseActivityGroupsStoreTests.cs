using System.Net;
using System.Net.Http.Json;
using Kairos.Application.Services;
using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Kairos.ValidationTest;

public class SupabaseActivityGroupsStoreTests
{
    private readonly TestHttpHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly StubSupabaseAuthService _authService;
    private readonly IOptions<SupabaseAuthOptions> _options;
    private readonly SupabaseActivityGroupsStore _sut;

    public SupabaseActivityGroupsStoreTests()
    {
        _httpHandler = new TestHttpHandler();
        _httpClient = new HttpClient(_httpHandler);
        _authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "test-token",
            CurrentUserId = "user-abc"
        };
        _options = Options.Create(new SupabaseAuthOptions
        {
            Url = "https://example.supabase.co",
            AnonKey = "anon-key"
        });

        _sut = new SupabaseActivityGroupsStore(_httpClient, _authService, _options);
    }

    [Fact]
    public async Task LoadGroupsAsync_WhenNotAuthenticated_ReturnsNull()
    {
        _authService.IsAuthenticated = false;

        var result = await _sut.LoadGroupsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadGroupsAsync_WhenUserIdNull_ReturnsNull()
    {
        _authService.CurrentUserId = null;

        var result = await _sut.LoadGroupsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadGroupsAsync_WhenSuccessful_ReturnsMappedGroups()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var rows = new[]
        {
            new { group_id = g2, group_order = 1, name = "Personal", color = (string?)null, icon = (string?)null },
            new { group_id = g1, group_order = 0, name = "Work", color = (string?)"#EF4444", icon = (string?)"💼" },
            new { group_id = Guid.Empty, group_order = 2, name = "Invalid", color = (string?)"#000", icon = (string?)"" }
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(rows)
        };

        var result = await _sut.LoadGroupsAsync();

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Work", result[0].Name);
        Assert.Equal(g1, result[0].GroupId);
        Assert.Equal("Personal", result[1].Name);
        Assert.Equal("#10B981", result[1].Color);
        Assert.Equal("🗂️", result[1].Icon);
    }

    [Fact]
    public async Task SaveGroupsAsync_WhenNotAuthenticated_DoesNotSendRequests()
    {
        _authService.IsAuthenticated = false;

        await _sut.SaveGroupsAsync(new List<SyncedActivityGroupData> { new() { Name = "Group" } });

        Assert.Empty(_httpHandler.Requests);
    }

    [Fact]
    public async Task SaveGroupsAsync_WhenEmptyList_SendsDeleteOnly()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.SaveGroupsAsync(new List<SyncedActivityGroupData>());

        Assert.Single(_httpHandler.Requests);
        Assert.Equal(HttpMethod.Delete, _httpHandler.Requests[0].Method);
        Assert.Contains("user_activity_groups?user_id=eq.user-abc", _httpHandler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task SaveGroupsAsync_WhenNonEmpty_SendsDeleteThenInsert()
    {
        var group = new SyncedActivityGroupData
        {
            GroupId = Guid.NewGuid(),
            GroupOrder = 0,
            Name = " Projects ",
            Color = " #3B82F6 ",
            Icon = " 📂 "
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.SaveGroupsAsync(new List<SyncedActivityGroupData> { group });

        Assert.Equal(2, _httpHandler.Requests.Count);
        Assert.Equal(HttpMethod.Delete, _httpHandler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, _httpHandler.Requests[1].Method);
        Assert.Contains("on_conflict=user_id,group_id", _httpHandler.Requests[1].RequestUri!.ToString());
    }

    private sealed class TestHttpHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = ResponseFactory != null
                ? ResponseFactory(request)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
            return Task.FromResult(response);
        }
    }
}
