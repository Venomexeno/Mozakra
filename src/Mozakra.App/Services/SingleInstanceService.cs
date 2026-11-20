using System.Windows;

namespace Mozakra.App.Services;

/// <summary>
/// Single instance: Program.cs holds <see cref="MutexName"/>. A second launch signals the
/// named event and exits; the running instance shows its note.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    public const string MutexName = @"Local\Mozakra.SingleInstance";
    private const string ShowEventName = @"Local\Mozakra.ShowNote";

    private readonly INavigationService _navigation;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _registration;

    public SingleInstanceService(INavigationService navigation) => _navigation = navigation;

    public static void SignalRunningInstance()
    {
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
            showEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The first instance is still starting; nothing to do.
        }
    }

    public void ListenForSecondInstance()
    {
        var dispatcher = Application.Current.Dispatcher;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _registration = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => dispatcher.BeginInvoke(_navigation.ShowNote),
            state: null, millisecondsTimeOutInterval: -1, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _showEvent?.Dispose();
    }
}
