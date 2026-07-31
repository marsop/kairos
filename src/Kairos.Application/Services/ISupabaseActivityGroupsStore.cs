namespace Kairos.Application.Services;

/// <summary>
/// Persists cross-device activity groups in Supabase.
/// </summary>
public interface ISupabaseActivityGroupsStore
{
    Task<IReadOnlyList<SyncedActivityGroupData>?> LoadGroupsAsync();
    Task SaveGroupsAsync(IReadOnlyList<SyncedActivityGroupData> groups);
}

public sealed class SyncedActivityGroupData
{
    public Guid GroupId { get; set; }
    public int GroupOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#10B981";
    public string Icon { get; set; } = "🗂️";
}