using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.BCI;
using NeuroSpectator.Services.Integration;
using NeuroSpectator.Services.Streaming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NeuroSpectator.PageModels
{
    public partial class StreamStreamerPageModel : ObservableObject
    {
        private readonly IBCIDeviceManager deviceManager;
        private readonly DeviceConnectionManager connectionManager;
        private readonly OBSIntegrationService obsService;
        private readonly IMKIOStreamingService streamingService;

        // This will be created on demand when stream starts
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
        private bool isDeviceConnected = false;

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

        // Timer for updating the stream time
        private System.Timers.Timer streamTimer;
        private DateTime streamStartTime;

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

        #endregion

        public StreamStreamerPageModel(
            IBCIDeviceManager deviceManager,
            DeviceConnectionManager connectionManager,
            OBSIntegrationService obsService,
            IMKIOStreamingService streamingService)
        {
            this.deviceManager = deviceManager;
            this.connectionManager = connectionManager;
            this.obsService = obsService;
            this.streamingService = streamingService;

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

            // Subscribe to events
            obsService.ConnectionStatusChanged += OnObsConnectionStatusChanged;
            obsService.StreamingStatusChanged += OnObsStreamingStatusChanged;
            obsService.SceneChanged += OnObsSceneChanged;

            connectionManager.ConnectionStatusChanged += OnDeviceConnectionStatusChanged;
            streamingService.StatusChanged += OnStreamingStatusChanged;
            streamingService.StatisticsUpdated += OnStreamingStatsUpdated;

            // Initialize brain metrics
            InitializeBrainMetrics();

            // Initialize stream timer
            streamTimer = new System.Timers.Timer(1000);
            streamTimer.Elapsed += OnStreamTimerElapsed;
        }

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
        /// Called when the page appears
        /// </summary>
        public async Task OnAppearingAsync()
        {
            // Check if OBS is connected
            IsConnectedToObs = obsService.IsConnected;

            if (IsConnectedToObs)
            {
                await RefreshObsInfoAsync();
            }

            // Check device connection status
            await RefreshDeviceConnectionStatusAsync();
        }

        /// <summary>
        /// Refreshes the device connection status
        /// </summary>
        private async Task RefreshDeviceConnectionStatusAsync()
        {
            // Check device connection status
            var statusInfo = await connectionManager.RefreshConnectionStatusAsync();
            IsDeviceConnected = statusInfo.IsConnected;

            if (IsDeviceConnected)
            {
                StatusMessage = $"Device '{statusInfo.DeviceName}' connected. Ready to stream.";
            }
            else
            {
                StatusMessage = "No BCI device connected. Connect a device in Your Devices.";
            }
        }

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
        /// Starts the stream
        /// </summary>
        private async Task StartStreamAsync()
        {
            if (IsLive)
                return;

            try
            {
                StatusMessage = "Starting stream...";

                // Check if OBS is connected
                if (!obsService.IsConnected)
                {
                    StatusMessage = "OBS is not connected";
                    return;
                }

                // Check if a BCI device is connected
                await RefreshDeviceConnectionStatusAsync();
                if (!IsDeviceConnected)
                {
                    StatusMessage = "No BCI device connected";
                    return;
                }

                // Create the BrainDataOBSHelper now that we have a connected device
                try
                {
                    // We'll create this using the service provider to ensure all dependencies are properly resolved
                    brainDataObsHelper = MauiProgram.Services.GetService<BrainDataOBSHelper>();

                    if (brainDataObsHelper != null)
                    {
                        // Subscribe to brain data events
                        brainDataObsHelper.BrainMetricsUpdated += OnBrainMetricsUpdated;
                        brainDataObsHelper.SignificantBrainEventDetected += OnSignificantBrainEventDetected;
                        brainDataObsHelper.ErrorOccurred += OnBrainDataError;

                        // Start brain data monitoring
                        await brainDataObsHelper.StartMonitoringAsync(true);
                    }
                    else
                    {
                        StatusMessage = "Failed to initialize brain data helper";
                        return;
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error initializing brain data: {ex.Message}";
                    return;
                }

                // Start streaming in OBS
                await obsService.StartStreamingAsync();

                // Start the stream timer
                streamStartTime = DateTime.Now;
                streamTimer.Start();
                UpdateStreamTimeDisplay();

                // Set status
                StatusMessage = "Stream started";
                IsLive = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error starting stream: {ex.Message}";
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

                // Stop streaming in OBS
                await obsService.StopStreamingAsync();

                // Stop brain data monitoring
                if (brainDataObsHelper != null)
                {
                    brainDataObsHelper.BrainMetricsUpdated -= OnBrainMetricsUpdated;
                    brainDataObsHelper.SignificantBrainEventDetected -= OnSignificantBrainEventDetected;
                    brainDataObsHelper.ErrorOccurred -= OnBrainDataError;

                    await brainDataObsHelper.StopMonitoringAsync();
                    brainDataObsHelper = null;
                }

                // Stop the stream timer
                streamTimer.Stop();

                // Set status
                StatusMessage = "Stream ended";
                IsLive = false;

                // Reset brain event count
                BrainEventCount = 0;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error ending stream: {ex.Message}";
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
            if (IsLive)
            {
                bool confirm = await Shell.Current.DisplayAlert(
                    "End Stream?",
                    "You are currently streaming. Are you sure you want to exit and end the stream?",
                    "End Stream", "Cancel");

                if (confirm)
                {
                    await EndStreamAsync();
                    await Shell.Current.GoToAsync("..");
                }
            }
            else
            {
                await Shell.Current.GoToAsync("..");
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

                // Create an OBS setup guide
                var setupGuide = MauiProgram.Services.GetService<OBSSetupGuide>();

                // Run the auto-configuration
                bool success = await setupGuide.AutoConfigureOBSForNeuroSpectatorAsync();

                if (success)
                {
                    StatusMessage = "OBS auto-configuration complete";
                    IsSetupComplete = true;

                    // Refresh OBS info
                    await RefreshObsInfoAsync();
                }
                else
                {
                    StatusMessage = "OBS auto-configuration failed";
                }
            }
            catch (Exception ex)
            {
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
        /// Handles device connection status changes
        /// </summary>
        private void OnDeviceConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            IsDeviceConnected = e.NewStatus == DeviceConnectionStatus.Connected;

            if (e.NewStatus == DeviceConnectionStatus.Connected)
            {
                StatusMessage = "Device connected";
            }
            else
            {
                StatusMessage = "Device disconnected";

                // If we're streaming and the device disconnects, we need to end the stream
                if (IsLive)
                {
                    MainThread.BeginInvokeOnMainThread(async () => await EndStreamAsync());
                }
            }
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
        #endregion
    }
}