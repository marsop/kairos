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
            BuildUrl($"rest/v1/activity_events?select=id,activity_id,start_time,end_time,activity_name,activity_emoji,activity_color,comment,metadata&user_id=eq.{Uri.EscapeDataString(userId!)}&order=start_time.desc"));

        AddHeaders(request);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<SupabaseActivityEventRow>>() ?? new List<SupabaseActivityEventRow>();

        return rows.Select(r => new ActivityEvent
        {
            Id = r.Id,
            ActivityId = r.ActivityId ?? Guid.Empty,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            ActivityName = r.ActivityName,
            ActivityEmoji = r.ActivityEmoji,
            ActivityColor = Activity.SanitizeColor(r.ActivityColor),
            Comment = r.Comment,
            Metadata = r.Metadata
        }).OrderBy(e => e.StartTime).ToList();
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
            ActivityId = e.ActivityId == Guid.Empty ? null : e.ActivityId,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            ActivityName = e.ActivityName,
            ActivityEmoji = e.ActivityEmoji,
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

        // Query existing IDs to determine what to delete, avoiding 'URI Too Long' exceptions
        using var fetchIdsRequest = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUrl($"rest/v1/activity_events?select=id&user_id=eq.{Uri.EscapeDataString(userId!)}&order=start_time.desc"));
        AddHeaders(fetchIdsRequest);
        using var fetchIdsResponse = await _httpClient.SendAsync(fetchIdsRequest);
        fetchIdsResponse.EnsureSuccessStatusCode();

        var existingRows = await fetchIdsResponse.Content.ReadFromJsonAsync<List<SupabaseActivityEventRow>>() ?? new List<SupabaseActivityEventRow>();

        var localIds = rows.Select(m => m.Id).ToHashSet();
        var toDelete = existingRows.Select(r => r.Id).Where(id => !localIds.Contains(id)).ToList();

        // Perform chunked deletes to keep URIs reasonably sized
        const int batchSize = 50;
        for (int i = 0; i < toDelete.Count; i += batchSize)
        {
            var chunk = toDelete.Skip(i).Take(batchSize);
            var idList = string.Join(",", chunk);
            using var deleteRequest = new HttpRequestMessage(
                HttpMethod.Delete,
                BuildUrl($"rest/v1/activity_events?user_id=eq.{Uri.EscapeDataString(userId!)}&id=in.({idList})"));
            AddHeaders(deleteRequest);
            using var deleteResponse = await _httpClient.SendAsync(deleteRequest);
            deleteResponse.EnsureSuccessStatusCode();
        }
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

    [JsonPropertyName("activity_emoji")]
    public string ActivityEmoji { get; set; } = string.Empty;

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

    [JsonPropertyName("activity_emoji")]
    public string ActivityEmoji { get; set; } = string.Empty;

    [JsonPropertyName("activity_color")]
    public string ActivityColor { get; set; } = string.Empty;

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public string Metadata { get; set; } = string.Empty;
}
