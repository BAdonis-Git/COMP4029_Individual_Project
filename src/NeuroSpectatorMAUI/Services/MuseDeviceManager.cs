using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NeuroSpectatorMAUI.Services
{
    public class MuseDeviceManager : IDisposable
    {
        #region Private Fields
        private MuseDevice? _currentDevice;
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
        private bool _isScanning;
        private bool _isDisposed;
        private MuseListChangedCallback? _museListChangedCallback;
        #endregion

        #region Public Events
        public event EventHandler<MuseDevice>? DeviceDiscovered;
        public event EventHandler<string>? ConnectionError;
        public event EventHandler? DeviceListChanged;
        public event EventHandler<(MuseDataPacketType type, double[] data)>? DataReceived;
        #endregion

        #region Public Properties
        public ObservableCollection<MuseDevice> AvailableDevices { get; }
        public bool IsScanning => _isScanning;
        public MuseDevice? CurrentDevice => _currentDevice;
        public bool IsInitialized { get; private set; }
        #endregion

        #region Constructor
        public MuseDeviceManager()
        {
            AvailableDevices = new ObservableCollection<MuseDevice>();
            _museListChangedCallback = new MuseListChangedCallback(OnMuseListChanged);
        }
        #endregion

        #region Public Methods
        public bool Initialize()
        {
            try
            {
                Debug.WriteLine("Initializing MuseDeviceManager");

                // Initialize the native interface
                bool success = MuseNativeInterface.Initialize();
                if (!success)
                {
                    Debug.WriteLine("Failed to initialize MuseNativeInterface");
                    return false;
                }

                // Register the muse list changed callback
                MuseNativeInterface.RegisterCallbacks(
                    _museListChangedCallback,
                    null,  // We'll register connection callbacks per device
                    null); // We'll register data callbacks per device

                IsInitialized = true;
                Debug.WriteLine("MuseDeviceManager initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in Initialize: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> StartScanningAsync()
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("Cannot start scanning: not initialized");
                return false;
            }

            if (_isScanning)
            {
                Debug.WriteLine("Already scanning");
                return true;
            }

            await _connectionSemaphore.WaitAsync();
            try
            {
                _isScanning = MuseNativeInterface.StartListening();
                Debug.WriteLine($"Started scanning: {_isScanning}");
                return _isScanning;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in StartScanningAsync: {ex.Message}");
                return false;
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        public async Task StopScanningAsync()
        {
            if (!_isScanning) return;

            await _connectionSemaphore.WaitAsync();
            try
            {
                MuseNativeInterface.StopListening();
                _isScanning = false;
                Debug.WriteLine("Stopped scanning");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in StopScanningAsync: {ex.Message}");
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        public async Task RefreshDeviceListAsync()
        {
            await _connectionSemaphore.WaitAsync();
            try
            {
                // Clear existing devices
                AvailableDevices.Clear();

                // Get updated list
                var devices = MuseDevice.GetAvailableDevices();
                foreach (var device in devices)
                {
                    AvailableDevices.Add(device);
                    DeviceDiscovered?.Invoke(this, device);
                }

                DeviceListChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in RefreshDeviceListAsync: {ex.Message}");
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        public async Task<bool> ConnectToDeviceAsync(MuseDevice device)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));

            await _connectionSemaphore.WaitAsync();
            try
            {
                // Disconnect from current device if any
                if (_currentDevice != null)
                {
                    await DisconnectCurrentDeviceAsync();
                }

                // Connect to new device
                bool success = device.Connect();
                if (!success)
                {
                    ConnectionError?.Invoke(this, $"Failed to connect to {device.Name}");
                    return false;
                }

                // Set as current device
                _currentDevice = device;

                // Subscribe to device events
                device.ConnectionStateChanged += Device_ConnectionStateChanged;
                device.DataReceived += Device_DataReceived;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in ConnectToDeviceAsync: {ex.Message}");
                ConnectionError?.Invoke(this, ex.Message);
                return false;
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        public async Task<bool> DisconnectCurrentDeviceAsync()
        {
            if (_currentDevice == null) return true;

            await _connectionSemaphore.WaitAsync();
            try
            {
                // Unsubscribe from device events
                _currentDevice.ConnectionStateChanged -= Device_ConnectionStateChanged;
                _currentDevice.DataReceived -= Device_DataReceived;

                // Disconnect
                _currentDevice.Disconnect();
                _currentDevice = null;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in DisconnectCurrentDeviceAsync: {ex.Message}");
                return false;
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        public async Task<bool> RegisterForDataAsync(MuseDataPacketType packetType)
        {
            if (_currentDevice == null)
            {
                Debug.WriteLine("Cannot register for data: no device connected");
                return false;
            }

            await _connectionSemaphore.WaitAsync();
            try
            {
                return _currentDevice.RegisterForData(packetType);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in RegisterForDataAsync: {ex.Message}");
                return false;
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        public async Task<bool> UnregisterForDataAsync(MuseDataPacketType packetType)
        {
            if (_currentDevice == null)
            {
                Debug.WriteLine("Cannot unregister for data: no device connected");
                return false;
            }

            await _connectionSemaphore.WaitAsync();
            try
            {
                return _currentDevice.UnregisterForData(packetType);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in UnregisterForDataAsync: {ex.Message}");
                return false;
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }
        #endregion

        #region Private Methods
        private void OnMuseListChanged()
        {
            Debug.WriteLine("Muse list changed event received");

            // Use MainThread to update UI-bound collections
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await RefreshDeviceListAsync();
            });
        }

        private void Device_ConnectionStateChanged(object? sender, ConnectionState state)
        {
            Debug.WriteLine($"Device connection state changed: {state}");

            // Handle connection state changes
            if (state == ConnectionState.Disconnected && sender == _currentDevice)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisconnectCurrentDeviceAsync();
                });
            }
        }

        private void Device_DataReceived(object? sender, (MuseDataPacketType packetType, double[] data) data)
        {
            // Forward the data to subscribers
            DataReceived?.Invoke(this, data);
        }
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                // Dispose managed resources
                _connectionSemaphore.Dispose();

                // Disconnect and clean up
                _ = DisconnectCurrentDeviceAsync().ConfigureAwait(false);

                // Clean up native resources
                MuseNativeInterface.Cleanup();
            }

            _isDisposed = true;
        }
        #endregion
    }
}