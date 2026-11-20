namespace Mozakra.App.ViewModels;

/// <summary>The questions the note asks the user. Views/NoteDialogs shows real windows; tests use a fake.</summary>
public interface INoteDialogs
{
    /// <summary>Yes/No question. Returns true on Yes.</summary>
    bool Confirm(string message);

    /// <summary>Edits the minutes per plan of one day. Null = cancelled; otherwise the rows with the new minutes.</summary>
    IReadOnlyList<NoteEditDayRow>? EditDay(string title, IReadOnlyList<NoteEditDayRow> rows);
}
