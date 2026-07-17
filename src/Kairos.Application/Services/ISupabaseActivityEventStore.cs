using Kairos.Core.Models;

namespace Kairos.Application.Services;

/// <summary>
/// Persists the individual activity events in Supabase.
/// </summary>
public interface ISupabaseActivityEventStore
{
    Task<IReadOnlyList<ActivityEvent>> LoadEventsAsync();
    Task SaveEventsAsync(IReadOnlyList<ActivityEvent> events);
}
