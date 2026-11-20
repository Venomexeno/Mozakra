using System.Windows;
using Mozakra.App.ViewModels;

namespace Mozakra.App.Views;

public partial class PlanEditorWindow : Window
{
    public PlanEditorWindow(PlanEditorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.Saved += (_, _) => Close();
    }

    public PlanEditorViewModel ViewModel { get; }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
