using Kairos.Core.Models;

namespace Kairos.Application.Services;

/// <summary>
/// Persists user activities in Supabase.
/// </summary>
public interface ISupabaseActivityStore
{
    Task<IReadOnlyList<Activity>> LoadActivitiesAsync();
    Task SaveActivitiesAsync(IReadOnlyList<Activity> activities);
}
