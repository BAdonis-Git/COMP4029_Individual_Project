using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Models.Stream;
using NeuroSpectator.Services.BCI;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.Integration;
using NeuroSpectator.Services.Streaming;
using NeuroSpectator.Services.Visualisation;
using System.Diagnostics;
using System.Net;
using System.Windows.Input;

namespace NeuroSpectator.PageModels
{
    public partial class StreamStreamerPageModel : ObservableObject
    {
        private readonly IBCIDeviceManager deviceManager;
        private readonly DeviceConnectionManager connectionManager;
        private readonly OBSIntegrationService obsService;
        private readonly IMKIOStreamingService streamingService;
        private readonly BrainDataVisualisationService visualizationService;
        private IDispatcherTimer deviceCheckTimer;
        private const int DeviceCheckIntervalMs = 5000;

        private BrainDataOBSHelper brainDataObsHelper;

        #region Properties

        [ObservableProperty]
        private string streamTitle = "NeuroSpectator Stream";

        [ObservableProperty]
        private string gameCategory = "Gaming";

        [ObservableProperty]
        private string streamTimeDisplay = "00:00:00";

        [ObservableProperty]
        private int viewerCount = 0;

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private bool isConnectedToObs = false;

        [ObservableProperty]
        private bool isLive = false;

        [ObservableProperty]
        private bool cameraEnabled = true;

        [ObservableProperty]
        private bool micEnabled = true;

        [ObservableProperty]
        private bool isBrainDataVisible = true;

        [ObservableProperty]
        private string obsScene = "Main";

        [ObservableProperty]
        private List<string> availableScenes = new List<string>();

        [ObservableProperty]
        private string chatMessageInput = "";

        [ObservableProperty]
        private string streamHealth = "Good";

        [ObservableProperty]
        private int brainEventCount = 0;

        [ObservableProperty]
        private Dictionary<string, string> brainMetrics = new Dictionary<string, string>();

        [ObservableProperty]
        private bool isSetupComplete = false;

        [ObservableProperty]
        private bool isAutoConfiguringObs = false;

        [ObservableProperty]
        private bool isVirtualCameraActive = false;

        [ObservableProperty]
        private string previewUrl = "about:blank";

        [ObservableProperty]
        private bool isCameraPermissionDenied = false;

        [ObservableProperty]
        private bool isPreviewAvailable = false;

        // Timer for updating the stream time
        private System.Timers.Timer streamTimer;
        private DateTime streamStartTime;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NoDeviceConnected))]
        [NotifyPropertyChangedFor(nameof(CanStartStream))]
        [NotifyPropertyChangedFor(nameof(DeviceStatusMessage))]
        private bool isDeviceConnected = false;

        [ObservableProperty]
        private string connectedDeviceName = "No Device";

        [ObservableProperty]
        private string deviceStatusMessage = "No BCI device connected";

        [ObservableProperty]
        private double deviceSignalQuality = 0;

        [ObservableProperty]
        private double deviceBatteryLevel = 0;

        [ObservableProperty]
        private bool isCheckingDevice = false;

        [ObservableProperty]
        private bool isStartingStream = false;

        [ObservableProperty]
        private string startStreamButtonText = "Start Stream";

        // Derived properties
        public bool NoDeviceConnected => !IsDeviceConnected;

        public bool CanStartStream => IsConnectedToObs && IsDeviceConnected && !IsLive;

        #endregion

        #region Commands

        public ICommand ConnectToObsCommand { get; }
        public ICommand RefreshObsInfoCommand { get; }
        public ICommand StartStreamCommand { get; }
        public ICommand EndStreamCommand { get; }
        public ICommand ToggleCameraCommand { get; }
        public ICommand ToggleMicCommand { get; }
        public ICommand ToggleBrainDataCommand { get; }
        public ICommand ConfigureBrainDataCommand { get; }
        public ICommand MarkHighlightCommand { get; }
        public ICommand TakeScreenshotCommand { get; }
        public ICommand SendChatMessageCommand { get; }
        public ICommand ShareBrainEventCommand { get; }
        public ICommand ConfirmExitAsync { get; }
        public ICommand AutoConfigureOBSCommand { get; }
        public ICommand ShowOBSSetupGuideCommand { get; }
        public ICommand DiagnoseObsCommand { get; }
        public ICommand ToggleVirtualCameraCommand { get; }
        public ICommand RefreshDeviceStatusCommand { get; }

        #endregion

        public StreamStreamerPageModel(
            IBCIDeviceManager deviceManager,
            DeviceConnectionManager connectionManager,
            OBSIntegrationService obsService,
            IMKIOStreamingService streamingService,
            BrainDataVisualisationService visualizationService)
        {
            this.deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            this.obsService = obsService ?? throw new ArgumentNullException(nameof(obsService));
            this.streamingService = streamingService ?? throw new ArgumentNullException(nameof(streamingService));
            this.visualizationService = visualizationService ?? throw new ArgumentNullException(nameof(visualizationService));

            // Initialize commands
            ConnectToObsCommand = new AsyncRelayCommand(ConnectToObsAsync);
            RefreshObsInfoCommand = new AsyncRelayCommand(RefreshObsInfoAsync);
            StartStreamCommand = new AsyncRelayCommand(StartStreamAsync);
            EndStreamCommand = new AsyncRelayCommand(EndStreamAsync);
            ToggleCameraCommand = new AsyncRelayCommand(ToggleCameraAsync);
            ToggleMicCommand = new AsyncRelayCommand(ToggleMicAsync);
            ToggleBrainDataCommand = new AsyncRelayCommand(ToggleBrainDataAsync);
            ConfigureBrainDataCommand = new AsyncRelayCommand(ConfigureBrainDataAsync);
            MarkHighlightCommand = new AsyncRelayCommand(MarkHighlightAsync);
            TakeScreenshotCommand = new AsyncRelayCommand(TakeScreenshotAsync);
            SendChatMessageCommand = new AsyncRelayCommand(SendChatMessageAsync);
            ShareBrainEventCommand = new AsyncRelayCommand(ShareBrainEventAsync);
            ConfirmExitAsync = new AsyncRelayCommand(ShowExitConfirmationAsync);
            AutoConfigureOBSCommand = new AsyncRelayCommand(AutoConfigureOBSAsync);
            ShowOBSSetupGuideCommand = new AsyncRelayCommand(ShowOBSSetupGuideAsync);
            DiagnoseObsCommand = new AsyncRelayCommand(DiagnoseOBSConnectionAsync);
            ToggleVirtualCameraCommand = new AsyncRelayCommand(ToggleVirtualCameraAsync);
            RefreshDeviceStatusCommand = new AsyncRelayCommand(RefreshDeviceConnectionStatusAsync);

            // Subscribe to events
            obsService.ConnectionStatusChanged += OnObsConnectionStatusChanged;
            obsService.StreamingStatusChanged += OnObsStreamingStatusChanged;
            obsService.SceneChanged += OnObsSceneChanged;

            connectionManager.ConnectionStatusChanged += OnDeviceConnectionStatusChanged;
            streamingService.StatusChanged += OnStreamingStatusChanged;
            streamingService.StatisticsUpdated += OnStreamingStatsUpdated;

            deviceManager.DeviceListChanged += OnDeviceListChanged;

            if (connectionManager != null)
            {
                connectionManager.ConnectionStatusChanged += OnDeviceConnectionStatusChanged;
                connectionManager.DeviceConnected += OnDeviceConnected;
                connectionManager.DeviceDisconnected += OnDeviceDisconnected;
            }

            // Check for currently connected device
            Task.Run(async () => await InitializeDeviceConnectionAsync());

            // Subscribe to other events
            obsService.ConnectionStatusChanged += OnObsConnectionStatusChanged;
            obsService.StreamingStatusChanged += OnObsStreamingStatusChanged;
            obsService.SceneChanged += OnObsSceneChanged;

            streamingService.StatusChanged += OnStreamingStatusChanged;
            streamingService.StatisticsUpdated += OnStreamingStatsUpdated;

            // Initialize brain metrics
            InitializeBrainMetrics();

            // Initialize stream timer
            streamTimer = new System.Timers.Timer(1000);
            streamTimer.Elapsed += OnStreamTimerElapsed;
        }

        #region Device Connection Management

        /// <summary>
        /// Initializes the device connection status
        /// </summary>
        private async Task InitializeDeviceConnectionAsync()
        {
            try
            {
                //Debug.WriteLine("StreamerPage: Initializing device connection");

                // Check if there's already a connected device
                if (deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        IsDeviceConnected = true;
                        ConnectedDeviceName = deviceManager.CurrentDevice.Name ?? "Unknown Device";
                        DeviceStatusMessage = $"Connected to {ConnectedDeviceName}";
                        //Debug.WriteLine($"StreamerPage: Found connected device: {ConnectedDeviceName}");
                    });

                    // Set up event handlers for the connected device
                    deviceManager.CurrentDevice.ConnectionStateChanged += OnDeviceConnectionStateChanged;
                    deviceManager.CurrentDevice.BrainWaveDataReceived += OnBrainWaveDataReceived;

                    // Get device info
                    await UpdateDeviceInfoAsync(deviceManager.CurrentDevice);
                }
                else if (connectionManager != null)
                {
                    // Check connection manager as fallback
                    var statusInfo = await connectionManager.RefreshConnectionStatusAsync();

                    if (statusInfo.IsConnected)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            IsDeviceConnected = true;
                            ConnectedDeviceName = statusInfo.DeviceName ?? "Unknown Device";
                            DeviceStatusMessage = $"Connected to {ConnectedDeviceName}";
                            //Debug.WriteLine($"StreamerPage: Found connected device via connection manager: {ConnectedDeviceName}");
                        });
                    }
                    else
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            IsDeviceConnected = false;
                            ConnectedDeviceName = "No Device";
                            DeviceStatusMessage = "No BCI device connected";
                            //Debug.WriteLine("StreamerPage: No connected device found");
                        });
                    }
                }

                // Start device check timer
                StartDeviceCheckTimer();
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"StreamerPage: Error initializing device connection: {ex.Message}");
                StatusMessage = $"Error checking device: {ex.Message}";
            }
        }

        /// <summary>
        /// Starts periodic device connection checking
        /// </summary>
        private void StartDeviceCheckTimer()
        {
            try
            {
                if (deviceCheckTimer != null)
                {
                    deviceCheckTimer.Stop();
                }

                deviceCheckTimer = Application.Current.Dispatcher.CreateTimer();
                deviceCheckTimer.Interval = TimeSpan.FromMilliseconds(DeviceCheckIntervalMs);
                deviceCheckTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        await RefreshDeviceConnectionStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        //Debug.WriteLine($"StreamerPage: Error in device check timer: {ex.Message}");
                    }
                };
                deviceCheckTimer.Start();

                //Debug.WriteLine("StreamerPage: Started device check timer");
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"StreamerPage: Error starting device check timer: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the device check timer
        /// </summary>
        private void StopDeviceCheckTimer()
        {
            if (deviceCheckTimer != null)
            {
                deviceCheckTimer.Stop();
                deviceCheckTimer = null;
                //Debug.WriteLine("StreamerPage: Stopped device check timer");
            }
        }

        // Helper method to update start stream button text based on state
        private void UpdateStartStreamButtonText(string status)
        {
            if (IsStartingStream)
            {
                StartStreamButtonText = status;
            }
            else
            {
                StartStreamButtonText = "Start Stream";
            }
        }

        /// <summary>
        /// Refreshes device connection status
        /// </summary>
        public async Task RefreshDeviceConnectionStatusAsync()
        {
            if (IsCheckingDevice)
                return;

            try
            {
                IsCheckingDevice = true;
                //Debug.WriteLine("StreamerPage: Refreshing device connection status");

                bool wasConnected = IsDeviceConnected;
                bool deviceFound = false;

                // IMPORTANT: First check connection manager as the source of truth
                if (connectionManager != null)
                {
                    var statusInfo = await connectionManager.RefreshConnectionStatusAsync();
                    if (statusInfo.IsConnected)
                    {
                        IsDeviceConnected = true;
                        ConnectedDeviceName = statusInfo.DeviceName ?? "Unknown Device";
                        DeviceStatusMessage = $"Connected to {ConnectedDeviceName}";
                        deviceFound = true;
                        //Debug.WriteLine($"StreamerPage: Connected device found via connection manager: {ConnectedDeviceName}");

                        // If connection manager knows about the device but deviceManager doesn't,
                        // there could be a sync issue - find the device in deviceManager
                        if (deviceManager.CurrentDevice == null || !deviceManager.CurrentDevice.IsConnected)
                        {
                            // Try to find a device with matching name/id in available devices
                            foreach (var deviceInfo in deviceManager.AvailableDevices)
                            {
                                if (deviceInfo.Name == ConnectedDeviceName)
                                {
                                    //Debug.WriteLine($"StreamerPage: Found matching device in deviceManager's available devices");
                                    // Don't connect here, as that could cause recursive issues
                                    break;
                                }
                            }
                        }
                    }
                }

                // If not found in connection manager, check device manager
                if (!deviceFound && deviceManager.CurrentDevice != null)
                {
                    bool isConnected = deviceManager.CurrentDevice.IsConnected;
                    if (isConnected)
                    {
                        IsDeviceConnected = true;
                        ConnectedDeviceName = deviceManager.CurrentDevice.Name ?? "Unknown Device";
                        DeviceStatusMessage = $"Connected to {ConnectedDeviceName}";
                        deviceFound = true;

                        // Update device info
                        await UpdateDeviceInfoAsync(deviceManager.CurrentDevice);

                        //Debug.WriteLine($"StreamerPage: Connected device found in deviceManager: {ConnectedDeviceName}");

                        // IMPORTANT: Register with connection manager if not already done
                        if (connectionManager != null)
                        {
                            Debug.WriteLine($"StreamerPage: Ensuring device is registered with connection manager");
                            connectionManager.RegisterDevice(deviceManager.CurrentDevice);
                        }
                    }
                }

                // Update if no device found
                if (!deviceFound)
                {
                    IsDeviceConnected = false;
                    ConnectedDeviceName = "No Device";
                    DeviceStatusMessage = "No BCI device connected";
                    DeviceBatteryLevel = 0;
                    DeviceSignalQuality = 0;

                    //Debug.WriteLine("StreamerPage: No connected device found");
                }

                // Update status message
                StatusMessage = IsDeviceConnected
                    ? $"Device '{ConnectedDeviceName}' connected."
                    : "No BCI device connected. Connect a device in Your Devices.";

                // If connection status changed, update UI properties
                if (wasConnected != IsDeviceConnected)
                {
                    OnPropertyChanged(nameof(IsDeviceConnected));
                    OnPropertyChanged(nameof(NoDeviceConnected));
                    OnPropertyChanged(nameof(CanStartStream));
                    OnPropertyChanged(nameof(DeviceStatusMessage));
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"StreamerPage: Error refreshing device status: {ex.Message}");
                StatusMessage = $"Error checking device: {ex.Message}";
            }
            finally
            {
                IsCheckingDevice = false;
            }
        }

        /// <summary>
        /// Updates device information (battery, signal quality)
        /// </summary>
        private async Task UpdateDeviceInfoAsync(IBCIDevice device)
        {
            if (device == null || !device.IsConnected)
                return;

            try
            {
                // Get battery level
                try
                {
                    double batteryLevel = await device.GetBatteryLevelAsync();
                    DeviceBatteryLevel = batteryLevel;
                    //Debug.WriteLine($"StreamerPage: Device battery level: {batteryLevel}%");
                }
                catch (Exception ex)
                {
                    //Debug.WriteLine($"StreamerPage: Error getting battery level: {ex.Message}");
                }

                // Get signal quality if available
                try
                {
                    double signalQuality = await device.GetSignalQualityAsync();
                    DeviceSignalQuality = signalQuality;
                    //Debug.WriteLine($"StreamerPage: Device signal quality: {signalQuality:P0}");
                }
                catch (Exception ex)
                {
                    //Debug.WriteLine($"StreamerPage: Error getting signal quality: {ex.Message}");
                }

                // Update status message with battery info
                DeviceStatusMessage = $"Connected to {device.Name} - Battery: {DeviceBatteryLevel:F0}%";
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"StreamerPage: Error updating device info: {ex.Message}");
            }
        }

        #endregion

        #region Brain Data Handling

        /// <summary>
        /// Initializes brain metrics with default values
        /// </summary>
        private void InitializeBrainMetrics()
        {
            BrainMetrics = new Dictionary<string, string>
            {
                { "Focus", "0%" },
                { "Alpha Wave", "Low" },
                { "Beta Wave", "Low" },
                { "Theta Wave", "Low" },
                { "Delta Wave", "Low" },
                { "Gamma Wave", "Low" }
            };
        }

        /// <summary>
        /// Handles brain wave data received from the device
        /// </summary>
        private void OnBrainWaveDataReceived(object sender, BrainWaveDataEventArgs e)
        {
            try
            {
                // Process the brain wave data
                ProcessBrainWaveData(e.BrainWaveData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StreamerPage: Error processing brain wave data: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a brain wave data packet
        /// </summary>
        private void ProcessBrainWaveData(BrainWaveData data)
        {
            // Process different types of brain wave data
            switch (data.WaveType)
            {
                case BrainWaveTypes.Alpha:
                    UpdateBrainMetric("Alpha Wave", ClassifyWaveLevel(data.AverageValue), $"{data.AverageValue:F1}μV");
                    break;

                case BrainWaveTypes.Beta:
                    UpdateBrainMetric("Beta Wave", ClassifyWaveLevel(data.AverageValue), $"{data.AverageValue:F1}μV");
                    break;

                case BrainWaveTypes.Theta:
                    UpdateBrainMetric("Theta Wave", ClassifyWaveLevel(data.AverageValue), $"{data.AverageValue:F1}μV");
                    break;

                case BrainWaveTypes.Delta:
                    UpdateBrainMetric("Delta Wave", ClassifyWaveLevel(data.AverageValue), $"{data.AverageValue:F1}μV");
                    break;

                case BrainWaveTypes.Gamma:
                    UpdateBrainMetric("Gamma Wave", ClassifyWaveLevel(data.AverageValue), $"{data.AverageValue:F1}μV");
                    break;
            }
        }

        /// <summary>
        /// Updates a brain metric with new data
        /// </summary>
        private void UpdateBrainMetric(string metricName, string level, string value)
        {
            if (BrainMetrics.ContainsKey(metricName))
            {
                // Update on the UI thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    BrainMetrics[metricName] = value;
                });
            }
        }

        /// <summary>
        /// Classifies a wave level as Low, Medium, or High
        /// </summary>
        private string ClassifyWaveLevel(double value)
        {
            // These thresholds would need to be calibrated for specific BCI device
            if (value < 30)
                return "Low";
            else if (value < 60)
                return "Medium";
            else
                return "High";
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the DeviceListChanged event
        /// </summary>
        private void OnDeviceListChanged(object sender, System.Collections.Generic.List<IBCIDeviceInfo> devices)
        {
            Debug.WriteLine($"StreamerPage: Device list changed - {devices.Count} devices available");
        }

        /// <summary>
        /// Handles the ConnectionStateChanged event from the device
        /// </summary>
        private async void OnDeviceConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            Debug.WriteLine($"StreamerPage: Device connection state changed {e.OldState} -> {e.NewState}");

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // If the state is now Connected or Disconnected, refresh device status
                if (e.NewState == ConnectionState.Connected || e.NewState == ConnectionState.Disconnected)
                {
                    await RefreshDeviceConnectionStatusAsync();
                }
            });
        }

        /// <summary>
        /// Handles the ConnectionStatusChanged event from the connection manager
        /// </summary>
        private async void OnDeviceConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            Debug.WriteLine($"StreamerPage: Connection status changed {e.OldStatus} -> {e.NewStatus}");

            // Refresh device status on UI thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await RefreshDeviceConnectionStatusAsync();
            });
        }

        /// <summary>
        /// Handles the DeviceConnected event from the connection manager
        /// </summary>
        private async void OnDeviceConnected(object sender, IBCIDevice device)
        {
            Debug.WriteLine($"StreamerPage: Device connected event: {device.Name}");

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Update connection status
                IsDeviceConnected = true;
                ConnectedDeviceName = device.Name ?? "Unknown Device";
                DeviceStatusMessage = $"Connected to {ConnectedDeviceName}";

                // Update device info
                await UpdateDeviceInfoAsync(device);

                // Setup event handlers for the connected device
                device.ConnectionStateChanged += OnDeviceConnectionStateChanged;
                device.BrainWaveDataReceived += OnBrainWaveDataReceived;

                StatusMessage = $"Device '{ConnectedDeviceName}' connected.";

                // Update dependent properties
                OnPropertyChanged(nameof(NoDeviceConnected));
                OnPropertyChanged(nameof(CanStartStream));
                OnPropertyChanged(nameof(DeviceStatusMessage));
            });
        }

        /// <summary>
        /// Handles the DeviceDisconnected event from the connection manager
        /// </summary>
        private async void OnDeviceDisconnected(object sender, IBCIDevice device)
        {
            Debug.WriteLine($"StreamerPage: Device disconnected event: {device.Name}");

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Refresh device status
                await RefreshDeviceConnectionStatusAsync();

                // If streaming and the device disconnects, end the stream
                if (IsLive)
                {
                    StatusMessage = "Device disconnected - ending stream";
                    await EndStreamAsync();
                }
                else
                {
                    StatusMessage = "Device disconnected";
                }
            });
        }

        #endregion

        #region Page Lifecycle

        public async Task OnAppearingAsync()
        {
            try
            {
                StatusMessage = "Initializing...";

                // Check if OBS is connected
                IsConnectedToObs = obsService.IsConnected;

                if (IsConnectedToObs)
                {
                    await RefreshObsInfoAsync();

                    // Verify preview template before setting up preview
                    await VerifyPreviewTemplateExistsAsync();

                    // Set up preview URL
                    await SetupVirtualCameraPreviewAsync();

                    // Check virtual camera status
                    await CheckVirtualCameraStatusAsync();

                    StatusMessage = "Connected to OBS";
                }
                else
                {
                    StatusMessage = "Not connected to OBS";
                }

                // Check device connection status
                await RefreshDeviceConnectionStatusAsync();

                // Start device check timer if not already started
                StartDeviceCheckTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearingAsync: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Called when the page disappears
        /// </summary>
        public async Task OnDisappearingAsync()
        {
            try
            {
                // Stop device check timer
                StopDeviceCheckTimer();

                // If streaming, ask user if they want to end the stream
                if (IsLive)
                {
                    bool endStream = await Application.Current.MainPage.DisplayAlert(
                        "End Stream?",
                        "You are currently streaming. Do you want to end the stream?",
                        "End Stream", "Keep Streaming");

                    if (endStream)
                    {
                        await EndStreamAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnDisappearingAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up resources
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // Stop timers
                StopDeviceCheckTimer();
                streamTimer?.Stop();

                // Unsubscribe from events
                if (deviceManager != null)
                {
                    deviceManager.DeviceListChanged -= OnDeviceListChanged;

                    if (deviceManager.CurrentDevice != null)
                    {
                        deviceManager.CurrentDevice.ConnectionStateChanged -= OnDeviceConnectionStateChanged;
                        deviceManager.CurrentDevice.BrainWaveDataReceived -= OnBrainWaveDataReceived;
                    }
                }

                if (connectionManager != null)
                {
                    connectionManager.ConnectionStatusChanged -= OnDeviceConnectionStatusChanged;
                    connectionManager.DeviceConnected -= OnDeviceConnected;
                    connectionManager.DeviceDisconnected -= OnDeviceDisconnected;
                }

                // Clean up brain data helper
                if (brainDataObsHelper != null)
                {
                    brainDataObsHelper.Dispose();
                    brainDataObsHelper = null;
                }

                Debug.WriteLine("StreamerPage: Cleanup completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in Cleanup: {ex.Message}");
            }
        }

        #endregion

        #region Command Implementations

        /// <summary>
        /// Connects to OBS
        /// </summary>
        private async Task ConnectToObsAsync()
        {
            try
            {
                StatusMessage = "Connecting to OBS...";

                // Create a prompt for connection details
                string websocketUrl = await Application.Current.MainPage.DisplayPromptAsync(
                    "OBS Connection",
                    "Enter WebSocket URL (including port)",
                    initialValue: "ws://localhost:4444",
                    maxLength: 100);

                if (string.IsNullOrEmpty(websocketUrl))
                {
                    StatusMessage = "Connection canceled";
                    return;
                }

                string password = await Application.Current.MainPage.DisplayPromptAsync(
                    "OBS Connection",
                    "Enter password (leave empty if none)",
                    initialValue: "",
                    maxLength: 100);

                // Connect to OBS with the provided parameters
                await obsService.ConnectAsync(websocketUrl, password);

                // Wait a moment for connection events to process
                await Task.Delay(500);

                if (obsService.IsConnected)
                {
                    await RefreshObsInfoAsync();
                    StatusMessage = "Connected to OBS";

                    // Set up the preview URL for virtual camera
                    await SetupVirtualCameraPreviewAsync();
                }
                else
                {
                    StatusMessage = "Failed to connect to OBS";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"OBS connection error: {ex.Message}";
            }
        }

        /// <summary>
        /// Refreshes OBS information
        /// </summary>
        private async Task RefreshObsInfoAsync()
        {
            if (!obsService.IsConnected)
                return;

            try
            {
                // Get available scenes
                AvailableScenes = await obsService.GetScenesAsync();

                // Get current scene
                ObsScene = obsService.CurrentScene;

                // Get streaming status
                var streamStatus = await obsService.GetStreamStatusAsync();
                IsLive = streamStatus.IsActive;

                if (IsLive && !streamTimer.Enabled)
                {
                    // Start timer if streaming is active
                    streamStartTime = DateTime.Now.AddMilliseconds(-streamStatus.Duration);
                    streamTimer.Start();
                    UpdateStreamTimeDisplay();
                }
                else if (!IsLive && streamTimer.Enabled)
                {
                    streamTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error refreshing OBS info: {ex.Message}";
            }
        }

        /// <summary>
        /// Sets up the virtual camera preview URL
        /// </summary>
        private async Task SetupVirtualCameraPreviewAsync()
        {
            try
            {
                // Ensure the visualization server is running
                if (!visualizationService.IsServerRunning)
                {
                    Debug.WriteLine("Starting visualization server...");
                    await visualizationService.StartServerAsync();
                    Debug.WriteLine($"Visualization server started at: {visualizationService.VisualisationUrl}");
                }
                else
                {
                    Debug.WriteLine($"Visualization server already running at: {visualizationService.VisualisationUrl}");
                }

                // Ensure the preview template is available
                await visualizationService.EnsureOBSPreviewTemplateAvailableAsync();

                // Set the preview URL
                PreviewUrl = visualizationService.GetPreviewUrl();
                IsPreviewAvailable = true;

                // Check if virtual camera is active
                await CheckVirtualCameraStatusAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error setting up preview: {ex.Message}";
                IsPreviewAvailable = false;
            }
        }

        /// <summary>
        /// Checks if the virtual camera is currently active
        /// </summary>
        private async Task CheckVirtualCameraStatusAsync()
        {
            try
            {
                if (!IsConnectedToObs)
                {
                    IsVirtualCameraActive = false;
                    return;
                }

                // Add debug output
                bool isActive = await obsService.IsVirtualCameraActiveAsync();
                Debug.WriteLine($"Virtual camera active status from OBS: {isActive}");

                IsVirtualCameraActive = isActive;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking virtual camera status: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles the virtual camera state
        /// </summary>
        private async Task ToggleVirtualCameraAsync()
        {
            if (!IsConnectedToObs)
            {
                StatusMessage = "Connect to OBS first";
                return;
            }

            try
            {
                StatusMessage = IsVirtualCameraActive ? "Stopping virtual camera..." : "Starting virtual camera...";

                // Check if OBS is connected
                if (!obsService.IsConnected)
                {
                    StatusMessage = "OBS is not connected";
                    await RefreshObsInfoAsync();
                    return;
                }

                Debug.WriteLine($"Virtual Camera: Current state is {IsVirtualCameraActive}");

                if (IsVirtualCameraActive)
                {
                    // Stop virtual camera
                    Debug.WriteLine("Virtual Camera: Attempting to stop");
                    await obsService.StopVirtualCameraAsync();
                    IsVirtualCameraActive = false;
                    StatusMessage = "Virtual camera stopped";
                }
                else
                {
                    // Start virtual camera
                    Debug.WriteLine("Virtual Camera: Attempting to start");
                    await obsService.StartVirtualCameraAsync();
                    IsVirtualCameraActive = true;
                    StatusMessage = "Virtual camera started";
                }

                // Force UI to update the button state
                OnPropertyChanged(nameof(IsVirtualCameraActive));

                // Force refresh of the preview by changing the URL with a timestamp
                string baseUrl = visualizationService.GetPreviewUrl();
                PreviewUrl = $"{baseUrl}?refresh={DateTime.Now.Ticks}";

                // Log the new URL
                Debug.WriteLine($"Refreshing preview with URL: {PreviewUrl}");

                // Make sure preview is visible
                IsPreviewAvailable = true;

                // Wait a short time and then refresh again to ensure the WebView updates
                await Task.Delay(500);
                PreviewUrl = $"{baseUrl}?refresh={DateTime.Now.Ticks + 1}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling virtual camera: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                StatusMessage = $"Error toggling virtual camera: {ex.Message}";
            }
        }

        private async Task VerifyPreviewTemplateExistsAsync()
        {
            try
            {
                // Get the visualization directory path
                string visualizationDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NeuroSpectator", "Visualisations");

                // Check if the directory exists
                if (!Directory.Exists(visualizationDirectory))
                {
                    Debug.WriteLine($"ERROR: Visualization directory doesn't exist: {visualizationDirectory}");
                    StatusMessage = "Preview error: Visualization directory not found";
                    return;
                }

                // Check if the preview file exists
                string previewFilePath = Path.Combine(visualizationDirectory, "obs_preview.html");
                bool fileExists = File.Exists(previewFilePath);

                Debug.WriteLine($"Preview template file exists: {fileExists}");
                if (fileExists)
                {
                    Debug.WriteLine($"Preview file size: {new FileInfo(previewFilePath).Length} bytes");
                    // Optionally read the first few lines to verify content
                    string firstFewLines = string.Join("\n", File.ReadLines(previewFilePath).Take(5));
                    Debug.WriteLine($"Preview file content starts with: \n{firstFewLines}");
                }
                else
                {
                    // If file doesn't exist, try to create it manually
                    Debug.WriteLine("Creating preview template file manually...");
                    await visualizationService.EnsureOBSPreviewTemplateAvailableAsync();

                    // Check again after attempted creation
                    fileExists = File.Exists(previewFilePath);
                    Debug.WriteLine($"Preview template now exists: {fileExists}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error verifying preview template: {ex.Message}");
                StatusMessage = $"Preview error: {ex.Message}";
            }
        }

        /// <summary>
        /// Checks if an RTMP endpoint is valid and ready to accept streaming data
        /// </summary>
        private async Task<bool> IsRtmpEndpointReadyAsync(string rtmpUrl, int maxAttempts = 10, int delayMs = 3000)
        {
            Debug.WriteLine($"Checking if RTMP endpoint is ready: {rtmpUrl}");

            // Ttry to validate the URL format and check if the hostname resolves
            try
            {
                // Parse the RTMP URL
                Uri uri = new Uri(rtmpUrl);

                // Check if can resolve the hostname
                var hostEntry = await Dns.GetHostEntryAsync(uri.Host);

                // For RTMP need a more sophisticated check,
                // For now assume if the host resolves, it's potentially valid
                Debug.WriteLine($"RTMP host {uri.Host} resolved successfully");

                // To improve:
                // 1. Send a request to backend to check if the RTMP endpoint is accepting connections
                // 2. Use a small test publishing to check if the endpoint accepts data

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating RTMP endpoint: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts the stream
        /// </summary>
        private async Task StartStreamAsync()
        {
            // Prevent multiple attempts while already processing
            if (IsLive || IsStartingStream)
                return;

            try
            {
                // Update UI to show starting state
                IsStartingStream = true;
                StatusMessage = "Preparing to start stream...";
                UpdateStartStreamButtonText("Starting...");

                // Check if OBS is connected
                if (!obsService.IsConnected)
                {
                    StatusMessage = "OBS is not connected";
                    IsStartingStream = false;
                    UpdateStartStreamButtonText("Start Failed");
                    return;
                }

                // Check if a BCI device is connected
                await RefreshDeviceConnectionStatusAsync();
                if (!IsDeviceConnected)
                {
                    StatusMessage = "No BCI device connected";
                    IsStartingStream = false;
                    UpdateStartStreamButtonText("Start Failed");
                    return;
                }

                // Create a new stream in MK.IO
                try
                {
                    // Reset the streaming service - this is critical to success
                    StatusMessage = "Resetting streaming service...";
                    UpdateStartStreamButtonText("Resetting...");

                    try
                    {
                        await streamingService.ResetStatusAsync();
                        // Add a larger delay to ensure reset has fully taken effect
                        await Task.Delay(2000);
                    }
                    catch (Exception ex)
                    {
                        // Log but continue
                        Debug.WriteLine($"Error resetting streaming service: {ex.Message}");
                    }

                    // Use the current title and game as the stream details
                    StatusMessage = "Creating stream on MK.IO...";
                    UpdateStartStreamButtonText("Creating Stream...");

                    var stream = await streamingService.CreateStreamAsync(
                        StreamTitle,
                        $"NeuroSpectator stream with brain data visualization for {GameCategory}",
                        GameCategory,
                        new List<string> { "NeuroSpectator", "BrainData", GameCategory }
                    );

                    // Update the UI with the stream information
                    if (stream != null)
                    {
                        // Verify have the necessary information
                        if (string.IsNullOrEmpty(stream.IngestUrl) || string.IsNullOrEmpty(stream.StreamKey))
                        {
                            StatusMessage = "Error: Missing stream URL or key";
                            IsStartingStream = false;
                            UpdateStartStreamButtonText("Start Failed");
                            return;
                        }

                        // Show stream details
                        StatusMessage = "Stream created successfully!";

                        // Configure OBS with the streaming settings
                        StatusMessage = "Configuring OBS stream settings...";
                        UpdateStartStreamButtonText("Configuring OBS...");
                        await obsService.SetStreamSettingsAsync(stream.IngestUrl, stream.StreamKey);

                        // Start the MK.IO live event
                        StatusMessage = "Starting MK.IO live event...";
                        UpdateStartStreamButtonText("Starting MK.IO...");

                        // IMPORTANT: Make sure to start the MK.IO stream first
                        await streamingService.StartStreamingAsync(stream.Id);

                        // Show information to user while waiting for event to start
                        await Shell.Current.DisplayAlert(
                            "Live Event Created",
                            $"RTMP Stream has been created!\n\nServer: {stream.IngestUrl}\nStream Key: {stream.StreamKey}\n\nWaiting for the live event to start...",
                            "OK"
                        );

                        // Wait for the live event to become active
                        StatusMessage = "Waiting for live event to become active...";
                        UpdateStartStreamButtonText("Waiting for Live...");

                        bool isLiveEventRunning = false;
                        int retryCount = 0;
                        // Use longer polling intervals and more retries
                        // Check every 10 seconds for up to 2 minutes (12 retries)
                        const int checkIntervalMs = 10000; // 10 seconds
                        const int maxRetries = 12; // 2 minutes total

                        // Create progress indicator in status message
                        var progressTimer = new System.Timers.Timer(500); // Update every 500ms
                        int dots = 0;

                        progressTimer.Elapsed += (s, e) =>
                        {
                            dots = (dots + 1) % 4; // Cycle through 0-3 dots
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                StatusMessage = $"Waiting for live event to become active{new string('.', dots)}";
                                UpdateStartStreamButtonText($"Waiting{new string('.', dots)}");
                            });
                        };

                        progressTimer.Start();

                        try
                        {
                            while (!isLiveEventRunning && retryCount < maxRetries)
                            {
                                // Poll the live event status
                                try
                                {
                                    var updatedStream = await streamingService.GetStreamAsync(stream.Id);
                                    isLiveEventRunning = updatedStream.IsLive;

                                    if (isLiveEventRunning)
                                    {
                                        Debug.WriteLine("Live event is now active!");
                                        break;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"Live event check #{retryCount + 1}: Not active yet");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error checking live event status: {ex.Message}");
                                    // Continue trying despite error
                                }

                                // Wait 10 seconds before checking again
                                await Task.Delay(checkIntervalMs);
                                retryCount++;
                            }
                        }
                        finally
                        {
                            progressTimer.Stop();
                            progressTimer.Dispose();
                        }

                        if (!isLiveEventRunning)
                        {
                            StatusMessage = "Live event failed to start within timeout period";
                            IsStartingStream = false;
                            UpdateStartStreamButtonText("Start Failed");

                            // Roll back - stop the stream that couldn't fully start
                            try
                            {
                                await streamingService.StopStreamingAsync(stream.Id);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error stopping failed stream: {ex.Message}");
                            }

                            // Reset the streaming service again
                            await streamingService.ResetStatusAsync();

                            return;
                        }

                        // Live event is now running - need to verify the RTMP endpoint is ready
                        StatusMessage = "Live event is active! Validating RTMP endpoint...";
                        UpdateStartStreamButtonText("Validating RTMP...");

                        // Create new progress timer for RTMP validation
                        progressTimer = new System.Timers.Timer(500); // Update every 500ms
                        dots = 0;

                        progressTimer.Elapsed += (s, e) =>
                        {
                            dots = (dots + 1) % 4; // Cycle through 0-3 dots
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                StatusMessage = $"Validating RTMP endpoint{new string('.', dots)}";
                                UpdateStartStreamButtonText($"Validating{new string('.', dots)}");
                            });
                        };

                        progressTimer.Start();

                        try
                        {
                            // Try to validate the RTMP endpoint is ready
                            bool isRtmpEndpointReady = false;
                            retryCount = 0;
                            const int rtmpCheckMaxRetries = 10;
                            const int rtmpCheckIntervalMs = 5000; // 5 seconds

                            while (!isRtmpEndpointReady && retryCount < rtmpCheckMaxRetries)
                            {
                                try
                                {
                                    isRtmpEndpointReady = await IsRtmpEndpointReadyAsync(stream.IngestUrl);
                                    isRtmpEndpointReady = true;

                                    if (isRtmpEndpointReady)
                                    {
                                        Debug.WriteLine("RTMP endpoint is now ready!");
                                        break;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"RTMP endpoint check #{retryCount + 1}: Not ready yet");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error checking RTMP endpoint: {ex.Message}");
                                    // Continue trying despite error
                                }

                                retryCount++;
                            }

                            if (!isRtmpEndpointReady)
                            {
                                StatusMessage = "RTMP endpoint failed to become ready within timeout period";
                                IsStartingStream = false;
                                UpdateStartStreamButtonText("Start Failed");

                                // Don't roll back the stream, as the live event is running
                                // Just don't start OBS streaming
                                return;
                            }
                        }
                        finally
                        {
                            progressTimer.Stop();
                            progressTimer.Dispose();
                        }

                        // RTMP endpoint is ready - start OBS streaming
                        StatusMessage = "RTMP endpoint ready! Starting OBS streaming...";
                        UpdateStartStreamButtonText("Starting OBS...");

                        try
                        {
                            bool obsStartSuccess = await obsService.ToggleStreamingAsync();

                            if (!obsStartSuccess)
                            {
                                // OBS streaming failed to start
                                StatusMessage = "Error starting OBS stream - please start it manually";
                            }
                            else
                            {
                                StatusMessage = "OBS streaming started successfully";
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error starting OBS stream: {ex.Message}");
                            StatusMessage = $"Error starting OBS stream: {ex.Message}";
                            // Continue anyway - the MK.IO stream is running
                        }

                        // Create the BrainDataOBSHelper now device connected
                        try
                        {
                            // Create this using the service provider to ensure all dependencies are properly resolved
                            brainDataObsHelper = MauiProgram.Services.GetService<BrainDataOBSHelper>();

                            if (brainDataObsHelper != null)
                            {
                                // Subscribe to brain data events
                                brainDataObsHelper.BrainMetricsUpdated += OnBrainMetricsUpdated;
                                brainDataObsHelper.SignificantBrainEventDetected += OnSignificantBrainEventDetected;
                                brainDataObsHelper.ErrorOccurred += OnBrainDataError;

                                // Start brain data monitoring
                                await brainDataObsHelper.StartMonitoringAsync(true);
                                StatusMessage = "Brain data monitoring started";
                            }
                            else
                            {
                                StatusMessage = "Failed to initialize brain data helper";
                                // Continue anyway - this isn't critical to the stream
                            }
                        }
                        catch (Exception ex)
                        {
                            StatusMessage = $"Error initializing brain data: {ex.Message}";
                            // Continue anyway - this isn't critical to the stream
                        }

                        // Start the stream timer
                        streamStartTime = DateTime.Now;
                        streamTimer.Start();
                        UpdateStreamTimeDisplay();

                        // Set status
                        StatusMessage = "Stream started successfully";
                        IsLive = true;

                        // Show a success message
                        await Shell.Current.DisplayAlert(
                            "Stream Started",
                            "Your stream has been started successfully!",
                            "OK"
                        );
                    }
                    else
                    {
                        StatusMessage = "Failed to create stream";
                        UpdateStartStreamButtonText("Start Failed");
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error creating MK.IO stream: {ex.Message}";
                    UpdateStartStreamButtonText("Start Failed");

                    // Try to reset the streaming service
                    try
                    {
                        await streamingService.ResetStatusAsync();
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error starting stream: {ex.Message}";
                UpdateStartStreamButtonText("Start Failed");
            }
            finally
            {
                // Always reset the starting flag, but keep button text if there was an error
                IsStartingStream = false;

                // Schedule a reset of the button text after a delay if it shows an error
                if (StartStreamButtonText.Contains("Failed"))
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Task.Delay(3000);
                        UpdateStartStreamButtonText("Start Stream");
                    });
                }
            }
        }

        /// <summary>
        /// Ends the stream
        /// </summary>
        private async Task EndStreamAsync()
        {
            if (!IsLive)
                return;

            try
            {
                StatusMessage = "Ending stream...";

                // FIRST: Stop OBS streaming
                if (obsService.IsConnected)
                {
                    StatusMessage = "Stopping OBS streaming...";
                    try
                    {
                        await obsService.StopStreamingAsync();
                        StatusMessage = "OBS streaming stopped";
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error stopping OBS stream: {ex.Message}");
                        StatusMessage = $"Warning: OBS stream stop error: {ex.Message}";
                        // Continue with MK.IO shutdown even if OBS stop fails
                    }

                    // Small delay to ensure OBS has fully stopped
                    await Task.Delay(1000);
                }

                // SECOND: Get the current stream from the streaming service
                var currentStream = streamingService.CurrentStream;
                if (currentStream != null)
                {
                    // Stop the MK.IO stream
                    StatusMessage = "Stopping MK.IO stream...";
                    await streamingService.StopStreamingAsync(currentStream.Id);
                    StatusMessage = "MK.IO stream stopped";

                    // Ask if user wants to create a VOD
                    bool createVod = await Application.Current.MainPage.DisplayAlert(
                        "Create VOD",
                        "Would you like to create a Video On Demand (VOD) from this stream?",
                        "Yes", "No");

                    if (createVod)
                    {
                        try
                        {
                            StatusMessage = "Creating VOD...";
                            // Create a VOD from the stream
                            var vod = await streamingService.CreateVodFromStreamAsync(
                                currentStream.Id,
                                $"{StreamTitle} - {DateTime.Now:yyyy-MM-dd}");

                            if (vod != null)
                            {
                                await Application.Current.MainPage.DisplayAlert(
                                    "VOD Created",
                                    $"VOD created successfully with title: {vod.Title}",
                                    "OK");
                                StatusMessage = "VOD created successfully";
                            }
                        }
                        catch (Exception ex)
                        {
                            await Application.Current.MainPage.DisplayAlert(
                                "VOD Creation Failed",
                                $"Failed to create VOD: {ex.Message}",
                                "OK");
                            StatusMessage = $"VOD creation failed: {ex.Message}";
                        }
                    }
                }

                // Stop brain data monitoring
                if (brainDataObsHelper != null)
                {
                    StatusMessage = "Stopping brain data monitoring...";
                    brainDataObsHelper.BrainMetricsUpdated -= OnBrainMetricsUpdated;
                    brainDataObsHelper.SignificantBrainEventDetected -= OnSignificantBrainEventDetected;
                    brainDataObsHelper.ErrorOccurred -= OnBrainDataError;

                    await brainDataObsHelper.StopMonitoringAsync();
                    brainDataObsHelper = null;
                    StatusMessage = "Brain data monitoring stopped";
                }

                // Stop the stream timer
                streamTimer.Stop();

                // Set status
                StatusMessage = "Stream ended successfully";
                IsLive = false;

                // Reset brain event count
                BrainEventCount = 0;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error ending stream: {ex.Message}";
                Debug.WriteLine($"Error ending stream: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Diagnoses OBS connection issues
        /// </summary>
        private async Task DiagnoseOBSConnectionAsync()
        {
            try
            {
                StatusMessage = "Diagnosing OBS connection...";

                string websocketUrl = await Application.Current.MainPage.DisplayPromptAsync(
                    "OBS Connection Diagnostics",
                    "Enter WebSocket URL to test",
                    initialValue: "ws://localhost:4444",
                    maxLength: 100);

                if (string.IsNullOrEmpty(websocketUrl))
                {
                    StatusMessage = "Diagnostics canceled";
                    return;
                }

                string password = await Application.Current.MainPage.DisplayPromptAsync(
                    "OBS Connection Diagnostics",
                    "Enter password (leave empty if none)",
                    initialValue: "",
                    maxLength: 100);

                // Run the compatibility check
                var result = await NeuroSpectator.Utilities.OBSVersionChecker.CheckOBSCompatibilityAsync(websocketUrl, password);

                if (result.Success)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "OBS Connection Successful",
                        $"Successfully connected to OBS\n\n" +
                        $"OBS Studio Version: {result.OBSVersion}\n" +
                        $"WebSocket Version: {result.WebSocketVersion}\n\n" +
                        "Your OBS is properly configured for NeuroSpectator.",
                        "OK");

                    StatusMessage = "OBS diagnosed successfully";
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "OBS Connection Failed",
                        $"Could not connect to OBS\n\n" +
                        $"Error: {result.Message}\n\n" +
                        "Please check:\n" +
                        "1. OBS is running\n" +
                        "2. WebSocket Server is enabled in Tools → WebSocket Server Settings\n" +
                        "3. Port matches in URL (default: ws://localhost:4444 for OBS 27 and earlier, ws://localhost:4455 for OBS 28+)\n" +
                        "4. Password is correct (if authentication is enabled)",
                        "OK");

                    StatusMessage = "OBS diagnosis failed";
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"An error occurred during diagnostics: {ex.Message}",
                    "OK");

                StatusMessage = "OBS diagnostics error";
            }
        }

        /// <summary>
        /// Toggles the camera source visibility
        /// </summary>
        private async Task ToggleCameraAsync()
        {
            if (!obsService.IsConnected)
                return;

            try
            {
                // Toggle camera state
                CameraEnabled = !CameraEnabled;

                // Update OBS source visibility
                await obsService.SetSourceVisibilityAsync(ObsScene, "Camera", CameraEnabled);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling camera: {ex.Message}";
            }
        }

        /// <summary>
        /// Toggles the microphone mute state
        /// </summary>
        private async Task ToggleMicAsync()
        {
            if (!obsService.IsConnected)
                return;

            try
            {
                // Toggle mic state
                MicEnabled = !MicEnabled;

                // Find the mic source and toggle its mute state
                await obsService.SetSourceVisibilityAsync(ObsScene, "Microphone", MicEnabled);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling microphone: {ex.Message}";
            }
        }

        /// <summary>
        /// Toggles the brain data visualization visibility
        /// </summary>
        private async Task ToggleBrainDataAsync()
        {
            if (!obsService.IsConnected)
                return;

            try
            {
                // Toggle brain data visibility
                IsBrainDataVisible = !IsBrainDataVisible;

                // Update OBS source visibility
                await obsService.SetSourceVisibilityAsync(ObsScene,
                    obsService.BrainDataSourceName, IsBrainDataVisible);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling brain data: {ex.Message}";
            }
        }

        /// <summary>
        /// Opens the brain data configuration dialog
        /// </summary>
        private async Task ConfigureBrainDataAsync()
        {
            // This would typically open a dialog to configure brain data settings
            await Task.CompletedTask;
        }

        /// <summary>
        /// Marks a highlight in the stream
        /// </summary>
        private async Task MarkHighlightAsync()
        {
            if (!IsLive || !obsService.IsConnected || brainDataObsHelper == null)
                return;

            try
            {
                // Create a highlight event
                await brainDataObsHelper.HandleSignificantBrainEventAsync(
                    "UserHighlight",
                    $"User marked a highlight at {StreamTimeDisplay}");

                StatusMessage = "Highlight marked";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error marking highlight: {ex.Message}";
            }
        }

        /// <summary>
        /// Takes a screenshot of the stream
        /// </summary>
        private async Task TakeScreenshotAsync()
        {
            if (!obsService.IsConnected)
                return;

            try
            {
                StatusMessage = "Taking screenshot...";

                // Take a screenshot
                string path = await obsService.TakeScreenshotAsync(null);

                StatusMessage = $"Screenshot saved to {path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error taking screenshot: {ex.Message}";
            }
        }

        /// <summary>
        /// Sends a chat message
        /// </summary>
        private async Task SendChatMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(ChatMessageInput))
                return;

            try
            {
                // In a real implementation, this would send a message to the streaming platform
                StatusMessage = $"Message sent: {ChatMessageInput}";

                // Clear the input
                ChatMessageInput = "";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error sending message: {ex.Message}";
            }
        }

        /// <summary>
        /// Shares a brain event with viewers
        /// </summary>
        private async Task ShareBrainEventAsync()
        {
            if (!IsLive || brainDataObsHelper == null)
                return;

            try
            {
                // Create a custom brain event based on current metrics
                string eventDescription = "Check out my brain activity!";

                if (BrainMetrics.TryGetValue("Focus", out string focus))
                {
                    eventDescription = $"My focus level is {focus}";
                }

                // Create a brain event
                await brainDataObsHelper.HandleSignificantBrainEventAsync(
                    "UserShared", eventDescription);

                StatusMessage = "Brain event shared with viewers";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error sharing brain event: {ex.Message}";
            }
        }

        /// <summary>
        /// Shows a confirmation dialog when exiting
        /// </summary>
        private async Task ShowExitConfirmationAsync()
        {
            try
            {
                if (IsLive)
                {
                    bool confirm = await Shell.Current.DisplayAlert(
                        "End Stream?",
                        "You are currently streaming. Are you sure you want to exit and end the stream?",
                        "End Stream", "Cancel");

                    if (confirm)
                    {
                        await EndStreamAsync();

                        // Additional check for virtual camera
                        if (IsVirtualCameraActive)
                        {
                            bool stopCamera = await Shell.Current.DisplayAlert(
                                "Virtual Camera",
                                "The OBS Virtual Camera is still running. Do you want to stop it?",
                                "Stop Camera", "Leave Running");

                            if (stopCamera)
                            {
                                await obsService.StopVirtualCameraAsync();
                            }
                        }

                        await Shell.Current.GoToAsync("..");
                    }
                }
                else if (IsVirtualCameraActive)
                {
                    bool stopCamera = await Shell.Current.DisplayAlert(
                        "Virtual Camera",
                        "The OBS Virtual Camera is still running. Do you want to stop it before exiting?",
                        "Stop Camera", "Leave Running");

                    if (stopCamera)
                    {
                        await obsService.StopVirtualCameraAsync();
                    }

                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during exit: {ex.Message}";
            }
        }

        /// <summary>
        /// Auto-configures OBS for NeuroSpectator
        /// </summary>
        private async Task AutoConfigureOBSAsync()
        {
            if (!obsService.IsConnected)
            {
                StatusMessage = "Connect to OBS first";
                return;
            }

            try
            {
                IsAutoConfiguringObs = true;
                StatusMessage = "Auto-configuring OBS...";

                // Create an OBS setup guide manually if DI fails
                OBSSetupGuide setupGuide = MauiProgram.Services.GetService<OBSSetupGuide>();

                if (setupGuide == null)
                {
                    Debug.WriteLine("Creating OBSSetupGuide manually since DI failed");
                    setupGuide = new OBSSetupGuide(obsService, visualizationService);
                }

                Debug.WriteLine("Starting auto-configuration process");
                bool success = await setupGuide.AutoConfigureOBSForNeuroSpectatorAsync();

                if (success)
                {
                    StatusMessage = "OBS auto-configuration complete";
                    IsSetupComplete = true;

                    // Refresh OBS info
                    await RefreshObsInfoAsync();

                    // Update UI
                    OnPropertyChanged(nameof(IsSetupComplete));
                }
                else
                {
                    StatusMessage = "OBS auto-configuration failed";
                    Debug.WriteLine("Auto-configuration returned false");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error configuring OBS: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                StatusMessage = $"Error configuring OBS: {ex.Message}";
            }
            finally
            {
                IsAutoConfiguringObs = false;
            }
        }

        /// <summary>
        /// Shows the OBS setup guide
        /// </summary>
        private async Task ShowOBSSetupGuideAsync()
        {
            try
            {
                // Create an OBS setup guide
                var setupGuide = MauiProgram.Services.GetService<OBSSetupGuide>();

                // Get the manual setup guide
                string guide = setupGuide.GetManualSetupGuide();

                // Show a popup with the guide
                await Application.Current.MainPage.DisplayAlert(
                    "OBS Setup Guide",
                    guide,
                    "OK");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error showing setup guide: {ex.Message}";
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles OBS connection status changes
        /// </summary>
        private void OnObsConnectionStatusChanged(object sender, bool connected)
        {
            IsConnectedToObs = connected;

            if (connected)
            {
                StatusMessage = "Connected to OBS";
                // Refresh OBS info
                MainThread.BeginInvokeOnMainThread(async () => await RefreshObsInfoAsync());
            }
            else
            {
                StatusMessage = "Disconnected from OBS";
            }
        }

        /// <summary>
        /// Handles OBS streaming status changes
        /// </summary>
        private void OnObsStreamingStatusChanged(object sender, bool streaming)
        {
            IsLive = streaming;

            if (streaming)
            {
                streamStartTime = DateTime.Now;
                streamTimer.Start();
                UpdateStreamTimeDisplay();
                StatusMessage = "Stream started";
            }
            else
            {
                streamTimer.Stop();
                StatusMessage = "Stream ended";
            }
        }

        /// <summary>
        /// Handles OBS scene changes
        /// </summary>
        private void OnObsSceneChanged(object sender, string sceneName)
        {
            ObsScene = sceneName;
        }

        /// <summary>
        /// Handles brain metrics updates
        /// </summary>
        private void OnBrainMetricsUpdated(object sender, Dictionary<string, string> metrics)
        {
            BrainMetrics = new Dictionary<string, string>(metrics);
        }

        /// <summary>
        /// Handles significant brain events
        /// </summary>
        private void OnSignificantBrainEventDetected(object sender, string eventDescription)
        {
            BrainEventCount++;
            StatusMessage = $"Brain event: {eventDescription}";
        }

        /// <summary>
        /// Handles brain data errors
        /// </summary>
        private void OnBrainDataError(object sender, Exception ex)
        {
            StatusMessage = $"Brain data error: {ex.Message}";
        }

        /// <summary>
        /// Handles streaming service status changes
        /// </summary>
        private void OnStreamingStatusChanged(object sender, StreamingStatus status)
        {
            IsLive = status == StreamingStatus.Streaming;

            if (status == StreamingStatus.Error)
            {
                StatusMessage = "Streaming error";
            }
        }

        /// <summary>
        /// Handles streaming statistics updates
        /// </summary>
        private void OnStreamingStatsUpdated(object sender, StreamingStatistics stats)
        {
            ViewerCount = stats.ViewerCount;

            // Update stream health based on dropped frames and bit rate
            if (stats.DroppedFrames > 100 || stats.CurrentBitrate < 1000000)
            {
                StreamHealth = "Poor";
            }
            else if (stats.DroppedFrames > 10 || stats.CurrentBitrate < 3000000)
            {
                StreamHealth = "Fair";
            }
            else
            {
                StreamHealth = "Good";
            }
        }

        /// <summary>
        /// Updates the stream time display
        /// </summary>
        private void OnStreamTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            UpdateStreamTimeDisplay();
        }

        /// <summary>
        /// Updates the stream time display
        /// </summary>
        private void UpdateStreamTimeDisplay()
        {
            TimeSpan elapsed = DateTime.Now - streamStartTime;
            StreamTimeDisplay = $"{elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }

        /// <summary>
        /// Called when the StatusMessage property value changes
        /// </summary>
        partial void OnStatusMessageChanged(string value)
        {
            // When status message changes, optionally update button text too
            if (IsStartingStream)
            {
                // Extract the main part before any dots
                string basePart = value;
                if (basePart.Contains("..."))
                    basePart = basePart.Substring(0, basePart.IndexOf("..."));

                // Keep it short for the button
                if (basePart.Length > 15)
                    basePart = basePart.Substring(0, 15) + "...";

                UpdateStartStreamButtonText(basePart);
            }
        }
        #endregion
    }
}