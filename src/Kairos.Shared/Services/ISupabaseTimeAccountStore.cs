using Kairos.Shared.Models;

namespace Kairos.Shared.Services;

/// <summary>
/// Persists the event/timeline portion of a user's time account in Supabase.
/// </summary>
public interface ISupabaseTimeAccountStore
{
    Task<TimeAccount?> LoadAccountAsync();
    Task SaveAccountAsync(TimeAccount account);
}
