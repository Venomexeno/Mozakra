using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using static Mozakra.App.Services.NativeMethods;

namespace Mozakra.App.Services;

/// <summary>
/// Keeps a window on the desktop layer: not topmost, no taskbar button, not in Alt+Tab,
/// visible on the desktop and after Win+D, behind normal apps.
///
/// How it works:
/// 1. WS_EX_TOOLWINDOW (no WS_EX_APPWINDOW) → no taskbar button, not in Alt+Tab. The window has no
///    minimize box, so Win+D (which minimizes the apps) leaves it alone.
/// 2. Owner = the desktop window (Progman, via GWLP_HWNDPARENT). An owned window always stays above its
///    owner, so <c>HWND_BOTTOM</c> puts it right above the desktop: visible on the desktop and after
///    Win+D, under every app.
/// 3. Every z-order change (WM_WINDOWPOSCHANGING) is rewritten: HWND_BOTTOM while owned, "don't move"
///    while not owned. So clicking the note never lifts it above other apps.
/// 4. The owner is dropped while the mouse is over the note and while it's active. Why: if a window owned by
///    Progman gets activated, Windows remembers it as the desktop's "last active popup" and activates it on
///    every Win+D. That steals the focus from the desktop, and the second Win+D no longer brings the apps
///    back. Activating it while it's not owned avoids that.
///    (Dropping the owner inside WM_MOUSEACTIVATE is too late: the click then never gets the foreground.)
///    When another app gets the focus (WM_ACTIVATE / WM_ACTIVATEAPP) or the mouse leaves an inactive note, it
///    is re-owned AND sent to the bottom (<c>SinkToDesktop</c>). Re-owning alone keeps the z-order it had
///    while not owned, which left it above inactive apps after Alt+Tab with the cursor over the note.
/// 5. <see cref="Peek"/> lifts it above apps on purpose (tray "Show") until it loses focus, then it sinks back.
/// 6. Explorer restarts create a new Progman: re-pin on the "TaskbarCreated" broadcast.
///
/// Manual test on Windows 11 Pro (build 26300), 20 Nov 2026, with a test window partly covering the note:
/// - apps open → the note is hidden behind them (only the uncovered part shows);
/// - click the uncovered part → the note gets the keyboard focus but stays under the covering app;
/// - Win+D → the apps minimize, the note stays visible, the desktop gets the focus;
/// - Win+D again → the apps come back on top of the note;
/// - click the note on the desktop, click the desktop, Win+D twice → same correct behavior.
/// Re-test after the z-order fix, same day, with two test windows (one active, one inactive behind it):
/// launch/tray "Show" (note on top) then click an app, Alt+Tab with the cursor over the note, Win+D while
/// hovering the note → every time the note ends up right above the desktop, under BOTH windows.
/// Tried and rejected: WS_EX_NOACTIVATE (clicks then never give the note the system focus, so text boxes
/// can't be typed into) and no owner at all (after Win+D the desktop covers the note).
/// </summary>
public sealed class DesktopPinService
{
    private static readonly int TaskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

    private readonly Dictionary<IntPtr, PinnedWindow> _pinned = new();

    public void Pin(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        if (_pinned.ContainsKey(hwnd)) return;

        var pinned = new PinnedWindow(hwnd);
        _pinned[hwnd] = pinned;
        HwndSource.FromHwnd(hwnd)?.AddHook(pinned.WndProc);
        window.Closed += (_, _) => _pinned.Remove(hwnd);
        pinned.Apply();
    }

    /// <summary>Lifts the pinned window above the other apps until the user activates something else.</summary>
    public void Peek(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (_pinned.TryGetValue(hwnd, out var pinned)) pinned.Peek();
    }

    private sealed class PinnedWindow
    {
        private readonly IntPtr _hwnd;
        private bool _peeking;   // lifted above apps on purpose (tray "Show")
        private bool _hovered;   // mouse over the note (owner dropped, a click may follow)

        public PinnedWindow(IntPtr hwnd) => _hwnd = hwnd;

        private bool IsOwned => GetWindowLongPtr(_hwnd, GWLP_HWNDPARENT) != IntPtr.Zero;

        public void Apply()
        {
            var ex = (long)GetWindowLongPtr(_hwnd, GWL_EXSTYLE);
            ex = (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
            SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(ex));
            if (!_hovered && GetForegroundWindow() != _hwnd) OwnByDesktop();
            SendToBottom();
        }

        public void Peek()
        {
            _peeking = true;
            Disown();
            SetWindowPos(_hwnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            SetForegroundWindow(_hwnd);
            if (GetForegroundWindow() != _hwnd)
            {
                // Windows refused the focus (we weren't launched by the user's last input): stay on the desktop.
                SinkToDesktop();
            }
        }

        public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_SETCURSOR when !_hovered:
                    // The mouse is over the note, so a click may follow: drop the owner before any activation.
                    _hovered = true;
                    Disown();
                    RequestMouseLeave();
                    break;

                case WM_MOUSELEAVE:
                    _hovered = false;
                    // Re-owning alone doesn't move the window: it stays wherever it was while not owned
                    // (e.g. above an app that lost the focus to Alt+Tab), so send it down too.
                    // Skip it while one of our windows (a note dialog, the tray menu) has the focus.
                    if (!IsOurs(GetForegroundWindow())) SinkToDesktop();
                    break;

                case WM_WINDOWPOSCHANGING when !_peeking:
                    var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                    if ((pos.flags & SWP_NOZORDER) == 0)
                    {
                        // Owned: HWND_BOTTOM lands just above the desktop. Not owned: HWND_BOTTOM would go
                        // under the desktop window, so just keep the current place.
                        if (IsOwned) pos.hwndInsertAfter = HWND_BOTTOM;
                        else pos.flags |= SWP_NOZORDER;
                        Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
                    }
                    break;

                case WM_ACTIVATE when (wParam.ToInt64() & 0xFFFF) == WA_INACTIVE && !IsOurs(lParam):
                case WM_ACTIVATEAPP when wParam == IntPtr.Zero:
                    // Another app got the focus (click, Alt+Tab, taskbar, Win+D), also when the focus was on a
                    // note dialog or the tray menu rather than on the note: back to the desktop layer.
                    // Focus moving to one of our own windows (a dialog) leaves the note where it is.
                    SinkToDesktop();
                    break;

                default:
                    if (msg == TaskbarCreatedMessage) Apply();
                    break;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Owned by the desktop and right above it. Always both: owning doesn't change the z-order, and
        /// HWND_BOTTOM only lands above the desktop while owned. If the mouse is still over the note, the next
        /// mouse move (WM_SETCURSOR) drops the owner again before a click can activate it.
        /// </summary>
        private void SinkToDesktop()
        {
            _peeking = false;
            _hovered = false;
            OwnByDesktop();
            SendToBottom();
        }

        private static bool IsOurs(IntPtr hwnd) =>
            hwnd != IntPtr.Zero && GetWindowThreadProcessId(hwnd, out var pid) != 0 && pid == Environment.ProcessId;

        private void OwnByDesktop()
        {
            var desktop = FindWindow("Progman", null);
            if (desktop != IntPtr.Zero) SetWindowLongPtr(_hwnd, GWLP_HWNDPARENT, desktop);
        }

        private void Disown() => SetWindowLongPtr(_hwnd, GWLP_HWNDPARENT, IntPtr.Zero);

        private void SendToBottom() =>
            SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        private void RequestMouseLeave()
        {
            var track = new TRACKMOUSEEVENT
            {
                cbSize = (uint)Marshal.SizeOf<TRACKMOUSEEVENT>(),
                dwFlags = TME_LEAVE,
                hwndTrack = _hwnd,
            };
            TrackMouseEvent(ref track);
        }
    }
}
