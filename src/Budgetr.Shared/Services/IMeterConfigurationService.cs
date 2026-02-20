using Budgetr.Shared.Models;

namespace Budgetr.Shared.Services;

/// <summary>
/// Interface for loading meter configuration.
/// </summary>
public interface IMeterConfigurationService
{
    /// <summary>
    /// Loads meters from configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when duplicate meter factors are detected.</exception>
    Task<List<Meter>> LoadMetersAsync();
}
