using System.Windows;
using ExitPlan.UI.ViewModels;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace ExitPlan.UI.Updates;

/// <summary>
/// Checks the public releases repository for a newer ExitPlan build.
/// Nothing is downloaded or applied unless the user answers Yes.
/// Offline failures are written to the log and do not interrupt startup.
/// </summary>
internal static class UpdateChecker
{
    /// <summary>
    /// Public releases repository. Source lives in the private repo; this URL does not.
    /// A public repo needs no access token for GithubSource.
    /// </summary>
    public const string RepoUrl = "https://github.com/pwscho/ExitPlan";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static void StartBackgroundCheck()
    {
        _ = CheckForUpdatesAsync(interactive: false);
    }

    public static async Task CheckForUpdatesAsync(bool interactive)
    {
        if (!await Gate.WaitAsync(interactive ? Timeout.Infinite : 0).ConfigureAwait(false))
            return;

        try
        {
            await CheckCoreAsync(interactive).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Write("Update check failed.", ex);
            if (interactive)
                await NotifyAsync("Could not check for updates. Details were written to the log.").ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task CheckCoreAsync(bool interactive)
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update == null)
            {
                AppLog.Write("No update available.");
                if (interactive)
                    await NotifyAsync("ExitPlan is up to date.").ConfigureAwait(false);
                return;
            }

            var version = FormatVersion(update.TargetFullRelease.Version.ToString());
            AppLog.Write($"Update {version} is available.");

            var accepted = await AskAsync($"ExitPlan {version} is available. Update now?").ConfigureAwait(false);
            if (!accepted)
            {
                AppLog.Write($"User declined update {version}.");
                return;
            }

            await SaveOpenScenarioAsync().ConfigureAwait(false);
            await DownloadAndApplyAsync(manager, update, version).ConfigureAwait(false);
        }
        catch (NotInstalledException ex)
        {
            AppLog.Write("Update check skipped because this copy is not a Velopack install.", ex);
            if (interactive)
            {
                await NotifyAsync(
                    "Updates are available after ExitPlan is installed with ExitPlan-win-Setup.exe.")
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (!interactive)
        {
            AppLog.Write("Update check failed.", ex);
        }
    }

    private static async Task DownloadAndApplyAsync(UpdateManager manager, UpdateInfo update, string version)
    {
        Window? progress = null;
        try
        {
            progress = await OnUiAsync(() =>
            {
                var window = new Window
                {
                    Title = "ExitPlan",
                    Width = 420,
                    Height = 150,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Topmost = true,
                    Content = new System.Windows.Controls.TextBlock
                    {
                        Text = $"Downloading ExitPlan {version}…",
                        Margin = new Thickness(24),
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 14
                    }
                };
                window.Show();
                return window;
            }).ConfigureAwait(false);

            await manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Download of {version} failed.", ex);
            await NotifyAsync("The update could not be downloaded. Details were written to the log.")
                .ConfigureAwait(false);
            return;
        }
        finally
        {
            if (progress != null)
            {
                await OnUiAsync(() =>
                {
                    progress.Close();
                    return true;
                }).ConfigureAwait(false);
            }
        }

        AppLog.Write($"Applying update {version} and restarting.");
        try
        {
            manager.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Applying update {version} failed.", ex);
            await NotifyAsync("The update was downloaded but could not be applied. Details were written to the log.")
                .ConfigureAwait(false);
        }
    }

    internal static string FormatVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "";

        var cut = version.IndexOfAny(new[] { '-', '+' });
        return cut >= 0 ? version[..cut] : version;
    }

    private static async Task SaveOpenScenarioAsync()
    {
        await OnUiAsync(() =>
        {
            if (Application.Current?.MainWindow?.DataContext is MainViewModel viewModel)
                viewModel.SaveCommand.Execute(null);
            return true;
        }).ConfigureAwait(false);
    }

    private static Task NotifyAsync(string message)
        => OnUiAsync(() =>
        {
            if (Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted)
                return false;

            MessageBox.Show(
                Application.Current.MainWindow,
                message,
                "ExitPlan",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return true;
        });

    private static Task<bool> AskAsync(string message)
        => OnUiAsync(() =>
        {
            if (Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted)
                return false;

            return MessageBox.Show(
                Application.Current.MainWindow,
                message,
                "ExitPlan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information) == MessageBoxResult.Yes;
        });

    private static Task<T> OnUiAsync<T>(Func<T> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            return Task.FromResult(action());

        return dispatcher.InvokeAsync(action).Task;
    }
}
