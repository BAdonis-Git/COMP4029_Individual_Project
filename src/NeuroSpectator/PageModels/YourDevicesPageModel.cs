using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Models.BCI.Muse;
using NeuroSpectator.Services;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.BCI.Muse;
using ConnectionState = NeuroSpectator.Models.BCI.Common.ConnectionState;
using Microsoft.Maui.Dispatching;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the YourDevicesPage
    /// </summary>
    public partial class YourDevicesPageModel : ObservableObject, IDisposable
    {
        private readonly IBCIDeviceManager deviceManager;
        private IDispatcherTimer batteryUpdateTimer;
        private IDispatcherTimer scanTimeoutTimer;
        private const int BatteryUpdateIntervalMs = 10000; // 10 seconds
        private const int ScanTimeoutMs = 15000; // 15 seconds
        private bool _disposed = false; // Track whether Dispose has been called

        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScanButtonText))]
        private bool isScanning;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotConnected))]
        [NotifyPropertyChangedFor(nameof(DevicePanelTitle))]
        private bool isConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotConnected))]
        private bool canInitialize = false; // Default to false since initialization is automatic

        [ObservableProperty]
        private bool canScan;

        [ObservableProperty]
        private bool canConnect;

        [ObservableProperty]
        private bool canDisconnect;

        [ObservableProperty]
        private string statusText = "Initializing...";

        [ObservableProperty]
        private string dataText = "No data";

        [ObservableProperty]
        private IBCIDeviceInfo selectedDevice;

        [ObservableProperty]
        private StoredDeviceInfo selectedStoredDevice;

        [ObservableProperty]
        private double batteryLevel = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BatteryArcAngle))]
        [NotifyPropertyChangedFor(nameof(BatteryPercentText))]
        private double batteryPercent = 0;

        [ObservableProperty]
        private Services.DeviceSettingsModel currentDeviceSettings = new Services.DeviceSettingsModel();

        // Derived Properties
        public bool IsNotConnected => !IsConnected;
        public double BatteryArcAngle => 360 * (BatteryPercent / 100.0);
        public string BatteryPercentText => $"{BatteryPercent:F0}%";
        public bool BatteryPercentIsLargeArc => BatteryPercent > 50;
        public string DevicePanelTitle => IsConnected ? "Connected Device" : "Available Devices";
        public string ScanButtonText => IsScanning ? "Scanning..." : "Scan for Devices";

        // Collections
        public ObservableCollection<IBCIDeviceInfo> AvailableDevices => deviceManager.AvailableDevices;

        [ObservableProperty]
        private ObservableCollection<StoredDeviceInfo> storedDevices = new ObservableCollection<StoredDeviceInfo>();

        // Commands
        public ICommand ScanCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ShowPresetsCommand { get; }
        public ICommand SaveDeviceSettingsCommand { get; }
        public ICommand ShowSupportedDevicesCommand { get; }
        public ICommand EditDevicePresetsCommand { get; }

        /// <summary>
        /// Creates a new instance of the YourDevicesPageModel class
        /// </summary>
        public YourDevicesPageModel(IBCIDeviceManager deviceManager)
        {
            this.deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));

            // Initialize commands
            ScanCommand = new AsyncRelayCommand(ToggleScanAsync, () => CanScan);
            ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => CanConnect);
            DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => CanDisconnect);
            ShowPresetsCommand = new AsyncRelayCommand(ShowPresetsAsync, () => IsConnected);
            SaveDeviceSettingsCommand = new AsyncRelayCommand(SaveDeviceSettingsAsync, () => IsConnected);
            ShowSupportedDevicesCommand = new AsyncRelayCommand(ShowSupportedDevicesAsync);
            EditDevicePresetsCommand = new AsyncRelayCommand<StoredDeviceInfo>(EditDevicePresetsAsync);

            // Subscribe to device manager events
            deviceManager.DeviceListChanged += OnDeviceListChanged;
            deviceManager.ErrorOccurred += OnErrorOccurred;

            // Load stored devices
            LoadStoredDevices();

            // Create the scan timeout timer (but don't start it yet)
            scanTimeoutTimer = Application.Current.Dispatcher.CreateTimer();
            scanTimeoutTimer.Interval = TimeSpan.FromMilliseconds(ScanTimeoutMs);
            scanTimeoutTimer.Tick += async (s, e) =>
            {
                if (IsScanning && AvailableDevices.Count == 0)
                {
                    await StopScanAsync();
                    StatusText = "No devices found. Tap 'Scan for Devices' to try again.";
                }
            };
        }

        /// <summary>
        /// Loads stored devices from preferences
        /// </summary>
        private void LoadStoredDevices()
        {
            // For demonstration - in a real app, load from preferences or database
            StoredDevices.Clear();

            // Add some example devices
            StoredDevices.Add(new StoredDeviceInfo
            {
                Name = "Muse-BAED",
                DeviceId = "00:55:da:b7:ba:ed",
                DeviceType = BCIDeviceType.MuseHeadband,
                LastConnected = DateTime.Now.AddDays(-1)
            });

            StoredDevices.Add(new StoredDeviceInfo
            {
                Name = "Muse-2",
                DeviceId = "00:55:da:c8:df:12",
                DeviceType = BCIDeviceType.MuseHeadband,
                LastConnected = DateTime.Now.AddDays(-7)
            });
        }

        /// <summary>
        /// Initializes the device manager and sets up event handlers
        /// This happens automatically when the page appears
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                if (IsInitialized)
                    return;

                StatusText = "Initializing...";

                // Small delay to avoid UI freezing
                await Task.Delay(100);

                // Set initialized flag
                IsInitialized = true;
                CanScan = true;
                StatusText = "Ready to scan for devices.";
            }
            catch (Exception ex)
            {
                StatusText = $"Initialization error: {ex.Message}";
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
                if (IsScanning)
                    return;

                StatusText = "Scanning for devices...";
                IsScanning = true;
                await deviceManager.StartScanningAsync();

                // Start the timeout timer
                scanTimeoutTimer.Start();
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
                if (!IsScanning)
                    return;

                // Stop the timeout timer
                scanTimeoutTimer.Stop();

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
        /// Initialize when page appears
        /// </summary>
        public async Task OnAppearingAsync()
        {
            try
            {
                if (!IsInitialized)
                {
                    await InitializeAsync();
                }

                // Start scanning automatically if no device is connected
                if (!IsConnected && !IsScanning && CanScan)
                {
                    await StartScanAsync();
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error during page initialization: {ex.Message}";
                Debug.WriteLine($"Error in OnAppearingAsync: {ex}");
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

                            // Get device settings
                            await LoadDeviceSettings(museDevice);

                            // Start battery monitoring
                            StartBatteryMonitoring();
                        }
                        else
                        {
                            StatusText += " (not verified - check device)";
                        }
                    }

                    IsConnected = true;
                    CanDisconnect = true;

                    // Add or update device in stored devices if not already present
                    AddOrUpdateStoredDevice(SelectedDevice);
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
        /// Loads device settings from the connected device
        /// </summary>
        private async Task LoadDeviceSettings(MuseDevice device)
        {
            try
            {
                if (device != null)
                {
                    var details = device.GetDeviceDetails();

                    // Create a settings model from the device details
                    CurrentDeviceSettings = new Services.DeviceSettingsModel
                    {
                        Name = details.TryGetValue("Name", out var name) ? name : "Unknown",
                        Model = details.TryGetValue("Model", out var model) ? model : "Unknown",
                        SerialNumber = details.TryGetValue("Serial", out var serial) ? serial : "Unknown",
                        Preset = details.TryGetValue("CurrentPreset", out var preset) ? preset : "Unknown",
                        NotchFilter = details.TryGetValue("NotchFilterEnabled", out var notchFilter) ? notchFilter : "Unknown",
                        SampleRate = details.TryGetValue("SampleRate", out var sampleRate) ? sampleRate : "Unknown",
                        EegChannels = details.TryGetValue("EEGChannels", out var eegChannels) ? eegChannels : "Unknown"
                    };

                    // Get initial battery level
                    BatteryPercent = await device.GetBatteryLevelAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading device settings: {ex.Message}");
                StatusText = $"Error loading device settings: {ex.Message}";
            }
        }

        /// <summary>
        /// Starts periodic battery level monitoring
        /// </summary>
        private void StartBatteryMonitoring()
        {
            try
            {
                // Stop existing timer if any
                if (batteryUpdateTimer != null)
                {
                    batteryUpdateTimer.Stop();
                }

                // Create new timer for battery updates
                batteryUpdateTimer = Application.Current.Dispatcher.CreateTimer();
                batteryUpdateTimer.Interval = TimeSpan.FromMilliseconds(BatteryUpdateIntervalMs);
                batteryUpdateTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        await UpdateBatteryLevelAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in battery update timer: {ex.Message}");
                    }
                };
                batteryUpdateTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting battery monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the battery level from the device
        /// </summary>
        private async Task UpdateBatteryLevelAsync()
        {
            try
            {
                if (deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected)
                {
                    var level = await deviceManager.CurrentDevice.GetBatteryLevelAsync();

                    // Update on UI thread
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        BatteryPercent = level;
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating battery level: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds or updates a device in the stored devices collection
        /// </summary>
        private void AddOrUpdateStoredDevice(IBCIDeviceInfo deviceInfo)
        {
            if (deviceInfo == null) return;

            // Check if device already exists in stored devices
            var existingDevice = StoredDevices.FirstOrDefault(d => d.DeviceId == deviceInfo.DeviceId);

            if (existingDevice != null)
            {
                // Update existing device
                existingDevice.LastConnected = DateTime.Now;
            }
            else
            {
                // Add new device
                var newDevice = new StoredDeviceInfo
                {
                    Name = deviceInfo.Name,
                    DeviceId = deviceInfo.DeviceId,
                    DeviceType = deviceInfo.DeviceType,
                    LastConnected = DateTime.Now
                };

                StoredDevices.Add(newDevice);
            }

            // In a real app, save to preferences or database
            // SaveStoredDevices();
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

                // Log the disconnection attempt
                Debug.WriteLine("DisconnectAsync called, attempting to disconnect from current device");

                // Stop battery monitoring
                if (batteryUpdateTimer != null)
                {
                    Debug.WriteLine("Stopping battery update timer");
                    batteryUpdateTimer.Stop();
                    batteryUpdateTimer = null;
                }

                // Track time for disconnection process
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Log current device status before disconnection
                if (deviceManager.CurrentDevice != null)
                {
                    Debug.WriteLine($"Current device before disconnect: {deviceManager.CurrentDevice.Name}, IsConnected: {deviceManager.CurrentDevice.IsConnected}");
                }
                else
                {
                    Debug.WriteLine("No current device found in device manager");
                }

                // Force manual device disconnection
                if (deviceManager.CurrentDevice is MuseDevice museDevice)
                {
                    Debug.WriteLine("Found Muse device, calling specific disconnect");
                    await museDevice.DisconnectAsync();
                }
                else
                {
                    Debug.WriteLine("Using device manager to disconnect");
                    await deviceManager.DisconnectCurrentDeviceAsync();
                }

                stopwatch.Stop();

                // Update the UI state
                IsConnected = false;
                CanConnect = SelectedDevice != null;
                StatusText = $"Disconnected (took {stopwatch.ElapsedMilliseconds}ms)";
                DataText = "No data";

                // Reset battery level
                BatteryPercent = 0;

                // Force property change notifications
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(IsNotConnected));
                OnPropertyChanged(nameof(DevicePanelTitle));

                Debug.WriteLine("Disconnect completed successfully");
            }
            catch (Exception ex)
            {
                StatusText = $"Disconnect error: {ex.Message}";
                CanDisconnect = IsConnected;
                Debug.WriteLine($"Disconnect error: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Force disconnect UI state in case of error
                IsConnected = false;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(IsNotConnected));
            }
        }

        /// <summary>
        /// Shows the presets dialog for the current device
        /// </summary>
        private async Task ShowPresetsAsync()
        {
            // Here you would show a dialog or navigate to a presets page
            await Shell.Current.DisplayAlert("Presets",
                $"Presets for {CurrentDeviceSettings.Name}\nCurrent preset: {CurrentDeviceSettings.Preset}",
                "OK");
        }

        /// <summary>
        /// Saves the current device settings
        /// </summary>
        private async Task SaveDeviceSettingsAsync()
        {
            // In a real app, save settings to preferences or database
            await Shell.Current.DisplayAlert("Settings Saved",
                $"Settings for {CurrentDeviceSettings.Name} have been saved.",
                "OK");
        }

        /// <summary>
        /// Shows the supported devices dialog
        /// </summary>
        private async Task ShowSupportedDevicesAsync()
        {
            // In a real app, show a dialog with supported device types
            await Shell.Current.DisplayAlert("Supported Devices",
                "Currently supported devices:\n" +
                "- Muse Headband\n" +
                "- Mendi Headband (Coming Soon)",
                "OK");
        }

        /// <summary>
        /// Shows the presets dialog for a stored device
        /// </summary>
        private async Task EditDevicePresetsAsync(StoredDeviceInfo deviceInfo)
        {
            if (deviceInfo == null) return;

            // Here you would show a dialog or navigate to a presets page
            await Shell.Current.DisplayAlert("Device Presets",
                $"Edit presets for {deviceInfo.Name}",
                "OK");
        }

        /// <summary>
        /// Toggles scanning state
        /// </summary>
        private async Task ToggleScanAsync()
        {
            try
            {
                if (IsScanning)
                {
                    await StopScanAsync();
                }
                else
                {
                    await StartScanAsync();
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error toggling scan: {ex.Message}";
                Debug.WriteLine($"Error in ToggleScanAsync: {ex}");
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

                    // Stop battery monitoring
                    batteryUpdateTimer?.Stop();
                    batteryUpdateTimer = null;

                    // Reset battery level
                    BatteryPercent = 0;
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    if (batteryUpdateTimer != null)
                    {
                        batteryUpdateTimer.Stop();
                        batteryUpdateTimer = null;
                    }

                    if (scanTimeoutTimer != null)
                    {
                        scanTimeoutTimer.Stop();
                        scanTimeoutTimer = null;
                    }

                    // Unsubscribe from events
                    if (deviceManager != null)
                    {
                        deviceManager.DeviceListChanged -= OnDeviceListChanged;
                        deviceManager.ErrorOccurred -= OnErrorOccurred;
                    }

                    if (deviceManager?.CurrentDevice != null)
                    {
                        var device = deviceManager.CurrentDevice;
                        device.ConnectionStateChanged -= OnDeviceConnectionStateChanged;
                        device.BrainWaveDataReceived -= OnBrainWaveDataReceived;
                        device.ArtifactDetected -= OnArtifactDetected;
                        device.ErrorOccurred -= OnDeviceErrorOccurred;
                    }
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Model for stored device information
    /// </summary>
    public partial class StoredDeviceInfo : ObservableObject
    {
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string deviceId;

        [ObservableProperty]
        private BCIDeviceType deviceType;

        [ObservableProperty]
        private DateTime lastConnected;
    }
}