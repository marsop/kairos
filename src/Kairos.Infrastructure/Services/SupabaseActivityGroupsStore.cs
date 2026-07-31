using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kairos.Application.Services;
using Microsoft.Extensions.Options;

namespace Kairos.Infrastructure.Services;

public sealed class SupabaseActivityGroupsStore : ISupabaseActivityGroupsStore
{
    private readonly HttpClient _httpClient;
    private readonly ISupabaseAuthService _authService;
    private readonly SupabaseAuthOptions _options;

    public SupabaseActivityGroupsStore(HttpClient httpClient, ISupabaseAuthService authService, IOptions<SupabaseAuthOptions> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<SyncedActivityGroupData>?> LoadGroupsAsync()
    {
        if (!await _authService.EnsureAuthenticatedAsync())
        {
            return null;
        }

        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUrl($"rest/v1/user_activity_groups?select=group_id,group_order,name,color,icon&user_id=eq.{Uri.EscapeDataString(userId!)}&order=group_order.asc"));

        AddHeaders(request);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<SupabaseActivityGroupRow>>() ?? [];
        return rows
            .Where(r => r.GroupId != Guid.Empty)
            .Select(r => new SyncedActivityGroupData
            {
                GroupId = r.GroupId,
                GroupOrder = Math.Max(0, r.GroupOrder),
                Name = r.Name ?? string.Empty,
                Color = r.Color ?? "#10B981",
                Icon = string.IsNullOrWhiteSpace(r.Icon) ? "🗂️" : r.Icon
            })
            .OrderBy(r => r.GroupOrder)
            .ToList();
    }

    public async Task SaveGroupsAsync(IReadOnlyList<SyncedActivityGroupData> groups)
    {
        if (!await _authService.EnsureAuthenticatedAsync())
        {
            return;
        }

        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return;
        }

        var normalizedRows = groups
            .OrderBy(g => g.GroupOrder)
            .Select(g => new SupabaseActivityGroupWriteRow
            {
                UserId = userId!,
                GroupId = g.GroupId == Guid.Empty ? Guid.NewGuid() : g.GroupId,
                GroupOrder = Math.Max(0, g.GroupOrder),
                Name = string.IsNullOrWhiteSpace(g.Name) ? string.Empty : g.Name.Trim(),
                Color = string.IsNullOrWhiteSpace(g.Color) ? "#10B981" : g.Color.Trim(),
                Icon = string.IsNullOrWhiteSpace(g.Icon) ? "🗂️" : g.Icon.Trim()
            })
            .ToList();

        using (var deleteRequest = new HttpRequestMessage(
                   HttpMethod.Delete,
                   BuildUrl($"rest/v1/user_activity_groups?user_id=eq.{Uri.EscapeDataString(userId!)}")))
        {
            AddHeaders(deleteRequest);
            deleteRequest.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
            using var deleteResponse = await _httpClient.SendAsync(deleteRequest);
            deleteResponse.EnsureSuccessStatusCode();
        }

        if (normalizedRows.Count == 0)
        {
            return;
        }

        using var insertRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUrl("rest/v1/user_activity_groups?on_conflict=user_id,group_id"));

        AddHeaders(insertRequest);
        insertRequest.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
        insertRequest.Content = JsonContent.Create(normalizedRows);

        using var insertResponse = await _httpClient.SendAsync(insertRequest);
        insertResponse.EnsureSuccessStatusCode();
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

internal sealed class SupabaseActivityGroupRow
{
    [JsonPropertyName("group_id")]
    public Guid GroupId { get; set; }

    [JsonPropertyName("group_order")]
    public int GroupOrder { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

}

internal sealed class SupabaseActivityGroupWriteRow
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("group_id")]
    public Guid GroupId { get; set; }

    [JsonPropertyName("group_order")]
    public int GroupOrder { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#10B981";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "🗂️";

}