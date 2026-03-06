using Kairos.Shared.Models;
using System.Text.Json;

namespace Kairos.Shared.Services;

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
        
        // Validation: Check for overlap with existing budgets for the same activity
        var overlapping = _budgets.Any(b => 
            b.Id != budget.Id && 
            b.ActivityId == budget.ActivityId && 
            b.StartDate <= budget.EndDate && 
            budget.StartDate <= b.EndDate);
            
        if (overlapping)
        {
            throw new InvalidOperationException("Budgets for the same activity cannot overlap in time.");
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

    public async Task<ActivityBudget?> GetBudgetForPeriodAsync(Guid activityId, DateOnly startDate, DateOnly endDate)
    {
        await EnsureLoadedAsync();
        return _budgets.FirstOrDefault(b => 
            b.ActivityId == activityId && 
            b.StartDate <= endDate && 
            startDate <= b.EndDate);
    }
}
