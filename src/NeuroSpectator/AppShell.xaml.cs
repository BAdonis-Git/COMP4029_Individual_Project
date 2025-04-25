using System;
using Microsoft.Maui.Controls;

namespace NeuroSpectator
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes for navigation
            Routing.RegisterRoute(nameof(Pages.YourDevicesPage), typeof(Pages.YourDevicesPage));
            Routing.RegisterRoute(nameof(Pages.DevicePresetsPage), typeof(Pages.DevicePresetsPage));

            // Register new pages
            Routing.RegisterRoute(nameof(Pages.YourDashboardPage), typeof(Pages.YourDashboardPage));
            Routing.RegisterRoute(nameof(Pages.YourNexusPage), typeof(Pages.YourNexusPage));
            Routing.RegisterRoute(nameof(Pages.BrowsePage), typeof(Pages.BrowsePage));
            Routing.RegisterRoute(nameof(Pages.ModPage), typeof(Pages.ModPage));
            Routing.RegisterRoute(nameof(Pages.StreamSpectatorPage), typeof(Pages.StreamSpectatorPage));

            // Register authentication pages
            Routing.RegisterRoute(nameof(Pages.LoginPage), typeof(Pages.LoginPage));
            Routing.RegisterRoute(nameof(Pages.YourAccountPage), typeof(Pages.YourAccountPage));
        }
    }
}