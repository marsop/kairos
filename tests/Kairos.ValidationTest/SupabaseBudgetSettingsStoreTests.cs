using System.Net;
using System.Net.Http.Json;
using Kairos.Application.Services;
using Kairos.Core.Models;
using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Kairos.ValidationTest;

public class SupabaseBudgetSettingsStoreTests
{
    private readonly TestHttpHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly StubSupabaseAuthService _authService;
    private readonly IOptions<SupabaseAuthOptions> _options;
    private readonly SupabaseBudgetSettingsStore _sut;

    public SupabaseBudgetSettingsStoreTests()
    {
        _httpHandler = new TestHttpHandler();
        _httpClient = new HttpClient(_httpHandler);
        _authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "test-token",
            CurrentUserId = "user-bs"
        };
        _options = Options.Create(new SupabaseAuthOptions
        {
            Url = "https://example.supabase.co",
            AnonKey = "anon-key"
        });

        _sut = new SupabaseBudgetSettingsStore(_httpClient, _authService, _options);
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
    public async Task LoadSettingsAsync_WhenEmptyRows_ReturnsNull()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };

        var result = await _sut.LoadSettingsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenSuccessful_ReturnsMappedSettingsWithFallbacks()
    {
        var row = new
        {
            minimum_enabled = true,
            threshold = 85,
            color_minimum_not_reached = (string?)null,
            color_minimum_reached_max_not_reached = (string?)null,
            color_between_threshold_max = "#F59E0B",
            color_over_max = "#EF4444",
            budget_type = (int)BudgetType.Daily,
            notifications_enabled = false
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[] { row })
        };

        var result = await _sut.LoadSettingsAsync();

        Assert.NotNull(result);
        Assert.True(result.MinimumEnabled);
        Assert.Equal(85, result.Threshold);
        Assert.Equal("#0000ff", result.ColorMinimumNotReached);
        Assert.Equal("#00ff00", result.ColorMinimumReachedMaxNotReached);
        Assert.Equal("#F59E0B", result.ColorBetweenThresholdMax);
        Assert.Equal("#EF4444", result.ColorOverMax);
        Assert.Equal(BudgetType.Daily, result.BudgetType);
        Assert.False(result.NotificationsEnabled);
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenHttpFails_ThrowsInvalidOperationException()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.LoadSettingsAsync());
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenNotAuthenticated_DoesNotSendRequest()
    {
        _authService.IsAuthenticated = false;

        await _sut.SaveSettingsAsync(new BudgetSettingsData());

        Assert.Empty(_httpHandler.Requests);
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenSuccessful_SendsUpsertPost()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        var data = new BudgetSettingsData
        {
            MinimumEnabled = true,
            Threshold = 90,
            ColorMinimumNotReached = "#111",
            ColorMinimumReachedMaxNotReached = "#222",
            ColorBetweenThresholdMax = "#333",
            ColorOverMax = "#444",
            BudgetType = BudgetType.Weekly,
            NotificationsEnabled = true
        };

        await _sut.SaveSettingsAsync(data);

        Assert.Single(_httpHandler.Requests);
        var req = _httpHandler.Requests[0];
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("budget_settings?on_conflict=user_id", req.RequestUri!.ToString());
    }

    [Fact]
    public async Task SaveSettingsAsync_WhenHttpFails_ThrowsInvalidOperationException()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.BadGateway);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SaveSettingsAsync(new BudgetSettingsData()));
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
