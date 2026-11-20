namespace Mozakra.Core.Models;

public sealed class AvailabilityException
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }       // inclusive
    public int Minutes { get; set; }       // 0 = vacation
    public string? Label { get; set; }
}
