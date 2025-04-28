using NeuroSpectator.Pages;
using System.Diagnostics;

namespace NeuroSpectator
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Use instead of direct instantiation
            if (MauiProgram.Services != null)
            {
                var loginPage = MauiProgram.Services.GetService<LoginPage>();
                if (loginPage != null)
                {
                    MainPage = loginPage;
                }
                else
                {
                    Debug.WriteLine("ERROR: LoginPage could not be resolved from services");
                    // Fallback but log the issue
                    MainPage = new LoginPage();
                }
            }
            else
            {
                MainPage = new LoginPage();
            }
        }
    }
}
