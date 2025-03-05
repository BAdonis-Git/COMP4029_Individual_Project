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

        // Register view models
        builder.Services.AddTransient<YourDevicesPageModel>();

        // Register pages
        builder.Services.AddTransient<YourDevicesPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}