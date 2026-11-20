namespace Mozakra.Core.Models;

public sealed class ActiveTimer
{
    public Guid PlanId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public double AccumulatedMs { get; set; }
    public bool Running { get; set; }
}
