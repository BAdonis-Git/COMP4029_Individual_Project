using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Models.BCI.Muse;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.BCI.Muse.Core;
using Microsoft.Maui.Dispatching;

namespace NeuroSpectator.Services.BCI.Muse
{
    /// <summary>
    /// Implementation of IBCIDevice for Muse headbands
    /// </summary>
    public class MuseDevice : IBCIDevice
    {
        private readonly MuseDeviceInfo deviceInfo;
        private readonly IDispatcher dispatcher;
        private readonly DeviceConnectionManager connectionManager;
        private Core.Muse museDevice;
        private NeuroSpectator.Models.BCI.Common.ConnectionState connectionState = NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected;
        private bool isDisposed;
        private int reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 3;
        private readonly SemaphoreSlim connectionSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource monitoringCts;

        // Track registered data types for reconnection
        private BrainWaveTypes registeredWaveTypes = BrainWaveTypes.None;

        /// <summary>
        /// Gets the name of the device
        /// </summary>
        public string Name => deviceInfo.Name;

        /// <summary>
        /// Gets the unique identifier for the device
        /// </summary>
        public string DeviceId => deviceInfo.DeviceId;

        /// <summary>
        /// Gets the current connection state of the device
        /// </summary>
        public NeuroSpectator.Models.BCI.Common.ConnectionState ConnectionState => connectionState;

        /// <summary>
        /// Gets a value indicating whether the device is currently connected
        /// </summary>
        public bool IsConnected => connectionState == NeuroSpectator.Models.BCI.Common.ConnectionState.Connected;

        /// <summary>
        /// Gets the type of the BCI device
        /// </summary>
        public BCIDeviceType DeviceType => deviceInfo.DeviceType;

        /// <summary>
        /// Gets the signal strength of the device
        /// </summary>
        public double SignalStrength => deviceInfo.SignalStrength;

        // Events
        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
        public event EventHandler<BrainWaveDataEventArgs> BrainWaveDataReceived;
        public event EventHandler<ArtifactEventArgs> ArtifactDetected;
        public event EventHandler<BCIErrorEventArgs> ErrorOccurred;

        // For the first few errors, log details, then stop to avoid spam
        static int errorCount = 0;

        /// <summary>
        /// Creates a new instance of the MuseDevice class
        /// </summary>
        public MuseDevice(MuseDeviceInfo deviceInfo, IDispatcher dispatcher = null, DeviceConnectionManager connectionManager = null)
        {
            this.deviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
            this.dispatcher = dispatcher;
            this.connectionManager = connectionManager; // Optional, can be null

            Debug.WriteLine($"Created MuseDevice: {deviceInfo.Name} ({deviceInfo.BluetoothMac})");
        }

        /// <summary>
        /// Connects to the device asynchronously with robust error handling
        /// </summary>
        public async Task ConnectAsync()
        {
            await connectionSemaphore.WaitAsync();
            try
            {
                if (IsConnected)
                {
                    Debug.WriteLine($"Device {Name} is already connected");
                    return;
                }

                // Update connection state
                UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState.Connecting);

                // Notify connection manager if available
                if (connectionManager != null)
                {
                    NotifyConnecting();
                }

                // Create Muse device if needed
                if (museDevice == null)
                {
                    var info = new MuseInfo
                    {
                        Name = deviceInfo.Name,
                        BluetoothMac = deviceInfo.BluetoothMac,
                        RSSI = deviceInfo.RSSI
                    };

                    museDevice = Core.Muse.GetInstance(info);
                }

                // Check current state and force disconnect if needed
                try
                {
                    var state = museDevice.GetConnectionState();
                    Debug.WriteLine($"Current device state before connection: {state}");

                    if (state != Core.ConnectionState.DISCONNECTED)
                    {
                        Debug.WriteLine("Device not in DISCONNECTED state. Disconnecting first.");
                        try
                        {
                            museDevice.Disconnect();
                            await Task.Delay(1000); // Brief delay for disconnection
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Pre-connect disconnect failed: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking connection state: {ex.Message}");
                }

                // Step 1: Clear all existing listeners
                try
                {
                    museDevice.UnregisterAllListeners();
                    Debug.WriteLine("Cleared existing listeners");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error clearing listeners: {ex.Message}");
                }

                // Step 2: Register essential listeners
                museDevice.RegisterConnectionListener(new MuseConnectionHandler(this));
                museDevice.RegisterErrorListener(new MuseErrorHandler(this));

                // Step 3: Set preset BEFORE connecting
                try
                {
                    museDevice.SetPreset(MusePreset.PRESET_21);
                    Debug.WriteLine("Set device preset to PRESET_21");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting preset: {ex.Message}");
                }

                // Step 4: Enable data transmission BEFORE connecting
                try
                {
                    museDevice.EnableDataTransmission(true);
                    Debug.WriteLine("Enabled data transmission");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error enabling data transmission: {ex.Message}");
                }

                // Step 5: Connect using RunAsynchronously instead of Connect
                Debug.WriteLine($"Starting connection to Muse device: {deviceInfo.Name}");
                try
                {
                    // CRITICAL CHANGE: Use RunAsynchronously instead of Connect
                    museDevice.RunAsynchronously();
                    Debug.WriteLine("Connection started asynchronously");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception during RunAsynchronously: {ex.Message}");
                    throw;
                }

                // Wait for connection with improved state checking
                var success = await WaitForConnectionStateWithDirectCheck(NeuroSpectator.Models.BCI.Common.ConnectionState.Connected, TimeSpan.FromSeconds(30));

                if (success)
                {
                    Debug.WriteLine($"Successfully connected to Muse device: {deviceInfo.Name}");
                    reconnectAttempts = 0;

                    // Re-register for brain wave data if needed
                    if (registeredWaveTypes != BrainWaveTypes.None)
                    {
                        RegisterForBrainWaveData(registeredWaveTypes);
                    }

                    // Set up connection monitoring
                    StartConnectionMonitoring();

                    // Notify connection manager
                    if (connectionManager != null)
                    {
                        NotifyConnected();
                    }
                }
                else
                {
                    Debug.WriteLine($"Failed to connect to Muse device: {deviceInfo.Name} (timeout)");
                    UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected);

                    if (connectionManager != null)
                    {
                        NotifyConnectionFailed("Connection timeout");
                    }

                    throw new TimeoutException("Connection timeout");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error connecting to Muse device: {ex.Message}");
                UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected);

                if (connectionManager != null)
                {
                    NotifyConnectionFailed(ex.Message);
                }

                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs($"Error connecting to device: {ex.Message}", ex, BCIErrorType.ConnectionFailed));
                throw;
            }
            finally
            {
                connectionSemaphore.Release();
            }
        }

        private async Task<bool> WaitForConnectionStateWithDirectCheck(NeuroSpectator.Models.BCI.Common.ConnectionState targetState, TimeSpan timeout)
        {
            var startTime = DateTime.Now;
            var endTime = startTime + timeout;
            int logInterval = 0;

            while (DateTime.Now < endTime)
            {
                try
                {
                    // Check the actual device state directly
                    var deviceState = museDevice.GetConnectionState();
                    var mappedState = MapConnectionState(deviceState);

                    // Log periodically
                    logInterval += 100;
                    if (logInterval >= 1000)
                    {
                        Debug.WriteLine($"Current state: {deviceState} (mapped: {mappedState}), waiting for: {targetState} (elapsed: {(DateTime.Now - startTime).TotalMilliseconds}ms)");
                        logInterval = 0;
                    }

                    // If actual device state matches what we want, update our state and return success
                    if (mappedState == targetState)
                    {
                        UpdateConnectionState(targetState);
                        Debug.WriteLine($"Device reached target state: {targetState} after {(DateTime.Now - startTime).TotalMilliseconds}ms");
                        return true;
                    }

                    // Also check our cached state (might be updated by events)
                    if (connectionState == targetState)
                    {
                        Debug.WriteLine($"Connection state reached target state: {targetState} after {(DateTime.Now - startTime).TotalMilliseconds}ms");
                        return true;
                    }

                    // Check for error states
                    if (targetState == NeuroSpectator.Models.BCI.Common.ConnectionState.Connected &&
                        (mappedState == NeuroSpectator.Models.BCI.Common.ConnectionState.NeedsUpdate ||
                         mappedState == NeuroSpectator.Models.BCI.Common.ConnectionState.NeedsLicense))
                    {
                        Debug.WriteLine($"Cannot connect: device needs update or license");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking connection state: {ex.Message}");
                }

                await Task.Delay(100);
            }

            Debug.WriteLine($"Timed out waiting for state: {targetState} after {(DateTime.Now - startTime).TotalMilliseconds}ms");
            return connectionState == targetState;
        }

        private static NeuroSpectator.Models.BCI.Common.ConnectionState MapConnectionState(Core.ConnectionState state)
        {
            return state switch
            {
                Core.ConnectionState.CONNECTED => NeuroSpectator.Models.BCI.Common.ConnectionState.Connected,
                Core.ConnectionState.CONNECTING => NeuroSpectator.Models.BCI.Common.ConnectionState.Connecting,
                Core.ConnectionState.DISCONNECTED => NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected,
                Core.ConnectionState.NEEDS_UPDATE => NeuroSpectator.Models.BCI.Common.ConnectionState.NeedsUpdate,
                Core.ConnectionState.NEEDS_LICENSE => NeuroSpectator.Models.BCI.Common.ConnectionState.NeedsLicense,
                _ => NeuroSpectator.Models.BCI.Common.ConnectionState.Unknown
            };
        }


        /// <summary>
        /// Disconnects from the device asynchronously
        /// </summary>
        public async Task DisconnectAsync()
        {
            await connectionSemaphore.WaitAsync();

            try
            {
                if (!IsConnected && ConnectionState != NeuroSpectator.Models.BCI.Common.ConnectionState.Connecting)
                {
                    Debug.WriteLine($"Device {Name} is already disconnected");
                    return;
                }

                Debug.WriteLine($"Disconnecting from Muse device: {deviceInfo.Name}");

                // Stop connection monitoring
                StopConnectionMonitoring();

                // First unregister from all data listeners to avoid callbacks during disconnect
                UnregisterFromBrainWaveData(BrainWaveTypes.All);

                // Then disconnect
                if (museDevice != null)
                {
                    try
                    {
                        // Use a try-catch block here specifically for the assertion failure
                        museDevice.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error during Muse disconnect (ignoring): {ex.Message}");
                        // Still update our internal state even if the SDK throws
                        UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected);
                    }

                    // Wait for state change or timeout
                    await WaitForConnectionStateAsync(NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected, TimeSpan.FromSeconds(5));
                }
                else
                {
                    // No device, so just update state
                    UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected);
                }

                // Notify connection manager if available
                if (connectionManager != null)
                {
                    NotifyDisconnected();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disconnecting from Muse device: {ex.Message}");

                // Force the state to disconnected regardless of errors
                UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected);

                // Notify connection manager if available
                if (connectionManager != null)
                {
                    NotifyDisconnected();
                }

                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs($"Error disconnecting from device: {ex.Message}", ex, BCIErrorType.DeviceDisconnected));
            }
            finally
            {
                // Ensure we're marked as disconnected regardless of what happened
                UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected);
                connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Registers for brain wave data with the specified types
        /// </summary>
        public void RegisterForBrainWaveData(BrainWaveTypes waveTypes)
        {
            if (!IsConnected)
            {
                Debug.WriteLine($"Saving registration for wave types {waveTypes} for when device is connected");
                // Save which types to register for when connected
                registeredWaveTypes |= waveTypes;
                return;
            }

            try
            {
                Debug.WriteLine($"Registering for brain wave data: {waveTypes}");
                registeredWaveTypes |= waveTypes;
                var handler = new MuseDataHandler(this);

                if ((waveTypes & BrainWaveTypes.Alpha) != 0)
                {
                    museDevice.RegisterDataListener(handler, MuseDataPacketType.ALPHA_ABSOLUTE);
                }

                if ((waveTypes & BrainWaveTypes.Beta) != 0)
                {
                    museDevice.RegisterDataListener(handler, MuseDataPacketType.BETA_ABSOLUTE);
                }

                if ((waveTypes & BrainWaveTypes.Delta) != 0)
                {
                    museDevice.RegisterDataListener(handler, MuseDataPacketType.DELTA_ABSOLUTE);
                }

                if ((waveTypes & BrainWaveTypes.Theta) != 0)
                {
                    museDevice.RegisterDataListener(handler, MuseDataPacketType.THETA_ABSOLUTE);
                }

                if ((waveTypes & BrainWaveTypes.Gamma) != 0)
                {
                    museDevice.RegisterDataListener(handler, MuseDataPacketType.GAMMA_ABSOLUTE);
                }

                if ((waveTypes & BrainWaveTypes.Raw) != 0)
                {
                    museDevice.RegisterDataListener(handler, MuseDataPacketType.EEG);
                }

                // Register for artifacts (blinks, jaw clench, etc.)
                museDevice.RegisterDataListener(handler, MuseDataPacketType.ARTIFACTS);

                // Enable data transmission
                museDevice.EnableDataTransmission(true);

                Debug.WriteLine($"Successfully registered for brain wave data: {waveTypes}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering for brain wave data: {ex.Message}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs($"Error registering for brain wave data: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Unregisters from brain wave data with the specified types
        /// </summary>
        public void UnregisterFromBrainWaveData(BrainWaveTypes waveTypes)
        {
            if (!IsConnected || museDevice == null)
                return;

            try
            {
                Debug.WriteLine($"Unregistering from brain wave data: {waveTypes}");
                registeredWaveTypes &= ~waveTypes;

                if (waveTypes == BrainWaveTypes.All)
                {
                    // Special case - unregister everything
                    try
                    {
                        museDevice.UnregisterAllListeners();

                        // Re-register for connection events (to maintain state tracking)
                        museDevice.RegisterConnectionListener(new MuseConnectionHandler(this));
                        museDevice.RegisterErrorListener(new MuseErrorHandler(this));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error unregistering all listeners: {ex.Message}");
                    }
                    registeredWaveTypes = BrainWaveTypes.None;
                    return;
                }

                var handler = new MuseDataHandler(this);

                if ((waveTypes & BrainWaveTypes.Alpha) != 0)
                {
                    museDevice.UnregisterDataListener(handler, MuseDataPacketType.ALPHA_ABSOLUTE);
                }

                if ((waveTypes & BrainWaveTypes.Beta) != 0)
                {
                    museDevice.UnregisterDataListener(handler, MuseDataPacketType.BETA_ABSOLUTE);
                }

                if ((waveTypes & BrainWaveTypes.Delta) != 0)
                {
                    museDevice.UnregisterDataListener(handler, MuseDataPacketType.DELTA_ABSOLUTE);
                }

                if ((waveTypes & BrainWaveTypes.Theta) != 0)
                {
                    museDevice.UnregisterDataListener(handler, MuseDataPacketType.THETA_ABSOLUTE);
                }

                if ((waveTypes & BrainWaveTypes.Gamma) != 0)
                {
                    museDevice.UnregisterDataListener(handler, MuseDataPacketType.GAMMA_ABSOLUTE);
                }

                if ((waveTypes & BrainWaveTypes.Raw) != 0)
                {
                    museDevice.UnregisterDataListener(handler, MuseDataPacketType.EEG);
                }

                // If we've unregistered everything, disable data transmission
                if (registeredWaveTypes == BrainWaveTypes.None)
                {
                    museDevice.EnableDataTransmission(false);
                }

                Debug.WriteLine($"Successfully unregistered from brain wave data: {waveTypes}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unregistering from brain wave data: {ex.Message}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs($"Error unregistering from brain wave data: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Gets the battery level as a percentage with improved error handling and debugging
        /// </summary>
        public async Task<double> GetBatteryLevelAsync()
        {
            if (!IsConnected)
            {
                Debug.WriteLine("GetBatteryLevelAsync: Device not connected");
                return 0;
            }

            try
            {
                // Check if museDevice is null
                if (museDevice == null)
                {
                    Debug.WriteLine("GetBatteryLevelAsync: museDevice is null");
                    return 0;
                }

                // Add detailed logging
                Debug.WriteLine("GetBatteryLevelAsync: Attempting to get configuration");

                // Get the configuration with try/catch
                var config = museDevice.GetMuseConfiguration();

                // Check if config is null
                if (config == null)
                {
                    Debug.WriteLine("GetBatteryLevelAsync: Configuration is null");
                    return 0;
                }

                // Log the battery value
                Debug.WriteLine($"GetBatteryLevelAsync: Battery level: {config.BatteryPercentRemaining}%");

                // Ensure the value is within reasonable bounds (0-100)
                double batteryLevel = Math.Clamp(config.BatteryPercentRemaining, 0, 100);
                return batteryLevel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting battery level: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs($"Error getting battery level: {ex.Message}", ex));

                // Return a default value on error
                return 0;
            }
        }

        /// <summary>
        /// Gets the signal quality as a value between 0 (poor) and 1 (excellent)
        /// </summary>
        public async Task<double> GetSignalQualityAsync()
        {
            if (!IsConnected)
                return 0;

            try
            {
                // Calculate signal quality from DRL/REF data or HSI_PRECISION if available
                // This is a simplified implementation - in a real app, you would
                // calculate this based on the electrode contact quality

                // For now, just return a value based on the connection state
                return IsConnected ? 0.8 : 0.0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting signal quality: {ex.Message}");
                return IsConnected ? 0.5 : 0.0; // Default to medium quality if connected
            }
        }

        /// <summary>
        /// Performs a simple diagnostic test of the device
        /// </summary>
        public async Task<string> PerformDiagnosticTestAsync()
        {
            if (!IsConnected)
                return "Device not connected";

            try
            {
                var result = new System.Text.StringBuilder();
                result.AppendLine($"Device: {Name} ({DeviceId})");

                // Check battery level
                var battery = await GetBatteryLevelAsync();
                result.AppendLine($"Battery: {battery:F1}%");

                // Check signal quality
                var quality = await GetSignalQualityAsync();
                result.AppendLine($"Signal Quality: {quality:F2}");

                // Get device configuration
                var config = museDevice.GetMuseConfiguration();
                result.AppendLine($"Headband Name: {config.HeadbandName}");
                result.AppendLine($"Model: {config.Model}");
                result.AppendLine($"EEG Channels: {config.EegChannelCount}");

                return result.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error performing diagnostic test: {ex.Message}");
                return $"Diagnostic error: {ex.Message}";
            }
        }

        /// <summary>
        /// Updates the connection state and raises the event
        /// </summary>
        internal void UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState newState)
        {
            if (connectionState == newState)
                return;

            var oldState = connectionState;
            connectionState = newState;

            Debug.WriteLine($"Device {Name} connection state changed: {oldState} → {newState}");

            // Invoke the event on the main thread if dispatcher is provided
            if (dispatcher != null)
            {
                dispatcher.Dispatch(() =>
                    ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState))
                );
            }
            else
            {
                // Otherwise invoke directly
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState));
            }
        }

        /// <summary>
        /// Handles a brain wave data packet
        /// </summary>
        internal void HandleBrainWaveData(MuseDataPacket packet)
        {
            if (!IsConnected)
                return;

            try
            {
                // Convert the Muse data packet to our BrainWaveData format
                BrainWaveData data = null;

                // Validate and correct timestamp before using it
                DateTimeOffset timestamp = SafeCreateDateTimeOffset(packet.Timestamp);

                switch (packet.PacketType)
                {
                    case MuseDataPacketType.ALPHA_ABSOLUTE:
                        data = BrainWaveData.CreateAlpha(packet.Values, timestamp);
                        break;

                    case MuseDataPacketType.BETA_ABSOLUTE:
                        data = BrainWaveData.CreateBeta(packet.Values, timestamp);
                        break;

                    case MuseDataPacketType.DELTA_ABSOLUTE:
                        data = BrainWaveData.CreateDelta(packet.Values, timestamp);
                        break;

                    case MuseDataPacketType.THETA_ABSOLUTE:
                        data = BrainWaveData.CreateTheta(packet.Values, timestamp);
                        break;

                    case MuseDataPacketType.GAMMA_ABSOLUTE:
                        data = BrainWaveData.CreateGamma(packet.Values, timestamp);
                        break;

                    case MuseDataPacketType.EEG:
                        data = BrainWaveData.CreateRaw(packet.Values, timestamp);
                        break;
                }

                if (data != null)
                {
                    // Dispatch to UI thread if we have a dispatcher
                    if (dispatcher != null)
                    {
                        dispatcher.Dispatch(() =>
                            BrainWaveDataReceived?.Invoke(this, new BrainWaveDataEventArgs(data))
                        );
                    }
                    else
                    {
                        BrainWaveDataReceived?.Invoke(this, new BrainWaveDataEventArgs(data));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling brain wave data: {ex.Message}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs($"Error handling brain wave data: {ex.Message}", ex));
            }
        }

        private DateTimeOffset SafeCreateDateTimeOffset(long timestamp)
        {
            try
            {
                // If timestamp is a reasonable value, use it directly
                // Unix timestamps are typically 13 digits for milliseconds
                if (timestamp > 1000000000000 && timestamp < 10000000000000)
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                }

                // For excessively large timestamps, it might be a raw device timestamp
                // Let's avoid logging each one to prevent console spam
                if (timestamp > 10000000000000)
                {
                    // No debug message here to avoid spam
                    // Try to convert from whatever format to a reasonable one
                    // The conversion method depends on how the device reports time

                    // Option 1: Might be in microseconds or nanoseconds
                    long milliseconds;
                    if (timestamp > 1000000000000000) // Likely nanoseconds (more than 16 digits)
                        milliseconds = timestamp / 1000000;
                    else // Likely microseconds (around 16 digits)
                        milliseconds = timestamp / 1000;

                    // Verify the result makes sense (roughly within last 10 years)
                    long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long tenYearsMs = 10L * 365 * 24 * 60 * 60 * 1000;

                    if (Math.Abs(nowMs - milliseconds) < tenYearsMs)
                        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
                }

                // Fallback - use current time without logging to avoid spam
                return DateTimeOffset.Now;
            }
            catch (ArgumentOutOfRangeException)
            {
                if (errorCount < 3)
                {
                    Debug.WriteLine($"Invalid timestamp value: {timestamp}, using current time instead");
                    errorCount++;

                    if (errorCount == 3)
                        Debug.WriteLine("Suppressing further timestamp error messages...");
                }

                return DateTimeOffset.Now;
            }
        }

        /// <summary>
        /// Handles an artifact packet
        /// </summary>
        internal void HandleArtifact(MuseArtifactPacket packet)
        {
            if (!IsConnected)
                return;

            try
            {
                // Use the same safe timestamp creation method
                DateTimeOffset timestamp = SafeCreateDateTimeOffset(packet.Timestamp);

                var eventArgs = new ArtifactEventArgs(
                    packet.Blink,
                    packet.JawClench,
                    !packet.HeadbandOn,
                    timestamp);

                // Rest of the method remains the same...
                if (dispatcher != null)
                {
                    dispatcher.Dispatch(() =>
                        ArtifactDetected?.Invoke(this, eventArgs)
                    );
                }
                else
                {
                    ArtifactDetected?.Invoke(this, eventArgs);
                }

                // If the headband is too loose, report that to connection manager
                if (!packet.HeadbandOn && connectionManager != null)
                {
                    NotifyDeviceWarning("Headband adjustment needed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling artifact: {ex.Message}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs($"Error handling artifact: {ex.Message}", ex));
            }
        }

        /// <summary>
        /// Waits for the connection state to change to the specified state
        /// </summary>
        private async Task<bool> WaitForConnectionStateAsync(NeuroSpectator.Models.BCI.Common.ConnectionState targetState, TimeSpan timeout)
        {
            var startTime = DateTime.Now;
            var endTime = startTime + timeout;

            while (DateTime.Now < endTime)
            {
                if (connectionState == targetState)
                    return true;

                // Use a short delay to avoid burning CPU
                await Task.Delay(100);

                // If we're waiting to connect and the state is NeedsUpdate or NeedsLicense, fail
                if (targetState == NeuroSpectator.Models.BCI.Common.ConnectionState.Connected &&
                    (connectionState == NeuroSpectator.Models.BCI.Common.ConnectionState.NeedsUpdate ||
                     connectionState == NeuroSpectator.Models.BCI.Common.ConnectionState.NeedsLicense))
                {
                    return false;
                }
            }

            return connectionState == targetState;
        }

        /// <summary>
        /// Starts monitoring the connection status
        /// </summary>
        private void StartConnectionMonitoring()
        {
            // Cancel any existing monitoring
            StopConnectionMonitoring();

            // Create a new cancellation token source
            monitoringCts = new CancellationTokenSource();

            // Start connection monitoring task
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!monitoringCts.IsCancellationRequested)
                    {
                        // Only check connection if we think we're connected
                        if (IsConnected && museDevice != null)
                        {
                            try
                            {
                                // Check the actual Muse connection state
                                var actualState = museDevice.GetConnectionState();
                                if (actualState != Core.ConnectionState.CONNECTED)
                                {
                                    Debug.WriteLine($"Connection monitor detected device disconnection: {actualState}");

                                    // Update our state
                                    UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected);

                                    // Try to reconnect if appropriate
                                    await TryReconnectAsync();
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error checking connection state: {ex.Message}");
                            }
                        }

                        // Check every 2 seconds
                        await Task.Delay(2000, monitoringCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, ignore
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in connection monitoring: {ex.Message}");
                }
            }, monitoringCts.Token);
        }

        /// <summary>
        /// Stops connection monitoring
        /// </summary>
        private void StopConnectionMonitoring()
        {
            if (monitoringCts != null)
            {
                monitoringCts.Cancel();
                monitoringCts.Dispose();
                monitoringCts = null;
            }
        }

        /// <summary>
        /// Verifies that the connection to the device is working properly
        /// </summary>
        /// <returns>True if the connection is verified, false otherwise</returns>
        public async Task<bool> VerifyConnectionAsync()
        {
            try
            {
                // Check if the device is connected
                if (!IsConnected)
                {
                    return false;
                }

                // Perform a basic command to verify the connection
                // This will depend on your specific device and SDK
                // For example, try to get the battery level
                double batteryLevel = await GetBatteryLevelAsync();

                // If we got here without an exception, the connection is working
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection verification failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets detailed information about the device
        /// </summary>
        /// <returns>A dictionary of device details</returns>
        public Dictionary<string, string> GetDeviceDetails()
        {
            Dictionary<string, string> details = new Dictionary<string, string>
    {
        { "Name", Name },
        { "DeviceId", DeviceId },
        { "DeviceType", DeviceType.ToString() },
        { "ConnectionState", ConnectionState.ToString() }
    };

            // Add more device-specific details if available
            // This will depend on your specific device and SDK

            return details;
        }

        /// <summary>
        /// Attempts to reconnect to the device
        /// </summary>
        private async Task TryReconnectAsync()
        {
            // Don't reconnect if we're disposed or have exceeded max attempts
            if (isDisposed || reconnectAttempts >= MaxReconnectAttempts)
                return;

            try
            {
                reconnectAttempts++;
                Debug.WriteLine($"Attempting to reconnect ({reconnectAttempts}/{MaxReconnectAttempts})...");

                // Notify connection manager
                if (connectionManager != null)
                {
                    NotifyReconnecting();
                }

                // Try to reconnect
                await ConnectAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Reconnection attempt failed: {ex.Message}");

                if (reconnectAttempts >= MaxReconnectAttempts)
                {
                    Debug.WriteLine("Maximum reconnection attempts reached");

                    // Notify connection manager
                    if (connectionManager != null)
                    {
                        NotifyConnectionFailed("Maximum reconnection attempts reached");
                    }
                }
            }
        }

        /// <summary>
        /// Notifies the connection manager that the device is connecting
        /// </summary>
        private void NotifyConnecting()
        {
            try
            {
                connectionManager?.RegisterDevice(this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NotifyConnecting: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies the connection manager that the device is connected
        /// </summary>
        private void NotifyConnected()
        {
            try
            {
                // Additional logic can be added here if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NotifyConnected: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies the connection manager that the device is disconnected
        /// </summary>
        private void NotifyDisconnected()
        {
            try
            {
                // Additional logic can be added here if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NotifyDisconnected: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies the connection manager that the device connection failed
        /// </summary>
        private void NotifyConnectionFailed(string reason)
        {
            try
            {
                // Additional logic can be added here if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NotifyConnectionFailed: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies the connection manager that the device is reconnecting
        /// </summary>
        private void NotifyReconnecting()
        {
            try
            {
                // Additional logic can be added here if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NotifyReconnecting: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies the connection manager of a device error
        /// </summary>
        private void NotifyDeviceError(string errorMessage)
        {
            try
            {
                // Additional logic can be added here if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NotifyDeviceError: {ex.Message}");
            }
        }

        /// <summary>
        /// Notifies the connection manager of a device warning
        /// </summary>
        private void NotifyDeviceWarning(string warningMessage)
        {
            try
            {
                // Additional logic can be added here if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in NotifyDeviceWarning: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    // Stop connection monitoring
                    StopConnectionMonitoring();

                    // Disconnect from the device
                    if (IsConnected || ConnectionState == NeuroSpectator.Models.BCI.Common.ConnectionState.Connecting)
                    {
                        try
                        {
                            DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                        }
                        catch
                        {
                            // Ignore exceptions during disposal
                        }
                    }

                    if (museDevice != null)
                    {
                        try
                        {
                            museDevice.UnregisterAllListeners();
                        }
                        catch
                        {
                            // Ignore exceptions during disposal
                        }
                        museDevice = null;
                    }

                    // Dispose of semaphore
                    connectionSemaphore.Dispose();
                }

                isDisposed = true;
            }
        }

        #region Muse API Event Handlers

        /// <summary>
        /// Handler for Muse connection events
        /// </summary>
        private class MuseConnectionHandler : IMuseConnectionListener
        {
            private readonly MuseDevice device;

            public MuseConnectionHandler(MuseDevice device)
            {
                this.device = device;
            }

            public void ReceiveMuseConnectionPacket(MuseConnectionPacket packet, Core.Muse muse)
            {
                try
                {
                    // Map the Muse connection state to our connection state
                    NeuroSpectator.Models.BCI.Common.ConnectionState newState;

                    switch (packet.CurrentConnectionState)
                    {
                        case Core.ConnectionState.CONNECTED:
                            newState = NeuroSpectator.Models.BCI.Common.ConnectionState.Connected;
                            break;
                        case Core.ConnectionState.CONNECTING:
                            newState = NeuroSpectator.Models.BCI.Common.ConnectionState.Connecting;
                            break;
                        case Core.ConnectionState.DISCONNECTED:
                            newState = NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected;
                            break;
                        case Core.ConnectionState.NEEDS_UPDATE:
                            newState = NeuroSpectator.Models.BCI.Common.ConnectionState.NeedsUpdate;
                            break;
                        case Core.ConnectionState.NEEDS_LICENSE:
                            newState = NeuroSpectator.Models.BCI.Common.ConnectionState.NeedsLicense;
                            break;
                        default:
                            newState = NeuroSpectator.Models.BCI.Common.ConnectionState.Unknown;
                            break;
                    }

                    // Update our state
                    device.UpdateConnectionState(newState);

                    // If state changed to disconnected, try to reconnect
                    if (newState == NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected &&
                        packet.PreviousConnectionState == Core.ConnectionState.CONNECTED)
                    {
                        // Use Task.Run to avoid blocking the callback
                        Task.Run(() => device.TryReconnectAsync());
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling connection packet: {ex.Message}");
                    device.ErrorOccurred?.Invoke(device, new BCIErrorEventArgs($"Error handling connection packet: {ex.Message}", ex));
                }
            }
        }

        /// <summary>
        /// Handler for Muse data events
        /// </summary>
        private class MuseDataHandler : IMuseDataListener
        {
            private readonly MuseDevice device;

            public MuseDataHandler(MuseDevice device)
            {
                this.device = device;
            }

            public void ReceiveMuseDataPacket(MuseDataPacket packet, Core.Muse muse)
            {
                device.HandleBrainWaveData(packet);
            }

            public void ReceiveMuseArtifactPacket(MuseArtifactPacket packet, Core.Muse muse)
            {
                device.HandleArtifact(packet);
            }
        }

        /// <summary>
        /// Handler for Muse error events
        /// </summary>
        private class MuseErrorHandler : IMuseErrorListener
        {
            private readonly MuseDevice device;

            public MuseErrorHandler(MuseDevice device)
            {
                this.device = device;
            }

            public void ReceiveError(MuseError packet, Core.Muse muse)
            {
                try
                {
                    // Map the error type
                    BCIErrorType errorType;

                    switch (packet.Type)
                    {
                        case ErrorType.FAILURE:
                            errorType = BCIErrorType.Unknown;
                            break;
                        case ErrorType.TIMEOUT:
                            errorType = BCIErrorType.ConnectionFailed;
                            break;
                        case ErrorType.OVERLOADED:
                        case ErrorType.UNIMPLEMENTED:
                        default:
                            errorType = BCIErrorType.Unknown;
                            break;
                    }

                    string errorMessage = $"Muse error: {packet.Info} (Code: {packet.Code}, Type: {packet.Type})";
                    Debug.WriteLine(errorMessage);

                    // Notify connection manager if available
                    if (device.connectionManager != null)
                    {
                        device.NotifyDeviceError(errorMessage);
                    }

                    // Raise the error event
                    device.ErrorOccurred?.Invoke(device, new BCIErrorEventArgs(
                        errorMessage,
                        null,
                        errorType));

                    // If we get an error and the device is marked as connected, check its actual state
                    if (device.IsConnected)
                    {
                        try
                        {
                            var state = device.museDevice.GetConnectionState();
                            if (state != Core.ConnectionState.CONNECTED)
                            {
                                // Update our state
                                device.UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected);

                                // Try to reconnect
                                Task.Run(() => device.TryReconnectAsync());
                            }
                        }
                        catch
                        {
                            // If we can't check the state, assume disconnected
                            device.UpdateConnectionState(NeuroSpectator.Models.BCI.Common.ConnectionState.Disconnected);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling Muse error: {ex.Message}");
                }
            }
        }

        #endregion
    }
}