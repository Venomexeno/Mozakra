using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mozakra.App.Localization;

/// <summary>
/// Current UI language ("ar" | "en"). <see cref="SetLanguage"/> swaps the per-feature string dictionaries
/// (Common, Note, Screens) in the application resources, so every <c>{DynamicResource Key}</c> updates live.
/// Also publishes the resource <c>AppFlowDirection</c>: windows use
/// <c>FlowDirection="{DynamicResource AppFlowDirection}"</c> to flip to RTL for Arabic.
/// </summary>
public sealed class LocalizationService : ObservableObject
{
    public const string FlowDirectionKey = "AppFlowDirection";

    // One dictionary pair per feature, to keep parallel plans out of each other's files.
    private static readonly string[] Features = ["Common", "Note", "Screens"];

    private readonly List<ResourceDictionary> _loaded = [];
    private string _language = "";

    public string Language
    {
        get => _language;
        private set
        {
            if (SetProperty(ref _language, value))
            {
                OnPropertyChanged(nameof(IsRightToLeft));
                OnPropertyChanged(nameof(FlowDirection));
                OnPropertyChanged(nameof(Culture));
            }
        }
    }

    public bool IsRightToLeft => Language == "ar";

    public FlowDirection FlowDirection => IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>Culture for formatting dates/numbers in the current language.</summary>
    public CultureInfo Culture => CultureInfo.GetCultureInfo(IsRightToLeft ? "ar-EG" : "en-US");

    public event EventHandler? LanguageChanged;

    public static string Normalize(string? language) => language == "ar" ? "ar" : "en";

    public void SetLanguage(string language)
    {
        language = Normalize(language);
        var resources = Application.Current.Resources;

        foreach (var dictionary in _loaded) resources.MergedDictionaries.Remove(dictionary);
        _loaded.Clear();
        foreach (var feature in Features)
        {
            var dictionary = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Mozakra;component/Localization/{feature}.{language}.xaml"),
            };
            resources.MergedDictionaries.Add(dictionary);
            _loaded.Add(dictionary);
        }

        Language = language;
        resources[FlowDirectionKey] = FlowDirection;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
