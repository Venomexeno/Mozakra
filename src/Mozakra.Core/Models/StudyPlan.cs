namespace Mozakra.Core.Models;

public sealed class StudyPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public PlanKind Kind { get; set; }
    public string ColorHex { get; set; } = "#FFF3A3";
    public DateOnly StartDate { get; set; }
    public DateOnly? Deadline { get; set; }              // null = no deadline (shares leftover time)
    public Guid? StartsAfterPlanId { get; set; }         // waits until that plan is complete
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Course
    public int TotalVideos { get; set; }
    public int VideosDone { get; set; }
    public int TotalVideoMinutes { get; set; }

    // Work
    public int EstimatedMinutes { get; set; }
    public int? RemainingOverrideMinutes { get; set; }   // set by "Adjust remaining"
    public DateOnly? RemainingOverrideSetOn { get; set; } // logs on/after this date reduce the override

    public List<ChecklistItem> Checklist { get; set; } = new();
}
