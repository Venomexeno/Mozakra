using System.Windows;
using Mozakra.App.ViewModels;

namespace Mozakra.App.Views;

public partial class PlansWindow : Window
{
    public PlansWindow(PlansViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
    }
}
