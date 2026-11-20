namespace Mozakra.App.ViewModels;

/// <summary>Per-plan status line in the details section.</summary>
public sealed record NotePlanStatusRow(
    Guid PlanId,
    string Name,
    string ColorHex,
    double ProgressPercent,
    string ProgressText,
    string ForecastText,
    string HealthIcon);
