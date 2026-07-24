using Kairos.Core.Models;
using System.Text.Json;

namespace Kairos.Application.Services;

public class StatisticsService : IStatisticsService
{
    private const string StorageKey = "Kairos_budgets";
    private readonly IStorageService _storage;

    // In-memory cache
    private List<ActivityBudget> _budgets = new();
    private bool _isLoaded = false;

    public StatisticsService(IStorageService storage)
    {
        _storage = storage;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;

        var json = await _storage.GetItemAsync(StorageKey);
        if (!string.IsNullOrEmpty(json))
        {
            _budgets = JsonSerializer.Deserialize<List<ActivityBudget>>(json) ?? new();
        }
        _isLoaded = true;
    }

    private async Task SaveChangesAsync()
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
