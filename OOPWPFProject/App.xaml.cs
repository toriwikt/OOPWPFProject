using System.Windows;
using SQLitePCL;

namespace OOPWPFProject
{
    public partial class App : Application
    {
        public App()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Batteries.Init();
            DatabaseManager.Initialize();
            var login = new LoginWindow();
            login.Show();
        }
    }
}