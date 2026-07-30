using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kairos.Application.Services;
using Kairos.Core.Models;
using Microsoft.Extensions.Options;

namespace Kairos.Infrastructure.Services;

public sealed class SupabaseBudgetStore : ISupabaseBudgetStore
{
    private readonly HttpClient _httpClient;
    private readonly ISupabaseAuthService _authService;
    private readonly SupabaseAuthOptions _options;

    public SupabaseBudgetStore(HttpClient httpClient, ISupabaseAuthService authService, IOptions<SupabaseAuthOptions> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ActivityBudget>> LoadBudgetsAsync()
    {
        if (!await _authService.EnsureAuthenticatedAsync())
        {
            return Array.Empty<ActivityBudget>();
        }

        var userId = _authService.CurrentUserId;
        if (!CanSync(userId))
        {
            return Array.Empty<ActivityBudget>();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUrl($"rest/v1/budgets?select=id,activity_id,allocated_time_span,minimum_time_span,budget_type&user_id=eq.{Uri.EscapeDataString(userId!)}"));

        AddHeaders(request);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<SupabaseBudgetRow>>() ?? new List<SupabaseBudgetRow>();
        return rows
            .Where(row => row.Id != Guid.Empty && row.ActivityId != Guid.Empty)
            .Select(row => new ActivityBudget
            {
                Id = row.Id,
                ActivityId = row.ActivityId,
                AllocatedTimeSpan = TimeSpan.FromTicks(row.AllocatedTimeSpanTicks),
                MinimumTimeSpan = TimeSpan.FromTicks(row.MinimumTimeSpanTicks),
                Type = (BudgetType)row.BudgetType
            })
            .ToList();
    }

    public async Task SaveBudgetsAsync(IReadOnlyList<ActivityBudget> budgets)
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

        var rows = budgets
            .Select(budget => new SupabaseBudgetWriteRow
            {
                Id = budget.Id,
                UserId = userId!,
                ActivityId = budget.ActivityId,
                AllocatedTimeSpanTicks = budget.AllocatedTimeSpan.Ticks,
                MinimumTimeSpanTicks = budget.MinimumTimeSpan.Ticks,
                BudgetType = (int)budget.Type
            })
            .ToList();

        using (var upsertRequest = new HttpRequestMessage(
                   HttpMethod.Post,
                   BuildUrl("rest/v1/budgets?on_conflict=user_id,id")))
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
                BuildUrl($"rest/v1/budgets?user_id=eq.{Uri.EscapeDataString(userId!)}"));
            AddHeaders(clearRequest);
            using var clearResponse = await _httpClient.SendAsync(clearRequest);
            clearResponse.EnsureSuccessStatusCode();
            return;
        }

        var idList = string.Join(",", rows.Select(row => row.Id));
        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            BuildUrl($"rest/v1/budgets?user_id=eq.{Uri.EscapeDataString(userId!)}&id=not.in.({idList})"));
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

internal sealed class SupabaseBudgetRow
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("activity_id")]
    public Guid ActivityId { get; set; }

    [JsonPropertyName("allocated_time_span")]
    public long AllocatedTimeSpanTicks { get; set; }

    [JsonPropertyName("minimum_time_span")]
    public long MinimumTimeSpanTicks { get; set; }

    [JsonPropertyName("budget_type")]
    public int BudgetType { get; set; }
}

internal sealed class SupabaseBudgetWriteRow
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("activity_id")]
    public Guid ActivityId { get; set; }

    [JsonPropertyName("allocated_time_span")]
    public long AllocatedTimeSpanTicks { get; set; }

    [JsonPropertyName("minimum_time_span")]
    public long MinimumTimeSpanTicks { get; set; }

    [JsonPropertyName("budget_type")]
    public int BudgetType { get; set; }
}
