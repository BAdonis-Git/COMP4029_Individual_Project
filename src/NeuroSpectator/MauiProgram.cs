using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NeuroSpectator.Controls;
using NeuroSpectator.PageModels;
using NeuroSpectator.Pages;
using NeuroSpectator.Services;
using NeuroSpectator.Services.Account;
using NeuroSpectator.Services.Authentication;
using NeuroSpectator.Services.BCI;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.Integration;
using NeuroSpectator.Services.Storage;
using NeuroSpectator.Services.Streaming;
using NeuroSpectator.Services.Visualisation;

namespace NeuroSpectator;

/// <summary>
/// Main entry point for the MAUI app
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Service provider for accessing services from non-DI contexts
    /// </summary>
    public static IServiceProvider Services { get; private set; }

    /// <summary>
    /// Creates the MAUI app
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Add configuration from appsettings.json
        builder.Configuration.AddJsonFile("appsettings.json", optional: true);

        // Register MAUI services
        builder.Services.AddSingleton<IConnectivity>(provider => Connectivity.Current);

        // Register device connection manager
        builder.Services.AddSingleton<DeviceConnectionManager>();

        // Register core services
        builder.Services.AddBCIServices();
        builder.Services.AddApplicationServices();

        // Register MK.IO streaming services
        builder.Services.AddSingleton<MKIOConfig>(provider =>
            new MKIOConfig(provider.GetRequiredService<IConfiguration>()));

        builder.Services.AddSingleton<IMKIOStreamingService, MKIOStreamingService>();

        // Register OBS integration and visualization services 
        builder.Services.AddSingleton<OBSIntegrationService>();
        builder.Services.AddSingleton<BrainDataVisualisationService>();
        builder.Services.AddSingleton<BrainDataJsonService>();
        builder.Services.AddTransient<OBSSetupGuide>();

        // Register the BrainDataOBSHelper with explicit dependencies
        builder.Services.AddTransient<BrainDataOBSHelper>(serviceProvider =>
        {
            var deviceManager = serviceProvider.GetRequiredService<IBCIDeviceManager>();
            var obsService = serviceProvider.GetRequiredService<OBSIntegrationService>();
            var visualizationService = serviceProvider.GetRequiredService<BrainDataVisualisationService>();
            var jsonService = serviceProvider.GetRequiredService<BrainDataJsonService>();

            // Handle potential null current device
            var device = deviceManager.CurrentDevice ?? deviceManager.GetDefaultDeviceOrNull();

            if (device == null)
            {
                // Throw an exception to make errors more visible
                throw new InvalidOperationException("No BCI device available for BrainDataOBSHelper");
            }

            return new BrainDataOBSHelper(
                device,
                obsService,
                visualizationService,
                jsonService
            );
        });

        // Register authentication and storage services
        builder.Services.AddSingleton<AuthenticationService>();
        builder.Services.AddSingleton<UserStorageService>(serviceProvider =>
            new UserStorageService(
                "DefaultEndpointsProtocol=https;AccountName=neurospectatorstorage;AccountKey=ZaiVwMLyTtZ/KP6EnhzbWWYvDHCMgEdg6ASouE1edz5c8tHTaApus7dUNtEaskEnXbOgvJCCH7g++AStWjVNTg==;EndpointSuffix=core.windows.net"));

        builder.Services.AddSingleton<AccountService>();

        // Register view models
        builder.Services.AddTransient<YourDevicesPageModel>();
        builder.Services.AddTransient<YourDashboardPageModel>();
        builder.Services.AddTransient<YourNexusPageModel>();
        builder.Services.AddTransient<BrowsePageModel>();
        builder.Services.AddTransient<ModPageModel>();
        builder.Services.AddTransient<StreamStreamerPageModel>();
        builder.Services.AddTransient<StreamSpectatorPageModel>();
        builder.Services.AddTransient<DevicePresetsPageModel>();
        builder.Services.AddTransient<LoginPageModel>();
        builder.Services.AddTransient<YourAccountPageModel>();

        // Register pages
        builder.Services.AddTransient<YourDevicesPage>();
        builder.Services.AddTransient<YourDashboardPage>();
        builder.Services.AddTransient<YourNexusPage>();
        builder.Services.AddTransient<BrowsePage>();
        builder.Services.AddTransient<ModPage>();
        builder.Services.AddTransient<StreamStreamerPage>();
        builder.Services.AddTransient<StreamSpectatorPage>();
        builder.Services.AddTransient<DevicePresetsPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<YourAccountPage>();

        // Register MKIOPlayer
        builder.Services.AddTransient<MKIOPlayer>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        Services = app.Services;
        return app;
    }
}

// Extension method for IBCIDeviceManager to handle null CurrentDevice
public static class BCIDeviceManagerExtensions
{
    public static IBCIDevice GetDefaultDeviceOrNull(this IBCIDeviceManager deviceManager)
    {
        if (deviceManager == null)
            return null;

        // Check if there are any available devices
        if (deviceManager.AvailableDevices.Count > 0)
        {
            // Try to connect to the first available device
            var deviceInfo = deviceManager.AvailableDevices[0];
            try
            {
                // Need sync result necessary for this specific use case
                return deviceManager.ConnectToDeviceAsync(deviceInfo).GetAwaiter().GetResult();
            }
            catch
            {
                // Failed to connect, return null
                return null;
            }
        }

        return null;
    }
}