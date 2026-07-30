using Kairos.Core.Models;

namespace Kairos.Application.Services;

/// <summary>
/// Persists activity budgets in Supabase for cross-device synchronization.
/// </summary>
public interface ISupabaseBudgetStore
{
    Task<IReadOnlyList<ActivityBudget>> LoadBudgetsAsync();
    Task SaveBudgetsAsync(IReadOnlyList<ActivityBudget> budgets);
}
