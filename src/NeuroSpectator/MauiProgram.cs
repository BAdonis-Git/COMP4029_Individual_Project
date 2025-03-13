using Microsoft.Extensions.Logging;
using NeuroSpectator.PageModels;
using NeuroSpectator.Pages;
using NeuroSpectator.Services;
using NeuroSpectator.Services.Authentication;
using NeuroSpectator.Services.Storage;
using NeuroSpectator.Services.Account;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Maui;

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

        // Register services
        builder.Services.AddBCIServices();
        builder.Services.AddApplicationServices();

        // Register authentication and storage services

        // Use offline authentication service templates
        //builder.Services.AddSingleton<AuthenticationService, DebugAuthenticationService>();
        //builder.Services.AddSingleton<UserStorageService>(serviceProvider =>
        //    new DebugUserStorageService());
        // Use real services
        builder.Services.AddSingleton<AuthenticationService>();
        builder.Services.AddSingleton<UserStorageService>(serviceProvider => 
            new UserStorageService(
                "DefaultEndpointsProtocol=https;AccountName=neurospectatorstorage;AccountKey=ZaiVwMLyTtZ/KP6EnhzbWWYvDHCMgEdg6ASouE1edz5c8tHTaApus7dUNtEaskEnXbOgvJCCH7g++AStWjVNTg==;EndpointSuffix=core.windows.net")); // Replace with your connection string

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

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        Services = app.Services;
        return app;
    }
}