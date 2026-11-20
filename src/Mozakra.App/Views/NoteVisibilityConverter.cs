using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Mozakra.App.Views;

/// <summary>
/// true → Visible, false/null → Collapsed. ConverterParameter="Invert" flips it.
/// Also works for strings (empty → Collapsed) and numbers (0 → Collapsed).
/// </summary>
public sealed class NoteVisibilityConverter : IValueConverter
{
    /// <summary>Use from XAML: <c>Converter={x:Static v:NoteVisibilityConverter.Instance}</c>.</summary>
    public static readonly NoteVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool visible = value switch
        {
            bool b => b,
            string s => s.Length > 0,
            int i => i != 0,
            double d => d > 0,
            null => false,
            _ => true,
        };
        if (parameter as string == "Invert") visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
