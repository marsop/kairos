using Kairos.Core.Models;

namespace Kairos.Application.Services;

public interface IStatisticsService
{
    Task<List<ActivityBudget>> GetBudgetsAsync();
    Task SaveBudgetAsync(ActivityBudget budget);
    Task DeleteBudgetAsync(Guid budgetId);

    /// <summary>
    /// Gets the budget for a specific activity and type.
    /// Assumes max 1 budget per activity and type.
    /// </summary>
    Task<ActivityBudget?> GetBudgetAsync(Guid activityId, BudgetType type);
}
