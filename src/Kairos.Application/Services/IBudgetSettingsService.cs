using Kairos.Core.Models;

namespace Kairos.Application.Services;

public interface IBudgetSettingsService
{
    bool MinimumEnabled { get; set; }
    int Threshold { get; set; }
    string ColorMinimumNotReached { get; set; }
    string ColorMinimumReachedMaxNotReached { get; set; }
    string ColorBetweenThresholdMax { get; set; }
    string ColorOverMax { get; set; }
    BudgetType BudgetType { get; set; }
    bool NotificationsEnabled { get; set; }

    event Action? OnSettingsChanged;

    Task LoadAsync();
    Task SaveAsync();
    Task InitializeDefaultsIfEmptyAsync();
}
