using Microsoft.Win32;

namespace Mozakra.App.Services;

/// <summary>
/// "Start with Windows" through HKCU\...\Run (no admin needed). Value name: <c>Mozakra</c>.
/// When installed by Velopack the exe runs from the stable "...\Mozakra\current\" folder,
/// so the stored path survives updates.
/// </summary>
public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Mozakra";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>True when the Run value points at this exe.</summary>
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value
                && string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
