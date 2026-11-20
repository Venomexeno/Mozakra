namespace Mozakra.App.ViewModels;

/// <summary>An Impossible/Overdue plan: needed vs available time and the actions.</summary>
public sealed record NoteWarning(
    Guid PlanId,
    string Title,
    string Message,
    DateOnly? SuggestedDeadline,
    string ExtendText)
{
    public bool CanExtend => SuggestedDeadline is not null;
}
