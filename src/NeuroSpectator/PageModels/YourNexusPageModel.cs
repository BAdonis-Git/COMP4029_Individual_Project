using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Pages;
using NeuroSpectator.Services;
using NeuroSpectator.Services.BCI.Interfaces;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the YourNexusPage
    /// </summary>
    public partial class YourNexusPageModel : ObservableObject, IDisposable
    {
        private readonly IBCIDeviceManager deviceManager;
        private bool _disposed = false;

        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private ObservableCollection<UserStreamModel> userStreams = new ObservableCollection<UserStreamModel>();

        [ObservableProperty]
        private ObservableCollection<BrainMetricDataPoint> brainMetricData = new ObservableCollection<BrainMetricDataPoint>();

        [ObservableProperty]
        private string selectedTimeRange = "Today";

        [ObservableProperty]
        private string totalStreamTime = "28h";

        [ObservableProperty]
        private string totalViewers = "1.2k";

        [ObservableProperty]
        private string totalSubscribers = "85";

        [ObservableProperty]
        private string averageFocus = "82%";

        // Commands
        public ICommand StartStreamCommand { get; }
        public ICommand ViewStreamCommand { get; }
        public ICommand ViewAnalyticsCommand { get; }
        public ICommand SelectTimeRangeCommand { get; }

        /// <summary>
        /// Creates a new instance of the YourNexusPageModel class
        /// </summary>
        public YourNexusPageModel(IBCIDeviceManager deviceManager)
        {
            this.deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));

            // Initialize commands
            StartStreamCommand = new AsyncRelayCommand(StartStreamAsync);
            ViewStreamCommand = new AsyncRelayCommand<UserStreamModel>(ViewStreamAsync);
            ViewAnalyticsCommand = new AsyncRelayCommand<UserStreamModel>(ViewAnalyticsAsync);
            SelectTimeRangeCommand = new AsyncRelayCommand<string>(SelectTimeRangeAsync);

            // Check if a device is already connected
            IsConnected = deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected;

            // Subscribe to device manager events
            if (deviceManager.CurrentDevice != null)
            {
                deviceManager.CurrentDevice.ConnectionStateChanged += OnDeviceConnectionStateChanged;
            }

            // Load placeholder data for demonstration
            LoadPlaceholderData();
        }

        /// <summary>
        /// Loads placeholder data for demonstration purposes
        /// </summary>
        private void LoadPlaceholderData()
        {
            // User streams
            UserStreams.Add(new UserStreamModel
            {
                Title = "CS:GO Tournament Finals",
                StreamDate = "Mar 2, 2025",
                ViewCount = 1245,
                PeakFocus = 92,
                Game = "Counter-Strike: Global Offensive"
            });

            UserStreams.Add(new UserStreamModel
            {
                Title = "Elden Ring Playthrough - Part A",
                StreamDate = "Feb 28, 2025",
                ViewCount = 876,
                PeakFocus = 88,
                Game = "Elden Ring"
            });

            // Brain metric data (placeholder for chart)
            // In a real implementation, this would be actual data points for the chart
            for (int i = 0; i < 24; i++)
            {
                BrainMetricData.Add(new BrainMetricDataPoint
                {
                    TimePoint = $"{i}:00",
                    Alpha = Math.Round(30 + 20 * Math.Sin(i * 0.5), 1),
                    Beta = Math.Round(40 + 15 * Math.Cos(i * 0.3), 1),
                    Theta = Math.Round(20 + 10 * Math.Sin(i * 0.7), 1),
                    Delta = Math.Round(15 + 12 * Math.Cos(i * 0.4), 1),
                    Gamma = Math.Round(10 + 8 * Math.Sin(i * 0.8), 1)
                });
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
                    // Check if a device is connected
                    IsConnected = deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected;

                    IsInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearingAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts a new stream
        /// </summary>
        private async Task StartStreamAsync()
        {
            try
            {
                // Check if a device is connected
                if (!IsConnected)
                {
                    var result = await Shell.Current.DisplayAlert("No Device Connected",
                        "You need to connect a BCI device before starting a stream. Would you like to connect a device now?",
                        "Yes", "No");

                    if (result)
                    {
                        // Navigate to devices page if user chooses to connect a device
                        await Shell.Current.GoToAsync("//YourDevicesPage");
                    }
                    return;
                }

                // Create a new window for the streaming page
                var streamWindow = new Window
                {
                    Page = new StreamStreamerPage(MauiProgram.Services.GetService<StreamStreamerPageModel>()),
                    Title = "NeuroSpectator Stream"
                };

                // Add the window to the application
                Application.Current.OpenWindow(streamWindow);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting stream: {ex.Message}");
                await Shell.Current.DisplayAlert("Error",
                    "An error occurred while trying to start the stream. Please try again.",
                    "OK");
            }
        }

        /// <summary>
        /// Views a user's stream
        /// </summary>
        private async Task ViewStreamAsync(UserStreamModel stream)
        {
            if (stream == null) return;

            // In a real implementation, this would navigate to the stream viewer
            // For now, just show a placeholder alert
            await Shell.Current.DisplayAlert("View Stream",
                $"Viewing stream: {stream.Title}",
                "OK");
        }

        /// <summary>
        /// Views analytics for a stream
        /// </summary>
        private async Task ViewAnalyticsAsync(UserStreamModel stream)
        {
            if (stream == null) return;

            // In a real implementation, this would navigate to the analytics page
            // For now, just show a placeholder alert
            await Shell.Current.DisplayAlert("Stream Analytics",
                $"Viewing analytics for stream: {stream.Title}\n\n" +
                $"Views: {stream.ViewCount}\n" +
                $"Peak Focus: {stream.PeakFocus}%\n" +
                $"Stream Date: {stream.StreamDate}",
                "OK");
        }

        /// <summary>
        /// Selects a time range for the metrics chart
        /// </summary>
        private async Task SelectTimeRangeAsync(string timeRange)
        {
            if (string.IsNullOrEmpty(timeRange)) return;

            SelectedTimeRange = timeRange;

            // In a real implementation, this would update the brain metrics chart
            // For now, just update the property
            Debug.WriteLine($"Selected time range: {timeRange}");
        }

        /// <summary>
        /// Handles the ConnectionStateChanged event
        /// </summary>
        private void OnDeviceConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            IsConnected = e.NewState == ConnectionState.Connected;
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
                    // Unsubscribe from events
                    if (deviceManager?.CurrentDevice != null)
                    {
                        deviceManager.CurrentDevice.ConnectionStateChanged -= OnDeviceConnectionStateChanged;
                    }
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Model for user stream information
    /// </summary>
    public class UserStreamModel
    {
        public string Title { get; set; }
        public string StreamDate { get; set; }
        public int ViewCount { get; set; }
        public double PeakFocus { get; set; }
        public string Game { get; set; }
    }

    /// <summary>
    /// Model for brain metric data points (for charts)
    /// </summary>
    public class BrainMetricDataPoint
    {
        public string TimePoint { get; set; }
        public double Alpha { get; set; }
        public double Beta { get; set; }
        public double Theta { get; set; }
        public double Delta { get; set; }
        public double Gamma { get; set; }
    }
}