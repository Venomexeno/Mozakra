using Mozakra.Core.Models;

namespace Mozakra.Core.Tests.Scheduling;

public sealed class FakeClock : IClock
{
    public FakeClock(DateOnly today) => Today = today;

    public DateOnly Today { get; set; }
    public DateTime UtcNow => Today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
}
