using Kairos.Shared.Models;

namespace Kairos.Shared.Services;

/// <summary>
/// Persists non-event account metadata (for example timeline settings) in Supabase.
/// </summary>
public interface ISupabaseTimeAccountStore
{
    Task<TimeAccount?> LoadAccountAsync();
    Task SaveAccountAsync(TimeAccount account);
}
