using System.Windows;

namespace Mozakra.App.Views;

/// <summary>Sticky-styled Yes/No question (exit). ShowDialog() returns true on Yes.</summary>
public partial class NoteConfirmDialog : Window
{
    public NoteConfirmDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void OnYes(object sender, RoutedEventArgs e) => DialogResult = true;
}
