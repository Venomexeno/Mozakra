using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Mozakra.App.ViewModels;

namespace Mozakra.App.Views;

/// <summary>
/// The sticky note. Pinning and the slide/shake animation come from NoteHost (Plan 04);
/// this code-behind only restores/saves the position and lets the user drag the card.
/// </summary>
public partial class NoteWindow : Window
{
    private readonly NoteViewModel _viewModel;

    public NoteWindow(NoteViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        RestorePosition();
    }

    private void RestorePosition()
    {
        if (_viewModel.SavedPosition is not { } pos) return;   // NaN → NoteHost puts it top-right
        // Ignore a position that is no longer on any screen (monitor unplugged, resolution changed).
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (!screen.Contains(new Point(pos.Left + Width / 2, pos.Top + 20))) return;
        Left = pos.Left;
        Top = pos.Top;
    }

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || IsInteractive(e.OriginalSource as DependencyObject)) return;
        double left = Left, top = Top;
        try { DragMove(); }
        catch (InvalidOperationException) { return; }
        if (Left != left || Top != top) _viewModel.SavePosition(Left, Top);
    }

    // Dragging starts only from the card itself, not from controls on it.
    private static bool IsInteractive(DependencyObject? source)
    {
        for (var node = source; node is not null; node = ParentOf(node))
        {
            if (node is ButtonBase or TextBox or ComboBox or DatePicker or ScrollBar) return true;
            if (node is NoteWindow) return false;
        }
        return false;
    }

    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);

    private void OnToastClick(object sender, MouseButtonEventArgs e)
    {
        _viewModel.HideToastCommand.Execute(null);
        e.Handled = true;
    }
}
