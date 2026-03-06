namespace Kairos.Shared.Models;

public class ActivityBudget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivityId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeSpan AllocatedTimeSpan { get; set; }
}
