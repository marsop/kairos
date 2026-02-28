using Kairos.Shared.Models;

namespace Kairos.Shared.Services;

/// <summary>
/// Interface for loading activity configuration.
/// </summary>
public interface IActivityConfigurationService
{
    /// <summary>
    /// Loads activities from configuration.
    /// </summary>
    Task<List<Activity>> LoadActivitiesAsync();
}
