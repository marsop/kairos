using Budgetr.Shared.Services;

namespace Budgetr.Web.Services;

/// <summary>
/// Supabase implementation of ISyncProvider.
/// </summary>
public class SupabaseSyncProvider : ISyncProvider
{
    private readonly SupabaseService _supabaseService;

    public string Name => "Supabase";

    public SupabaseSyncProvider(SupabaseService supabaseService)
    {
        _supabaseService = supabaseService;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        await _supabaseService.TryAutoInitializeAsync();
        return await _supabaseService.IsSignedInAsync();
    }

    public async Task<string?> DownloadDataAsync()
    {
        return await _supabaseService.GetLatestBackupContentAsync();
    }

    public async Task UploadDataAsync(string jsonData)
    {
        await _supabaseService.SaveBackupAsync(jsonData);
    }

    public async Task<DateTimeOffset?> GetLastBackupTimeAsync()
    {
        return await _supabaseService.GetBackupLastModifiedAsync();
    }
}
