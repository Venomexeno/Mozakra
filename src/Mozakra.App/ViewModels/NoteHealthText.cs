using Mozakra.App.Localization;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>Icon + localized label for a <see cref="PlanHealth"/>.</summary>
public static class NoteHealthText
{
    public static string Icon(PlanHealth health) => health switch
    {
        PlanHealth.OnTrack => "🟢",
        PlanHealth.AtRisk => "🟠",
        PlanHealth.Impossible => "⚠️",
        PlanHealth.Overdue => "⏰",
        PlanHealth.Completed => "🎓",
        PlanHealth.Waiting => "⏳",
        _ => "♾️",
    };

    public static string Label(PlanHealth health) => Loc.T($"Note.Health.{health}");
}
