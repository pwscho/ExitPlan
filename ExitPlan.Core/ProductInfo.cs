using System.Reflection;

namespace ExitPlan.Core
{
    public static class ProductInfo
    {
        public const string Name = "ExitPlan";

        /// <summary>
        /// Assembly informational version with the SDK +git suffix removed.
        /// </summary>
        public static string Version { get; } = WithoutGitHash(ReadInformationalVersion());

        public static string Title => Name + " " + Version;

        /// <summary>
        /// Short notice on the main window. Plain language only.
        /// </summary>
        public const string ModelNotice =
            "A household planning model, not financial, tax, or Social Security advice, and not a guarantee. Tables go stale. Full retirement age modeling is simplified.";

        public const string SplashTitle = "Before you continue";

        /// <summary>
        /// Startup disclaimer. Fuller than <see cref="ModelNotice"/>, same limits:
        /// not advice, tables go stale, simplified Social Security, not a guarantee.
        /// </summary>
        public const string SplashLead =
            "ExitPlan is a household planning simulation. It is not financial, tax, investment, or Social Security advice, and it is not a substitute for advice from a qualified professional.";

        public const string SplashLimits =
            "Results depend on the assumptions you enter. Tables and tax rules can go stale, and full retirement age and related Social Security modeling are simplified. Past or projected market returns are not a guarantee of future results.";

        public const string SplashAcknowledgement =
            "By continuing, you acknowledge that you use ExitPlan at your own risk for personal planning only.";

        /// <summary>
        /// Velopack <c>CurrentVersion</c> when this copy is installed, otherwise <see cref="Version"/>.
        /// Either value loses a trailing +git hash.
        /// </summary>
        public static string ChooseDisplayVersion(string? installedVersion)
        {
            var installed = WithoutGitHash(installedVersion);
            return installed.Length == 0 ? Version : installed;
        }

        public static string WithoutGitHash(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "";

            var plus = version.IndexOf('+');
            return (plus >= 0 ? version[..plus] : version).Trim();
        }

        private static string ReadInformationalVersion()
        {
            var informational = typeof(ProductInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
                return informational;

            var version = typeof(ProductInfo).Assembly.GetName().Version;
            if (version == null)
                return "";

            if (version.Revision > 0)
                return version.ToString();

            return version.Major + "." + version.Minor + "." + Math.Max(version.Build, 0);
        }
    }
}
