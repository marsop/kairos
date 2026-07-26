using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Kairos.Application.Services;
using Kairos.Core.Models;

namespace Kairos.Infrastructure.Services;

public sealed class SupabaseBudgetSettingsStore : ISupabaseBudgetSettingsStore
{
    private readonly HttpClient _httpClient;
    private readonly ISupabaseAuthService _authService;
    private readonly SupabaseAuthOptions _options;

    public SupabaseBudgetSettingsStore(HttpClient httpClient, ISupabaseAuthService authService, IOptions<SupabaseAuthOptions> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _options = options.Value;
    }

    public async Task<BudgetSettingsData?> LoadSettingsAsync()
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
            BuildUrl($"rest/v1/budget_settings?select=minimum_enabled,threshold,color_minimum_not_reached,color_minimum_reached_max_not_reached,color_between_threshold_max,color_over_max,budget_type,notifications_enabled&user_id=eq.{Uri.EscapeDataString(userId!)}&limit=1"));

        AddHeaders(request);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<SupabaseBudgetSettingsRow>>() ?? new List<SupabaseBudgetSettingsRow>();
        var row = rows.FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        return new BudgetSettingsData
        {
            MinimumEnabled = row.MinimumEnabled,
            Threshold = row.Threshold,
            ColorMinimumNotReached = row.ColorMinimumNotReached ?? "#0000ff",
            ColorMinimumReachedMaxNotReached = row.ColorMinimumReachedMaxNotReached ?? "#00ff00",
            ColorBetweenThresholdMax = row.ColorBetweenThresholdMax ?? "#ffff00",
            ColorOverMax = row.ColorOverMax ?? "#ff0000",
            BudgetType = (BudgetType)row.BudgetType,
            NotificationsEnabled = row.NotificationsEnabled
        };
    }

    public async Task SaveSettingsAsync(BudgetSettingsData settings)
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

        var row = new SupabaseBudgetSettingsWriteRow
        {
            UserId = userId!,
            MinimumEnabled = settings.MinimumEnabled,
            Threshold = settings.Threshold,
            ColorMinimumNotReached = settings.ColorMinimumNotReached,
            ColorMinimumReachedMaxNotReached = settings.ColorMinimumReachedMaxNotReached,
            ColorBetweenThresholdMax = settings.ColorBetweenThresholdMax,
            ColorOverMax = settings.ColorOverMax,
            BudgetType = (int)settings.BudgetType,
            NotificationsEnabled = settings.NotificationsEnabled
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUrl("rest/v1/budget_settings?on_conflict=user_id"));

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

internal sealed class SupabaseBudgetSettingsRow
{
    [JsonPropertyName("minimum_enabled")]
    public bool MinimumEnabled { get; set; }

    [JsonPropertyName("threshold")]
    public int Threshold { get; set; }

    [JsonPropertyName("color_minimum_not_reached")]
    public string? ColorMinimumNotReached { get; set; }

    [JsonPropertyName("color_minimum_reached_max_not_reached")]
    public string? ColorMinimumReachedMaxNotReached { get; set; }

    [JsonPropertyName("color_between_threshold_max")]
    public string? ColorBetweenThresholdMax { get; set; }

    [JsonPropertyName("color_over_max")]
    public string? ColorOverMax { get; set; }

    [JsonPropertyName("budget_type")]
    public int BudgetType { get; set; }

    [JsonPropertyName("notifications_enabled")]
    public bool NotificationsEnabled { get; set; }
}

internal sealed class SupabaseBudgetSettingsWriteRow
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("minimum_enabled")]
    public bool MinimumEnabled { get; set; }

    [JsonPropertyName("threshold")]
    public int Threshold { get; set; }

    [JsonPropertyName("color_minimum_not_reached")]
    public string ColorMinimumNotReached { get; set; } = string.Empty;

    [JsonPropertyName("color_minimum_reached_max_not_reached")]
    public string ColorMinimumReachedMaxNotReached { get; set; } = string.Empty;

    [JsonPropertyName("color_between_threshold_max")]
    public string ColorBetweenThresholdMax { get; set; } = string.Empty;

    [JsonPropertyName("color_over_max")]
    public string ColorOverMax { get; set; } = string.Empty;

    [JsonPropertyName("budget_type")]
    public int BudgetType { get; set; }

    [JsonPropertyName("notifications_enabled")]
    public bool NotificationsEnabled { get; set; }
}
