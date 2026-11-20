using System.Windows;
using Mozakra.App.Localization;
using SD = System.Drawing;
using WF = System.Windows.Forms;

namespace Mozakra.App.Services;

/// <summary>
/// The tray icon. Left click or "Show" → the note. Menu: Show · ✓ Start with Windows · Exit.
/// Exit raises <see cref="ExitRequested"/> (Plan 05 asks for confirmation and shuts down);
/// with no subscriber the app shuts down directly.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly INavigationService _navigation;
    private readonly StartupService _startup;
    private readonly AppStateService _state;
    private readonly LocalizationService _localization;

    private WF.NotifyIcon? _icon;
    private WF.ContextMenuStrip? _menu;
    private WF.ToolStripMenuItem? _show, _startWithWindows, _exit;

    public TrayService(INavigationService navigation, StartupService startup,
        AppStateService state, LocalizationService localization)
    {
        _navigation = navigation;
        _startup = startup;
        _state = state;
        _localization = localization;
    }

    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_icon is not null) return;

        _show = new WF.ToolStripMenuItem("", null, (_, _) => _navigation.ShowNote());
        _startWithWindows = new WF.ToolStripMenuItem { CheckOnClick = true };
        _startWithWindows.Click += (_, _) => SetStartWithWindows(_startWithWindows.Checked);
        _exit = new WF.ToolStripMenuItem("", null, (_, _) => RequestExit());

        _menu = new WF.ContextMenuStrip();
        _menu.Items.AddRange([_show, _startWithWindows, new WF.ToolStripSeparator(), _exit]);
        _menu.Opening += (_, _) => _startWithWindows.Checked = _startup.IsEnabled;

        _icon = new WF.NotifyIcon { Icon = LoadIcon(), ContextMenuStrip = _menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == WF.MouseButtons.Left) _navigation.ShowNote(); };

        ApplyTexts();
        _localization.LanguageChanged += (_, _) => ApplyTexts();
    }

    private void ApplyTexts()
    {
        _show!.Text = Loc.T("Common.Show");
        _startWithWindows!.Text = Loc.T("Common.StartWithWindows");
        _exit!.Text = Loc.T("Common.Exit");
        _menu!.RightToLeft = _localization.IsRightToLeft ? WF.RightToLeft.Yes : WF.RightToLeft.No;
        _icon!.Text = Truncate(Loc.T("Common.TrayTooltip"), 127);
    }

    private void SetStartWithWindows(bool enabled)
    {
        _startup.Set(enabled);
        _state.State.Settings.StartWithWindows = enabled;
        _state.Save();
    }

    private void RequestExit()
    {
        var handler = ExitRequested;
        if (handler is not null) handler(this, EventArgs.Empty);
        else Application.Current.Shutdown();
    }

    private static SD.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/mozakra.ico"));
        using var stream = resource!.Stream;
        return new SD.Icon(stream, WF.SystemInformation.SmallIconSize);
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    public void Dispose()
    {
        if (_icon is null) return;
        _icon.Visible = false;
        _icon.Dispose();
        _menu?.Dispose();
        _icon = null;
    }
}
