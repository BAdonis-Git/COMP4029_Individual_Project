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

        }
    }
}
