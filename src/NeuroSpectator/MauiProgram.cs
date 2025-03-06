using Microsoft.Extensions.Logging;
using NeuroSpectator.PageModels;
using NeuroSpectator.Pages;
using NeuroSpectator.Services;
using CommunityToolkit.Maui;

namespace NeuroSpectator;

/// <summary>
/// Main entry point for the MAUI app
/// </summary>
public static class MauiProgram
{
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

        // Register services
        builder.Services.AddBCIServices();
        builder.Services.AddApplicationServices();

        // Register view models
        builder.Services.AddTransient<YourDevicesPageModel>();
        builder.Services.AddTransient<YourDashboardPageModel>();
        builder.Services.AddTransient<YourNexusPageModel>();
        builder.Services.AddTransient<BrowsePageModel>();
        builder.Services.AddTransient<ModPageModel>();
        builder.Services.AddTransient<StreamStreamerPageModel>();
        builder.Services.AddTransient<StreamSpectatorPageModel>();
        builder.Services.AddTransient<DevicePresetsPageModel>();

        // Register pages
        builder.Services.AddTransient<YourDevicesPage>();
        builder.Services.AddTransient<YourDashboardPage>();
        builder.Services.AddTransient<YourNexusPage>();
        builder.Services.AddTransient<BrowsePage>();
        builder.Services.AddTransient<ModPage>();
        builder.Services.AddTransient<StreamStreamerPage>();
        builder.Services.AddTransient<StreamSpectatorPage>();
        builder.Services.AddTransient<DevicePresetsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}