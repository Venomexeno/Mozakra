namespace Mozakra.Core.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "ar";   // "ar" | "en"
    public bool StartWithWindows { get; set; } = true;
    public bool OnboardingDone { get; set; }
    public double? NoteLeft { get; set; }
    public double? NoteTop { get; set; }
    public bool ShowDetails { get; set; }
}
