using NeuroSpectator.Models.BCI.Common;
namespace NeuroSpectator.Services.BCI.Interfaces
{
    /// <summary>
    /// Represents information about a discovered BCI device
    /// </summary>
    public interface IBCIDeviceInfo
    {
        /// <summary>
        /// Gets the name of the device
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the unique identifier for the device (e.g. Bluetooth MAC address)
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// Gets the signal strength indicator (e.g. RSSI for Bluetooth devices)
        /// </summary>
        double SignalStrength { get; }

        /// <summary>
        /// Gets the type of the BCI device
        /// </summary>
        BCIDeviceType DeviceType { get; }
    }
}