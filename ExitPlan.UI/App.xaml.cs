using System.Windows;
using ExitPlan.UI.Updates;
using ExitPlan.UI.Views;

namespace ExitPlan.UI
{
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                AppLog.Write("Unhandled exception.", e.ExceptionObject as Exception);
                if (e.ExceptionObject is not Exception)
                    AppLog.Write(e.ExceptionObject?.ToString() ?? "Unknown unhandled exception.");
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // The disclaimer is the only window until Continue. The default
            // shutdown mode would exit when that dialog closes.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            UpdateChecker.StartBackgroundCheck();

            var disclaimer = new DisclaimerWindow();
            bool accepted = disclaimer.ShowDialog() == true;
            if (!accepted)
            {
                Shutdown();
                return;
            }

            var main = new MainWindow();
            MainWindow = main;
            main.Show();
            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
    }
}



