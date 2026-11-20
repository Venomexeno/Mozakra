namespace Mozakra.App.ViewModels;

/// <summary>An entry of the "starts after" dropdown. <see cref="Id"/> null = starts right away.</summary>
public sealed record PlanEditorOption(Guid? Id, string Name)
{
    public override string ToString() => Name;
}
