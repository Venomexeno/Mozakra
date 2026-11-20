using System.Windows;

namespace Mozakra.App.Services;

/// <summary>
/// Owns the single note window: creates it on first use, pins it to the desktop layer,
/// and shows/hides it with the animation. Calls are serialized so animations never overlap.
/// </summary>
public sealed class NoteHost
{
    private const double EdgeMargin = 16;

    private readonly NoteWindowFactory _factory;
    private readonly DesktopPinService _pin;
    private readonly NoteAnimator _animator;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Window? _window;

    public NoteHost(NoteWindowFactory factory, DesktopPinService pin, NoteAnimator animator)
    {
        _factory = factory;
        _pin = pin;
        _animator = animator;
    }

    public bool IsShown { get; private set; }

    public Window Window => _window ??= Create();

    public async Task ShowAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var window = Window;
            if (IsShown)
            {
                // Already on the desktop (maybe behind apps): bring it up until the user clicks elsewhere.
                _pin.Peek(window);
                return;
            }
            IsShown = true;
            await _animator.ShowAsync(window);
            _pin.Peek(window);
        }
        finally { _gate.Release(); }
    }

    public async Task HideAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!IsShown || _window is null) return;
            IsShown = false;
            await _animator.HideAsync(_window);
        }
        finally { _gate.Release(); }
    }

    private Window Create()
    {
        var window = _factory();
        window.ShowActivated = false;   // Peek() activates it explicitly after the slide-in
        // Default spot: top-right corner of the primary work area (Plan 05 restores the saved position).
        if (double.IsNaN(window.Left) || double.IsNaN(window.Top))
        {
            var area = SystemParameters.WorkArea;
            window.Left = area.Right - window.Width - EdgeMargin;
            window.Top = area.Top + EdgeMargin;
        }
        _pin.Pin(window);
        return window;
    }
}
