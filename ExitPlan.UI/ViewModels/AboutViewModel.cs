using System.IO;
using ExitPlan.Core;
using ExitPlan.UI.Updates;

namespace ExitPlan.UI.ViewModels;

public sealed class AboutViewModel
{
    public const string LicenseFileName = "LICENSE.md";

    public AboutViewModel()
    {
        var installed = InstalledAppInfo.Read();
        Version = installed.Version;
        UpdateChannel = installed.Channel;
        UpdateStatus = installed.Status;
        ActiveDataFolder = AppDataPaths.ActiveRoot;
        AppStatePath = AppDataPaths.AppStatePath;
        HasLicenseFile = File.Exists(LicenseFilePath);
    }

    public string AppName => ProductInfo.Name;

    public string Version { get; }

    public string Publisher => "Paul Schofield";

    public string Copyright => "Copyright (c) 2026 Paul Schofield";

    public string ActiveDataFolder { get; }

    public string AppStatePath { get; }

    public string UpdateChannel { get; }

    public string UpdateStatus { get; }

    public string Disclaimer => ProductInfo.SplashLead;

    public bool HasLicenseFile { get; }

    public static string LicenseFilePath => Path.Combine(AppContext.BaseDirectory, LicenseFileName);
}
