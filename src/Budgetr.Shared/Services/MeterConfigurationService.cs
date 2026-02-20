using Budgetr.Shared.Models;
using System.Net.Http.Json;

namespace Budgetr.Shared.Services;

/// <summary>
/// Service that loads meter configuration from JSON file.
/// </summary>
public class MeterConfigurationService : IMeterConfigurationService
{
    private readonly HttpClient _httpClient;

    public MeterConfigurationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Meter>> LoadMetersAsync()
    {
        var config = await _httpClient.GetFromJsonAsync<MeterConfiguration>("_content/Budgetr.Shared/meters.json");
        
        if (config?.Meters == null || config.Meters.Count == 0)
        {
            throw new InvalidOperationException("No meters configured in meters.json");
        }

        // Assign display order based on definition order
        var meters = new List<Meter>();
        for (int i = 0; i < config.Meters.Count; i++)
        {
            var meterConfig = config.Meters[i];
            meters.Add(new Meter
            {
                Name = meterConfig.Name,
                Factor = meterConfig.Factor,
                DisplayOrder = i
            });
        }

        return meters;
    }
}

/// <summary>
/// JSON configuration model for meters.
/// </summary>
internal class MeterConfiguration
{
    public List<MeterConfigItem> Meters { get; set; } = new();
}

/// <summary>
/// JSON configuration model for a single meter.
/// </summary>
internal class MeterConfigItem
{
    public string Name { get; set; } = string.Empty;
    public double Factor { get; set; }
}
