using System.Windows;

namespace Mozakra.App.Localization;

/// <summary>Strings for code (ViewModels): <c>Loc.T("Note.Start")</c>, <c>Loc.T("Common.MinutesFormat", 35)</c>.</summary>
public static class Loc
{
    /// <summary>The string for <paramref name="key"/> in the current language, or the key itself if it's missing.</summary>
    public static string T(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>Same as <see cref="T(string)"/>, then <c>string.Format</c> with the arguments.</summary>
    public static string T(string key, params object[] args) =>
        string.Format(T(key), args);
}
