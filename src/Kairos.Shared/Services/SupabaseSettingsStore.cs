using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Kairos.Shared.Services;

public sealed class SupabaseSettingsStore : ISupabaseSettingsStore
{
    private readonly HttpClient _httpClient;
    private readonly ISupabaseAuthService _authService;
    private readonly SupabaseAuthOptions _options;

    public SupabaseSettingsStore(HttpClient httpClient, ISupabaseAuthService authService, IOptions<SupabaseAuthOptions> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _options = options.Value;
    }

    public async Task<SyncedSettingsData?> LoadSettingsAsync()
    {
        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUrl($"rest/v1/user_settings?select=theme,language,tutorial_completed&user_id=eq.{Uri.EscapeDataString(userId!)}&limit=1"));

        AddHeaders(request);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<SupabaseSettingsRow>>() ?? new List<SupabaseSettingsRow>();
        var row = rows.FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        return new SyncedSettingsData
        {
            Theme = row.Theme ?? "light",
            Language = row.Language ?? "en",
            TutorialCompleted = row.TutorialCompleted
        };
    }

    public async Task SaveSettingsAsync(SyncedSettingsData settings)
    {
        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return;
        }

        var row = new SupabaseSettingsWriteRow
        {
            UserId = userId!,
            Theme = settings.Theme,
            Language = settings.Language,
            TutorialCompleted = settings.TutorialCompleted
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUrl("rest/v1/user_settings?on_conflict=user_id"));

        AddHeaders(request);
        request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
        request.Content = JsonContent.Create(new[] { row });

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private bool CanSync(string? userId)
    {
        return _authService.IsAuthenticated
               && !string.IsNullOrWhiteSpace(_authService.CurrentAccessToken)
               && !string.IsNullOrWhiteSpace(userId)
               && !string.IsNullOrWhiteSpace(_options.Url)
               && !string.IsNullOrWhiteSpace(_options.AnonKey);
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("apikey", _options.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.CurrentAccessToken);
    }

    private string BuildUrl(string relativePath)
    {
        var baseUrl = _options.Url.TrimEnd('/');
        return $"{baseUrl}/{relativePath}";
    }
}

internal sealed class SupabaseSettingsRow
{
    [JsonPropertyName("theme")]
    public string? Theme { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("tutorial_completed")]
    public bool TutorialCompleted { get; set; }
}

internal sealed class SupabaseSettingsWriteRow
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "light";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("tutorial_completed")]
    public bool TutorialCompleted { get; set; }
}
