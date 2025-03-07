using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services;
using NeuroSpectator.Services.BCI.Interfaces;
using System.Windows.Input;
using NeuroSpectator.Models.Stream;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the StreamStreamerPage
    /// </summary>
    public partial class StreamStreamerPageModel : ObservableObject, IDisposable
    {
        private readonly IBCIDeviceManager deviceManager;
        private IDispatcherTimer streamTimer;
        private IDispatcherTimer brainDataTimer;
        private DateTime streamStartTime;
        private bool _disposed = false;

        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        private string streamTitle = "CS:GO Tournament Finals";

        [ObservableProperty]
        private bool isLive = true;

        [ObservableProperty]
        private bool cameraEnabled = true;

        [ObservableProperty]
        private bool micEnabled = true;

        [ObservableProperty]
        private bool brainDataEnabled = true;

        [ObservableProperty]
        private string streamQuality = "1080p60fps";

        [ObservableProperty]
        private string streamTimeDisplay = "00:00:00";

        [ObservableProperty]
        private int viewerCount = 128;

        [ObservableProperty]
        private int brainEventCount = 47;

        [ObservableProperty]
        private string streamHealth = "Excellent";

        [ObservableProperty]
        private ObservableCollection<ChatMessage> chatMessages = new ObservableCollection<ChatMessage>();

        [ObservableProperty]
        private string chatMessageInput = "";

        [ObservableProperty]
        private string statusMessage = "Streaming successfully - All systems nominal";

        [ObservableProperty]
        private Dictionary<string, string> brainMetrics = new Dictionary<string, string>();

        // Commands
        public ICommand ToggleCameraCommand { get; }
        public ICommand ToggleMicCommand { get; }
        public ICommand ToggleBrainDataCommand { get; }
        public ICommand StreamQualityCommand { get; }
        public ICommand MoreOptionsCommand { get; }
        public ICommand ConfigureBrainDataCommand { get; }
        public ICommand SendChatMessageCommand { get; }
        public ICommand TakeScreenshotCommand { get; }
        public ICommand MarkHighlightCommand { get; }
        public ICommand ShareBrainEventCommand { get; }
        public ICommand EndStreamCommand { get; }

        /// <summary>
        /// Creates a new instance of the StreamStreamerPageModel class
        /// </summary>
        public StreamStreamerPageModel(IBCIDeviceManager deviceManager)
        {
            this.deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));

            // Initialize commands
            ToggleCameraCommand = new RelayCommand(ToggleCamera);
            ToggleMicCommand = new RelayCommand(ToggleMic);
            ToggleBrainDataCommand = new RelayCommand(ToggleBrainData);
            StreamQualityCommand = new AsyncRelayCommand(ShowStreamQualityOptionsAsync);
            MoreOptionsCommand = new AsyncRelayCommand(ShowMoreOptionsAsync);
            ConfigureBrainDataCommand = new AsyncRelayCommand(ConfigureBrainDataAsync);
            SendChatMessageCommand = new RelayCommand(SendChatMessage);
            TakeScreenshotCommand = new RelayCommand(TakeScreenshot);
            MarkHighlightCommand = new RelayCommand(MarkHighlight);
            ShareBrainEventCommand = new RelayCommand(ShareBrainEvent);
            EndStreamCommand = new AsyncRelayCommand(EndStreamAsync);

            // Initialize brain metrics
            BrainMetrics["Focus"] = "87%";
            BrainMetrics["Alpha Wave"] = "High";
            BrainMetrics["Beta Wave"] = "Medium";
            BrainMetrics["Theta Wave"] = "Low";
            BrainMetrics["Delta Wave"] = "Low";
            BrainMetrics["Gamma Wave"] = "Medium";

            // Load placeholder chat messages
            LoadPlaceholderChatMessages();
        }

        /// <summary>
        /// Loads placeholder chat messages for demonstration
        /// </summary>
        private void LoadPlaceholderChatMessages()
        {
            ChatMessages.Add(ChatMessage.CreateViewerMessage("User1", "Hello everyone!"));
            ChatMessages.Add(ChatMessage.CreateViewerMessage("User2", "Great stream today!"));
            ChatMessages.Add(ChatMessage.CreateViewerMessage("User3", "How's your focus level now?"));
            ChatMessages.Add(ChatMessage.CreateViewerMessage("User4", "Brain data looks interesting!"));
            ChatMessages.Add(ChatMessage.CreateViewerMessage("User5", "What sensitivity settings are you using?"));
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
                    // Start the stream timer
                    StartStreamTimer();

                    // Start brain data updates
                    StartBrainDataUpdates();

                    // Simulate stream starting
                    streamStartTime = DateTime.Now;
                    StatusMessage = "Stream started successfully - All systems nominal";

                    IsInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearingAsync: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Starts the stream timer
        /// </summary>
        private void StartStreamTimer()
        {
            try
            {
                streamTimer = Application.Current.Dispatcher.CreateTimer();
                streamTimer.Interval = TimeSpan.FromSeconds(1);
                streamTimer.Tick += (s, e) =>
                {
                    // Update stream time
                    var elapsed = DateTime.Now - streamStartTime;
                    StreamTimeDisplay = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

                    // Periodically update viewer count (for demonstration)
                    if (new Random().Next(0, 10) == 0)
                    {
                        ViewerCount += new Random().Next(-2, 5);
                        if (ViewerCount < 0) ViewerCount = 0;
                    }
                };
                streamTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting stream timer: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts brain data updates
        /// </summary>
        private void StartBrainDataUpdates()
        {
            try
            {
                brainDataTimer = Application.Current.Dispatcher.CreateTimer();
                brainDataTimer.Interval = TimeSpan.FromSeconds(5);
                brainDataTimer.Tick += (s, e) =>
                {
                    // Simulate brain data updates (for demonstration)
                    var random = new Random();
                    BrainMetrics["Focus"] = $"{random.Next(75, 95)}%";

                    string[] levels = { "Low", "Medium", "High" };
                    BrainMetrics["Alpha Wave"] = levels[random.Next(0, levels.Length)];
                    BrainMetrics["Beta Wave"] = levels[random.Next(0, levels.Length)];

                    // Occasionally add a brain event
                    if (random.Next(0, 5) == 0)
                    {
                        BrainEventCount++;
                        StatusMessage = "Brain event detected - Processing...";
                    }
                    else
                    {
                        StatusMessage = "Streaming successfully - All systems nominal";
                    }

                    // Update UI
                    OnPropertyChanged(nameof(BrainMetrics));
                };
                brainDataTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting brain data updates: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles the camera
        /// </summary>
        private void ToggleCamera()
        {
            CameraEnabled = !CameraEnabled;
            StatusMessage = CameraEnabled ? "Camera enabled" : "Camera disabled";
        }

        /// <summary>
        /// Toggles the microphone
        /// </summary>
        private void ToggleMic()
        {
            MicEnabled = !MicEnabled;
            StatusMessage = MicEnabled ? "Microphone enabled" : "Microphone disabled";
        }

        /// <summary>
        /// Toggles brain data display
        /// </summary>
        private void ToggleBrainData()
        {
            BrainDataEnabled = !BrainDataEnabled;
            StatusMessage = BrainDataEnabled ? "Brain data display enabled" : "Brain data display disabled";
        }

        /// <summary>
        /// Shows stream quality options
        /// </summary>
        private async Task ShowStreamQualityOptionsAsync()
        {
            var quality = await Shell.Current.DisplayActionSheet(
                "Stream Quality",
                "Cancel",
                null,
                "1080p60fps",
                "1080p30fps",
                "720p60fps",
                "720p30fps",
                "480p30fps");

            if (!string.IsNullOrEmpty(quality) && quality != "Cancel")
            {
                StreamQuality = quality;
                StatusMessage = $"Stream quality changed to {quality}";
            }
        }

        /// <summary>
        /// Shows more stream options
        /// </summary>
        private async Task ShowMoreOptionsAsync()
        {
            var option = await Shell.Current.DisplayActionSheet(
                "Stream Options",
                "Cancel",
                null,
                "Edit Stream Info",
                "Change Game",
                "Stream Settings",
                "Network Diagnostics",
                "Help");

            if (!string.IsNullOrEmpty(option) && option != "Cancel")
            {
                StatusMessage = $"Selected option: {option}";

                // Handle specific options
                if (option == "Edit Stream Info")
                {
                    await EditStreamInfoAsync();
                }
            }
        }

        /// <summary>
        /// Edits stream information
        /// </summary>
        private async Task EditStreamInfoAsync()
        {
            var newTitle = await Shell.Current.DisplayPromptAsync(
                "Edit Stream Title",
                "Enter a new stream title:",
                initialValue: StreamTitle);

            if (!string.IsNullOrEmpty(newTitle))
            {
                StreamTitle = newTitle;
                StatusMessage = "Stream title updated";
            }
        }

        /// <summary>
        /// Configures brain data display
        /// </summary>
        private async Task ConfigureBrainDataAsync()
        {
            var option = await Shell.Current.DisplayActionSheet(
                "Brain Data Configuration",
                "Cancel",
                null,
                "Basic Display",
                "Detailed Display",
                "Focus Mode",
                "Alpha/Beta Mode",
                "Custom Layout");

            if (!string.IsNullOrEmpty(option) && option != "Cancel")
            {
                StatusMessage = $"Brain data display set to {option}";
            }
        }

        /// <summary>
        /// Sends a chat message
        /// </summary>
        private void SendChatMessage()
        {
            if (string.IsNullOrWhiteSpace(ChatMessageInput))
                return;

            ChatMessages.Add(ChatMessage.CreateStreamerMessage("You (Streamer)", ChatMessageInput));

            ChatMessageInput = "";

            // Simulate a response (for demonstration)
            Task.Delay(2000).ContinueWith(_ =>
            {
                var responseUser = "User" + new Random().Next(1, 10);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ChatMessages.Add(ChatMessage.CreateViewerMessage(responseUser, "Thanks for the info!"));
                });
            });
        }

        /// <summary>
        /// Takes a screenshot of the current stream
        /// </summary>
        private void TakeScreenshot()
        {
            // In a real implementation, this would capture a screenshot
            StatusMessage = "Screenshot captured!";
        }

        /// <summary>
        /// Marks a stream highlight
        /// </summary>
        private void MarkHighlight()
        {
            // In a real implementation, this would mark a highlight in the stream
            StatusMessage = "Stream highlight marked! (Last 30 seconds)";
        }

        /// <summary>
        /// Shares a brain event with viewers
        /// </summary>
        private void ShareBrainEvent()
        {
            // In a real implementation, this would share a brain event with viewers
            StatusMessage = "Brain event shared with viewers!";

            // Add a chat message about the shared event
            ChatMessages.Add(ChatMessage.CreateSystemMessage("Streamer shared a brain event: Focus spike to 95%!"));
        }

        /// <summary>
        /// Ends the stream after confirmation
        /// </summary>
        private async Task EndStreamAsync()
        {
            var confirm = await Shell.Current.DisplayAlert(
                "End Stream",
                "Are you sure you want to end the current stream?",
                "End Stream",
                "Cancel");

            if (confirm)
            {
                // Stop timers
                streamTimer?.Stop();
                brainDataTimer?.Stop();

                // In a real implementation, this would end the stream
                StatusMessage = "Ending stream...";
                IsLive = false;

                // Show a summary
                await Shell.Current.DisplayAlert(
                    "Stream Summary",
                    $"Stream duration: {StreamTimeDisplay}\n" +
                    $"Peak viewers: {ViewerCount}\n" +
                    $"Brain events: {BrainEventCount}",
                    "OK");

                // Close the window
                await Shell.Current.Navigation.PopModalAsync();
            }
        }

        /// <summary>
        /// Confirms exit from the stream window
        /// </summary>
        public async void ConfirmExitAsync()
        {
            if (IsLive)
            {
                await EndStreamAsync();
            }
            else
            {
                await Shell.Current.Navigation.PopModalAsync();
            }
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
                    // Stop timers
                    streamTimer?.Stop();
                    brainDataTimer?.Stop();
                }

                _disposed = true;
            }
        }
    }
}