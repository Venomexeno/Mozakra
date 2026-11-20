using System.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace Mozakra.App.Services;

/// <summary>
/// Auto-update from GitHub Releases (Velopack). Updates download in the background and are applied
/// silently when the app exits, so the next start runs the new version. Does nothing in dev runs.
/// </summary>
public sealed class UpdateService
{
    private UpdateManager? _manager;

    /// <summary>False in dev runs (not installed by Setup.exe) or before the real repo URL is set.</summary>
    public bool IsAvailable => Manager?.IsInstalled == true;

    private UpdateManager? Manager =>
        _manager ??= AppInfo.HasRealRepository
            ? new UpdateManager(new GithubSource(AppInfo.RepositoryUrl, accessToken: null, prerelease: false))
            : null;

    /// <summary>
    /// Checks GitHub, downloads a newer version if there is one, and schedules it for when the app exits.
    /// Returns true when an update is waiting. Never throws.
    /// </summary>
    public async Task<bool> CheckAndApplyInBackgroundAsync()
    {
        try
        {
            if (!IsAvailable) return false;
            var manager = Manager!;

            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null) return false;

            await manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex}");
            return false;
        }
    }
}
