using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NeuroSpectator.Models.BCI.Common;

namespace NeuroSpectator.Services.BCI.Interfaces
{
    /// <summary>
    /// Manages the discovery and connection to BCI devices
    /// </summary>
    public interface IBCIDeviceManager : IDisposable
    {
        /// <summary>
        /// Gets a collection of available device infos discovered during scanning
        /// </summary>
        ObservableCollection<IBCIDeviceInfo> AvailableDevices { get; }

        /// <summary>
        /// Gets the currently connected device, if any
        /// </summary>
        IBCIDevice CurrentDevice { get; }

        /// <summary>
        /// Gets a value indicating whether a scan is currently in progress
        /// </summary>
        bool IsScanning { get; }

        /// <summary>
        /// Starts scanning for available BCI devices
        /// </summary>
        Task StartScanningAsync();

        /// <summary>
        /// Stops scanning for available BCI devices
        /// </summary>
        Task StopScanningAsync();

        /// <summary>
        /// Connects to the specified device
        /// </summary>
        Task<IBCIDevice> ConnectToDeviceAsync(IBCIDeviceInfo deviceInfo);

        /// <summary>
        /// Disconnects from the current device, if any
        /// </summary>
        Task DisconnectCurrentDeviceAsync();

        /// <summary>
        /// Event raised when the list of available devices changes
        /// </summary>
        event EventHandler<List<IBCIDeviceInfo>> DeviceListChanged;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event EventHandler<BCIErrorEventArgs> ErrorOccurred;
    }
}