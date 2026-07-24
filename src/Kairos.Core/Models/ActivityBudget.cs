namespace Kairos.Core.Models;

public enum BudgetType
{
    Monthly = 0,
    Weekly = 1,
    Daily = 2
}

public class ActivityBudget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivityId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeSpan AllocatedTimeSpan { get; set; }
    public BudgetType Type { get; set; } = BudgetType.Monthly;
}
