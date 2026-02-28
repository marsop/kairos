using Kairos.Shared.Models;

namespace Kairos.Shared.Services;

/// <summary>
/// Interface for time tracking operations.
/// </summary>
public interface ITimeTrackingService
{
    /// <summary>
    /// Gets the current time account state.
    /// </summary>
    TimeAccount Account { get; }

    /// <summary>
    /// Gets or sets the current timeline period.
    /// </summary>
    TimeSpan TimelinePeriod { get; set; }
    
    /// <summary>
    /// Gets the current balance, calculated in real-time.
    /// </summary>
    TimeSpan GetCurrentBalance();
    
    /// <summary>
    /// Gets the currently active activity event, if any.
    /// </summary>
    ActivityEvent? GetActiveEvent();
    
    /// <summary>
    /// Activates a activity by its ID. Deactivates any currently active activity first.
    /// </summary>
    /// <param name="activityId">The ID of the activity to activate.</param>
    /// <param name="comment">A required comment between 1 and 250 characters.</param>
    void ActivateActivity(Guid activityId, string comment);
    
    /// <summary>
    /// Deactivates the currently active activity.
    /// </summary>
    void DeactivateActivity();
    
    /// <summary>
    /// Gets timeline data points for the specified period.
    /// </summary>
    List<TimelineDataPoint> GetTimelineData(TimeSpan period);
    
    /// <summary>
    /// Deletes an event by its ID. Updates balance and triggers state change.
    /// </summary>
    void DeleteEvent(Guid eventId);
    
    /// <summary>
    /// Updates the start and end times of a non-active event.
    /// </summary>
    /// <param name="eventId">The ID of the event to update.</param>
    /// <param name="newStartTime">The new start time.</param>
    /// <param name="newEndTime">The new end time.</param>
    /// <exception cref="InvalidOperationException">Thrown when the event is active.</exception>
    /// <exception cref="ArgumentException">Thrown when newStartTime >= newEndTime or newEndTime is in the future.</exception>
    void UpdateEventTimes(Guid eventId, DateTimeOffset newStartTime, DateTimeOffset newEndTime);
    
    /// <summary>
    /// Saves the current state to persistent storage.
    /// </summary>
    Task SaveAsync();
    
    /// <summary>
    /// Loads the state from persistent storage.
    /// </summary>
    Task LoadAsync();
    
    /// <summary>
    /// Event raised when the account state changes.
    /// </summary>
    event Action? OnStateChanged;
    
    /// <summary>
    /// Exports all data (activities and events) as a JSON string.
    /// </summary>
    string ExportData();
    
    /// <summary>
    /// Imports data from a JSON string, replacing current activities and events.
    /// </summary>
    Task ImportDataAsync(string json);
    
    /// <summary>
    /// Renames a activity. Only affects future events; existing events keep the old name.
    /// </summary>
    /// <param name="activityId">The ID of the activity to rename.</param>
    /// <param name="newName">The new name (1-40 characters).</param>
    /// <exception cref="ArgumentException">Thrown when the name is invalid.</exception>
    void RenameActivity(Guid activityId, string newName);

    /// <summary>
    /// Deletes a activity by its ID.
    /// </summary>
    /// <param name="activityId">The ID of the activity to delete.</param>
    /// <exception cref="InvalidOperationException">Thrown when trying to delete an active activity.</exception>
    void DeleteActivity(Guid activityId);

    /// <summary>
    /// Adds a new activity with the specified name.
    /// </summary>
    /// <param name="name">Name of the activity.</param>
    /// <exception cref="ArgumentException">Thrown when name is invalid.</exception>
    void AddActivity(string name);
    /// <summary>
    /// Resets all data to initial state (default activities, no history).
    /// </summary>
    Task ResetDataAsync();

    /// <summary>
    /// Reorders the activities based on the provided list of activity IDs.
    /// </summary>
    /// <param name="orderedActivityIds">The list of activity IDs in the desired order.</param>
    void ReorderActivities(List<Guid> orderedActivityIds);
}

/// <summary>
/// Represents a point on the timeline graph.
/// </summary>
public class TimelineDataPoint
{
    public DateTimeOffset Timestamp { get; set; }
    public double BalanceHours { get; set; }
}
