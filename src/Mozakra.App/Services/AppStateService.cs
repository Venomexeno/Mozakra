using Mozakra.Core.Models;
using Mozakra.Core.Persistence;

namespace Mozakra.App.Services;

/// <summary>
/// The one in-memory <see cref="AppState"/> shared by every window. Loaded once at startup;
/// call <see cref="Save"/> after each change so other views can refresh on <see cref="Changed"/>.
/// </summary>
public sealed class AppStateService
{
    private readonly IStateStore _store;

    public AppStateService(IStateStore store)
    {
        _store = store;
        State = store.Load();
    }

    public AppState State { get; private set; }

    public string DataDirectory => _store.DataDirectory;

    public event EventHandler? Changed;

    public void Save()
    {
        _store.Save(State);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Swaps the whole state (undo, import) and saves it.</summary>
    public void Replace(AppState state)
    {
        State = state;
        Save();
    }
}
