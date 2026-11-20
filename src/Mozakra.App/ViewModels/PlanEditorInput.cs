using System.Globalization;
using System.Text;
using Mozakra.App.Localization;

namespace Mozakra.App.ViewModels;

/// <summary>Parsing of typed numbers (accepts Arabic-Indic digits and "," or "٫" as decimal point) and duration text.</summary>
public static class PlanEditorInput
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim())
        {
            builder.Append(c switch
            {
                >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
                '٫' or ',' => '.',
                _ => c,
            });
        }
        return builder.ToString();
    }

    public static bool TryInt(string? text, out int value) =>
        int.TryParse(Normalize(text), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryDouble(string? text, out double value) =>
        double.TryParse(Normalize(text), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary>"HH:mm" (or "H") → time; empty → null. Returns false when the text isn't a time.</summary>
    public static bool TryTime(string? text, out TimeOnly? value)
    {
        value = null;
        var normalized = Normalize(text);
        if (normalized.Length == 0) return true;
        if (TimeOnly.TryParseExact(normalized, ["H:mm", "HH:mm", "H"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time))
        {
            value = time;
            return true;
        }
        return false;
    }

    public static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>95 → "1 h 35 min", 45 → "45 min", 120 → "2 h" (localized units).</summary>
    public static string Duration(int minutes)
    {
        minutes = Math.Max(0, minutes);
        int hours = minutes / 60, rest = minutes % 60;
        if (hours == 0) return Loc.T("Common.MinutesFormat", rest);
        return rest == 0 ? Loc.T("Common.HoursFormat", hours) : Loc.T("Common.HoursMinutesFormat", hours, rest);
    }
}
