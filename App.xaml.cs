using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace UniversalTrackerMarkers
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static class Const
        {
            public const string DefaultConfigName = "config.json";
            public const string DefaultConfigPath = "jangxx\\Universal Tracker Markers";
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            MainWindow wnd = new MainWindow();

            bool initSuccess = wnd.Init();

            if (!initSuccess)
            {
                Shutdown();
                return;
            }


            if (e.Args.Length > 0)
            {
                wnd.LoadConfig(e.Args[0]);
            }
            else
            {

                var defaultConfigFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Const.DefaultConfigPath,
                    Const.DefaultConfigName
                );

                Debug.WriteLine("Looking for configuration in " + defaultConfigFilePath);

                // try to load default config
                if (File.Exists(defaultConfigFilePath))
                {
                    wnd.LoadConfig(defaultConfigFilePath);
                }
                else
                {
                    Debug.WriteLine("Default config file doesn't exist. Skipping...");
                }
            }

            wnd.Show();

            //wnd.ProcessStartupConfig();
        }
    }
}
