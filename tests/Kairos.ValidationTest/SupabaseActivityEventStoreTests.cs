using System.Net;
using System.Net.Http.Json;
using Kairos.Core.Models;
using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Kairos.ValidationTest;

public class SupabaseActivityEventStoreTests
{
    private readonly TestHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly StubSupabaseAuthService _authService;
    private readonly IOptions<SupabaseAuthOptions> _options;
    private readonly SupabaseActivityEventStore _sut;

    public SupabaseActivityEventStoreTests()
    {
        _httpHandler = new TestHandler();
        _httpClient = new HttpClient(_httpHandler);
        _authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "test-token",
            CurrentUserId = "test-user-id"
        };
        _options = Options.Create(new SupabaseAuthOptions
        {
            Url = "https://example.supabase.co",
            AnonKey = "anon-key"
        });

        _sut = new SupabaseActivityEventStore(_httpClient, _authService, _options);
    }

    [Fact]
    public async Task LoadEventsAsync_WithDateRanges_BuildsCorrectPostgrestQuery()
    {
        // Arrange
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var options = new ActivityEventQueryOptions
        {
            From = from,
            To = to
        };

        _httpHandler.ResponseFactory = req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };

        // Act
        var result = await _sut.LoadEventsAsync(options);

        // Assert
        Assert.NotNull(result);
        var url = _httpHandler.LastRequest?.RequestUri?.ToString();
        Assert.NotNull(url);
        Assert.Contains($"start_time=gte.{Uri.EscapeDataString(from.UtcDateTime.ToString("O"))}", url);
        Assert.Contains($"start_time=lte.{Uri.EscapeDataString(to.UtcDateTime.ToString("O"))}", url);
    }

    [Fact]
    public async Task LoadEventsAsync_WithSince_BuildsDeltaSyncQuery()
    {
        // Arrange
        var since = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var options = new ActivityEventQueryOptions
        {
            Since = since
        };

        _httpHandler.ResponseFactory = req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };

        // Act
        var result = await _sut.LoadEventsAsync(options);

        // Assert
        Assert.NotNull(result);
        var url = _httpHandler.LastRequest?.RequestUri?.ToString();
        Assert.NotNull(url);
        Assert.Contains($"updated_at=gte.{Uri.EscapeDataString(since.UtcDateTime.ToString("O"))}", url);
    }

    [Fact]
    public async Task LoadEventsAsync_WithPagination_BuildsLimitAndOffsetQuery()
    {
        // Arrange
        var options = new ActivityEventQueryOptions
        {
            Limit = 50,
            Offset = 100
        };

        _httpHandler.ResponseFactory = req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };

        // Act
        var result = await _sut.LoadEventsAsync(options);

        // Assert
        Assert.NotNull(result);
        var url = _httpHandler.LastRequest?.RequestUri?.ToString();
        Assert.NotNull(url);
        Assert.Contains("&limit=50", url);
        Assert.Contains("&offset=100", url);
    }

    [Fact]
    public async Task SaveEventsAsync_WithScopedWindow_ScopesDeleteQueryToWindow()
    {
        // Arrange
        var from = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 5, 31, 23, 59, 59, TimeSpan.Zero);
        var scopedWindow = new ActivityEventQueryOptions
        {
            From = from,
            To = to
        };

        var keptEventId = Guid.NewGuid();
        var localEvents = new List<ActivityEvent>
        {
            new()
            {
                Id = keptEventId,
                ActivityId = Guid.NewGuid(),
                StartTime = from.AddDays(5),
                EndTime = from.AddDays(5).AddHours(1),
                ActivityName = "Work"
            }
        };

        _httpHandler.ResponseFactory = req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri?.ToString().Contains("select=id") == true)
            {
                // Return remote list containing keptEventId and an obsolete event
                var existing = new[]
                {
                    new { id = keptEventId },
                    new { id = Guid.NewGuid() }
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(existing)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        // Act
        await _sut.SaveEventsAsync(localEvents, scopedWindow);

        // Assert
        var getReq = _httpHandler.Requests.FirstOrDefault(r => r.Method == HttpMethod.Get && r.RequestUri?.ToString().Contains("select=id") == true);
        Assert.NotNull(getReq);
        var getUrl = getReq.RequestUri?.ToString();
        Assert.Contains($"start_time=gte.{Uri.EscapeDataString(from.UtcDateTime.ToString("O"))}", getUrl);
        Assert.Contains($"start_time=lte.{Uri.EscapeDataString(to.UtcDateTime.ToString("O"))}", getUrl);

        var deleteReq = _httpHandler.Requests.FirstOrDefault(r => r.Method == HttpMethod.Delete);
        Assert.NotNull(deleteReq);
    }

    private sealed class TestHandler : HttpMessageHandler
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
