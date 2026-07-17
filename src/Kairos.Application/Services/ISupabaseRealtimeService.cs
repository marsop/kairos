namespace Kairos.Application.Services;

/// <summary>
/// Provides Supabase Realtime notifications for synchronized tables.
/// </summary>
public interface ISupabaseRealtimeService
{
    event Action<string>? OnTableChanged;
    event Action? OnConnected;
    Task InitializeAsync();
}
