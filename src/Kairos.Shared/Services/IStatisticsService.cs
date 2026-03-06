using Kairos.Shared.Models;

namespace Kairos.Shared.Services;

public interface IStatisticsService
{
    Task<List<ActivityBudget>> GetBudgetsAsync();
    Task SaveBudgetAsync(ActivityBudget budget);
    Task DeleteBudgetAsync(Guid budgetId);
    
    /// <summary>
    /// Gets the active budget for a specific activity that intersects with the given date range.
    /// Assumes max 1 active budget per activity at a time.
    /// </summary>
    Task<ActivityBudget?> GetBudgetForPeriodAsync(Guid activityId, DateOnly startDate, DateOnly endDate);
}
