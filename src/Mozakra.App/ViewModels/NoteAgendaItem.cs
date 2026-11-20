namespace Mozakra.App.ViewModels;

/// <summary>One plan in today's agenda.</summary>
public sealed record NoteAgendaItem(
    Guid PlanId,
    string Name,
    string ColorHex,
    string TargetText,
    string ProgressText,
    double ProgressPercent,
    string HealthIcon,
    string HealthText,
    bool IsDone);
