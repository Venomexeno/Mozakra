using System.Windows;
using Mozakra.App.ViewModels;

namespace Mozakra.App.Views;

/// <summary>
/// First-run wizard. Finishing or skipping marks onboarding done and shows the note; closing the window
/// shows the note too, but the wizard comes back on the next start.
/// </summary>
public partial class OnboardingWindow : Window
{
    private bool _completed;

    public OnboardingWindow(OnboardingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Completed += (_, _) => { _completed = true; Close(); };
        Closed += (_, _) => { if (!_completed) viewModel.ShowNoteWithoutFinishing(); };
    }
}
