namespace Mozakra.App.ViewModels;

/// <summary>
/// An exception type. <see cref="Key"/> is what gets stored in <c>AvailabilityException.Label</c>
/// ("Vacation", "FreeDay", "BusyDay", or a custom label from an imported file).
/// </summary>
public sealed record FreeTimeLabelOption(string Key, string Display, int? DefaultMinutes)
{
    public override string ToString() => Display;
}
