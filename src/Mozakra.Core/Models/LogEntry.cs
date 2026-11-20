namespace Mozakra.Core.Models;

public sealed class LogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public DateOnly Date { get; set; }
    public int Minutes { get; set; }       // can be negative (manual correction)
    public int VideosDone { get; set; }    // videos completed in this entry (courses)
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
