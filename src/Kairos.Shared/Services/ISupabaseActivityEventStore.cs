using Kairos.Shared.Models;

namespace Kairos.Shared.Services;

/// <summary>
/// Persists the individual activity events in Supabase.
/// </summary>
public interface ISupabaseActivityEventStore
{
    Task<IReadOnlyList<ActivityEvent>> LoadEventsAsync();
    Task SaveEventsAsync(IReadOnlyList<ActivityEvent> events);
}
