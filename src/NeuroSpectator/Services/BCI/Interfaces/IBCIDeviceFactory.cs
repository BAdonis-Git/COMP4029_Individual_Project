using NeuroSpectator.Models.BCI.Common;

namespace NeuroSpectator.Services.BCI.Interfaces
{
    /// <summary>
    /// Factory interface for creating BCI device instances
    /// </summary>
    public interface IBCIDeviceFactory
    {
        /// <summary>
        /// Creates a device instance based on device info
        /// </summary>
        Task<IBCIDevice> CreateDeviceAsync(IBCIDeviceInfo deviceInfo);

        /// <summary>
        /// Gets all available BCI device manager implementations
        /// </summary>
        IEnumerable<IBCIDeviceManager> GetDeviceManagers();

        /// <summary>
        /// Gets a device manager for a specific device type
        /// </summary>
        IBCIDeviceManager GetDeviceManager(BCIDeviceType deviceType);

        /// <summary>
        /// Gets all supported device types by this factory
        /// </summary>
        IEnumerable<BCIDeviceType> GetSupportedDeviceTypes();
    }
}