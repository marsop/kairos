using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kairos.Shared.Models;
using Microsoft.Extensions.Options;

namespace Kairos.Shared.Services;

public sealed class SupabaseTimeAccountStore : ISupabaseTimeAccountStore
{
    private readonly HttpClient _httpClient;
    private readonly ISupabaseAuthService _authService;
    private readonly SupabaseAuthOptions _options;

    public SupabaseTimeAccountStore(HttpClient httpClient, ISupabaseAuthService authService, IOptions<SupabaseAuthOptions> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _options = options.Value;
    }

    public async Task<TimeAccount?> LoadAccountAsync()
    {
        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUrl($"rest/v1/time_accounts?select=payload&user_id=eq.{Uri.EscapeDataString(userId!)}&limit=1"));

        AddHeaders(request);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<SupabaseTimeAccountRow>>() ?? new List<SupabaseTimeAccountRow>();
        var payload = rows.FirstOrDefault()?.Payload;
        if (payload is null || payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return JsonSerializer.Deserialize<TimeAccount>(payload.Value.GetRawText());
    }

    public async Task SaveAccountAsync(TimeAccount account)
    {
        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return;
        }

        var row = new SupabaseTimeAccountWriteRow
        {
            UserId = userId!,
            Payload = JsonSerializer.SerializeToElement(new SupabaseTimeAccountPayload
            {
                Events = account.Events,
                TimelinePeriod = account.TimelinePeriod
            })
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUrl("rest/v1/time_accounts?on_conflict=user_id"));

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

internal sealed class SupabaseTimeAccountRow
{
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }
}

internal sealed class SupabaseTimeAccountWriteRow
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}

internal sealed class SupabaseTimeAccountPayload
{
    [JsonPropertyName("events")]
    public List<ActivityEvent> Events { get; set; } = new();

    [JsonPropertyName("timelinePeriod")]
    public TimeSpan TimelinePeriod { get; set; } = TimeSpan.FromHours(24);
}
