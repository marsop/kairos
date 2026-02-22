using Kairos.Shared.Models;

namespace Kairos.Shared.Services;

/// <summary>
/// Interface for loading meter configuration.
/// </summary>
public interface IMeterConfigurationService
{
    /// <summary>
    /// Loads meters from configuration.
    /// </summary>
    Task<List<Meter>> LoadMetersAsync();
}
