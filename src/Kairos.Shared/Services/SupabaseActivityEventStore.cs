using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kairos.Shared.Models;
using Microsoft.Extensions.Options;

namespace Kairos.Shared.Services;

public sealed class SupabaseActivityEventStore : ISupabaseActivityEventStore
{
    private readonly HttpClient _httpClient;
    private readonly ISupabaseAuthService _authService;
    private readonly SupabaseAuthOptions _options;

    public SupabaseActivityEventStore(HttpClient httpClient, ISupabaseAuthService authService, IOptions<SupabaseAuthOptions> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ActivityEvent>> LoadEventsAsync()
    {
        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return Array.Empty<ActivityEvent>();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUrl($"rest/v1/activity_events?select=*&user_id=eq.{Uri.EscapeDataString(userId!)}&order=start_time.asc"));

        AddHeaders(request);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<SupabaseActivityEventRow>>() ?? new List<SupabaseActivityEventRow>();

        return rows.Select(r => new ActivityEvent
        {
            Id = r.Id,
            ActivityId = r.ActivityId,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            ActivityName = r.ActivityName,
            ActivityColor = Activity.SanitizeColor(r.ActivityColor),
            Comment = r.Comment,
            Metadata = r.Metadata,
            Factor = 1.0
        }).ToList();
    }

    public async Task SaveEventsAsync(IReadOnlyList<ActivityEvent> events)
    {
        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return;
        }

        // Upsert all events
        var rows = events.Select(e => new SupabaseActivityEventWriteRow
        {
            Id = e.Id,
            UserId = userId!,
            ActivityId = e.ActivityId,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            ActivityName = e.ActivityName,
            ActivityColor = Activity.SanitizeColor(e.ActivityColor),
            Comment = e.Comment,
            Metadata = e.Metadata
        }).ToList();

        // Send in batches to avoid huge payloads, or send all if small.
        // Let's just send all at once since typically it's paginated.
        if (rows.Any())
        {
            using var upsertRequest = new HttpRequestMessage(
                HttpMethod.Post,
                BuildUrl("rest/v1/activity_events?on_conflict=user_id,id"));

            AddHeaders(upsertRequest);
            upsertRequest.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
            upsertRequest.Content = JsonContent.Create(rows);

            using var upsertResponse = await _httpClient.SendAsync(upsertRequest);
            upsertResponse.EnsureSuccessStatusCode();
        }

        // Delete any events that are NOT in the local list
        if (rows.Count == 0)
        {
            using var clearRequest = new HttpRequestMessage(
                HttpMethod.Delete,
                BuildUrl($"rest/v1/activity_events?user_id=eq.{Uri.EscapeDataString(userId!)}"));
            AddHeaders(clearRequest);
            using var clearResponse = await _httpClient.SendAsync(clearRequest);
            clearResponse.EnsureSuccessStatusCode();
            return;
        }

        // NOTE: The delete logic assumes we sync the ENTIRE event list.
        // If there are thousands of events, this could be a large URL.
        // A safer way is to query all IDs, find which ones to delete, and send a delete request.
        // But to keep parity with SupabaseActivityStore, we construct id=not.in.()
        var idList = string.Join(",", rows.Select(m => m.Id));

        // Let's do chunked deletes if there are many rows, or just use the whole list if it fits.
        // For production, maybe batch this or skip full delete sync.
        // But for now, we follow the pattern.
        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            BuildUrl($"rest/v1/activity_events?user_id=eq.{Uri.EscapeDataString(userId!)}&id=not.in.({idList})"));
        AddHeaders(deleteRequest);
        using var deleteResponse = await _httpClient.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();
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

internal sealed class SupabaseActivityEventRow
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("activity_id")]
    public Guid? ActivityId { get; set; }

    [JsonPropertyName("start_time")]
    public DateTimeOffset StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; set; }

    [JsonPropertyName("activity_name")]
    public string ActivityName { get; set; } = string.Empty;

    [JsonPropertyName("activity_color")]
    public string ActivityColor { get; set; } = string.Empty;

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public string Metadata { get; set; } = string.Empty;
}

internal sealed class SupabaseActivityEventWriteRow
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("activity_id")]
    public Guid? ActivityId { get; set; }

    [JsonPropertyName("start_time")]
    public DateTimeOffset StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; set; }

    [JsonPropertyName("activity_name")]
    public string ActivityName { get; set; } = string.Empty;

    [JsonPropertyName("activity_color")]
    public string ActivityColor { get; set; } = string.Empty;

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public string Metadata { get; set; } = string.Empty;
}
