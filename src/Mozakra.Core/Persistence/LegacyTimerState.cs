namespace Mozakra.Core.Persistence;

/// <summary>The old Study Note timer (part of <see cref="LegacyStudyNoteData"/>).</summary>
public sealed class LegacyTimerState
{
    public DateTime StartedAtUtc { get; set; }
    public double AccumulatedMs { get; set; }
    public bool Running { get; set; }
}
