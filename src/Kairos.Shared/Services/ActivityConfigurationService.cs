using Kairos.Shared.Models;
using System.Net.Http.Json;

namespace Kairos.Shared.Services;

/// <summary>
/// Service that loads activity configuration from JSON file.
/// </summary>
public class ActivityConfigurationService : IActivityConfigurationService
{
    private readonly HttpClient _httpClient;

    public ActivityConfigurationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Activity>> LoadActivitiesAsync()
    {
        var config = await _httpClient.GetFromJsonAsync<ActivityConfiguration>("_content/Kairos.Shared/activities.json");
        
        if (config?.Activities == null || config.Activities.Count == 0)
        {
            throw new InvalidOperationException("No activities configured in activities.json");
        }

        // Assign display order based on definition order
        var activities = new List<Activity>();
        for (int i = 0; i < config.Activities.Count; i++)
        {
            var activityConfig = config.Activities[i];
            activities.Add(new Activity
            {
                Name = activityConfig.Name,
                DisplayOrder = i
            });
        }

        return activities;
    }
}

/// <summary>
/// JSON configuration model for activities.
/// </summary>
internal class ActivityConfiguration
{
    public List<ActivityConfigItem> Activities { get; set; } = new();
}

/// <summary>
/// JSON configuration model for a single activity.
/// </summary>
internal class ActivityConfigItem
{
    public string Name { get; set; } = string.Empty;
}
