using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services.BCI;
using NeuroSpectator.Services.BCI.Factory;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.BCI.Muse.Core;
using NeuroSpectator.Services.BCI.Muse.Platform;
using NeuroSpectator.Services.Integration;
using NeuroSpectator.Services.Streaming;
using NeuroSpectator.Services.Visualisation;

namespace NeuroSpectator.Services
{
    /// <summary>
    /// Extension methods for registering services with the dependency injection container
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds BCI services to the service collection
        /// </summary>
        public static IServiceCollection AddBCIServices(this IServiceCollection services)
        {
            // Extract native libraries if needed
            PlatformHelpers.ExtractNativeLibraries();

            // Register the device factory
            services.AddSingleton<IBCIDeviceFactory, BCIDeviceFactory>();

            // Register device managers
            services.AddSingleton<IBCIDeviceManager>(sp =>
                sp.GetRequiredService<IBCIDeviceFactory>().GetDeviceManager(BCIDeviceType.MuseHeadband));

            // Register Muse-specific services explicitly for use cases that need them directly
            services.AddSingleton<MuseDeviceManager>();

            // Register the Device Connection Manager if not already registered
            if (!services.Any(s => s.ServiceType == typeof(DeviceConnectionManager)))
            {
                services.AddSingleton<DeviceConnectionManager>();
            }

            return services;
        }

        /// <summary>
        /// Adds streaming services to the service collection
        /// </summary>
        public static IServiceCollection AddStreamingServices(this IServiceCollection services)
        {
            // Register MAUI services needed for streaming if not already registered
            if (!services.Any(s => s.ServiceType == typeof(IConnectivity)))
            {
                services.AddSingleton<IConnectivity>(provider => Connectivity.Current);
            }

            // Register OBS integration service
            services.AddSingleton<OBSIntegrationService>();

            // Register streaming service for MK.IO
            services.AddSingleton<IMKIOStreamingService, MKIOStreamingService>();

            // Register brain data services
            services.AddSingleton<BrainDataVisualisationService>();
            services.AddSingleton<BrainDataJsonService>();

            // Register OBS integration helpers
            services.AddTransient<OBSSetupGuide>();

            return services;
        }

        /// <summary>
        /// Adds application services to the service collection
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Register the device settings service
            services.AddSingleton<DeviceSettingsService>();

            // Register streaming services
            services.AddStreamingServices();

            // Register integration services
            if (!services.Any(s => s.ServiceType == typeof(BrainDataOBSHelper)))
            {
                // The BrainDataOBSHelper requires more specific registration which is handled in MauiProgram.cs
                // because it depends on the IBCIDevice which may need custom resolution
                // services.AddTransient<BrainDataOBSHelper>();
            }

            return services;
        }
    }
}