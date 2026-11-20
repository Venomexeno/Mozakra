namespace Mozakra.App.ViewModels;

/// <summary>One bar in the details week view.</summary>
public sealed record NoteWeekDay(
    DateOnly Date,
    string Label,
    string ValueText,
    double LoggedHeight,
    double CapacityHeight,
    bool IsToday,
    bool IsFuture,
    bool GoalMet,
    string Tooltip);
