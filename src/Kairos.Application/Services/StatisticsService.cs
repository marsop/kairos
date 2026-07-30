using Kairos.Core.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kairos.Application.Services;

public class StatisticsService : IStatisticsService
{
    private const string StorageKey = "Kairos_budgets";
    private readonly IStorageService _storage;
    private readonly ISupabaseBudgetStore? _supabaseStore;
    private readonly ILogger<StatisticsService> _logger;

    // In-memory cache
    private List<ActivityBudget> _budgets = new();
    private bool _isLoaded = false;

    public StatisticsService(
        IStorageService storage,
        ILogger<StatisticsService>? logger = null,
        ISupabaseBudgetStore? supabaseStore = null)
    {
        _storage = storage;
        _logger = logger ?? NullLogger<StatisticsService>.Instance;
        _supabaseStore = supabaseStore;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;

        var json = await _storage.GetItemAsync(StorageKey);
        if (!string.IsNullOrEmpty(json))
        {
            _budgets = JsonSerializer.Deserialize<List<ActivityBudget>>(json) ?? new();
        }

        if (_supabaseStore != null)
        {
            try
            {
                var remoteBudgets = (await _supabaseStore.LoadBudgetsAsync()).ToList();

                if (remoteBudgets.Count > 0)
                {
                    _budgets = remoteBudgets;
                    await SaveLocalAsync();
                }
                else if (_budgets.Count > 0)
                {
                    await _supabaseStore.SaveBudgetsAsync(_budgets);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to synchronize budgets from Supabase.");
            }
        }

        _isLoaded = true;
    }

    private async Task SaveChangesAsync()
    {
        await SaveLocalAsync();

        if (_supabaseStore != null)
        {
            try
            {
                await _supabaseStore.SaveBudgetsAsync(_budgets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save budgets to Supabase.");
            }
        }
    }

    private async Task SaveLocalAsync()
    {
        var json = JsonSerializer.Serialize(_budgets);
        await _storage.SetItemAsync(StorageKey, json);
    }

    public async Task<List<ActivityBudget>> GetBudgetsAsync()
    {
        await EnsureLoadedAsync();
        return _budgets.ToList();
    }

    public async Task SaveBudgetAsync(ActivityBudget budget)
    {
        await EnsureLoadedAsync();

        // Remove any existing budget for the same activity and type
        var existingByType = _budgets.FirstOrDefault(b => b.ActivityId == budget.ActivityId && b.Type == budget.Type);
        if (existingByType != null && existingByType.Id != budget.Id)
        {
            _budgets.Remove(existingByType);
        }

        var existing = _budgets.FirstOrDefault(b => b.Id == budget.Id);
        if (existing != null)
        {
            _budgets.Remove(existing);
        }

        _budgets.Add(budget);
        await SaveChangesAsync();
    }

    public async Task DeleteBudgetAsync(Guid budgetId)
    {
        await EnsureLoadedAsync();
        var existing = _budgets.FirstOrDefault(b => b.Id == budgetId);
        if (existing != null)
        {
            _budgets.Remove(existing);
            await SaveChangesAsync();
        }
    }

    public async Task<ActivityBudget?> GetBudgetAsync(Guid activityId, BudgetType type)
    {
        await EnsureLoadedAsync();
        return _budgets.FirstOrDefault(b =>
            b.ActivityId == activityId &&
            b.Type == type);
    }
}
