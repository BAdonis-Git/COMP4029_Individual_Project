using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Dispatching;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Models.BCI.Muse;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.BCI.Muse.Platform;
using NeuroSpectator.Services.BCI.Muse.Core;

namespace NeuroSpectator.Services.BCI.Muse.Core
{
    /// <summary>
    /// Implementation of IBCIDeviceManager for Muse headbands
    /// </summary>
    public class MuseDeviceManager : IBCIDeviceManager, IMuseListener
    {
        private readonly MuseManager museManager;
        private readonly SemaphoreSlim semaphore = new(1, 1);
        private bool isScanning;
        private bool isDisposed;

        public ObservableCollection<IBCIDeviceInfo> AvailableDevices { get; } = new();

        public IBCIDevice CurrentDevice { get; private set; }

        public bool IsScanning => isScanning;

        public event EventHandler<List<IBCIDeviceInfo>> DeviceListChanged;
        public event EventHandler<BCIErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Creates a new instance of the MuseDeviceManager class
        /// </summary>
        public MuseDeviceManager()
        {
            try
            {
                // Check and extract native libraries if needed
                PlatformHelpers.ExtractNativeLibraries();

                // Initialize the Muse manager
                museManager = MuseManager.GetInstance();
                museManager.SetMuseListener(this);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    "Failed to initialize Muse manager",
                    ex,
                    BCIErrorType.NativeLibraryError));
            }
        }

        /// <summary>
        /// Starts scanning for Muse devices
        /// </summary>
        public async Task StartScanningAsync()
        {
            // Check Bluetooth permissions
            if (!PlatformHelpers.CheckBluetoothPermissions())
            {
                // Fixed: Call the async method properly
                await PlatformHelpers.RequestBluetoothPermissionsAsync();
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    "Bluetooth permissions not granted",
                    null,
                    BCIErrorType.PermissionDenied));
                return;
            }

            await semaphore.WaitAsync();
            try
            {
                isScanning = true;
                museManager.StartListening();
            }
            catch (Exception ex)
            {
                isScanning = false;
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    "Error starting scan",
                    ex,
                    BCIErrorType.ScanningFailed));
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Stops scanning for Muse devices
        /// </summary>
        public async Task StopScanningAsync()
        {
            await semaphore.WaitAsync();
            try
            {
                museManager.StopListening();
                isScanning = false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    "Error stopping scan",
                    ex,
                    BCIErrorType.ScanningFailed));
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Connects to a specified Muse device
        /// </summary>
        public async Task<IBCIDevice> ConnectToDeviceAsync(IBCIDeviceInfo deviceInfo)
        {
            if (deviceInfo == null) throw new ArgumentNullException(nameof(deviceInfo));

            if (deviceInfo is not MuseDeviceInfo museInfo)
            {
                throw new ArgumentException("DeviceInfo must be of type MuseDeviceInfo", nameof(deviceInfo));
            }

            await semaphore.WaitAsync();
            try
            {
                // Stop scanning while we connect to a device
                if (isScanning)
                {
                    Console.WriteLine("Stopping scanning before connecting to device");
                    await StopScanningAsync();
                }

                // Disconnect from current device if any
                if (CurrentDevice != null)
                {
                    Console.WriteLine("Disconnecting from current device before connecting to new device");
                    await DisconnectCurrentDeviceAsync();

                    // Small delay to ensure proper cleanup
                    await Task.Delay(1000);
                }

                // Log connection attempt
                Console.WriteLine($"Connecting to device: {museInfo.Name} ({museInfo.BluetoothMac})");

                // Create a new device instance
                var device = new MuseDevice(museInfo);

                try
                {
                    // Connect to the device
                    Console.WriteLine("Initiating connection sequence");
                    await device.ConnectAsync();

                    // Check if connection was successful
                    if (device.IsConnected)
                    {
                        Console.WriteLine($"Successfully connected to {museInfo.Name}");

                        // Set as current device
                        CurrentDevice = device;

                        // Return the device
                        return device;
                    }
                    else
                    {
                        Console.WriteLine($"Connection to {museInfo.Name} failed - device reports not connected");
                        device.Dispose();
                        throw new Exception("Device reported successful connection but is not connected");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during connection process: {ex.Message}");
                    device.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ConnectToDeviceAsync: {ex.Message}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    $"Error connecting to device {deviceInfo.Name}: {ex.Message}",
                    ex,
                    BCIErrorType.ConnectionFailed));
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Disconnects from the current Muse device
        /// </summary>
        public async Task DisconnectCurrentDeviceAsync()
        {
            if (CurrentDevice == null) return;

            await semaphore.WaitAsync();
            try
            {
                await CurrentDevice.DisconnectAsync();
                CurrentDevice.Dispose();
                CurrentDevice = null;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    "Error disconnecting from device",
                    ex,
                    BCIErrorType.DeviceDisconnected));
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Called when the Muse list changes
        /// </summary>
        public void MuseListChanged()
        {
            try
            {
                var muses = museManager.GetMuses();
                var deviceInfos = new List<IBCIDeviceInfo>();

                // Convert Muse SDK objects to our model objects properly
                foreach (var muse in muses)
                {
                    // Create a MuseDeviceInfo object from the Muse SDK object
                    var deviceInfo = new MuseDeviceInfo
                    {
                        Name = muse.Name,
                        BluetoothMac = muse.BluetoothMac,
                        RSSI = muse.RSSI
                    };

                    deviceInfos.Add(deviceInfo);
                }

                // Update UI on the main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AvailableDevices.Clear();
                    foreach (var deviceInfo in deviceInfos)
                    {
                        AvailableDevices.Add(deviceInfo);
                    }

                    // Notify subscribers
                    DeviceListChanged?.Invoke(this, deviceInfos);
                });
            }
            catch (Exception ex)
            {
                // Log the detailed exception for debugging purposes
                Console.WriteLine($"Device list processing error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    "Error processing device list",
                    ex,
                    BCIErrorType.Unknown));
            }
        }

        /// <summary>
        /// Disposes of the Muse device manager
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the Muse device manager
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    // Disconnect current device and stop scanning
                    DisconnectCurrentDeviceAsync().Wait();

                    if (isScanning)
                    {
                        StopScanningAsync().Wait();
                    }

                    semaphore.Dispose();
                }

                isDisposed = true;
            }
        }
    }
}