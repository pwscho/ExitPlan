using ExitPlan.Core;
using Velopack;
using Velopack.Sources;

namespace ExitPlan.UI.Updates;

/// <summary>
/// Version, channel, and install state for the About window.
/// Version is the Velopack installed version when this copy was installed,
/// otherwise the assembly informational version without the +git hash.
/// </summary>
internal static class InstalledAppInfo
{
    public static Snapshot Read()
    {
        var version = ProductInfo.Version;
        var channel = "—";
        var status = "Unavailable";

        try
        {
            var manager = new Probe(new GithubSource(UpdateChecker.RepoUrl, accessToken: null, prerelease: false));
            if (manager.IsInstalled)
                version = ProductInfo.ChooseDisplayVersion(manager.CurrentVersion?.ToFullString());

            channel = string.IsNullOrWhiteSpace(manager.UpdateChannel)
                ? "—"
                : manager.UpdateChannel.Trim();

            if (!manager.IsInstalled)
            {
                status = "Not installed";
            }
            else
            {
                VelopackAsset? pending = null;
                try
                {
                    pending = manager.UpdatePendingRestart;
                }
                catch (Exception ex)
                {
                    AppLog.Write("Could not read a pending update.", ex);
                }

                status = pending == null ? "Installed" : "Installed, update waiting to restart";
            }
        }
        catch (Exception ex)
        {
            AppLog.Write("Could not read the Velopack install.", ex);
        }

        return new Snapshot(version, channel, status);
    }

    internal readonly record struct Snapshot(string Version, string Channel, string Status);

    /// <summary>
    /// UpdateManager.Channel is protected. This subclass only exposes it.
    /// </summary>
    private sealed class Probe : UpdateManager
    {
        public Probe(IUpdateSource source)
            : base(source)
        {
        }

        public string UpdateChannel => Channel;
    }
}
