using System.Net;
using System.Net.Http.Json;
using Kairos.Core.Models;
using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Kairos.ValidationTest;

public class SupabaseBudgetStoreTests
{
    private readonly TestHttpHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly StubSupabaseAuthService _authService;
    private readonly IOptions<SupabaseAuthOptions> _options;
    private readonly SupabaseBudgetStore _sut;

    public SupabaseBudgetStoreTests()
    {
        _httpHandler = new TestHttpHandler();
        _httpClient = new HttpClient(_httpHandler);
        _authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "test-token",
            CurrentUserId = "user-budget"
        };
        _options = Options.Create(new SupabaseAuthOptions
        {
            Url = "https://example.supabase.co",
            AnonKey = "anon-key"
        });

        _sut = new SupabaseBudgetStore(_httpClient, _authService, _options);
    }

    [Fact]
    public async Task LoadBudgetsAsync_WhenNotAuthenticated_ReturnsEmpty()
    {
        _authService.IsAuthenticated = false;

        var result = await _sut.LoadBudgetsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadBudgetsAsync_WhenUserIdNull_ReturnsEmpty()
    {
        _authService.CurrentUserId = null;

        var result = await _sut.LoadBudgetsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadBudgetsAsync_WhenSuccessful_ReturnsMappedBudgets()
    {
        var bId = Guid.NewGuid();
        var aId = Guid.NewGuid();
        var rows = new[]
        {
            new
            {
                id = bId,
                activity_id = aId,
                allocated_time_span = TimeSpan.FromHours(5).Ticks,
                minimum_time_span = TimeSpan.FromHours(2).Ticks,
                budget_type = (int)BudgetType.Weekly
            },
            new
            {
                id = Guid.Empty,
                activity_id = aId,
                allocated_time_span = 0L,
                minimum_time_span = 0L,
                budget_type = 0
            }
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(rows)
        };

        var result = await _sut.LoadBudgetsAsync();

        Assert.Single(result);
        Assert.Equal(bId, result[0].Id);
        Assert.Equal(aId, result[0].ActivityId);
        Assert.Equal(TimeSpan.FromHours(5), result[0].AllocatedTimeSpan);
        Assert.Equal(TimeSpan.FromHours(2), result[0].MinimumTimeSpan);
        Assert.Equal(BudgetType.Weekly, result[0].Type);
    }

    [Fact]
    public async Task SaveBudgetsAsync_WhenNotAuthenticated_DoesNotSendRequests()
    {
        _authService.IsAuthenticated = false;

        await _sut.SaveBudgetsAsync(new List<ActivityBudget> { new() });

        Assert.Empty(_httpHandler.Requests);
    }

    [Fact]
    public async Task SaveBudgetsAsync_WhenEmpty_SendsDeleteOnly()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.SaveBudgetsAsync(new List<ActivityBudget>());

        Assert.Equal(2, _httpHandler.Requests.Count);
        Assert.Equal(HttpMethod.Post, _httpHandler.Requests[0].Method);
        Assert.Equal(HttpMethod.Delete, _httpHandler.Requests[1].Method);
        Assert.Contains("budgets?user_id=eq.user-budget", _httpHandler.Requests[1].RequestUri!.ToString());
        Assert.DoesNotContain("id=not.in", _httpHandler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task SaveBudgetsAsync_WhenNonEmpty_SendsUpsertAndScopedDelete()
    {
        var bId = Guid.NewGuid();
        var budget = new ActivityBudget
        {
            Id = bId,
            ActivityId = Guid.NewGuid(),
            AllocatedTimeSpan = TimeSpan.FromHours(8),
            MinimumTimeSpan = TimeSpan.FromHours(4),
            Type = BudgetType.Monthly
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        await _sut.SaveBudgetsAsync(new List<ActivityBudget> { budget });

        Assert.Equal(2, _httpHandler.Requests.Count);
        Assert.Equal(HttpMethod.Post, _httpHandler.Requests[0].Method);
        Assert.Equal(HttpMethod.Delete, _httpHandler.Requests[1].Method);
        var delUri = _httpHandler.Requests[1].RequestUri!.ToString();
        Assert.Contains("id=not.in", delUri);
        Assert.Contains(bId.ToString(), delUri);
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
