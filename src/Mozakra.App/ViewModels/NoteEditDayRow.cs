using CommunityToolkit.Mvvm.ComponentModel;

namespace Mozakra.App.ViewModels;

/// <summary>One plan's minutes on the day being edited (EditDay dialog).</summary>
public sealed partial class NoteEditDayRow : ObservableObject
{
    public NoteEditDayRow(Guid planId, string name, string colorHex, int minutes)
    {
        PlanId = planId;
        Name = name;
        ColorHex = colorHex;
        OriginalMinutes = minutes;
        _minutesText = minutes.ToString();
    }

    public Guid PlanId { get; }
    public string Name { get; }
    public string ColorHex { get; }
    public int OriginalMinutes { get; }

    [ObservableProperty]
    private string _minutesText;

    /// <summary>The typed minutes, or null when the text isn't a whole number ≥ 0.</summary>
    public int? Minutes => int.TryParse(MinutesText?.Trim(), out int m) && m >= 0 ? m : null;
}
