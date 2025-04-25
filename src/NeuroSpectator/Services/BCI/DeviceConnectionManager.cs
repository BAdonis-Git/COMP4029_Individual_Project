using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services.BCI.Interfaces;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace NeuroSpectator.Services.BCI
{
    /// <summary>
    /// Connection status for device connection management
    /// </summary>
    public enum DeviceConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting,
        Error
    }

    /// <summary>
    /// Central manager for handling BCI device connections and status
    /// </summary>
    public class DeviceConnectionManager
    {
        private readonly IDispatcher dispatcher;
        private readonly Dictionary<string, IBCIDevice> connectedDevices = new Dictionary<string, IBCIDevice>();
        private readonly ObservableCollection<DeviceConnectionInfo> deviceConnectionLog = new ObservableCollection<DeviceConnectionInfo>();
        private const int MaxLogEntries = 50;
        private DeviceConnectionStatus connectionStatus = DeviceConnectionStatus.Disconnected;

        /// <summary>
        /// Gets a collection of currently connected devices
        /// </summary>
        public ReadOnlyDictionary<string, IBCIDevice> ConnectedDevices =>
            new ReadOnlyDictionary<string, IBCIDevice>(connectedDevices);

        /// <summary>
        /// Gets a log of device connection events
        /// </summary>
        public ObservableCollection<DeviceConnectionInfo> ConnectionLog => deviceConnectionLog;

        /// <summary>
        /// Gets or sets the current connection status
        /// </summary>
        public DeviceConnectionStatus ConnectionStatus
        {
            get => connectionStatus;
            private set
            {
                if (connectionStatus != value)
                {
                    var oldStatus = connectionStatus;
                    connectionStatus = value;
                    ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(oldStatus, value));
                }
            }
        }

        /// <summary>
        /// Represents the current connection status with details
        /// </summary>
        public class ConnectionStatusInfo
        {
            /// <summary>
            /// Gets or sets whether a device is connected
            /// </summary>
            public bool IsConnected { get; set; }

            /// <summary>
            /// Gets or sets the name of the connected device (if any)
            /// </summary>
            public string DeviceName { get; set; }
        }

        /// <summary>
        /// Gets whether any device is connected
        /// </summary>
        public bool IsDeviceConnected => connectedDevices.Count > 0;

        /// <summary>
        /// Event raised when a device connects
        /// </summary>
        public event EventHandler<IBCIDevice> DeviceConnected;

        /// <summary>
        /// Event raised when a device disconnects
        /// </summary>
        public event EventHandler<IBCIDevice> DeviceDisconnected;

        /// <summary>
        /// Event raised when a device has an error
        /// </summary>
        public event EventHandler<DeviceErrorEventArgs> DeviceError;

        /// <summary>
        /// Event raised when a device has a warning condition
        /// </summary>
        public event EventHandler<DeviceWarningEventArgs> DeviceWarning;

        /// <summary>
        /// Event raised when connection status changes
        /// </summary>
        public event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// Creates a new instance of the DeviceConnectionManager
        /// </summary>
        public DeviceConnectionManager(IDispatcher dispatcher = null)
        {
            this.dispatcher = dispatcher;
            Debug.WriteLine("DeviceConnectionManager initialized");
        }

        /// <summary>
        /// Refreshes connection status from devices and returns the current status
        /// </summary>
        /// <returns>Current connection status with device details</returns>
        public async Task<ConnectionStatusInfo> RefreshConnectionStatusAsync()
        {
            try
            {
                // Check all connected devices
                bool anyConnected = false;
                string deviceName = null;

                foreach (var device in connectedDevices.Values)
                {
                    if (device.IsConnected)
                    {
                        anyConnected = true;
                        deviceName = device.Name;
                        break;
                    }
                }

                // Update connection status based on devices
                ConnectionStatus = anyConnected ? DeviceConnectionStatus.Connected : DeviceConnectionStatus.Disconnected;

                // Return detailed status
                return new ConnectionStatusInfo
                {
                    IsConnected = anyConnected,
                    DeviceName = deviceName
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing connection status: {ex.Message}");
                ConnectionStatus = DeviceConnectionStatus.Error;

                // Return error status
                return new ConnectionStatusInfo
                {
                    IsConnected = false,
                    DeviceName = null
                };
            }
        }

        /// <summary>
        /// Registers a device with the connection manager
        /// </summary>
        public void RegisterDevice(IBCIDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            Debug.WriteLine($"Registering device: {device.Name} ({device.DeviceId})");

            // Subscribe to device events
            device.ConnectionStateChanged += OnDeviceConnectionStateChanged;
            device.ErrorOccurred += OnDeviceErrorOccurred;

            // If the device is already connected, add it to our list
            if (device.IsConnected && !connectedDevices.ContainsKey(device.DeviceId))
            {
                connectedDevices[device.DeviceId] = device;
                AddLogEntry(new DeviceConnectionInfo(device, "Registered", DateTime.Now));
                RaiseDeviceConnected(device);
                ConnectionStatus = DeviceConnectionStatus.Connected;
            }
        }

        /// <summary>
        /// Unregisters a device from the connection manager
        /// </summary>
        public void UnregisterDevice(IBCIDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            Debug.WriteLine($"Unregistering device: {device.Name} ({device.DeviceId})");

            // Unsubscribe from device events
            device.ConnectionStateChanged -= OnDeviceConnectionStateChanged;
            device.ErrorOccurred -= OnDeviceErrorOccurred;

            // Remove from our list if connected
            if (connectedDevices.ContainsKey(device.DeviceId))
            {
                connectedDevices.Remove(device.DeviceId);
                AddLogEntry(new DeviceConnectionInfo(device, "Unregistered", DateTime.Now));
                RaiseDeviceDisconnected(device);

                // Update connection status if this was the last device
                if (connectedDevices.Count == 0)
                {
                    ConnectionStatus = DeviceConnectionStatus.Disconnected;
                }
            }
        }

        /// <summary>
        /// Attempts to connect to a device
        /// </summary>
        public async Task<bool> ConnectDeviceAsync(IBCIDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            if (device.IsConnected)
            {
                Debug.WriteLine($"Device already connected: {device.Name} ({device.DeviceId})");
                return true;
            }

            try
            {
                Debug.WriteLine($"Connecting to device: {device.Name} ({device.DeviceId})");
                AddLogEntry(new DeviceConnectionInfo(device, "Connecting", DateTime.Now));
                ConnectionStatus = DeviceConnectionStatus.Connecting;

                // Register the device if not already registered
                RegisterDevice(device);

                // Connect to the device
                await device.ConnectAsync();

                // The connection event will handle adding to connected devices
                return device.IsConnected;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error connecting to device: {ex.Message}");
                AddLogEntry(new DeviceConnectionInfo(device, $"Connection failed: {ex.Message}", DateTime.Now));
                ConnectionStatus = DeviceConnectionStatus.Error;
                return false;
            }
        }

        /// <summary>
        /// Disconnects from a device
        /// </summary>
        public async Task DisconnectDeviceAsync(IBCIDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            if (!device.IsConnected)
            {
                Debug.WriteLine($"Device already disconnected: {device.Name} ({device.DeviceId})");
                return;
            }

            try
            {
                Debug.WriteLine($"Disconnecting from device: {device.Name} ({device.DeviceId})");
                AddLogEntry(new DeviceConnectionInfo(device, "Disconnecting", DateTime.Now));
                ConnectionStatus = DeviceConnectionStatus.Disconnecting;

                // Disconnect from the device
                await device.DisconnectAsync();

                // The disconnection event will handle removing from connected devices
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disconnecting from device: {ex.Message}");
                AddLogEntry(new DeviceConnectionInfo(device, $"Disconnection error: {ex.Message}", DateTime.Now));
                ConnectionStatus = DeviceConnectionStatus.Error;

                // Force removal from connected devices list
                if (connectedDevices.ContainsKey(device.DeviceId))
                {
                    connectedDevices.Remove(device.DeviceId);
                    RaiseDeviceDisconnected(device);

                    // Update connection status if this was the last device
                    if (connectedDevices.Count == 0)
                    {
                        ConnectionStatus = DeviceConnectionStatus.Disconnected;
                    }
                }
            }
        }

        /// <summary>
        /// Disconnects all connected devices
        /// </summary>
        public async Task DisconnectAllDevicesAsync()
        {
            Debug.WriteLine("Disconnecting all devices");
            ConnectionStatus = DeviceConnectionStatus.Disconnecting;

            // Make a copy of the list to avoid modification during enumeration
            var devices = new List<IBCIDevice>(connectedDevices.Values);

            foreach (var device in devices)
            {
                await DisconnectDeviceAsync(device);
            }

            ConnectionStatus = DeviceConnectionStatus.Disconnected;
        }

        /// <summary>
        /// Called when a device is connecting
        /// </summary>
        internal void NotifyConnecting(IBCIDevice device)
        {
            AddLogEntry(new DeviceConnectionInfo(device, "Connecting", DateTime.Now));
            ConnectionStatus = DeviceConnectionStatus.Connecting;
        }

        /// <summary>
        /// Called when a device connects
        /// </summary>
        internal void NotifyConnected(IBCIDevice device)
        {
            Debug.WriteLine($"Device connected: {device.Name} ({device.DeviceId})");
            AddLogEntry(new DeviceConnectionInfo(device, "Connected", DateTime.Now));
            ConnectionStatus = DeviceConnectionStatus.Connected;

            // Add to connected devices if not already there
            if (!connectedDevices.ContainsKey(device.DeviceId))
            {
                connectedDevices[device.DeviceId] = device;
                RaiseDeviceConnected(device);
            }
        }

        /// <summary>
        /// Called when a device disconnects
        /// </summary>
        internal void NotifyDisconnected(IBCIDevice device)
        {
            Debug.WriteLine($"Device disconnected: {device.Name} ({device.DeviceId})");
            AddLogEntry(new DeviceConnectionInfo(device, "Disconnected", DateTime.Now));

            // Remove from connected devices if there
            if (connectedDevices.ContainsKey(device.DeviceId))
            {
                connectedDevices.Remove(device.DeviceId);
                RaiseDeviceDisconnected(device);

                // Update connection status if this was the last device
                if (connectedDevices.Count == 0)
                {
                    ConnectionStatus = DeviceConnectionStatus.Disconnected;
                }
            }
        }

        /// <summary>
        /// Called when a device connection fails
        /// </summary>
        internal void NotifyConnectionFailed(IBCIDevice device, string reason)
        {
            Debug.WriteLine($"Device connection failed: {device.Name} ({device.DeviceId}) - {reason}");
            AddLogEntry(new DeviceConnectionInfo(device, $"Connection failed: {reason}", DateTime.Now));
            ConnectionStatus = DeviceConnectionStatus.Error;

            // Remove from connected devices if there
            if (connectedDevices.ContainsKey(device.DeviceId))
            {
                connectedDevices.Remove(device.DeviceId);
                RaiseDeviceDisconnected(device);

                // Update connection status if this was the last device
                if (connectedDevices.Count == 0)
                {
                    ConnectionStatus = DeviceConnectionStatus.Disconnected;
                }
            }
        }

        /// <summary>
        /// Handles cases where device configuration is missing or unavailable
        /// </summary>
        internal void HandleMissingConfiguration(IBCIDevice device)
        {
            try
            {
                Debug.WriteLine($"Handling missing configuration for device {device.Name}");

                // Add to connected devices list anyway
                if (!connectedDevices.ContainsKey(device.DeviceId))
                {
                    connectedDevices[device.DeviceId] = device;
                    AddLogEntry(new DeviceConnectionInfo(device, "Connected (limited capabilities)", DateTime.Now));
                    RaiseDeviceConnected(device);
                    ConnectionStatus = DeviceConnectionStatus.Connected;
                }

                // Notify with a warning
                NotifyDeviceWarning(device, "Limited device capabilities - configuration unavailable");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling missing configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when a device is reconnecting
        /// </summary>
        internal void NotifyReconnecting(IBCIDevice device)
        {
            Debug.WriteLine($"Device reconnecting: {device.Name} ({device.DeviceId})");
            AddLogEntry(new DeviceConnectionInfo(device, "Reconnecting", DateTime.Now));
            ConnectionStatus = DeviceConnectionStatus.Connecting;
        }

        /// <summary>
        /// Called when a device has an error
        /// </summary>
        internal void NotifyDeviceError(IBCIDevice device, string errorMessage)
        {
            Debug.WriteLine($"Device error: {device.Name} ({device.DeviceId}) - {errorMessage}");
            AddLogEntry(new DeviceConnectionInfo(device, $"Error: {errorMessage}", DateTime.Now));
            ConnectionStatus = DeviceConnectionStatus.Error;

            // Raise device error event
            RaiseDeviceError(device, errorMessage);
        }

        /// <summary>
        /// Called when a device has a warning condition
        /// </summary>
        internal void NotifyDeviceWarning(IBCIDevice device, string warningMessage)
        {
            Debug.WriteLine($"Device warning: {device.Name} ({device.DeviceId}) - {warningMessage}");
            AddLogEntry(new DeviceConnectionInfo(device, $"Warning: {warningMessage}", DateTime.Now));

            // Raise device warning event
            RaiseDeviceWarning(device, warningMessage);
        }

        /// <summary>
        /// Called when a device's connection state changes
        /// </summary>
        private void OnDeviceConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            if (sender is IBCIDevice device)
            {
                Debug.WriteLine($"Device state changed: {device.Name} ({device.DeviceId}) - {e.OldState} to {e.NewState}");

                switch (e.NewState)
                {
                    case ConnectionState.Connected:
                        NotifyConnected(device);
                        break;

                    case ConnectionState.Disconnected:
                        NotifyDisconnected(device);
                        break;

                    case ConnectionState.Connecting:
                        NotifyConnecting(device);
                        break;

                    case ConnectionState.NeedsUpdate:
                        NotifyDeviceWarning(device, "Device firmware update required");
                        break;

                    case ConnectionState.NeedsLicense:
                        NotifyDeviceWarning(device, "Device license validation required");
                        break;
                }
            }
        }

        /// <summary>
        /// Called when a device reports an error
        /// </summary>
        private void OnDeviceErrorOccurred(object sender, BCIErrorEventArgs e)
        {
            if (sender is IBCIDevice device)
            {
                NotifyDeviceError(device, e.Message);
            }
        }

        /// <summary>
        /// Adds an entry to the connection log
        /// </summary>
        private void AddLogEntry(DeviceConnectionInfo entry)
        {
            // Use the dispatcher if available, otherwise add directly
            if (dispatcher != null)
            {
                dispatcher.Dispatch(() =>
                {
                    deviceConnectionLog.Add(entry);
                    TrimLogIfNeeded();
                });
            }
            else
            {
                deviceConnectionLog.Add(entry);
                TrimLogIfNeeded();
            }
        }

        /// <summary>
        /// Trims the log if it exceeds the maximum number of entries
        /// </summary>
        private void TrimLogIfNeeded()
        {
            while (deviceConnectionLog.Count > MaxLogEntries)
            {
                deviceConnectionLog.RemoveAt(0);
            }
        }

        /// <summary>
        /// Raises the DeviceConnected event
        /// </summary>
        private void RaiseDeviceConnected(IBCIDevice device)
        {
            if (dispatcher != null)
            {
                dispatcher.Dispatch(() => DeviceConnected?.Invoke(this, device));
            }
            else
            {
                DeviceConnected?.Invoke(this, device);
            }
        }

        /// <summary>
        /// Raises the DeviceDisconnected event
        /// </summary>
        private void RaiseDeviceDisconnected(IBCIDevice device)
        {
            if (dispatcher != null)
            {
                dispatcher.Dispatch(() => DeviceDisconnected?.Invoke(this, device));
            }
            else
            {
                DeviceDisconnected?.Invoke(this, device);
            }
        }

        /// <summary>
        /// Raises the DeviceError event
        /// </summary>
        private void RaiseDeviceError(IBCIDevice device, string errorMessage)
        {
            if (dispatcher != null)
            {
                dispatcher.Dispatch(() => DeviceError?.Invoke(this, new DeviceErrorEventArgs(device, errorMessage)));
            }
            else
            {
                DeviceError?.Invoke(this, new DeviceErrorEventArgs(device, errorMessage));
            }
        }

        /// <summary>
        /// Raises the DeviceWarning event
        /// </summary>
        private void RaiseDeviceWarning(IBCIDevice device, string warningMessage)
        {
            if (dispatcher != null)
            {
                dispatcher.Dispatch(() => DeviceWarning?.Invoke(this, new DeviceWarningEventArgs(device, warningMessage)));
            }
            else
            {
                DeviceWarning?.Invoke(this, new DeviceWarningEventArgs(device, warningMessage));
            }
        }
    }

    /// <summary>
    /// Arguments for the ConnectionStatusChanged event
    /// </summary>
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the old connection status
        /// </summary>
        public DeviceConnectionStatus OldStatus { get; }

        /// <summary>
        /// Gets the new connection status
        /// </summary>
        public DeviceConnectionStatus NewStatus { get; }

        /// <summary>
        /// Creates a new instance of ConnectionStatusChangedEventArgs
        /// </summary>
        public ConnectionStatusChangedEventArgs(DeviceConnectionStatus oldStatus, DeviceConnectionStatus newStatus)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }
    }

    /// <summary>
    /// Represents a device connection information entry
    /// </summary>
    public class DeviceConnectionInfo
    {
        /// <summary>
        /// Gets the device name
        /// </summary>
        public string DeviceName { get; }

        /// <summary>
        /// Gets the device ID
        /// </summary>
        public string DeviceId { get; }

        /// <summary>
        /// Gets the device type
        /// </summary>
        public BCIDeviceType DeviceType { get; }

        /// <summary>
        /// Gets the message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Creates a new instance of the DeviceConnectionInfo class
        /// </summary>
        public DeviceConnectionInfo(IBCIDevice device, string message, DateTime timestamp)
        {
            DeviceName = device.Name;
            DeviceId = device.DeviceId;
            DeviceType = device.DeviceType;
            Message = message;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Arguments for the DeviceError event
    /// </summary>
    public class DeviceErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the device
        /// </summary>
        public IBCIDevice Device { get; }

        /// <summary>
        /// Gets the error message
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Creates a new instance of the DeviceErrorEventArgs class
        /// </summary>
        public DeviceErrorEventArgs(IBCIDevice device, string errorMessage)
        {
            Device = device;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Arguments for the DeviceWarning event
    /// </summary>
    public class DeviceWarningEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the device
        /// </summary>
        public IBCIDevice Device { get; }

        /// <summary>
        /// Gets the warning message
        /// </summary>
        public string WarningMessage { get; }

        /// <summary>
        /// Creates a new instance of the DeviceWarningEventArgs class
        /// </summary>
        public DeviceWarningEventArgs(IBCIDevice device, string warningMessage)
        {
            Device = device;
            WarningMessage = warningMessage;
        }
    }
}