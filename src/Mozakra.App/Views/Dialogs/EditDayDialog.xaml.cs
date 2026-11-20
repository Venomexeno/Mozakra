using System.Windows;
using Mozakra.App.ViewModels;

namespace Mozakra.App.Views.Dialogs;

/// <summary>Edits the minutes logged per plan on one day (click a day in the week bars).</summary>
public partial class EditDayDialog : Window
{
    private readonly IReadOnlyList<NoteEditDayRow> _rows;

    public EditDayDialog(string title, IReadOnlyList<NoteEditDayRow> rows)
    {
        InitializeComponent();
        _rows = rows;
        TitleText.Text = title;
        Rows.ItemsSource = rows;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_rows.Any(r => r.Minutes is null))
        {
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        DialogResult = true;
    }
}
