using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kairos.Shared.Models;
using Microsoft.Extensions.Options;

namespace Kairos.Shared.Services;

public sealed class SupabaseActivityStore : ISupabaseActivityStore
{
    private readonly HttpClient _httpClient;
    private readonly ISupabaseAuthService _authService;
    private readonly SupabaseAuthOptions _options;

    public SupabaseActivityStore(HttpClient httpClient, ISupabaseAuthService authService, IOptions<SupabaseAuthOptions> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Activity>> LoadActivitiesAsync()
    {
        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return Array.Empty<Activity>();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUrl($"rest/v1/activities?select=id,name,display_order&user_id=eq.{Uri.EscapeDataString(userId!)}&order=display_order.asc"));

        AddHeaders(request);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<SupabaseActivityRow>>() ?? new List<SupabaseActivityRow>();
        return rows
            .Where(m => m.Id != Guid.Empty && !string.IsNullOrWhiteSpace(m.Name))
            .Select(m => new Activity
            {
                Id = m.Id,
                Name = m.Name!,
                Factor = 1.0,
                DisplayOrder = m.DisplayOrder
            })
            .ToList();
    }

    public async Task SaveActivitiesAsync(IReadOnlyList<Activity> activities)
    {
        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return;
        }

        var rows = activities
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new SupabaseActivityWriteRow
            {
                Id = m.Id,
                UserId = userId!,
                Name = m.Name,
                DisplayOrder = m.DisplayOrder
            })
            .ToList();

        using (var upsertRequest = new HttpRequestMessage(
                   HttpMethod.Post,
                   BuildUrl("rest/v1/activities?on_conflict=user_id,id")))
        {
            AddHeaders(upsertRequest);
            upsertRequest.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
            upsertRequest.Content = JsonContent.Create(rows);

            using var upsertResponse = await _httpClient.SendAsync(upsertRequest);
            upsertResponse.EnsureSuccessStatusCode();
        }

        if (rows.Count == 0)
        {
            using var clearRequest = new HttpRequestMessage(
                HttpMethod.Delete,
                BuildUrl($"rest/v1/activities?user_id=eq.{Uri.EscapeDataString(userId!)}"));
            AddHeaders(clearRequest);
            using var clearResponse = await _httpClient.SendAsync(clearRequest);
            clearResponse.EnsureSuccessStatusCode();
            return;
        }

        var idList = string.Join(",", rows.Select(m => m.Id));
        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            BuildUrl($"rest/v1/activities?user_id=eq.{Uri.EscapeDataString(userId!)}&id=not.in.({idList})"));
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

internal sealed class SupabaseActivityRow
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("display_order")]
    public int DisplayOrder { get; set; }
}

internal sealed class SupabaseActivityWriteRow
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("display_order")]
    public int DisplayOrder { get; set; }
}
