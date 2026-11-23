using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kairos.Core.Models;
using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Kairos.ValidationTest;

public class SupabaseTimeAccountStoreTests
{
    private readonly TestHttpHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly StubSupabaseAuthService _authService;
    private readonly IOptions<SupabaseAuthOptions> _options;
    private readonly SupabaseTimeAccountStore _sut;

    public SupabaseTimeAccountStoreTests()
    {
        _httpHandler = new TestHttpHandler();
        _httpClient = new HttpClient(_httpHandler);
        _authService = new StubSupabaseAuthService
        {
            IsAuthenticated = true,
            CurrentAccessToken = "test-token",
            CurrentUserId = "user-ta"
        };
        _options = Options.Create(new SupabaseAuthOptions
        {
            Url = "https://example.supabase.co",
            AnonKey = "anon-key"
        });

        _sut = new SupabaseTimeAccountStore(_httpClient, _authService, _options);
    }

    [Fact]
    public async Task LoadAccountAsync_WhenNotAuthenticated_ReturnsNull()
    {
        _authService.IsAuthenticated = false;

        var result = await _sut.LoadAccountAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAccountAsync_WhenUserIdNull_ReturnsNull()
    {
        _authService.CurrentUserId = null;

        var result = await _sut.LoadAccountAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAccountAsync_WhenPayloadNullOrEmpty_ReturnsNull()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };

        var result = await _sut.LoadAccountAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAccountAsync_WhenPayloadValid_DeserializesTimeAccount()
    {
        var original = new TimeAccount
        {
            TimelinePeriod = TimeSpan.FromHours(48),
            LastModifiedAtUtc = DateTimeOffset.UtcNow
        };
        var row = new
        {
            payload = JsonSerializer.SerializeToElement(original)
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[] { row })
        };

        var result = await _sut.LoadAccountAsync();

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromHours(48), result.TimelinePeriod);
    }

    [Fact]
    public async Task SaveAccountAsync_WhenNotAuthenticated_DoesNotSendRequest()
    {
        _authService.IsAuthenticated = false;

        await _sut.SaveAccountAsync(new TimeAccount());

        Assert.Empty(_httpHandler.Requests);
    }

    [Fact]
    public async Task SaveAccountAsync_WhenSuccessful_SendsPostUpsert()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK);

        var account = new TimeAccount
        {
            TimelinePeriod = TimeSpan.FromHours(12),
            LastModifiedAtUtc = DateTimeOffset.UtcNow
        };

        await _sut.SaveAccountAsync(account);

        Assert.Single(_httpHandler.Requests);
        var req = _httpHandler.Requests[0];
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("time_accounts?on_conflict=user_id", req.RequestUri!.ToString());
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
