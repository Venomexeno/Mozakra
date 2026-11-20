using System.Windows;

namespace Mozakra.App.Services;

/// <summary>
/// Creates the note window once. Registered in <c>AddNoteFeature()</c> (returns the singleton NoteWindow).
/// </summary>
public delegate Window NoteWindowFactory();
