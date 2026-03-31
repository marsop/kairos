namespace Kairos.Shared.Services;

/// <summary>
/// Persists the cross-device user settings in Supabase.
/// </summary>
public interface ISupabaseSettingsStore
{
    Task<SyncedSettingsData?> LoadSettingsAsync();
    Task SaveSettingsAsync(SyncedSettingsData settings);
}
