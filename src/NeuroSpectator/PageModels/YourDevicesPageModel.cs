using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.BCI.Muse; // Add this using directive to find MuseDevice
using ConnectionState = NeuroSpectator.Models.BCI.Common.ConnectionState;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the YourDevicesPage
    /// </summary>
    public partial class YourDevicesPageModel : ObservableObject
    {
        private readonly IBCIDeviceManager deviceManager;

        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        private bool isScanning;

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private bool canInitialize = true;

        [ObservableProperty]
        private bool canScan;

        [ObservableProperty]
        private bool canConnect;

        [ObservableProperty]
        private bool canDisconnect;

        [ObservableProperty]
        private string statusText = "Not initialized";

        [ObservableProperty]
        private string dataText = "No data";

        [ObservableProperty]
        private IBCIDeviceInfo selectedDevice;

        /// <summary>
        /// Gets the collection of available devices
        /// </summary>
        public ObservableCollection<IBCIDeviceInfo> AvailableDevices => deviceManager.AvailableDevices;

        // Commands
        public ICommand InitializeCommand { get; }
        public ICommand StartScanCommand { get; }
        public ICommand StopScanCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }

        /// <summary>
        /// Creates a new instance of the YourDevicesPageModel class
        /// </summary>
        public YourDevicesPageModel(IBCIDeviceManager deviceManager)
        {
            this.deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));

            // Initialize commands
            InitializeCommand = new AsyncRelayCommand(InitializeAsync);
            StartScanCommand = new AsyncRelayCommand(StartScanAsync, () => CanScan && !IsScanning);
            StopScanCommand = new AsyncRelayCommand(StopScanAsync, () => IsScanning);
            ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => CanConnect);
            DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => CanDisconnect);

            // Subscribe to device manager events
            deviceManager.DeviceListChanged += OnDeviceListChanged;
            deviceManager.ErrorOccurred += OnErrorOccurred;
        }

        /// <summary>
        /// Initializes the device manager and sets up event handlers
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                CanInitialize = false;
                StatusText = "Initializing...";

                // Wait a bit for the UI to update
                await Task.Delay(100);

                // Set initialized flag
                IsInitialized = true;
                CanScan = true;
                StatusText = "Initialized successfully. Ready to scan for devices.";
            }
            catch (Exception ex)
            {
                StatusText = $"Initialization error: {ex.Message}";
                CanInitialize = true;
                Debug.WriteLine($"Initialization error: {ex}");
            }
        }

        /// <summary>
        /// Starts scanning for devices
        /// </summary>
        private async Task StartScanAsync()
        {
            try
            {
                StatusText = "Scanning for devices...";
                IsScanning = true;
                await deviceManager.StartScanningAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"Error scanning: {ex.Message}";
                IsScanning = false;
                Debug.WriteLine($"Error scanning: {ex}");
            }
        }

        /// <summary>
        /// Stops scanning for devices
        /// </summary>
        private async Task StopScanAsync()
        {
            try
            {
                await deviceManager.StopScanningAsync();
                IsScanning = false;
                StatusText = "Scan stopped";
            }
            catch (Exception ex)
            {
                StatusText = $"Error stopping scan: {ex.Message}";
                Debug.WriteLine($"Error stopping scan: {ex}");
            }
        }

        /// <summary>
        /// Connects to the selected device with improved error handling
        /// </summary>
        private async Task ConnectAsync()
        {
            if (SelectedDevice == null) return;

            try
            {
                StatusText = $"Connecting to {SelectedDevice.Name}...";
                CanConnect = false;
                CanDisconnect = false; // Disable disconnect while connecting

                // Stop scanning first to avoid interference
                if (IsScanning)
                {
                    await StopScanAsync();
                }

                // Track time for connection process
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Attempt to connect
                IBCIDevice device = await deviceManager.ConnectToDeviceAsync(SelectedDevice);

                if (device != null)
                {
                    stopwatch.Stop();
                    DataText = $"Connection established in {stopwatch.ElapsedMilliseconds}ms";
                    StatusText = $"Connected to {SelectedDevice.Name}";

                    // Subscribe to device events
                    device.ConnectionStateChanged += OnDeviceConnectionStateChanged;
                    device.BrainWaveDataReceived += OnBrainWaveDataReceived;
                    device.ArtifactDetected += OnArtifactDetected;
                    device.ErrorOccurred += OnDeviceErrorOccurred;

                    // Ensure we get all brain wave data
                    device.RegisterForBrainWaveData(BrainWaveTypes.All);

                    // Verify connection
                    if (device is MuseDevice museDevice)
                    {
                        var verified = await museDevice.VerifyConnectionAsync();
                        if (verified)
                        {
                            StatusText += " (verified)";
                        }
                        else
                        {
                            StatusText += " (not verified - check device)";
                        }
                    }

                    IsConnected = true;
                    CanDisconnect = true;
                }
                else
                {
                    stopwatch.Stop();
                    StatusText = $"Connection failed after {stopwatch.ElapsedMilliseconds}ms";
                    CanConnect = true;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Connection error: {ex.Message}";
                CanConnect = SelectedDevice != null;
                Debug.WriteLine($"Connection error: {ex}");
            }
        }

        /// <summary>
        /// Disconnects from the current device with improved handling
        /// </summary>
        private async Task DisconnectAsync()
        {
            try
            {
                StatusText = "Disconnecting...";
                CanDisconnect = false;

                // Track time for disconnection process
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                await deviceManager.DisconnectCurrentDeviceAsync();

                stopwatch.Stop();

                IsConnected = false;
                CanConnect = SelectedDevice != null;
                StatusText = $"Disconnected (took {stopwatch.ElapsedMilliseconds}ms)";
                DataText = "No data";
            }
            catch (Exception ex)
            {
                StatusText = $"Disconnect error: {ex.Message}";
                CanDisconnect = IsConnected;
                Debug.WriteLine($"Disconnect error: {ex}");
            }
        }

        /// <summary>
        /// Handles the DeviceListChanged event
        /// </summary>
        private void OnDeviceListChanged(object sender, System.Collections.Generic.List<IBCIDeviceInfo> devices)
        {
            StatusText = $"Found {devices.Count} device(s)";

            // If we have devices and none is selected, select the first one
            if (SelectedDevice == null && devices.Any())
            {
                SelectedDevice = devices.First();
            }

            CanConnect = SelectedDevice != null && !IsConnected;
        }

        /// <summary>
        /// Handles the ConnectionStateChanged event with improved diagnostics
        /// </summary>
        private void OnDeviceConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            StatusText = $"Connection state: {e.NewState}";

            // Log transition for debugging
            Debug.WriteLine($"Connection transition: {e.OldState} -> {e.NewState}");

            switch (e.NewState)
            {
                case ConnectionState.Connected:
                    IsConnected = true;
                    CanConnect = false;
                    CanDisconnect = true;
                    DataText = "Connected and waiting for data...";
                    break;

                case ConnectionState.Disconnected:
                    IsConnected = false;
                    CanConnect = SelectedDevice != null;
                    CanDisconnect = false;
                    DataText = "No data - device disconnected";
                    break;

                case ConnectionState.Connecting:
                    CanConnect = false;
                    CanDisconnect = false;
                    DataText = "Establishing connection...";
                    break;

                case ConnectionState.NeedsUpdate:
                    StatusText = "Device needs firmware update";
                    IsConnected = false;
                    CanConnect = false;
                    CanDisconnect = false;
                    break;

                case ConnectionState.NeedsLicense:
                    StatusText = "Device needs license activation";
                    IsConnected = false;
                    CanConnect = false;
                    CanDisconnect = false;
                    break;
            }
        }

        /// <summary>
        /// Improved brain wave data handler with robust error handling
        /// </summary>
        private void OnBrainWaveDataReceived(object sender, BrainWaveDataEventArgs e)
        {
            try
            {
                // Basic validation
                if (e?.BrainWaveData == null)
                {
                    DataText = "Received invalid data packet";
                    return;
                }

                var waveType = e.BrainWaveData.WaveType;

                // Handle different wave types
                switch (waveType)
                {
                    case BrainWaveTypes.Alpha:
                        DataText = $"Alpha: {e.BrainWaveData.AverageValue:F2}";
                        break;

                    case BrainWaveTypes.Beta:
                        DataText = $"Beta: {e.BrainWaveData.AverageValue:F2}";
                        break;

                    case BrainWaveTypes.Delta:
                        DataText = $"Delta: {e.BrainWaveData.AverageValue:F2}";
                        break;

                    case BrainWaveTypes.Theta:
                        DataText = $"Theta: {e.BrainWaveData.AverageValue:F2}";
                        break;

                    case BrainWaveTypes.Gamma:
                        DataText = $"Gamma: {e.BrainWaveData.AverageValue:F2}";
                        break;

                    case BrainWaveTypes.Raw:
                        // Safer handling of raw EEG data
                        try
                        {
                            if (e.BrainWaveData.ChannelValues != null && e.BrainWaveData.ChannelValues.Length > 0)
                            {
                                // Check for invalid values
                                bool hasInvalidValues = false;
                                foreach (var value in e.BrainWaveData.ChannelValues)
                                {
                                    if (double.IsNaN(value) || double.IsInfinity(value))
                                    {
                                        hasInvalidValues = true;
                                        break;
                                    }
                                }

                                if (hasInvalidValues)
                                {
                                    DataText = "Raw: Contains invalid values";
                                }
                                else
                                {
                                    // Safe min/max calculation
                                    double min = double.MaxValue;
                                    double max = double.MinValue;

                                    foreach (var value in e.BrainWaveData.ChannelValues)
                                    {
                                        if (value < min) min = value;
                                        if (value > max) max = value;
                                    }

                                    // Only use min/max if we found valid values
                                    if (min != double.MaxValue && max != double.MinValue)
                                    {
                                        DataText = $"EEG: {e.BrainWaveData.ChannelCount} channels, range: {min:F1} to {max:F1}";
                                    }
                                    else
                                    {
                                        DataText = $"EEG: {e.BrainWaveData.ChannelCount} channels";
                                    }
                                }
                            }
                            else
                            {
                                DataText = $"EEG: {e.BrainWaveData.ChannelCount} channels";
                            }
                        }
                        catch (Exception ex)
                        {
                            // Fallback for any errors in raw data processing
                            Debug.WriteLine($"Error processing raw EEG data: {ex.Message}");
                            DataText = "EEG: Error processing data";
                        }
                        break;

                    default:
                        DataText = $"Received {waveType} data";
                        break;
                }
            }
            catch (Exception ex)
            {
                // Top-level exception handler
                Debug.WriteLine($"Error in OnBrainWaveDataReceived: {ex.Message}");
                DataText = "Error processing brain wave data";
            }
        }

        /// <summary>
        /// Handles the ArtifactDetected event
        /// </summary>
        private void OnArtifactDetected(object sender, ArtifactEventArgs e)
        {
            DataText = $"Artifacts: Blink={e.Blink}, Jaw Clench={e.JawClench}, Headband Loose={e.HeadbandTooLoose}";
        }

        /// <summary>
        /// Handles the ErrorOccurred event from the device manager
        /// </summary>
        private void OnErrorOccurred(object sender, BCIErrorEventArgs e)
        {
            StatusText = $"Error: {e.Message}";
            Debug.WriteLine($"Device manager error: {e.Message}, Type: {e.ErrorType}");
        }

        /// <summary>
        /// Handles the ErrorOccurred event from the device
        /// </summary>
        private void OnDeviceErrorOccurred(object sender, BCIErrorEventArgs e)
        {
            StatusText = $"Device error: {e.Message}";
            Debug.WriteLine($"Device error: {e.Message}, Type: {e.ErrorType}");
        }
    }
}