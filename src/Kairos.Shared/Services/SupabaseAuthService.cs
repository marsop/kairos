using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Kairos.Shared.Services;

public sealed class SupabaseAuthService : ISupabaseAuthService
{
    private const string SessionStorageKey = "Kairos_supabase_session";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IStorageService _storageService;
    private readonly SupabaseAuthOptions _options;

    private SupabaseSession? _session;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Url) && !string.IsNullOrWhiteSpace(_options.AnonKey);
    public bool IsAuthenticated => _session is not null && _session.ExpiresAt > DateTimeOffset.UtcNow;
    public string? CurrentUserEmail => _session?.User?.Email;
    public string? CurrentUserId => _session?.User?.Id;
    public string? CurrentAccessToken => IsAuthenticated ? _session?.AccessToken : null;

    public event Action? OnAuthStateChanged;

    public SupabaseAuthService(HttpClient httpClient, IStorageService storageService, IOptions<SupabaseAuthOptions> options)
    {
        _httpClient = httpClient;
        _storageService = storageService;
        _options = options.Value;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        var stored = await _storageService.GetItemAsync(SessionStorageKey);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            try
            {
                _session = JsonSerializer.Deserialize<SupabaseSession>(stored, JsonOptions);
            }
            catch
            {
                _session = null;
            }
        }

        if (_session is not null && _session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await SignOutAsync();
        }

        _isInitialized = true;
        OnAuthStateChanged?.Invoke();
    }

    public async Task<SupabaseAuthResult> SignInAsync(string email, string password)
    {
        if (!IsConfigured)
        {
            return Fail("Supabase is not configured.");
        }

        var normalizedEmail = email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
        {
            return Fail("Email and password are required.");
        }

        var request = new SupabaseCredentialsRequest
        {
            Email = normalizedEmail,
            Password = password
        };

        var response = await SendAsync("auth/v1/token?grant_type=password", request);
        if (!response.IsSuccessStatusCode)
        {
            return Fail(await ReadErrorMessageAsync(response));
        }

        var authResponse = await response.Content.ReadFromJsonAsync<SupabaseAuthResponse>(JsonOptions);
        if (authResponse?.AccessToken is null)
        {
            return Fail("Supabase did not return a valid session.");
        }

        _session = BuildSession(authResponse);
        await PersistSessionAsync();
        OnAuthStateChanged?.Invoke();

        return new SupabaseAuthResult { Succeeded = true };
    }

    public async Task<SupabaseAuthResult> SignUpAsync(string email, string password)
    {
        if (!IsConfigured)
        {
            return Fail("Supabase is not configured.");
        }

        var normalizedEmail = email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
        {
            return Fail("Email and password are required.");
        }

        var request = new SupabaseCredentialsRequest
        {
            Email = normalizedEmail,
            Password = password
        };

        var response = await SendAsync("auth/v1/signup", request);
        if (!response.IsSuccessStatusCode)
        {
            return Fail(await ReadErrorMessageAsync(response));
        }

        var authResponse = await response.Content.ReadFromJsonAsync<SupabaseAuthResponse>(JsonOptions);
        if (authResponse?.AccessToken is not null)
        {
            _session = BuildSession(authResponse);
            await PersistSessionAsync();
            OnAuthStateChanged?.Invoke();
            return new SupabaseAuthResult { Succeeded = true };
        }

        return new SupabaseAuthResult
        {
            Succeeded = true,
            RequiresEmailConfirmation = true
        };
    }

    public async Task SignOutAsync()
    {
        _session = null;
        await _storageService.RemoveItemAsync(SessionStorageKey);
        OnAuthStateChanged?.Invoke();
    }

    private async Task<HttpResponseMessage> SendAsync(string relativePath, object body)
    {
        var url = BuildUrl(relativePath);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        request.Headers.Add("apikey", _options.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AnonKey);

        return await _httpClient.SendAsync(request);
    }

    private string BuildUrl(string relativePath)
    {
        var baseUrl = _options.Url.TrimEnd('/');
        return $"{baseUrl}/{relativePath}";
    }

    private async Task PersistSessionAsync()
    {
        if (_session is null)
        {
            return;
        }

        var serialized = JsonSerializer.Serialize(_session);
        await _storageService.SetItemAsync(SessionStorageKey, serialized);
    }

    private static SupabaseSession BuildSession(SupabaseAuthResponse response)
    {
        var expiresAt = response.ExpiresAt > 0
            ? DateTimeOffset.FromUnixTimeSeconds(response.ExpiresAt)
            : DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn > 0 ? response.ExpiresIn : 3600);

        return new SupabaseSession
        {
            AccessToken = response.AccessToken ?? string.Empty,
            RefreshToken = response.RefreshToken,
            ExpiresAt = expiresAt,
            User = response.User
        };
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<SupabaseErrorResponse>(JsonOptions);
            if (!string.IsNullOrWhiteSpace(payload?.Message))
            {
                return payload.Message;
            }

            if (!string.IsNullOrWhiteSpace(payload?.ErrorDescription))
            {
                return payload.ErrorDescription;
            }

            if (!string.IsNullOrWhiteSpace(payload?.Error))
            {
                return payload.Error;
            }
        }
        catch
        {
            // Ignore parse failures and return fallback message below.
        }

        return $"Supabase request failed ({(int)response.StatusCode}).";
    }

    private static SupabaseAuthResult Fail(string message) =>
        new()
        {
            Succeeded = false,
            ErrorMessage = message
        };
}

internal sealed class SupabaseCredentialsRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

internal sealed class SupabaseAuthResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; set; }

    [JsonPropertyName("user")]
    public SupabaseUser? User { get; set; }
}

internal sealed class SupabaseErrorResponse
{
    [JsonPropertyName("msg")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

internal sealed class SupabaseSession
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public SupabaseUser? User { get; set; }
}

internal sealed class SupabaseUser
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}
