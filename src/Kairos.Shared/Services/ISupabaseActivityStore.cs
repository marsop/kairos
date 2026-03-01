using Kairos.Shared.Models;

namespace Kairos.Shared.Services;

/// <summary>
/// Persists user activities in Supabase.
/// </summary>
public interface ISupabaseActivityStore
{
    Task<IReadOnlyList<Activity>> LoadActivitiesAsync();
    Task SaveActivitiesAsync(IReadOnlyList<Activity> activities);
}
