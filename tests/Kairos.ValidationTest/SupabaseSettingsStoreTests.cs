using System.Net;
using System.Net.Http.Json;
using Kairos.Application.Services;
using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Kairos.ValidationTest;

public class SupabaseSettingsStoreTests
{
    private readonly TestHttpHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly StubSupabaseAuthService _authService;
    private readonly IOptions<SupabaseAuthOptions> _options;
    private readonly SupabaseSettingsStore _sut;

    public SupabaseSettingsStoreTests()
    {
        _httpHandler = new TestHttpHandler();
        _httpClient = new HttpClient(_httpHandler);
        _authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "test-token",
            CurrentUserId = "user-settings"
        };
        _options = Options.Create(new SupabaseAuthOptions
        {
            Url = "https://example.supabase.co",
            AnonKey = "anon-key"
        });

        _sut = new SupabaseSettingsStore(_httpClient, _authService, _options);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenNotAuthenticated_ReturnsNull()
    {
        _authService.IsAuthenticated = false;

        var result = await _sut.LoadSettingsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenUserIdNull_ReturnsNull()
    {
        _authService.CurrentUserId = null;

        var result = await _sut.LoadSettingsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenEmpty_ReturnsNull()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };

        var result = await _sut.LoadSettingsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenSuccessful_ReturnsMappedSettingsWithDefaults()
    {
        var row = new
        {
            theme = (string?)null,
            language = (string?)null,
            tutorial_completed = true,
            sounds_enabled = true,
            advanced_settings_enabled = true,
            timeular_settings_enabled = false,
            activity_groups_enabled = true,
            budgets_enabled = true,
            active_activity_group = 1,
            auto_delete_event_duration = 30,
            sticky_events_duration = 15
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[] { row })
        };

        var result = await _sut.LoadSettingsAsync();

        Assert.NotNull(result);
        Assert.Equal("light", result.Theme);
        Assert.Equal("en", result.Language);
        Assert.True(result.TutorialCompleted);
        Assert.True(result.SoundsEnabled);
        Assert.True(result.AdvancedSettingsEnabled);
        Assert.False(result.TimeularSettingsEnabled);
        Assert.True(result.ActivityGroupsEnabled);
        Assert.True(result.BudgetsEnabled);
        Assert.Equal(1, result.ActiveActivityGroup);
        Assert.Equal(30, result.AutoDeleteEventDuration);
        Assert.Equal(15, result.StickyEventsDuration);
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenNotAuthenticated_DoesNotSendRequest()
    {
        _authService.IsAuthenticated = false;

        await _sut.SaveSettingsAsync(new SyncedSettingsData());

        Assert.Empty(_httpHandler.Requests);
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenSuccessful_SendsPostUpsert()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        var data = new SyncedSettingsData
        {
            Theme = "dark",
            Language = "es",
            TutorialCompleted = true,
            SoundsEnabled = true,
            AdvancedSettingsEnabled = false,
            TimeularSettingsEnabled = true,
            ActivityGroupsEnabled = true,
            BudgetsEnabled = false,
            ActiveActivityGroup = 2,
            AutoDeleteEventDuration = 60,
            StickyEventsDuration = 20
        };

        await _sut.SaveSettingsAsync(data);

        Assert.Single(_httpHandler.Requests);
        var req = _httpHandler.Requests[0];
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("user_settings?on_conflict=user_id", req.RequestUri!.ToString());
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
