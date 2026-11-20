using System.Windows;
using Mozakra.App.ViewModels;

namespace Mozakra.App.Views;

public partial class FreeTimeWindow : Window
{
    public FreeTimeWindow(FreeTimeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Saved += (_, _) => Close();
        Closed += (_, _) => viewModel.Dispose();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
