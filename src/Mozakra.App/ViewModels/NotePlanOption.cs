namespace Mozakra.App.ViewModels;

/// <summary>A plan in the note's pickers (Start dropdown, manual log).</summary>
public sealed record NotePlanOption(Guid Id, string Name, string ColorHex, bool IsCourse)
{
    public override string ToString() => Name;
}
