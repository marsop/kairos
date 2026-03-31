namespace Kairos.Shared.Services;

/// <summary>
/// Provides Supabase Realtime notifications for synchronized tables.
/// </summary>
public interface ISupabaseRealtimeService
{
    event Action<string>? OnTableChanged;
    Task InitializeAsync();
}
