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
        [NotifyPropertyChangedFor(nameof(IsNotConnecting))]
        private bool isConnecting;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotConnected))]
        [NotifyPropertyChangedFor(nameof(DevicePanelTitle))]
        private bool isConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotConnected))]
        private bool canInitialize = false;

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
        public bool IsNotConnecting => !IsConnecting;
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

        public YourDevicesPageModel(IBCIDeviceManager deviceManager)
        {
            this.deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));

            // Initialize commands
            ScanCommand = new AsyncRelayCommand(ToggleScanAsync, () => CanScan);
            ConnectCommand = new AsyncRelayCommand<IBCIDeviceInfo>(ConnectToDeviceAsync);
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

            // Create the scan timeout timer
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

        private void LoadStoredDevices()
        {
            StoredDevices.Clear();

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

        private async Task InitializeAsync()
        {
            try
            {
                if (IsInitialized)
                    return;

                StatusText = "Initializing...";
                await Task.Delay(100);
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

        private async Task StartScanAsync()
        {
            try
            {
                if (IsScanning)
                    return;

                StatusText = "Scanning for devices...";
                IsScanning = true;
                await deviceManager.StartScanningAsync();

                // Reset and start the timeout timer
                scanTimeoutTimer?.Stop();
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
                scanTimeoutTimer.Start();
            }
            catch (Exception ex)
            {
                StatusText = $"Error scanning: {ex.Message}";
                IsScanning = false;
                Debug.WriteLine($"Error scanning: {ex}");
            }
        }

        private async Task StopScanAsync()
        {
            try
            {
                if (!IsScanning)
                    return;

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

        public async Task OnAppearingAsync()
        {
            try
            {
                if (!IsInitialized)
                {
                    await InitializeAsync();
                }

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

        private async Task ConnectAsync()
        {
            try
            {
                if (SelectedDevice == null)
                {
                    Debug.WriteLine("No device selected for connection");
                    return;
                }

                Debug.WriteLine($"Attempting to connect to device: {SelectedDevice.Name} (ID: {SelectedDevice.DeviceId})");

                StatusText = $"Connecting to {SelectedDevice.Name}...";
                IsConnecting = true;
                CanConnect = false;
                CanDisconnect = false;

                if (IsScanning)
                {
                    await StopScanAsync();
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                IBCIDevice device = await deviceManager.ConnectToDeviceAsync(SelectedDevice);

                if (device != null)
                {
                    stopwatch.Stop();
                    DataText = $"Connection established in {stopwatch.ElapsedMilliseconds}ms";
                    StatusText = $"Connected to {SelectedDevice.Name}";
                    Debug.WriteLine($"Successfully connected to {SelectedDevice.Name} in {stopwatch.ElapsedMilliseconds}ms");

                    device.ConnectionStateChanged += OnDeviceConnectionStateChanged;
                    device.BrainWaveDataReceived += OnBrainWaveDataReceived;
                    device.ArtifactDetected += OnArtifactDetected;
                    device.ErrorOccurred += OnDeviceErrorOccurred;

                    device.RegisterForBrainWaveData(BrainWaveTypes.All);

                    if (device is MuseDevice museDevice)
                    {
                        var verified = await museDevice.VerifyConnectionAsync();
                        if (verified)
                        {
                            StatusText += " (verified)";
                            Debug.WriteLine("Connection verified successfully");
                            await LoadDeviceSettings(museDevice);
                            StartBatteryMonitoring();
                        }
                        else
                        {
                            StatusText += " (not verified - check device)";
                            Debug.WriteLine("Connection verification failed");
                        }
                    }

                    IsConnected = true;
                    CanDisconnect = true;

                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(IsNotConnected));
                    OnPropertyChanged(nameof(DevicePanelTitle));

                    AddOrUpdateStoredDevice(SelectedDevice);
                }
                else
                {
                    stopwatch.Stop();
                    StatusText = $"Connection failed after {stopwatch.ElapsedMilliseconds}ms";
                    CanConnect = true;
                    Debug.WriteLine($"Connection failed after {stopwatch.ElapsedMilliseconds}ms - device is null");
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Connection error: {ex.Message}";
                CanConnect = SelectedDevice != null;
                Debug.WriteLine($"Connection error: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private async Task ConnectToDeviceAsync(IBCIDeviceInfo device)
        {
            if (device == null)
            {
                Debug.WriteLine("No device provided to connect");
                return;
            }

            // Set selected device first
            SelectedDevice = device;
            Debug.WriteLine($"Device selected: {device.Name}");

            // Then connect
            await ConnectAsync();
        }

        private async Task LoadDeviceSettings(MuseDevice device)
        {
            try
            {
                if (device != null)
                {
                    var details = device.GetDeviceDetails();

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

                    BatteryPercent = await device.GetBatteryLevelAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading device settings: {ex.Message}");
                StatusText = $"Error loading device settings: {ex.Message}";
            }
        }

        private void StartBatteryMonitoring()
        {
            try
            {
                if (batteryUpdateTimer != null)
                {
                    batteryUpdateTimer.Stop();
                }

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

        private async Task UpdateBatteryLevelAsync()
        {
            try
            {
                if (deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected)
                {
                    var level = await deviceManager.CurrentDevice.GetBatteryLevelAsync();

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

        private void AddOrUpdateStoredDevice(IBCIDeviceInfo deviceInfo)
        {
            if (deviceInfo == null) return;

            var existingDevice = StoredDevices.FirstOrDefault(d => d.DeviceId == deviceInfo.DeviceId);

            if (existingDevice != null)
            {
                existingDevice.LastConnected = DateTime.Now;
            }
            else
            {
                var newDevice = new StoredDeviceInfo
                {
                    Name = deviceInfo.Name,
                    DeviceId = deviceInfo.DeviceId,
                    DeviceType = deviceInfo.DeviceType,
                    LastConnected = DateTime.Now
                };

                StoredDevices.Add(newDevice);
            }
        }

        private async Task DisconnectAsync()
        {
            try
            {
                StatusText = "Disconnecting...";
                CanDisconnect = false;

                Debug.WriteLine("DisconnectAsync called, attempting to disconnect from current device");

                if (batteryUpdateTimer != null)
                {
                    Debug.WriteLine("Stopping battery update timer");
                    batteryUpdateTimer.Stop();
                    batteryUpdateTimer = null;
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                if (deviceManager.CurrentDevice != null)
                {
                    Debug.WriteLine($"Current device before disconnect: {deviceManager.CurrentDevice.Name}, IsConnected: {deviceManager.CurrentDevice.IsConnected}");
                }
                else
                {
                    Debug.WriteLine("No current device found in device manager");
                }

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

                IsConnected = false;
                CanConnect = SelectedDevice != null;
                StatusText = $"Disconnected (took {stopwatch.ElapsedMilliseconds}ms)";
                DataText = "No data";

                BatteryPercent = 0;

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

                IsConnected = false;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(IsNotConnected));
            }
        }

        private async Task ShowPresetsAsync()
        {
            await Shell.Current.DisplayAlert("Presets",
                $"Presets for {CurrentDeviceSettings.Name}\nCurrent preset: {CurrentDeviceSettings.Preset}",
                "OK");
        }

        private async Task SaveDeviceSettingsAsync()
        {
            await Shell.Current.DisplayAlert("Settings Saved",
                $"Settings for {CurrentDeviceSettings.Name} have been saved.",
                "OK");
        }

        private async Task ShowSupportedDevicesAsync()
        {
            await Shell.Current.DisplayAlert("Supported Devices",
                "Currently supported devices:\n" +
                "- Muse Headband\n" +
                "- Mendi Headband (Coming Soon)",
                "OK");
        }

        private async Task EditDevicePresetsAsync(StoredDeviceInfo deviceInfo)
        {
            if (deviceInfo == null) return;

            await Shell.Current.DisplayAlert("Device Presets",
                $"Edit presets for {deviceInfo.Name}",
                "OK");
        }

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

        private void OnDeviceListChanged(object sender, System.Collections.Generic.List<IBCIDeviceInfo> devices)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (devices.Count > 0)
                    {
                        StatusText = $"Found {devices.Count} device(s)";
                        Debug.WriteLine($"DeviceListChanged: Found {devices.Count} device(s)");

                        foreach (var device in devices)
                        {
                            Debug.WriteLine($"  > Device: {device.Name} (ID: {device.DeviceId}, Signal: {device.SignalStrength})");
                        }
                    }
                    else
                    {
                        StatusText = "No devices found";
                        Debug.WriteLine("DeviceListChanged: No devices found");
                    }

                    CanConnect = SelectedDevice != null && !IsConnected;
                    OnPropertyChanged(nameof(AvailableDevices));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnDeviceListChanged: {ex.Message}");
                StatusText = "Error updating device list";
            }
        }

        private void OnDeviceConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            StatusText = $"Connection state: {e.NewState}";

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

                    batteryUpdateTimer?.Stop();
                    batteryUpdateTimer = null;

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

        private void OnBrainWaveDataReceived(object sender, BrainWaveDataEventArgs e)
        {
            try
            {
                if (e?.BrainWaveData == null)
                {
                    DataText = "Received invalid data packet";
                    return;
                }

                var waveType = e.BrainWaveData.WaveType;

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
                        try
                        {
                            if (e.BrainWaveData.ChannelValues != null && e.BrainWaveData.ChannelValues.Length > 0)
                            {
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
                                    double min = double.MaxValue;
                                    double max = double.MinValue;

                                    foreach (var value in e.BrainWaveData.ChannelValues)
                                    {
                                        if (value < min) min = value;
                                        if (value > max) max = value;
                                    }

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
                Debug.WriteLine($"Error in OnBrainWaveDataReceived: {ex.Message}");
                DataText = "Error processing brain wave data";
            }
        }

        private void OnArtifactDetected(object sender, ArtifactEventArgs e)
        {
            DataText = $"Artifacts: Blink={e.Blink}, Jaw Clench={e.JawClench}, Headband Loose={e.HeadbandTooLoose}";
        }

        private void OnErrorOccurred(object sender, BCIErrorEventArgs e)
        {
            StatusText = $"Error: {e.Message}";
            Debug.WriteLine($"Device manager error: {e.Message}, Type: {e.ErrorType}");
        }

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