using Kairos.Core.Models;

namespace Kairos.Application.Services;

/// <summary>
/// Persists the cross-device user budget settings in Supabase.
/// </summary>
public interface ISupabaseBudgetSettingsStore
{
    Task<BudgetSettingsData?> LoadSettingsAsync();
    Task SaveSettingsAsync(BudgetSettingsData settings);
}

public class BudgetSettingsData
{
    public bool MinimumEnabled { get; set; } = false;
    public int Threshold { get; set; } = 95;
    public string ColorMinimumNotReached { get; set; } = "#0000ff";
    public string ColorMinimumReachedMaxNotReached { get; set; } = "#00ff00";
    public string ColorBetweenThresholdMax { get; set; } = "#ffff00";
    public string ColorOverMax { get; set; } = "#ff0000";
    public BudgetType BudgetType { get; set; } = BudgetType.Monthly;
    public bool NotificationsEnabled { get; set; } = true;
}
