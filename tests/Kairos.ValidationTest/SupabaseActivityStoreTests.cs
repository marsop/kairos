using System.Net;
using System.Net.Http.Json;
using Kairos.Core.Models;
using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Kairos.ValidationTest;

public class SupabaseActivityStoreTests
{
    private readonly TestHttpHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly StubSupabaseAuthService _authService;
    private readonly IOptions<SupabaseAuthOptions> _options;
    private readonly SupabaseActivityStore _sut;

    public SupabaseActivityStoreTests()
    {
        _httpHandler = new TestHttpHandler();
        _httpClient = new HttpClient(_httpHandler);
        _authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "test-token",
            CurrentUserId = "user-123"
        };
        _options = Options.Create(new SupabaseAuthOptions
        {
            Url = "https://example.supabase.co",
            AnonKey = "anon-key"
        });

        _sut = new SupabaseActivityStore(_httpClient, _authService, _options);
    }

    [Fact]
    public async Task LoadActivitiesAsync_WhenNotAuthenticated_ReturnsEmpty()
    {
        _authService.IsAuthenticated = false;

        var result = await _sut.LoadActivitiesAsync();

        Assert.Empty(result);
        Assert.Empty(_httpHandler.Requests);
    }

    [Fact]
    public async Task LoadActivitiesAsync_WhenUserIdMissing_ReturnsEmpty()
    {
        _authService.CurrentUserId = null;

        var result = await _sut.LoadActivitiesAsync();

        Assert.Empty(result);
        Assert.Empty(_httpHandler.Requests);
    }

    [Fact]
    public async Task LoadActivitiesAsync_WhenOptionsMissing_ReturnsEmpty()
    {
        var store = new SupabaseActivityStore(_httpClient, _authService, Options.Create(new SupabaseAuthOptions()));

        var result = await store.LoadActivitiesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadActivitiesAsync_Successful_ReturnsMappedActivities()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var rows = new[]
        {
            new { id = id1, name = "Coding", color = "#10B981", emoji = "💻", display_order = 0, activity_group_id = 1 },
            new { id = id2, name = "Reading", color = "#3B82F6", emoji = "📚", display_order = 1, activity_group_id = 1 },
            new { id = Guid.Empty, name = "Invalid", color = "#000000", emoji = "", display_order = 2, activity_group_id = 0 },
            new { id = Guid.NewGuid(), name = "   ", color = "#000000", emoji = "", display_order = 3, activity_group_id = 0 }
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(rows)
        };

        var result = await _sut.LoadActivitiesAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Coding", result[0].Name);
        Assert.Equal("#10B981", result[0].Color);
        Assert.Equal("💻", result[0].Emoji);
        Assert.Equal(1, result[0].ActivityGroupId);

        var req = _httpHandler.LastRequest;
        Assert.NotNull(req);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
        Assert.Equal("test-token", req.Headers.Authorization?.Parameter);
        Assert.Equal("anon-key", req.Headers.GetValues("apikey").First());
        Assert.Contains("activities?select=id,name,color,emoji,display_order,activity_group_id", req.RequestUri!.ToString());
    }

    [Fact]
    public async Task SaveActivitiesAsync_WhenNotAuthenticated_DoesNotSendRequests()
    {
        _authService.IsAuthenticated = false;

        await _sut.SaveActivitiesAsync(new List<Activity> { new() { Name = "Work" } });

        Assert.Empty(_httpHandler.Requests);
    }

    [Fact]
    public async Task SaveActivitiesAsync_WhenEmptyList_ClearsAllUserActivities()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.SaveActivitiesAsync(new List<Activity>());

        Assert.Equal(2, _httpHandler.Requests.Count);
        var postReq = _httpHandler.Requests[0];
        Assert.Equal(HttpMethod.Post, postReq.Method);
        Assert.Contains("activities?on_conflict=user_id,id", postReq.RequestUri!.ToString());

        var deleteReq = _httpHandler.Requests[1];
        Assert.Equal(HttpMethod.Delete, deleteReq.Method);
        Assert.Contains("activities?user_id=eq.user-123", deleteReq.RequestUri!.ToString());
        Assert.DoesNotContain("id=not.in", deleteReq.RequestUri!.ToString());
    }

    [Fact]
    public async Task SaveActivitiesAsync_WhenNonEmpty_UpsertsAndDeletesExcluded()
    {
        var act1 = new Activity { Id = Guid.NewGuid(), Name = "Work", Color = "#EF4444", Emoji = "💼", DisplayOrder = 0, ActivityGroupId = 1 };
        var act2 = new Activity { Id = Guid.NewGuid(), Name = "Exercise", Color = "#10B981", Emoji = "🏃", DisplayOrder = 1, ActivityGroupId = 1 };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.SaveActivitiesAsync(new List<Activity> { act1, act2 });

        Assert.Equal(2, _httpHandler.Requests.Count);
        var postReq = _httpHandler.Requests[0];
        Assert.Equal(HttpMethod.Post, postReq.Method);

        var deleteReq = _httpHandler.Requests[1];
        Assert.Equal(HttpMethod.Delete, deleteReq.Method);
        var delUri = deleteReq.RequestUri!.ToString();
        Assert.Contains("id=not.in", delUri);
        Assert.Contains(act1.Id.ToString(), delUri);
        Assert.Contains(act2.Id.ToString(), delUri);
    }

    private sealed class TestHttpHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public List<HttpRequestMessage> Requests { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            Requests.Add(request);
            var response = ResponseFactory != null
                ? ResponseFactory(request)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
            return Task.FromResult(response);
        }
    }
}
