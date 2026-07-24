using Kairos.Core.Models;

namespace Kairos.Application.Services;

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
    /// Gets the last comment used for a specific activity.
    /// </summary>
    List<string> GetLastCommentsForActivity(Guid activityId, int count = 3);

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
    /// Gets timeline segments for the specified period.
    /// </summary>
    List<TimelineSegment> GetTimelineData(TimeSpan period);

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
    void UpdateEventDetails(Guid eventId, DateTimeOffset newStartTime, DateTimeOffset newEndTime, string comment);

    /// <summary>
    /// Updates the comment of an event.
    /// </summary>
    /// <param name="eventId">The ID of the event to update.</param>
    /// <param name="comment">The new comment.</param>
    void UpdateEventComment(Guid eventId, string comment);

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
    /// Exports all events whose local start date matches the specified day as CSV.
    /// </summary>
    string ExportDayAsCsv(DateOnly day);

    /// <summary>
    /// Exports all activities as CSV.
    /// </summary>
    string ExportActivitiesAsCsv();

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
    /// Updates the editable properties of an activity.
    /// </summary>
    /// <param name="activityId">The ID of the activity to update.</param>
    /// <param name="newName">The new name (1-40 characters).</param>
    /// <param name="newColor">The new color in #RRGGBB format.</param>
    /// <param name="emoji">Optional emoji string.</param>
    /// <param name="metadata">Optional metadata string.</param>
    void UpdateActivity(Guid activityId, string newName, string newColor, string emoji = "", string metadata = "");

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
    /// Adds a new activity with the specified name and color.
    /// </summary>
    /// <param name="name">Name of the activity.</param>
    /// <param name="color">Hex color in #RRGGBB format.</param>
    /// <param name="emoji">Optional emoji string.</param>
    /// <param name="metadata">Optional metadata string.</param>
    void AddActivity(string name, string color, string emoji = "", string metadata = "");

    /// <summary>
    /// Adds a new activity with the specified name and color to a specific group.
    /// </summary>
    void AddActivity(string name, string color, string emoji, string metadata, int groupId);
    /// <summary>
    /// Resets all data to initial state (default activities, no history).
    /// </summary>
    Task ResetDataAsync();

    /// <summary>
    /// Reorders the activities based on the provided list of activity IDs.
    /// </summary>
    /// <param name="orderedActivityIds">The list of activity IDs in the desired order.</param>
    void ReorderActivities(List<Guid> orderedActivityIds);

    /// <summary>
    /// Replaces the local events with the ones from the server.
    /// </summary>
    void UpdateEventsFromServer(IReadOnlyList<ActivityEvent> serverEvents);
}

/// <summary>
/// Represents a point on the timeline graph.
/// </summary>
public class TimelineDataPoint
{
    public DateTimeOffset Timestamp { get; set; }
    public double BalanceHours { get; set; }
}

/// <summary>
/// Represents a segment on the timeline graph.
/// </summary>
public class TimelineSegment
{
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public double StartBalance { get; set; }
    public double EndBalance { get; set; }
    public string Color { get; set; } = "#ffffff";
}
