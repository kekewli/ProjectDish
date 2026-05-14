using AutoUpdaterDotNET;
using ProjectDish.MVVM.Models;
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
        public static UserModel CurrentUser { get; set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AutoUpdater.Mandatory = true;

            AutoUpdater.RunUpdateAsAdmin = false;

            AutoUpdater.Start("https://github.com/kekewli/ProjectDish/releases/download/release/update.xml");
        }
    }

}
