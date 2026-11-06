namespace Kairos.Core.Models;

/// <summary>
/// Options for bounding and paginating queries for activity events.
/// </summary>
public sealed record ActivityEventQueryOptions
{
    /// <summary>
    /// Lower bound on event start time (inclusive).
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// Upper bound on event start time (inclusive).
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Delta sync filter to return events updated at or after this timestamp (inclusive).
    /// </summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>
    /// Maximum number of events to return.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Number of events to skip for pagination.
    /// </summary>
    public int? Offset { get; init; }
}
