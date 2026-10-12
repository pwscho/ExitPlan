namespace ExitPlan.Core
{
    public static class ProductInfo
    {
        public const string Name = "ExitPlan";
        public const string Version = "2.3";
        public const string Title = "ExitPlan 2.3";

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
    }
}
