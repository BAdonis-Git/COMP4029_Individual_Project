using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Models.BCI.Muse;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.BCI.Muse;
using NeuroSpectator.Services.BCI.Muse.Core;

namespace NeuroSpectator.Services.BCI.Factory
{
    /// <summary>
    /// Factory for creating BCI device instances
    /// </summary>
    public class BCIDeviceFactory : IBCIDeviceFactory
    {
        private readonly Dictionary<BCIDeviceType, IBCIDeviceManager> deviceManagers;

        /// <summary>
        /// Creates a new instance of the BCIDeviceFactory class
        /// </summary>
        public BCIDeviceFactory()
        {
            // Initialize device managers
            deviceManagers = new Dictionary<BCIDeviceType, IBCIDeviceManager>
            {
                { BCIDeviceType.MuseHeadband, new MuseDeviceManager() }
                // Add more device managers as they're implemented
            };
        }

        /// <summary>
        /// Creates a device instance based on device info
        /// </summary>
        public async Task<IBCIDevice> CreateDeviceAsync(IBCIDeviceInfo deviceInfo)
        {
            if (deviceInfo == null) throw new ArgumentNullException(nameof(deviceInfo));

            // Get the appropriate device manager based on the device type
            if (deviceManagers.TryGetValue(deviceInfo.DeviceType, out var manager))
            {
                // Connect to the device
                return await manager.ConnectToDeviceAsync(deviceInfo);
            }

            throw new NotSupportedException($"Device type {deviceInfo.DeviceType} is not supported");
        }

        /// <summary>
        /// Gets all available BCI device manager implementations
        /// </summary>
        public IEnumerable<IBCIDeviceManager> GetDeviceManagers()
        {
            return deviceManagers.Values;
        }

        /// <summary>
        /// Gets a device manager for a specific device type
        /// </summary>
        public IBCIDeviceManager GetDeviceManager(BCIDeviceType deviceType)
        {
            if (deviceManagers.TryGetValue(deviceType, out var manager))
            {
                return manager;
            }

            throw new NotSupportedException($"Device type {deviceType} is not supported");
        }

        /// <summary>
        /// Gets all supported device types by this factory
        /// </summary>
        public IEnumerable<BCIDeviceType> GetSupportedDeviceTypes()
        {
            return deviceManagers.Keys;
        }
    }
}