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
        private readonly DeviceConnectionManager connectionManager;
        private readonly IDispatcher dispatcher;
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
        public MuseDeviceManager(IDispatcher dispatcher = null, DeviceConnectionManager connectionManager = null)
        {
            this.dispatcher = dispatcher;
            this.connectionManager = connectionManager;

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
                var device = new MuseDevice(museInfo, dispatcher, connectionManager);

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

                        // Register the device with the connection manager if available
                        // This is crucial to maintain consistent state across app
                        if (connectionManager != null)
                        {
                            Console.WriteLine($"Registering device with connection manager: {museInfo.Name}");
                            connectionManager.RegisterDevice(device);

                            // Explicitly notify the connection manager about the connection
                            connectionManager.NotifyConnected(device);
                        }
                        else
                        {
                            Console.WriteLine("No connection manager available for device registration");
                        }

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
                // Unregister the device from the connection manager if available
                connectionManager?.UnregisterDevice(CurrentDevice);

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
                Console.WriteLine("MuseListChanged: Processing updated device list");

                // Get the latest list from the Muse SDK
                var muses = museManager.GetMuses();

                // Log how many devices were found
                Console.WriteLine($"MuseListChanged: Raw device count from SDK: {muses?.Count ?? 0}");

                // Early exit if no devices found
                if (muses == null || muses.Count == 0)
                {
                    Console.WriteLine("MuseListChanged: No devices found in SDK response");

                    // Update UI on the main thread with empty list
                    if (dispatcher != null)
                    {
                        dispatcher.Dispatch(() =>
                        {
                            AvailableDevices.Clear();
                            DeviceListChanged?.Invoke(this, new List<IBCIDeviceInfo>());
                        });
                    }
                    else
                    {
                        AvailableDevices.Clear();
                        DeviceListChanged?.Invoke(this, new List<IBCIDeviceInfo>());
                    }

                    return;
                }

                // Process devices with better duplicate detection
                var deviceInfos = new List<IBCIDeviceInfo>();
                var uniqueMacs = new HashSet<string>(); // Track unique devices by MAC address
                var duplicateCount = 0;

                // Debug each device found
                foreach (var muse in muses)
                {
                    Console.WriteLine($"  Device found: '{muse.Name}' - MAC: {muse.BluetoothMac} - RSSI: {muse.RSSI}");

                    // Skip duplicates by MAC address
                    if (!String.IsNullOrEmpty(muse.BluetoothMac) && !uniqueMacs.Add(muse.BluetoothMac))
                    {
                        Console.WriteLine($"  Skipping duplicate device: {muse.Name} ({muse.BluetoothMac})");
                        duplicateCount++;
                        continue;
                    }

                    // Skip devices with empty/invalid names or MAC addresses
                    if (String.IsNullOrEmpty(muse.Name) || String.IsNullOrEmpty(muse.BluetoothMac))
                    {
                        Console.WriteLine($"  Skipping device with invalid name or MAC: {muse.Name} ({muse.BluetoothMac})");
                        continue;
                    }

                    // Create a MuseDeviceInfo object from the Muse SDK object
                    var deviceInfo = new MuseDeviceInfo
                    {
                        Name = muse.Name,
                        BluetoothMac = muse.BluetoothMac,
                        RSSI = muse.RSSI
                    };

                    deviceInfos.Add(deviceInfo);
                    Console.WriteLine($"  Added to available devices: {deviceInfo.Name} ({deviceInfo.DeviceId})");
                }

                Console.WriteLine($"MuseListChanged: Found {deviceInfos.Count} unique devices (filtered {duplicateCount} duplicates)");

                // Update UI on the main thread
                if (dispatcher != null)
                {
                    dispatcher.Dispatch(() =>
                    {
                        UpdateAvailableDevices(deviceInfos);
                    });
                }
                else
                {
                    UpdateAvailableDevices(deviceInfos);
                }
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
        /// Updates the available devices list
        /// </summary>
        private void UpdateAvailableDevices(List<IBCIDeviceInfo> deviceInfos)
        {
            // Deduplicate devices based on MAC address
            var uniqueDevices = deviceInfos
                .GroupBy(d => d.DeviceId)
                .Select(g => g.First())
                .ToList();

            // Update the available devices collection
            AvailableDevices.Clear();
            foreach (var deviceInfo in uniqueDevices)
            {
                AvailableDevices.Add(deviceInfo);
            }

            // Notify subscribers
            DeviceListChanged?.Invoke(this, uniqueDevices);

            // Check for automatic reconnection if needed
            TryReconnectToLastDeviceIfNeeded();
        }

        /// <summary>
        /// Tries to reconnect to the last connected device if needed
        /// </summary>
        private async void TryReconnectToLastDeviceIfNeeded()
        {
            // If we have no current device but there are available devices,
            // and we have a previously connected device ID saved, try to reconnect
            if (CurrentDevice == null && AvailableDevices.Count > 0)
            {
                string lastDeviceId = await GetLastConnectedDeviceIdAsync();
                if (!string.IsNullOrEmpty(lastDeviceId))
                {
                    // Look for the last connected device in the available devices
                    var lastDevice = AvailableDevices.FirstOrDefault(d => d.DeviceId == lastDeviceId);
                    if (lastDevice != null)
                    {
                        // Try to reconnect
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ConnectToDeviceAsync(lastDevice);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Auto-reconnect failed: {ex.Message}");
                            }
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Gets the ID of the last connected device
        /// </summary>
        private async Task<string> GetLastConnectedDeviceIdAsync()
        {
            // This would typically load from app settings or preferences
            // For now, we'll just return null
            return null;
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