using AutoUpdaterDotNET;
using System.Configuration;
using System.Data;
using System.Windows;

namespace ProjectDish
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AutoUpdater.Mandatory = true;

            // AutoUpdater.UpdateMode = Mode.ForcedDownload; 

            AutoUpdater.RunUpdateAsAdmin = false;

            AutoUpdater.Start("https://github.com/kekewli/ProjectDish/releases/download/release/update.xml");
        }
    }

}
