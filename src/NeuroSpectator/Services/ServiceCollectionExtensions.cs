using Microsoft.Extensions.DependencyInjection;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services.BCI.Factory;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.BCI.Muse.Core;
using NeuroSpectator.Services.BCI.Muse.Platform;
using NeuroSpectator.Services;

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

            return services;
        }

        /// <summary>
        /// Adds application services to the service collection
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Register the device settings service
            services.AddSingleton<DeviceSettingsService>();

            // Add other application services here

            return services;
        }
    }
}