namespace Mozakra.Core.Models;

public interface IClock
{
    DateOnly Today { get; }
    DateTime UtcNow { get; }
}
