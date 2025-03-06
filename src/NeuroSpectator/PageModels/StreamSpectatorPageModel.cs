using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Models.Stream;
using NeuroSpectator.Services;
using NeuroSpectator.Services.BCI.Interfaces;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the StreamSpectatorPage
    /// </summary>
    public partial class StreamSpectatorPageModel : ObservableObject, IDisposable
    {
        private IDispatcherTimer streamTimer;
        private IDispatcherTimer brainDataTimer;
        private bool _disposed = false;

        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        private string streamerName = "ProGamer123";

        [ObservableProperty]
        private string streamTitle = "CS:GO Tournament Finals";

        [ObservableProperty]
        private string streamInfo = "CS:GO • Brain Integrated • 60fps";

        [ObservableProperty]
        private int viewerCount = 1258;

        [ObservableProperty]
        private string statusMessage = "Stream quality: Excellent";

        [ObservableProperty]
        private string selectedQuality = "1080p";

        [ObservableProperty]
        private ObservableCollection<string> qualityOptions = new ObservableCollection<string>
        {
            "1080p",
            "720p",
            "480p",
            "360p",
            "Auto"
        };

        [ObservableProperty]
        private ObservableCollection<ChatMessage> chatMessages = new ObservableCollection<ChatMessage>();

        [ObservableProperty]
        private string messageText = "";

        [ObservableProperty]
        private ObservableCollection<BrainMetric> brainMetrics = new ObservableCollection<BrainMetric>();

        [ObservableProperty]
        private ObservableCollection<BrainEvent> brainEvents = new ObservableCollection<BrainEvent>();

        [ObservableProperty]
        private string activeMetricView = "Focus";

        [ObservableProperty]
        private bool isPipModeEnabled = false;

        // Commands
        public ICommand ToggleBrainMetricCommand { get; }
        public ICommand TakeScreenshotCommand { get; }
        public ICommand TogglePiPModeCommand { get; }
        public ICommand SendChatMessageCommand { get; }
        public ICommand SubscribeCommand { get; }
        public ICommand ShowSettingsCommand { get; }
        public ICommand CloseStreamCommand { get; }

        /// <summary>
        /// Creates a new instance of the StreamSpectatorPageModel class
        /// </summary>
        public StreamSpectatorPageModel()
        {
            // Initialize commands
            ToggleBrainMetricCommand = new RelayCommand<string>(ToggleBrainMetric);
            TakeScreenshotCommand = new RelayCommand(TakeScreenshot);
            TogglePiPModeCommand = new RelayCommand(TogglePiPMode);
            SendChatMessageCommand = new RelayCommand(SendChatMessage);
            SubscribeCommand = new AsyncRelayCommand(SubscribeToStreamerAsync);
            ShowSettingsCommand = new AsyncRelayCommand(ShowSettingsAsync);
            CloseStreamCommand = new AsyncRelayCommand(CloseStreamAsync);

            // Load placeholder data for demonstration
            LoadPlaceholderData();
        }

        /// <summary>
        /// Loads placeholder data for demonstration
        /// </summary>
        private void LoadPlaceholderData()
        {
            // Chat messages
            ChatMessages.Add(ChatMessage.CreateViewerMessage("User1", "Hello everyone!"));
            ChatMessages.Add(ChatMessage.CreateViewerMessage("User2", "Great stream today!"));
            ChatMessages.Add(ChatMessage.CreateViewerMessage("User3", "How's your focus level now?"));
            ChatMessages.Add(ChatMessage.CreateViewerMessage("User4", "Brain data looks interesting!"));
            ChatMessages.Add(ChatMessage.CreateViewerMessage("User5", "What sensitivity settings are you using?"));
            ChatMessages.Add(ChatMessage.CreateStreamerMessage("Streamer", "Thanks for watching everyone!"));

            // Brain metrics
            BrainMetrics.Add(new BrainMetric { Name = "Focus Level", Value = "87%", Level = "High", Color = "#92D36E" });
            BrainMetrics.Add(new BrainMetric { Name = "Alpha Wave", Value = "64μV", Level = "Medium", Color = "#FFD740" });
            BrainMetrics.Add(new BrainMetric { Name = "Beta Wave", Value = "32μV", Level = "Medium", Color = "#FFD740" });
            BrainMetrics.Add(new BrainMetric { Name = "Theta Wave", Value = "28μV", Level = "Low", Color = "#AAAAAA" });
            BrainMetrics.Add(new BrainMetric { Name = "Delta Wave", Value = "42μV", Level = "Medium", Color = "#FFD740" });
            BrainMetrics.Add(new BrainMetric { Name = "Gamma Wave", Value = "18μV", Level = "Low", Color = "#AAAAAA" });

            // Brain events
            BrainEvents.Add(new BrainEvent { Timestamp = DateTime.Now.AddMinutes(-2).ToString("HH:mm:ss"), Description = "Focus spike detected (92%)" });
            BrainEvents.Add(new BrainEvent { Timestamp = DateTime.Now.AddMinutes(-4).ToString("HH:mm:ss"), Description = "Alpha wave peak" });
            BrainEvents.Add(new BrainEvent { Timestamp = DateTime.Now.AddMinutes(-6).ToString("HH:mm:ss"), Description = "Beta/Alpha ratio shift" });
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
                    // Start timers for simulating dynamic content
                    StartStreamTimer();
                    StartBrainDataTimer();

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
        /// Starts the stream timer for updating viewer count
        /// </summary>
        private void StartStreamTimer()
        {
            try
            {
                streamTimer = Application.Current.Dispatcher.CreateTimer();
                streamTimer.Interval = TimeSpan.FromSeconds(5);
                streamTimer.Tick += (s, e) =>
                {
                    // Simulate viewer count fluctuation
                    var random = new Random();
                    ViewerCount += random.Next(-5, 10);
                    if (ViewerCount < 0) ViewerCount = 0;

                    // Occasionally update status
                    if (random.Next(0, 5) == 0)
                    {
                        string[] statuses = {
                            "Stream quality: Excellent",
                            "Stream quality: Good",
                            "Network connection stable",
                            "Brain data connection stable",
                            "Stream buffering optimized"
                        };
                        StatusMessage = statuses[random.Next(0, statuses.Length)];
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
        /// Starts the brain data timer for updating metrics
        /// </summary>
        private void StartBrainDataTimer()
        {
            try
            {
                brainDataTimer = Application.Current.Dispatcher.CreateTimer();
                brainDataTimer.Interval = TimeSpan.FromSeconds(3);
                brainDataTimer.Tick += (s, e) =>
                {
                    // Simulate brain metric updates
                    var random = new Random();

                    // Update focus level
                    var focusMetric = BrainMetrics[0];
                    int focusValue = random.Next(75, 95);
                    focusMetric.Value = $"{focusValue}%";
                    focusMetric.Level = focusValue > 85 ? "High" : "Medium";
                    focusMetric.Color = focusValue > 85 ? "#92D36E" : "#FFD740";

                    // Update other metrics
                    for (int i = 1; i < BrainMetrics.Count; i++)
                    {
                        var metric = BrainMetrics[i];
                        int value = random.Next(15, 70);
                        metric.Value = $"{value}μV";

                        if (value > 60)
                        {
                            metric.Level = "High";
                            metric.Color = "#92D36E";
                        }
                        else if (value > 30)
                        {
                            metric.Level = "Medium";
                            metric.Color = "#FFD740";
                        }
                        else
                        {
                            metric.Level = "Low";
                            metric.Color = "#AAAAAA";
                        }
                    }

                    // Occasionally add a brain event
                    if (random.Next(0, 10) == 0)
                    {
                        string[] events = {
                            "Focus spike detected (90%)",
                            "Alpha wave peak",
                            "Beta/Alpha ratio shift",
                            "High cognitive activity detected",
                            "Meditation state detected"
                        };
                        BrainEvents.Insert(0, new BrainEvent
                        {
                            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                            Description = events[random.Next(0, events.Length)]
                        });

                        // Limit to 10 events
                        if (BrainEvents.Count > 10)
                        {
                            BrainEvents.RemoveAt(BrainEvents.Count - 1);
                        }
                    }

                    // Force UI update
                    OnPropertyChanged(nameof(BrainMetrics));
                    OnPropertyChanged(nameof(BrainEvents));
                };
                brainDataTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting brain data timer: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles which brain metric is displayed prominently
        /// </summary>
        private void ToggleBrainMetric(string metricType)
        {
            if (string.IsNullOrEmpty(metricType))
                return;

            ActiveMetricView = metricType;
            StatusMessage = $"Brain data view: {metricType}";
        }

        /// <summary>
        /// Takes a screenshot of the current stream view
        /// </summary>
        private void TakeScreenshot()
        {
            // In a real implementation, this would capture a screenshot
            StatusMessage = "Screenshot captured!";
        }

        /// <summary>
        /// Toggles picture-in-picture mode
        /// </summary>
        private void TogglePiPMode()
        {
            IsPipModeEnabled = !IsPipModeEnabled;
            StatusMessage = IsPipModeEnabled ? "Picture-in-Picture mode enabled" : "Picture-in-Picture mode disabled";
        }

        /// <summary>
        /// Sends a chat message
        /// </summary>
        private void SendChatMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText))
                return;

            ChatMessages.Add(ChatMessage.CreateViewerMessage("You", MessageText));

            MessageText = "";

            // Simulate a response (for demonstration)
            Task.Delay(3000).ContinueWith(_ =>
            {
                var random = new Random();
                if (random.Next(0, 3) == 0) // 1 in 3 chance of streamer response
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ChatMessages.Add(ChatMessage.CreateStreamerMessage("Streamer", "Thanks for the comment!"));
                    });
                }
            });
        }

        /// <summary>
        /// Subscribes to the streamer
        /// </summary>
        private async Task SubscribeToStreamerAsync()
        {
            var confirm = await Shell.Current.DisplayAlert(
                "Subscribe",
                $"Would you like to subscribe to {StreamerName}?",
                "Subscribe",
                "Cancel");

            if (confirm)
            {
                // In a real implementation, this would subscribe to the streamer
                StatusMessage = $"Subscribed to {StreamerName}!";

                // Add a system message to chat
                ChatMessages.Add(ChatMessage.CreateSystemMessage($"You are now subscribed to {StreamerName}!"));
            }
        }

        /// <summary>
        /// Shows stream settings options
        /// </summary>
        private async Task ShowSettingsAsync()
        {
            var option = await Shell.Current.DisplayActionSheet(
                "Stream Settings",
                "Cancel",
                null,
                "Video Quality",
                "Audio Settings",
                "Chat Settings",
                "Brain Data Display",
                "Notifications");

            if (!string.IsNullOrEmpty(option) && option != "Cancel")
            {
                StatusMessage = $"Opening settings: {option}";
            }
        }

        /// <summary>
        /// Closes the stream
        /// </summary>
        private async Task CloseStreamAsync()
        {
            // Stop timers
            streamTimer?.Stop();
            brainDataTimer?.Stop();

            // In a real implementation, this would clean up stream resources
            await Shell.Current.Navigation.PopAsync();
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

    /// <summary>
    /// Model for brain metrics
    /// </summary>
    public partial class BrainMetric : ObservableObject
    {
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string value;

        [ObservableProperty]
        private string level;

        [ObservableProperty]
        private string color;
    }

    /// <summary>
    /// Model for brain events
    /// </summary>
    public class BrainEvent
    {
        public string Timestamp { get; set; }
        public string Description { get; set; }
    }
}