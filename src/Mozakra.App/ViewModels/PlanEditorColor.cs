using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mozakra.App.ViewModels;

/// <summary>A swatch of the pastel palette in the plan editor.</summary>
public sealed partial class PlanEditorColor : ObservableObject
{
    public PlanEditorColor(string hex)
    {
        Hex = hex;
        Brush = PlansItemViewModel.ToBrush(hex);
    }

    public string Hex { get; }
    public Brush Brush { get; }

    [ObservableProperty] private bool _isSelected;
}
