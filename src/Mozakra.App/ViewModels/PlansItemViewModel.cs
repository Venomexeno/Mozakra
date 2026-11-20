using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Mozakra.App.Localization;
using Mozakra.Core.Models;
using Mozakra.Core.Scheduling;

namespace Mozakra.App.ViewModels;

/// <summary>One row of the plans list: color, name, kind, progress, deadline and health.</summary>
public sealed class PlansItemViewModel
{
    public PlansItemViewModel(StudyPlan plan, double progressPercent, PlanHealth? health, CultureInfo culture)
    {
        Plan = plan;
        Id = plan.Id;
        Name = plan.Name;
        ColorBrush = ToBrush(plan.ColorHex);
        KindText = Loc.T(plan.Kind == PlanKind.Course ? "Screens.Kind.Course" : "Screens.Kind.Work");
        Progress = Math.Clamp(progressPercent, 0, 100);
        ProgressText = $"{Math.Floor(Progress).ToString(CultureInfo.InvariantCulture)}%";
        DeadlineText = plan.Deadline is { } deadline
            ? Loc.T("Screens.Plans.DeadlineFormat", deadline.ToString("d MMM yyyy", culture))
            : Loc.T("Screens.Plans.NoDeadline");
        Health = health;
        HealthText = health is { } h ? Loc.T($"Screens.Health.{h}") : Loc.T("Screens.Plans.Archived");
        HealthBrush = HealthToBrush(health);
    }

    public StudyPlan Plan { get; }
    public Guid Id { get; }
    public string Name { get; }
    public Brush ColorBrush { get; }
    public string KindText { get; }
    public double Progress { get; }
    public string ProgressText { get; }
    public string DeadlineText { get; }
    public PlanHealth? Health { get; }
    public string HealthText { get; }
    public Brush HealthBrush { get; }

    public static Brush ToBrush(string? hex)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex ?? "#FFF3A3"));
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return Brushes.LightYellow;
        }
    }

    private static Brush HealthToBrush(PlanHealth? health)
    {
        var key = health switch
        {
            PlanHealth.OnTrack or PlanHealth.Completed => "Success",
            PlanHealth.AtRisk => "Warning",
            PlanHealth.Impossible or PlanHealth.Overdue => "Accent",
            _ => "Muted",
        };
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }
}
