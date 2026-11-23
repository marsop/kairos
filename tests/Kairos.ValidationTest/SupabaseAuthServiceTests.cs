using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Kairos.ValidationTest;

public class SupabaseAuthServiceTests
{
    private readonly TestHttpHandler _httpHandler;
    private readonly HttpClient _httpClient;
    private readonly InMemoryStorageService _storageService;
    private readonly IOptions<SupabaseAuthOptions> _options;
    private readonly SupabaseAuthService _sut;

    public SupabaseAuthServiceTests()
    {
        _httpHandler = new TestHttpHandler();
        _httpClient = new HttpClient(_httpHandler);
        _storageService = new InMemoryStorageService();
        _options = Options.Create(new SupabaseAuthOptions
        {
            Url = "https://example.supabase.co",
            AnonKey = "test-anon-key"
        });

        _sut = new SupabaseAuthService(_httpClient, _storageService, _options);
    }

    [Fact]
    public void IsConfigured_ReturnsExpectedBasedOnOptions()
    {
        Assert.True(_sut.IsConfigured);

        var unconfigured = new SupabaseAuthService(_httpClient, _storageService, Options.Create(new SupabaseAuthOptions()));
        Assert.False(unconfigured.IsConfigured);
    }

    [Fact]
    public async Task InitializeAsync_WhenNoStoredSession_SetsInitialized()
    {
        var authChangedFired = false;
        _sut.OnAuthStateChanged += () => authChangedFired = true;

        await _sut.InitializeAsync();

        Assert.True(_sut.IsInitialized);
        Assert.False(_sut.IsAuthenticated);
        Assert.True(authChangedFired);

        // Calling a second time should no-op
        authChangedFired = false;
        await _sut.InitializeAsync();
        Assert.False(authChangedFired);
    }

    [Fact]
    public async Task InitializeAsync_WhenStoredSessionCorrupted_HandlesGracefully()
    {
        await _storageService.SetItemAsync("Kairos_supabase_session", "invalid-json{");

        await _sut.InitializeAsync();

        Assert.True(_sut.IsInitialized);
        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task InitializeAsync_WhenValidStoredSessionNotExpired_RestoresSession()
    {
        var session = new
        {
            AccessToken = "valid-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
            User = new { Id = "user-1", Email = "test@example.com" }
        };
        await _storageService.SetItemAsync("Kairos_supabase_session", JsonSerializer.Serialize(session));

        await _sut.InitializeAsync();

        Assert.True(_sut.IsAuthenticated);
        Assert.Equal("user-1", _sut.CurrentUserId);
        Assert.Equal("test@example.com", _sut.CurrentUserEmail);
        Assert.Equal("valid-token", _sut.CurrentAccessToken);
    }

    [Fact]
    public async Task InitializeAsync_WhenStoredSessionExpired_RefreshesToken()
    {
        var session = new
        {
            AccessToken = "expired-token",
            RefreshToken = "valid-refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            User = new { Id = "user-1", Email = "test@example.com" }
        };
        await _storageService.SetItemAsync("Kairos_supabase_session", JsonSerializer.Serialize(session));

        var refreshResponse = new
        {
            access_token = "new-access-token",
            refresh_token = "new-refresh-token",
            expires_in = 3600,
            expires_at = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            user = new { id = "user-1", email = "test@example.com" }
        };

        _httpHandler.ResponseFactory = req =>
        {
            if (req.RequestUri!.ToString().Contains("grant_type=refresh_token"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(refreshResponse)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        await _sut.InitializeAsync();

        Assert.True(_sut.IsAuthenticated);
        Assert.Equal("new-access-token", _sut.CurrentAccessToken);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenRefreshFailsWith401_ClearsSession()
    {
        var session = new
        {
            AccessToken = "expired-token",
            RefreshToken = "revoked-refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            User = new { Id = "user-1", Email = "test@example.com" }
        };
        await _storageService.SetItemAsync("Kairos_supabase_session", JsonSerializer.Serialize(session));

        _httpHandler.ResponseFactory = req => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        await _sut.InitializeAsync();

        Assert.False(_sut.IsAuthenticated);
        Assert.Null(_sut.CurrentUserId);
        Assert.Contains("Kairos_supabase_session", _storageService.RemovedKeys);
    }

    [Fact]
    public async Task SignInAsync_WhenUnconfigured_Fails()
    {
        var unconfigured = new SupabaseAuthService(_httpClient, _storageService, Options.Create(new SupabaseAuthOptions()));

        var result = await unconfigured.SignInAsync("test@example.com", "pass");

        Assert.False(result.Succeeded);
        Assert.Equal("Supabase is not configured.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("email@example.com", "")]
    [InlineData("  ", "  ")]
    public async Task SignInAsync_WhenMissingCredentials_Fails(string email, string password)
    {
        var result = await _sut.SignInAsync(email, password);

        Assert.False(result.Succeeded);
        Assert.Equal("Email and password are required.", result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_WhenHttpError_ReturnsParsedErrorMessage()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { msg = "Invalid login credentials" })
        };

        var result = await _sut.SignInAsync("user@example.com", "wrongpass");

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid login credentials", result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_WhenErrorDescriptionReturned_UsesErrorDescription()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { error_description = "Account locked" })
        };

        var result = await _sut.SignInAsync("user@example.com", "locked");

        Assert.False(result.Succeeded);
        Assert.Equal("Account locked", result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_WhenErrorFieldReturned_UsesError()
    {
        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { error = "invalid_grant" })
        };

        var result = await _sut.SignInAsync("user@example.com", "err");

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_grant", result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_WhenSuccessful_PersistsSessionAndReturnsSuccess()
    {
        var authResponse = new
        {
            access_token = "signed-in-token",
            refresh_token = "refresh-token-1",
            expires_in = 7200,
            expires_at = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds(),
            user = new { id = "new-user-id", email = "hello@example.com" }
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(authResponse)
        };

        var result = await _sut.SignInAsync("hello@example.com", "secret123");

        Assert.True(result.Succeeded);
        Assert.False(result.RequiresEmailConfirmation);
        Assert.True(_sut.IsAuthenticated);
        Assert.Equal("new-user-id", _sut.CurrentUserId);
        Assert.Equal("hello@example.com", _sut.CurrentUserEmail);
        Assert.Equal("signed-in-token", _sut.CurrentAccessToken);
    }

    [Fact]
    public async Task SignUpAsync_WhenRequiresEmailConfirmation_ReturnsFlag()
    {
        // Supabase sign-up when email confirmation is required does not return access_token
        var authResponse = new
        {
            access_token = (string?)null,
            refresh_token = (string?)null,
            user = new { id = "unconfirmed-id", email = "confirm@example.com" }
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(authResponse)
        };

        var result = await _sut.SignUpAsync("confirm@example.com", "secret123");

        Assert.True(result.Succeeded);
        Assert.True(result.RequiresEmailConfirmation);
        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task SignUpAsync_WhenReturnsAccessToken_AuthenticatesImmediately()
    {
        var authResponse = new
        {
            access_token = "signup-token",
            refresh_token = "signup-refresh",
            expires_in = 3600,
            expires_at = 0L,
            user = new { id = "instant-id", email = "instant@example.com" }
        };

        _httpHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(authResponse)
        };

        var result = await _sut.SignUpAsync("instant@example.com", "secret123");

        Assert.True(result.Succeeded);
        Assert.False(result.RequiresEmailConfirmation);
        Assert.True(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task SignUpAsync_WhenUnconfiguredOrMissingCredentials_Fails()
    {
        var unconfigured = new SupabaseAuthService(_httpClient, _storageService, Options.Create(new SupabaseAuthOptions()));
        var res1 = await unconfigured.SignUpAsync("test@example.com", "pass");
        Assert.False(res1.Succeeded);

        var res2 = await _sut.SignUpAsync("", "");
        Assert.False(res2.Succeeded);
    }

    [Fact]
    public async Task SignOutAsync_ClearsSessionAndFiresEvent()
    {
        var session = new
        {
            AccessToken = "valid-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
            User = new { Id = "user-1", Email = "test@example.com" }
        };
        await _storageService.SetItemAsync("Kairos_supabase_session", JsonSerializer.Serialize(session));
        await _sut.InitializeAsync();
        Assert.True(_sut.IsAuthenticated);

        var authChangedFired = false;
        _sut.OnAuthStateChanged += () => authChangedFired = true;

        await _sut.SignOutAsync();

        Assert.False(_sut.IsAuthenticated);
        Assert.Null(_sut.CurrentUserId);
        Assert.True(authChangedFired);
        Assert.Contains("Kairos_supabase_session", _storageService.RemovedKeys);
    }

    private sealed class TestHttpHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = ResponseFactory != null
                ? ResponseFactory(request)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            return Task.FromResult(response);
        }
    }
}
