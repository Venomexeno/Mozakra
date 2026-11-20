using System.Windows;
using Mozakra.App.ViewModels;
using Mozakra.App.Views.Dialogs;

namespace Mozakra.App.Views;

/// <summary>
/// Real dialogs for the note. They are top-most windows centered on the note (or the screen when the note is
/// hidden): the note itself lives on the desktop layer, so an owned dialog could end up behind other apps.
/// </summary>
public sealed class NoteDialogs : INoteDialogs
{
    public bool Confirm(string message) => Show(new NoteConfirmDialog(message));

    public IReadOnlyList<NoteEditDayRow>? EditDay(string title, IReadOnlyList<NoteEditDayRow> rows)
        => Show(new EditDayDialog(title, rows)) ? rows : null;

    private static bool Show(Window dialog)
    {
        var note = Application.Current.Windows.OfType<NoteWindow>().FirstOrDefault(w => w.IsVisible);
        if (note is null)
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Loaded += (_, _) =>
            {
                var area = SystemParameters.WorkArea;
                dialog.Left = Math.Clamp(note.Left + (note.ActualWidth - dialog.ActualWidth) / 2,
                    area.Left, Math.Max(area.Left, area.Right - dialog.ActualWidth));
                dialog.Top = Math.Clamp(note.Top + 80, area.Top, Math.Max(area.Top, area.Bottom - dialog.ActualHeight));
            };
            dialog.Left = note.Left;
            dialog.Top = note.Top + 80;
        }
        return dialog.ShowDialog() == true;
    }
}
