namespace Mozakra.App.Services;

/// <summary>Opens the app's windows. The contract between the note (Plan 05) and the screens (Plan 06).</summary>
public interface INavigationService
{
    void ShowNote();
    void HideNote();
    void OpenPlans();
    void OpenPlanEditor(Guid? planId);
    void OpenFreeTime();
    void OpenSettings();
    void OpenOnboarding();
}
