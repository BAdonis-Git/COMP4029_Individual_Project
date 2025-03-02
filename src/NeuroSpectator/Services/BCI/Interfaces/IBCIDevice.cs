using System;
using System.Threading.Tasks;
using NeuroSpectator.Models.BCI.Common;

namespace NeuroSpectator.Services.BCI.Interfaces
{
    /// <summary>
    /// Represents a generic Brain-Computer Interface device
    /// </summary>
    public interface IBCIDevice : IDisposable
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
        /// Gets the current connection state of the device
        /// </summary>
        ConnectionState ConnectionState { get; }

        /// <summary>
        /// Gets a value indicating whether the device is currently connected
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the type of the BCI device
        /// </summary>
        BCIDeviceType DeviceType { get; }

        /// <summary>
        /// Connects to the device asynchronously
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// Disconnects from the device asynchronously
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Registers for brain wave data from the device
        /// </summary>
        void RegisterForBrainWaveData(BrainWaveTypes waveTypes);

        /// <summary>
        /// Unregisters from brain wave data
        /// </summary>
        void UnregisterFromBrainWaveData(BrainWaveTypes waveTypes);

        /// <summary>
        /// Event raised when the connection state changes
        /// </summary>
        event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        /// <summary>
        /// Event raised when brain wave data is received
        /// </summary>
        event EventHandler<BrainWaveDataEventArgs> BrainWaveDataReceived;

        /// <summary>
        /// Event raised when an artifact (like blink, jaw clench) is detected
        /// </summary>
        event EventHandler<ArtifactEventArgs> ArtifactDetected;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event EventHandler<BCIErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Gets battery level as a percentage
        /// </summary>
        Task<double> GetBatteryLevelAsync();

        /// <summary>
        /// Gets signal quality as a value between 0 (poor) and 1 (excellent)
        /// </summary>
        Task<double> GetSignalQualityAsync();
    }
}