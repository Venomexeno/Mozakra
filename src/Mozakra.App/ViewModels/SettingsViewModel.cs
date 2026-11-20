using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Mozakra.App.Localization;
using Mozakra.App.Services;
using Mozakra.Core.Models;
using Mozakra.Core.Persistence;

namespace Mozakra.App.ViewModels;

/// <summary>Language, start with Windows, export/import, data folder, updates, about.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppStateService _state;
    private readonly LocalizationService _localization;
    private readonly StartupService _startup;
    private readonly UpdateService _updates;
    private readonly ExportImportService _exportImport;
    private readonly IClock _clock;
    private bool _loading = true;

    public SettingsViewModel(AppStateService state, LocalizationService localization, StartupService startup,
        UpdateService updates, ExportImportService exportImport, IClock clock)
    {
        _state = state;
        _localization = localization;
        _startup = startup;
        _updates = updates;
        _exportImport = exportImport;
        _clock = clock;
        IsArabic = localization.Language == "ar";
        StartWithWindows = startup.IsEnabled;
        _loading = false;
    }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEnglish))] private bool _isArabic;
    public bool IsEnglish { get => !IsArabic; set => IsArabic = !value; }

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(CheckUpdatesCommand))] private bool _isCheckingUpdates;
    [ObservableProperty] private string _updateStatus = "";

    public string VersionText => Loc.T("Screens.Settings.VersionFormat", AppInfo.Version);
    public string RepositoryUrl => AppInfo.RepositoryUrl;

    partial void OnIsArabicChanged(bool value)
    {
        if (_loading) return;
        var language = value ? "ar" : "en";
        _localization.SetLanguage(language);
        _state.State.Settings.Language = language;
        _state.Save();
        OnPropertyChanged(nameof(VersionText));
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading) return;
        _startup.Set(value);
        _state.State.Settings.StartWithWindows = value;
        _state.Save();
    }

    [RelayCommand]
    private void Export()
    {
        var dialog = new SaveFileDialog
        {
            Title = Loc.T("Screens.Settings.Export"),
            Filter = Loc.T("Screens.Settings.FileFilter"),
            FileName = $"mozakra-{_clock.Today:yyyy-MM-dd}.json",
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _exportImport.Export(_state.State, dialog.FileName);
            PlansMessageBox.Info(Loc.T("Screens.Settings.ExportDone", dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PlansMessageBox.Error(Loc.T("Screens.Settings.ExportFailed", ex.Message));
        }
    }

    [RelayCommand]
    private void Import()
    {
        var dialog = new OpenFileDialog { Title = Loc.T("Screens.Settings.Import"), Filter = Loc.T("Screens.Settings.FileFilter") };
        if (dialog.ShowDialog() != true) return;
        if (!PlansMessageBox.Confirm(Loc.T("Screens.Settings.ImportConfirm"))) return;

        ImportResult result;
        try { result = _exportImport.Import(dialog.FileName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PlansMessageBox.Error(Loc.T("Screens.Settings.ImportFailed", ex.Message));
            return;
        }
        if (!result.Success || result.State is null)
        {
            PlansMessageBox.Error(Loc.T($"Screens.Settings.ImportError.{result.Error}"));
            return;
        }

        var imported = result.State;
        imported.Settings.OnboardingDone = true;   // already set up; don't show the wizard again
        _state.Replace(imported);
        _loading = true;
        IsArabic = LocalizationService.Normalize(imported.Settings.Language) == "ar";
        _loading = false;
        _localization.SetLanguage(imported.Settings.Language);
        PlansMessageBox.Info(Loc.T("Screens.Settings.ImportDone"));
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(_state.DataDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_state.DataDirectory}\"") { UseShellExecute = true });
    }

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task CheckUpdates()
    {
        if (!_updates.IsAvailable)
        {
            UpdateStatus = Loc.T("Screens.Settings.UpdatesUnavailable");
            return;
        }
        IsCheckingUpdates = true;
        UpdateStatus = Loc.T("Screens.Settings.UpdatesChecking");
        bool ready = await _updates.CheckAndApplyInBackgroundAsync();
        UpdateStatus = Loc.T(ready ? "Screens.Settings.UpdateReady" : "Screens.Settings.UpToDate");
        IsCheckingUpdates = false;
    }

    private bool CanCheckUpdates() => !IsCheckingUpdates;

    [RelayCommand]
    private void OpenRepository()
    {
        if (Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out _))
            Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true });
    }
}
