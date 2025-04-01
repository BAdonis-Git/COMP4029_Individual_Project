using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.Integration;
using NeuroSpectator.Services.Streaming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NeuroSpectator.PageModels
{
    public partial class StreamStreamerPageModel : ObservableObject
    {
        private readonly IBCIDeviceManager deviceManager;
        private readonly OBSIntegrationService obsService;
        private readonly BrainDataOBSHelper brainDataObsHelper;
        private readonly IMKIOStreamingService streamingService;

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

        #endregion

        public StreamStreamerPageModel(
            IBCIDeviceManager deviceManager,
            OBSIntegrationService obsService,
            BrainDataOBSHelper brainDataObsHelper,
            IMKIOStreamingService streamingService)
        {
            this.deviceManager = deviceManager;
            this.obsService = obsService;
            this.brainDataObsHelper = brainDataObsHelper;
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

            // Subscribe to events
            obsService.ConnectionStatusChanged += OnObsConnectionStatusChanged;
            obsService.StreamingStatusChanged += OnObsStreamingStatusChanged;
            obsService.SceneChanged += OnObsSceneChanged;

            brainDataObsHelper.BrainMetricsUpdated += OnBrainMetricsUpdated;
            brainDataObsHelper.SignificantBrainEventDetected += OnSignificantBrainEventDetected;
            brainDataObsHelper.ErrorOccurred += OnBrainDataError;

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

            // Check if a BCI device is connected
            if (deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected)
            {
                StatusMessage = "BCI device connected. Ready to stream.";
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

                await obsService.ConnectAsync();

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
                if (deviceManager.CurrentDevice == null || !deviceManager.CurrentDevice.IsConnected)
                {
                    StatusMessage = "No BCI device connected";
                    return;
                }

                // Start streaming in OBS
                await obsService.StartStreamingAsync();

                // Start brain data monitoring
                await brainDataObsHelper.StartMonitoringAsync(true);

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
                await brainDataObsHelper.StopMonitoringAsync();

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
            if (!IsLive || !obsService.IsConnected)
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
            if (!IsLive)
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
        #endregion
    }
}