namespace Mozakra.Core.Models;

public sealed class SystemClock : IClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
    public DateTime UtcNow => DateTime.UtcNow;
}
