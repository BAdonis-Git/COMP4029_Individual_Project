using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Models.BCI.Muse;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.BCI.Muse.Core;
using NeuroSpectator.Services.BCI.Muse.Interop;

// Using aliases to resolve ambiguities
using BCIConnectionState = NeuroSpectator.Models.BCI.Common.ConnectionState;
using MuseConnectionState = NeuroSpectator.Services.BCI.Muse.Core.ConnectionState;

namespace NeuroSpectator.Services.BCI.Muse
{
    /// <summary>
    /// Implementation of IBCIDevice for the Muse headband
    /// </summary>
    public class MuseDevice : IBCIDevice, IMuseConnectionListener, IMuseDataListener, IMuseErrorListener
    {
        private readonly NeuroSpectator.Services.BCI.Muse.Core.Muse museDevice;
        private readonly MuseDeviceInfo deviceInfo;
        private readonly Dictionary<BrainWaveTypes, MuseDataPacketType> waveTypeMapping;
        private readonly Dictionary<MuseDataPacketType, BrainWaveTypes> packetTypeMapping;
        private bool isDisposed;
        private bool isConnecting;
        private bool isDisconnecting;
        private SemaphoreSlim connectionSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Gets the name of the Muse device
        /// </summary>
        public string Name => deviceInfo.Name;

        /// <summary>
        /// Gets the device ID (MAC address) of the Muse device
        /// </summary>
        public string DeviceId => deviceInfo.BluetoothMac;

        /// <summary>
        /// Gets the connection state of the Muse device
        /// </summary>
        public BCIConnectionState ConnectionState => MapConnectionState(museDevice.GetConnectionState());

        /// <summary>
        /// Gets a value indicating whether the Muse device is connected
        /// </summary>
        public bool IsConnected => ConnectionState == BCIConnectionState.Connected;

        /// <summary>
        /// Gets the type of the device
        /// </summary>
        public BCIDeviceType DeviceType => BCIDeviceType.MuseHeadband;

        // Events
        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
        public event EventHandler<BrainWaveDataEventArgs> BrainWaveDataReceived;
        public event EventHandler<ArtifactEventArgs> ArtifactDetected;
        public event EventHandler<BCIErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Creates a new instance of the MuseDevice class
        /// </summary>
        public MuseDevice(MuseDeviceInfo deviceInfo)
        {
            this.deviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));

            // Create a MuseInfo object from MuseDeviceInfo
            var museInfo = new MuseInfo
            {
                Name = deviceInfo.Name,
                BluetoothMac = deviceInfo.BluetoothMac,
                RSSI = deviceInfo.RSSI
            };

            museDevice = NeuroSpectator.Services.BCI.Muse.Core.Muse.GetInstance(museInfo);

            // Map brain wave types to Muse data packet types
            waveTypeMapping = new Dictionary<BrainWaveTypes, MuseDataPacketType>
            {
                { BrainWaveTypes.Alpha, MuseDataPacketType.ALPHA_ABSOLUTE },
                { BrainWaveTypes.Beta, MuseDataPacketType.BETA_ABSOLUTE },
                { BrainWaveTypes.Delta, MuseDataPacketType.DELTA_ABSOLUTE },
                { BrainWaveTypes.Theta, MuseDataPacketType.THETA_ABSOLUTE },
                { BrainWaveTypes.Gamma, MuseDataPacketType.GAMMA_ABSOLUTE },
                { BrainWaveTypes.Raw, MuseDataPacketType.EEG }
            };

            // Map Muse data packet types to brain wave types
            packetTypeMapping = new Dictionary<MuseDataPacketType, BrainWaveTypes>
            {
                { MuseDataPacketType.ALPHA_ABSOLUTE, BrainWaveTypes.Alpha },
                { MuseDataPacketType.BETA_ABSOLUTE, BrainWaveTypes.Beta },
                { MuseDataPacketType.DELTA_ABSOLUTE, BrainWaveTypes.Delta },
                { MuseDataPacketType.THETA_ABSOLUTE, BrainWaveTypes.Theta },
                { MuseDataPacketType.GAMMA_ABSOLUTE, BrainWaveTypes.Gamma },
                { MuseDataPacketType.EEG, BrainWaveTypes.Raw },
                { MuseDataPacketType.BATTERY, BrainWaveTypes.None }
            };

            // Register listeners
            museDevice.RegisterConnectionListener(this);
            museDevice.RegisterErrorListener(this);
        }

        /// <summary>
        /// Connects to the Muse device asynchronously
        /// </summary>
        public async Task ConnectAsync()
        {
            await connectionSemaphore.WaitAsync();
            try
            {
                if (isConnecting || IsConnected)
                {
                    Console.WriteLine("Already connecting or connected. Skipping connect operation.");
                    return;
                }

                var state = museDevice.GetConnectionState();
                Console.WriteLine($"Current device state before connection attempt: {state}");

                // Force disconnection if not in DISCONNECTED state
                if (state != MuseConnectionState.DISCONNECTED)
                {
                    Console.WriteLine($"Device not in DISCONNECTED state (current: {state}). Attempting to disconnect first.");
                    try
                    {
                        museDevice.Disconnect();
                        await Task.Delay(1000); // Brief delay for disconnection
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Pre-connect disconnect failed: {ex.Message}");
                    }
                }

                isConnecting = true;
                Console.WriteLine("Starting connection process...");

                try
                {
                    // Step 1: Clear all existing listeners
                    museDevice.UnregisterAllListeners();
                    Console.WriteLine("Cleared existing listeners");

                    // Step 2: Register essential listeners BEFORE connection
                    museDevice.RegisterConnectionListener(this);
                    museDevice.RegisterErrorListener(this);
                    Console.WriteLine("Registered connection and error listeners");

                    // Step 3: Set the preset BEFORE connection attempt
                    // Using PRESET_21 which works well for most models
                    museDevice.SetPreset(MusePreset.PRESET_21);
                    Console.WriteLine("Set device preset to PRESET_21");

                    // Step 4: Enable data transmission before connecting
                    museDevice.EnableDataTransmission(true);
                    Console.WriteLine("Enabled data transmission");

                    // Step 5: Register data listeners BEFORE connection
                    Console.WriteLine("Registering for brain wave data types");
                    foreach (BrainWaveTypes type in Enum.GetValues(typeof(BrainWaveTypes)))
                    {
                        if (type != BrainWaveTypes.None && type != BrainWaveTypes.All &&
                            waveTypeMapping.TryGetValue(type, out MuseDataPacketType packetType))
                        {
                            Console.WriteLine($"Registering for {type} data");
                            museDevice.RegisterDataListener(this, packetType);
                        }
                    }

                    // Always register for artifacts and battery
                    Console.WriteLine("Registering for artifact data");
                    museDevice.RegisterDataListener(this, MuseDataPacketType.ARTIFACTS);
                    Console.WriteLine("Registering for battery data");
                    museDevice.RegisterDataListener(this, MuseDataPacketType.BATTERY);

                    // Step 6: Start connection - USING RUNASYNCHRONOUSLY!
                    Console.WriteLine("Starting connection to device");
                    museDevice.RunAsynchronously();

                    // Step 7: Wait for connection to establish
                    var connectionSuccess = await WaitForConnection(
                        MuseConnectionState.CONNECTED, timeoutMs: 15000);

                    if (!connectionSuccess)
                    {
                        throw new Exception("Failed to establish connection within timeout period");
                    }

                    Console.WriteLine("Connection established successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during connection: {ex.Message}");
                    // Try to disconnect cleanly if connection fails
                    try
                    {
                        museDevice.Disconnect();
                    }
                    catch { /* Ignore exceptions during cleanup */ }
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ConnectAsync: {ex.Message}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    $"Connection failed: {ex.Message}", ex, BCIErrorType.ConnectionFailed));
            }
            finally
            {
                isConnecting = false;
                connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Wait for a specific connection state
        /// </summary>
        private async Task<bool> WaitForConnection(
            MuseConnectionState targetState, int timeoutMs = 10000, int pollIntervalMs = 500)
        {
            Console.WriteLine($"Waiting for device to reach state: {targetState}");
            int elapsed = 0;

            while (elapsed < timeoutMs)
            {
                var currentState = museDevice.GetConnectionState();

                if (elapsed % 1000 == 0) // Log every second
                {
                    Console.WriteLine($"Current state: {currentState}, waiting for: {targetState} (elapsed: {elapsed}ms)");
                }

                if (currentState == targetState)
                {
                    Console.WriteLine($"Device reached target state: {targetState} after {elapsed}ms");
                    return true;
                }

                await Task.Delay(pollIntervalMs);
                elapsed += pollIntervalMs;
            }

            Console.WriteLine($"Timed out waiting for state: {targetState} after {elapsed}ms");
            return false;
        }

        /// <summary>
        /// Disconnects from the Muse device asynchronously
        /// </summary>
        public async Task DisconnectAsync()
        {
            await connectionSemaphore.WaitAsync();
            try
            {
                if (isDisconnecting)
                {
                    Console.WriteLine("Already disconnecting. Skipping disconnect operation.");
                    return;
                }

                // Check if already disconnected
                var currentState = museDevice.GetConnectionState();
                if (currentState == MuseConnectionState.DISCONNECTED)
                {
                    Console.WriteLine("Device already disconnected.");
                    return;
                }

                isDisconnecting = true;
                Console.WriteLine("Starting disconnection process...");

                try
                {
                    // First disable data transmission
                    Console.WriteLine("Disabling data transmission");
                    museDevice.EnableDataTransmission(false);

                    // Brief delay to let data transmission stop
                    await Task.Delay(200);

                    // Unregister all listeners except connection listener
                    Console.WriteLine("Unregistering listeners");
                    museDevice.UnregisterAllListeners();

                    // Re-register connection listener
                    Console.WriteLine("Re-registering connection listener");
                    museDevice.RegisterConnectionListener(this);

                    // Perform the disconnection
                    Console.WriteLine("Sending disconnect command");
                    museDevice.Disconnect();

                    // Wait for the device to actually disconnect
                    bool disconnected = await WaitForConnection(
                        MuseConnectionState.DISCONNECTED, timeoutMs: 5000);

                    if (disconnected)
                    {
                        Console.WriteLine("Device disconnected successfully");
                    }
                    else
                    {
                        Console.WriteLine("Disconnect timeout - device might still be connected");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during disconnect operation: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DisconnectAsync: {ex.Message}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    $"Disconnection failed: {ex.Message}", ex, BCIErrorType.DeviceDisconnected));
            }
            finally
            {
                isDisconnecting = false;
                connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Registers for brain wave data from the Muse device
        /// </summary>
        public void RegisterForBrainWaveData(BrainWaveTypes waveTypes)
        {
            try
            {
                Console.WriteLine($"Registering for brain wave data: {waveTypes}");

                // Handle the All flag
                if (waveTypes.HasFlag(BrainWaveTypes.All))
                {
                    // Register for all types except None and All
                    foreach (BrainWaveTypes type in Enum.GetValues(typeof(BrainWaveTypes)))
                    {
                        if (type != BrainWaveTypes.None && type != BrainWaveTypes.All &&
                            waveTypeMapping.TryGetValue(type, out MuseDataPacketType packetType))
                        {
                            Console.WriteLine($"Registering for {type} data");
                            museDevice.RegisterDataListener(this, packetType);
                        }
                    }
                }
                else
                {
                    // Register only for specific types
                    foreach (BrainWaveTypes type in Enum.GetValues(typeof(BrainWaveTypes)))
                    {
                        if (type != BrainWaveTypes.None && type != BrainWaveTypes.All &&
                            waveTypes.HasFlag(type) &&
                            waveTypeMapping.TryGetValue(type, out MuseDataPacketType packetType))
                        {
                            Console.WriteLine($"Registering for {type} data");
                            museDevice.RegisterDataListener(this, packetType);
                        }
                    }
                }

                // Always register for artifacts
                Console.WriteLine("Registering for artifact data");
                museDevice.RegisterDataListener(this, MuseDataPacketType.ARTIFACTS);

                // Always register for battery
                Console.WriteLine("Registering for battery data");
                museDevice.RegisterDataListener(this, MuseDataPacketType.BATTERY);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registering for brain wave data: {ex.Message}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    $"Failed to register data: {ex.Message}", ex, BCIErrorType.Unknown));
            }
        }

        /// <summary>
        /// Unregisters from brain wave data
        /// </summary>
        public void UnregisterFromBrainWaveData(BrainWaveTypes waveTypes)
        {
            try
            {
                Console.WriteLine($"Unregistering from brain wave data: {waveTypes}");

                // Handle the All flag
                if (waveTypes.HasFlag(BrainWaveTypes.All))
                {
                    foreach (BrainWaveTypes type in Enum.GetValues(typeof(BrainWaveTypes)))
                    {
                        if (type != BrainWaveTypes.None && type != BrainWaveTypes.All &&
                            waveTypeMapping.TryGetValue(type, out MuseDataPacketType packetType))
                        {
                            Console.WriteLine($"Unregistering from {type} data");
                            museDevice.UnregisterDataListener(this, packetType);
                        }
                    }

                    // Also unregister from artifacts
                    Console.WriteLine("Unregistering from artifact data");
                    museDevice.UnregisterDataListener(this, MuseDataPacketType.ARTIFACTS);

                    // And battery
                    Console.WriteLine("Unregistering from battery data");
                    museDevice.UnregisterDataListener(this, MuseDataPacketType.BATTERY);
                }
                else
                {
                    // Unregister only from specific types
                    foreach (BrainWaveTypes type in Enum.GetValues(typeof(BrainWaveTypes)))
                    {
                        if (type != BrainWaveTypes.None && type != BrainWaveTypes.All &&
                            waveTypes.HasFlag(type) &&
                            waveTypeMapping.TryGetValue(type, out MuseDataPacketType packetType))
                        {
                            Console.WriteLine($"Unregistering from {type} data");
                            museDevice.UnregisterDataListener(this, packetType);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error unregistering from brain wave data: {ex.Message}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    $"Failed to unregister data: {ex.Message}", ex, BCIErrorType.Unknown));
            }
        }

        /// <summary>
        /// Gets battery level from the device
        /// </summary>
        public async Task<double> GetBatteryLevelAsync()
        {
            if (!IsConnected)
            {
                Console.WriteLine("Cannot get battery level: device not connected");
                return 0.0;
            }

            try
            {
                var config = museDevice.GetMuseConfiguration();
                if (config != null)
                {
                    // Use property directly, not a getter method
                    double batteryPercent = config.BatteryPercentRemaining;
                    Console.WriteLine($"Battery level: {batteryPercent}%");
                    return batteryPercent;
                }
                else
                {
                    Console.WriteLine("Cannot get battery level: configuration is null");
                    return 0.0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting battery level: {ex.Message}");
                return 0.0;
            }
        }

        /// <summary>
        /// Gets signal quality by checking configuration properties
        /// </summary>
        public async Task<double> GetSignalQualityAsync()
        {
            if (!IsConnected)
            {
                Console.WriteLine("Cannot get signal quality: device not connected");
                return 0.0;
            }

            // In a real implementation, you'd use the horseshoe values or DRL/REF data
            // For now, we're returning a placeholder value
            return await Task.FromResult(1.0);
        }

        /// <summary>
        /// Gets detailed information about the connected device
        /// </summary>
        public Dictionary<string, string> GetDeviceDetails()
        {
            var details = new Dictionary<string, string>();

            if (!IsConnected)
            {
                details["Status"] = "Not connected";
                return details;
            }

            try
            {
                var config = museDevice.GetMuseConfiguration();
                if (config != null)
                {
                    // Use properties directly instead of getter methods
                    details["Name"] = config.HeadbandName;
                    details["Model"] = config.Model.ToString();
                    details["Serial"] = config.SerialNumber;
                    details["BluetoothMAC"] = config.BluetoothMac;
                    details["Battery"] = $"{config.BatteryPercentRemaining}%";

                    // Technical configuration information
                    details["EEGChannels"] = config.EegChannelCount.ToString();
                    details["CurrentPreset"] = config.Preset.ToString();
                    details["SampleRate"] = config.DownsampleRate.ToString();
                    details["NotchFilterEnabled"] = config.NotchFilterEnabled.ToString();
                    details["DRL/REFEnabled"] = config.DrlRefEnabled.ToString();

                    // Additional useful details
                    details["AFEGain"] = config.AfeGain.ToString();
                    details["OutputFrequency"] = config.OutputFrequency.ToString();
                    details["ADCFrequency"] = config.AdcFrequency.ToString();
                }
                else
                {
                    details["Error"] = "Configuration object is null";
                }
            }
            catch (Exception ex)
            {
                details["Error"] = $"Failed to retrieve details: {ex.Message}";
            }

            return details;
        }

        /// <summary>
        /// Receives a connection packet from the Muse device
        /// </summary>
        public void ReceiveMuseConnectionPacket(MuseConnectionPacket packet, NeuroSpectator.Services.BCI.Muse.Core.Muse muse)
        {
            try
            {
                Console.WriteLine($"Connection state changed: {packet.PreviousConnectionState} -> {packet.CurrentConnectionState}");

                var oldState = MapConnectionState(packet.PreviousConnectionState);
                var newState = MapConnectionState(packet.CurrentConnectionState);

                // If we're now connected, ensure data transmission is enabled
                if (packet.CurrentConnectionState == MuseConnectionState.CONNECTED)
                {
                    Console.WriteLine("Device connected successfully!");

                    try
                    {
                        // Ensure data transmission is enabled
                        museDevice.EnableDataTransmission(true);
                        Console.WriteLine("Data transmission confirmed enabled");

                        // Make sure data listeners are registered
                        RegisterForBrainWaveData(BrainWaveTypes.All);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in post-connection setup: {ex.Message}");
                    }
                }
                // If we're disconnected, log it
                else if (packet.CurrentConnectionState == MuseConnectionState.DISCONNECTED)
                {
                    Console.WriteLine("Device disconnected");
                }

                // Notify subscribers of the state change
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing connection packet: {ex.Message}");
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    $"Connection state error: {ex.Message}", ex, BCIErrorType.Unknown));
            }
        }

        // Connection verification method
        public async Task<bool> VerifyConnectionAsync()
        {
            if (!IsConnected)
            {
                Console.WriteLine("Cannot verify connection - device not connected");
                return false;
            }

            try
            {
                Console.WriteLine("Verifying device connection...");

                // Check the connection state again
                var state = museDevice.GetConnectionState();
                if (state != MuseConnectionState.CONNECTED)
                {
                    Console.WriteLine($"Device reports state {state}, not CONNECTED");
                    return false;
                }

                // Brief delay to wait for initial data
                await Task.Delay(1000);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection verification failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Receives a data packet from the Muse device
        /// </summary>
        public void ReceiveMuseDataPacket(MuseDataPacket packet, NeuroSpectator.Services.BCI.Muse.Core.Muse muse)
        {
            try
            {
                if (packetTypeMapping.TryGetValue(packet.PacketType, out BrainWaveTypes waveType))
                {
                    if (waveType != BrainWaveTypes.None)
                    {
                        // Create a safe timestamp - handle potential out-of-range values
                        DateTimeOffset timestamp;
                        try
                        {
                            // Check if timestamp is within valid range for DateTimeOffset
                            const long minTimestamp = -62135596800000; // DateTimeOffset.MinValue in milliseconds
                            const long maxTimestamp = 253402300799999; // DateTimeOffset.MaxValue in milliseconds

                            if (packet.Timestamp < minTimestamp || packet.Timestamp > maxTimestamp)
                            {
                                timestamp = DateTimeOffset.Now;
                                Console.WriteLine($"Warning: Invalid timestamp {packet.Timestamp} (out of range), using current time");
                            }
                            else
                            {
                                timestamp = DateTimeOffset.FromUnixTimeMilliseconds(packet.Timestamp);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Use current time if the device timestamp is invalid
                            timestamp = DateTimeOffset.Now;
                            Console.WriteLine($"Warning: Error converting timestamp {packet.Timestamp}: {ex.Message}, using current time");
                        }

                        // Validate channel values
                        double[] validatedValues = packet.Values;
                        if (validatedValues != null)
                        {
                            // Check for NaN and Infinity values and replace them
                            for (int i = 0; i < validatedValues.Length; i++)
                            {
                                if (double.IsNaN(validatedValues[i]) || double.IsInfinity(validatedValues[i]))
                                {
                                    validatedValues[i] = 0.0;
                                    Console.WriteLine($"Warning: Invalid value at index {i} replaced with 0.0");
                                }
                            }
                        }

                        var brainWaveData = new BrainWaveData(
                            waveType,
                            validatedValues ?? Array.Empty<double>(), // Ensure we never pass null
                            timestamp
                        );

                        BrainWaveDataReceived?.Invoke(this, new BrainWaveDataEventArgs(brainWaveData));
                    }
                }
            }
            catch (Exception ex)
            {
                // Log detailed information about the exception
                Console.WriteLine($"Error in ReceiveMuseDataPacket: {ex.Message}");
                Console.WriteLine($"PacketType: {packet.PacketType}, Timestamp: {packet.Timestamp}");
                Console.WriteLine($"Values: {(packet.Values != null ? string.Join(", ", packet.Values) : "null")}");

                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    $"Error processing data packet: {ex.Message}",
                    ex,
                    BCIErrorType.Unknown));
            }
        }

        /// <summary>
        /// Receives an artifact packet from the Muse device
        /// </summary>
        public void ReceiveMuseArtifactPacket(MuseArtifactPacket packet, NeuroSpectator.Services.BCI.Muse.Core.Muse muse)
        {
            try
            {
                // Create a safe timestamp - handle potential out-of-range values
                DateTimeOffset timestamp;
                try
                {
                    // Check if timestamp is within valid range for DateTimeOffset
                    const long minTimestamp = -62135596800000; // DateTimeOffset.MinValue in milliseconds
                    const long maxTimestamp = 253402300799999; // DateTimeOffset.MaxValue in milliseconds

                    if (packet.Timestamp < minTimestamp || packet.Timestamp > maxTimestamp)
                    {
                        timestamp = DateTimeOffset.Now;
                        Console.WriteLine($"Warning: Invalid artifact timestamp {packet.Timestamp} (out of range), using current time");
                    }
                    else
                    {
                        timestamp = DateTimeOffset.FromUnixTimeMilliseconds(packet.Timestamp);
                    }
                }
                catch (Exception ex)
                {
                    // Use current time if the device timestamp is invalid
                    timestamp = DateTimeOffset.Now;
                    Console.WriteLine($"Warning: Error converting artifact timestamp {packet.Timestamp}: {ex.Message}, using current time");
                }

                ArtifactDetected?.Invoke(this, new ArtifactEventArgs(
                    packet.Blink,
                    packet.JawClench,
                    !packet.HeadbandOn,
                    timestamp
                ));
            }
            catch (Exception ex)
            {
                // Log detailed information about the exception
                Console.WriteLine($"Error in ReceiveMuseArtifactPacket: {ex.Message}");

                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    $"Error processing artifact packet: {ex.Message}",
                    ex,
                    BCIErrorType.Unknown));
            }
        }

        /// <summary>
        /// Receives an error from the Muse device
        /// </summary>
        public void ReceiveError(MuseError error, NeuroSpectator.Services.BCI.Muse.Core.Muse muse)
        {
            try
            {
                Console.WriteLine($"[ERROR] Received Muse error: Type={error.Type}, Code={error.Code}, Info={error.Info}");

                // Create more user-friendly error details
                string errorDetails = $"Muse device error - Type: {error.Type}, Code: {error.Code}";
                string recoveryAction = "Unknown";

                // Map the error type
                BCIErrorType errorType;
                bool attemptRecovery = false;

                switch ((int)error.Type)
                {
                    case 0: // FAILURE
                        errorType = BCIErrorType.ConnectionFailed;
                        errorDetails += " - General failure";
                        recoveryAction = "Try disconnecting and reconnecting";
                        attemptRecovery = true;
                        break;

                    case 1: // TIMEOUT
                        errorType = BCIErrorType.ConnectionFailed;
                        errorDetails += " - Operation timeout";
                        recoveryAction = "Check device is powered on and in range";
                        attemptRecovery = true;
                        break;

                    case 2: // OVERLOADED
                        errorType = BCIErrorType.DeviceDisconnected;
                        errorDetails += " - Device overloaded";
                        recoveryAction = "Try restarting the device";
                        attemptRecovery = true;
                        break;

                    case 3: // UNIMPLEMENTED
                        errorType = BCIErrorType.DeviceNotSupported;
                        errorDetails += " - Feature not implemented";
                        recoveryAction = "Check device compatibility";
                        attemptRecovery = false;
                        break;

                    default:
                        errorType = BCIErrorType.Unknown;
                        break;
                }

                Console.WriteLine($"Error details: {errorDetails}");
                Console.WriteLine($"Recommended action: {recoveryAction}");

                // If error during connection, attempt recovery
                if (isConnecting && attemptRecovery)
                {
                    Console.WriteLine("Error during connection, attempting recovery");

                    try
                    {
                        // Reset and disconnect
                        museDevice.EnableDataTransmission(false);
                        museDevice.UnregisterAllListeners();
                        museDevice.Disconnect();

                        Console.WriteLine("Recovery disconnect completed");
                    }
                    catch (Exception innerEx)
                    {
                        Console.WriteLine($"Recovery failed: {innerEx.Message}");
                    }
                }

                // Notify subscribers about the error
                ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(
                    $"{errorDetails}. {error.Info}. {recoveryAction}",
                    null,
                    errorType));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling Muse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Maps a Muse connection state to a generic connection state
        /// </summary>
        private static BCIConnectionState MapConnectionState(MuseConnectionState state)
        {
            return state switch
            {
                MuseConnectionState.CONNECTED => BCIConnectionState.Connected,
                MuseConnectionState.CONNECTING => BCIConnectionState.Connecting,
                MuseConnectionState.DISCONNECTED => BCIConnectionState.Disconnected,
                MuseConnectionState.NEEDS_UPDATE => BCIConnectionState.NeedsUpdate,
                MuseConnectionState.NEEDS_LICENSE => BCIConnectionState.NeedsLicense,
                _ => BCIConnectionState.Unknown
            };
        }

        /// <summary>
        /// Disposes of the Muse device
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the Muse device
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        // Make sure we're disconnected
                        DisconnectAsync().Wait();

                        // Clean up the semaphore
                        connectionSemaphore.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during disposal: {ex.Message}");
                    }
                }

                isDisposed = true;
            }
        }
    }
}