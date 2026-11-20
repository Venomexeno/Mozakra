using CommunityToolkit.Mvvm.ComponentModel;

namespace Mozakra.App.ViewModels;

/// <summary>An editable checklist row (work plans; not used in the math).</summary>
public sealed partial class PlanEditorChecklistItem : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _done;
}
