using System.Windows;
using Mozakra.App.Localization;

namespace Mozakra.App.ViewModels;

/// <summary>Localized message boxes for the screens (right-to-left in Arabic).</summary>
public static class PlansMessageBox
{
    private static MessageBoxOptions Options =>
        Application.Current?.TryFindResource(LocalizationService.FlowDirectionKey) is FlowDirection.RightToLeft
            ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
            : MessageBoxOptions.None;

    private static Window? Owner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

    public static bool Confirm(string message) =>
        Show(message, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public static void Info(string message) => Show(message, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Error(string message) => Show(message, MessageBoxButton.OK, MessageBoxImage.Warning);

    private static MessageBoxResult Show(string message, MessageBoxButton buttons, MessageBoxImage image)
    {
        var title = Loc.T("Common.AppName");
        var owner = Owner;
        return owner is null
            ? MessageBox.Show(message, title, buttons, image, MessageBoxResult.None, Options)
            : MessageBox.Show(owner, message, title, buttons, image, MessageBoxResult.None, Options);
    }
}
