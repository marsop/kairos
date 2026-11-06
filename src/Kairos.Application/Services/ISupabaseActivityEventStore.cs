using Kairos.Core.Models;

namespace Kairos.Application.Services;

/// <summary>
/// Persists the individual activity events in Supabase.
/// </summary>
public interface ISupabaseActivityEventStore
{
    Task<IReadOnlyList<ActivityEvent>> LoadEventsAsync(ActivityEventQueryOptions? options = null);
    Task SaveEventsAsync(IReadOnlyList<ActivityEvent> events, ActivityEventQueryOptions? scopedWindow = null);
}
