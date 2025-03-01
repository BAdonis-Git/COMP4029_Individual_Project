using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Models.BCI.Muse;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.BCI.Muse.Core;

// Using aliases to resolve ambiguities
using BCIConnectionState = NeuroSpectator.Models.BCI.Common.ConnectionState;
using MuseConnectionState = NeuroSpectator.Services.BCI.Muse.Core.ConnectionState;
using MuseErrorType = NeuroSpectator.Services.BCI.Muse.Core.ErrorType;

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
            await Task.Run(() =>
            {
                museDevice.RunAsynchronously();
            });
        }

        /// <summary>
        /// Disconnects from the Muse device asynchronously
        /// </summary>
        public async Task DisconnectAsync()
        {
            await Task.Run(() =>
            {
                museDevice.Disconnect();
            });
        }

        /// <summary>
        /// Registers for brain wave data from the Muse device
        /// </summary>
        public void RegisterForBrainWaveData(BrainWaveTypes waveTypes)
        {
            foreach (BrainWaveTypes type in Enum.GetValues(typeof(BrainWaveTypes)))
            {
                if (type != BrainWaveTypes.None && type != BrainWaveTypes.All && waveTypes.HasFlag(type))
                {
                    if (waveTypeMapping.TryGetValue(type, out MuseDataPacketType packetType))
                    {
                        museDevice.RegisterDataListener(this, packetType);
                    }
                }
            }

            // Always register for artifacts
            museDevice.RegisterDataListener(this, MuseDataPacketType.ARTIFACTS);

            // Always register for battery updates
            museDevice.RegisterDataListener(this, MuseDataPacketType.BATTERY);
        }

        /// <summary>
        /// Unregisters from brain wave data
        /// </summary>
        public void UnregisterFromBrainWaveData(BrainWaveTypes waveTypes)
        {
            foreach (BrainWaveTypes type in Enum.GetValues(typeof(BrainWaveTypes)))
            {
                if (type != BrainWaveTypes.None && type != BrainWaveTypes.All && waveTypes.HasFlag(type))
                {
                    if (waveTypeMapping.TryGetValue(type, out MuseDataPacketType packetType))
                    {
                        museDevice.UnregisterDataListener(this, packetType);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the battery level asynchronously
        /// </summary>
        public async Task<double> GetBatteryLevelAsync()
        {
            // The actual implementation would need to query the battery level from the Muse device
            // For now, we'll return a placeholder value
            return await Task.FromResult(100.0);
        }

        /// <summary>
        /// Gets the signal quality asynchronously
        /// </summary>
        public async Task<double> GetSignalQualityAsync()
        {
            // The actual implementation would need to query the signal quality from the Muse device
            // For now, we'll return a placeholder value
            return await Task.FromResult(1.0);
        }

        /// <summary>
        /// Receives a connection packet from the Muse device
        /// </summary>
        public void ReceiveMuseConnectionPacket(MuseConnectionPacket packet, NeuroSpectator.Services.BCI.Muse.Core.Muse muse)
        {
            var oldState = MapConnectionState(packet.PreviousConnectionState);
            var newState = MapConnectionState(packet.CurrentConnectionState);

            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState));
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
                            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(packet.Timestamp);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            // Use current time if the device timestamp is invalid
                            timestamp = DateTimeOffset.Now;
                            Console.WriteLine($"Warning: Received invalid timestamp {packet.Timestamp} from device, using current time instead.");
                        }

                        var brainWaveData = new BrainWaveData(
                            waveType,
                            packet.Values,
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
                    timestamp = DateTimeOffset.FromUnixTimeMilliseconds(packet.Timestamp);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Use current time if the device timestamp is invalid
                    timestamp = DateTimeOffset.Now;
                    Console.WriteLine($"Warning: Received invalid timestamp {packet.Timestamp} from artifact packet, using current time instead.");
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
            // Fixed: Using the proper enum type from Core namespace
            var errorType = error.Type switch
            {
                MuseErrorType.FAILURE => BCIErrorType.ConnectionFailed,
                MuseErrorType.TIMEOUT => BCIErrorType.ConnectionFailed,
                MuseErrorType.OVERLOADED => BCIErrorType.DeviceDisconnected,
                MuseErrorType.UNIMPLEMENTED => BCIErrorType.DeviceNotSupported,
                _ => BCIErrorType.Unknown
            };

            ErrorOccurred?.Invoke(this, new BCIErrorEventArgs(error.Info, null, errorType));
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
                    // Unregister all listeners and disconnect
                    museDevice.UnregisterAllListeners();
                    museDevice.Disconnect();
                }

                isDisposed = true;
            }
        }
    }
}