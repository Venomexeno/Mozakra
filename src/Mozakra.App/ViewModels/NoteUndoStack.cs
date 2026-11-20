using Mozakra.Core.Models;
using Mozakra.Core.Persistence;

namespace Mozakra.App.ViewModels;

/// <summary>In-memory snapshots of the state taken before each change (newest last, at most <see cref="Capacity"/>).</summary>
public sealed class NoteUndoStack
{
    private readonly List<(string Label, string Json)> _items = [];

    public NoteUndoStack(int capacity = 30) => Capacity = capacity;

    public int Capacity { get; }
    public int Count => _items.Count;
    public string? NextLabel => _items.Count > 0 ? _items[^1].Label : null;

    public void Push(string label, AppState state)
    {
        _items.Add((label, StateJson.Serialize(state)));
        if (_items.Count > Capacity) _items.RemoveAt(0);
    }

    /// <summary>The newest snapshot (a fresh copy) and its label, or null when empty.</summary>
    public (string Label, AppState State)? Pop()
    {
        if (_items.Count == 0) return null;
        var (label, json) = _items[^1];
        _items.RemoveAt(_items.Count - 1);
        return (label, StateJson.Deserialize(json));
    }
}
