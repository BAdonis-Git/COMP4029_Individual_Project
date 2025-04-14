using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services.BCI;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Pages;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the YourNexusPage
    /// </summary>
    public partial class YourNexusPageModel : ObservableObject, IDisposable
    {
        private readonly IBCIDeviceManager deviceManager;
        private readonly DeviceConnectionManager connectionManager; // Added connection manager
        private bool _disposed = false;

        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotConnected))]
        [NotifyPropertyChangedFor(nameof(HasConnectedDevice))]
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

        // Added property to check connection status
        public bool IsNotConnected => !IsConnected;
        public bool HasConnectedDevice => IsConnected;

        // Commands
        public ICommand StartStreamCommand { get; }
        public ICommand ViewStreamCommand { get; }
        public ICommand ViewAnalyticsCommand { get; }
        public ICommand SelectTimeRangeCommand { get; }

        /// <summary>
        /// Creates a new instance of the YourNexusPageModel class
        /// </summary>
        public YourNexusPageModel(IBCIDeviceManager deviceManager, DeviceConnectionManager connectionManager = null)
        {
            this.deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            this.connectionManager = connectionManager; // Store connection manager

            // Initialize commands
            StartStreamCommand = new AsyncRelayCommand(StartStreamAsync);
            ViewStreamCommand = new AsyncRelayCommand<UserStreamModel>(ViewStreamAsync);
            ViewAnalyticsCommand = new AsyncRelayCommand<UserStreamModel>(ViewAnalyticsAsync);
            SelectTimeRangeCommand = new AsyncRelayCommand<string>(SelectTimeRangeAsync);

            // Check connection status from BOTH managers
            IsConnected = CheckDeviceConnected();

            // Subscribe to device manager events
            deviceManager.DeviceListChanged += OnDeviceListChanged;

            if (deviceManager.CurrentDevice != null)
            {
                deviceManager.CurrentDevice.ConnectionStateChanged += OnDeviceConnectionStateChanged;
            }

            // Subscribe to connection manager events if available
            if (connectionManager != null)
            {
                connectionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
                connectionManager.DeviceConnected += OnDeviceConnected;
                connectionManager.DeviceDisconnected += OnDeviceDisconnected;
            }

            // Load placeholder data for demonstration
            LoadPlaceholderData();
        }

        /// <summary>
        /// Check if a device is connected by checking both managers
        /// </summary>
        private bool CheckDeviceConnected()
        {
            bool deviceManagerConnected = deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected;

            // Also check connection manager if available
            if (connectionManager != null)
            {
                return deviceManagerConnected || connectionManager.IsDeviceConnected;
            }

            return deviceManagerConnected;
        }

        /// <summary>
        /// Refreshes the device connection status from both managers
        /// </summary>
        private async Task RefreshDeviceConnectionStatusAsync()
        {
            try
            {
                Debug.WriteLine("YourNexusPage: Refreshing device connection status");
                bool wasConnected = IsConnected;

                // Check device manager
                bool deviceManagerConnected = deviceManager.CurrentDevice != null &&
                                             deviceManager.CurrentDevice.IsConnected;

                // Check connection manager if available
                bool connectionManagerConnected = false;
                if (connectionManager != null)
                {
                    var statusInfo = await connectionManager.RefreshConnectionStatusAsync();
                    connectionManagerConnected = statusInfo.IsConnected;
                }

                // Update connection status
                IsConnected = deviceManagerConnected || connectionManagerConnected;

                Debug.WriteLine($"YourNexusPage: Device connection status: {IsConnected} (was {wasConnected})");

                // If connection status changed, update properties
                if (wasConnected != IsConnected)
                {
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(IsNotConnected));
                    OnPropertyChanged(nameof(HasConnectedDevice));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"YourNexusPage: Error refreshing device status: {ex.Message}");
            }
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
                    // Check device connection status
                    await RefreshDeviceConnectionStatusAsync();

                    IsInitialized = true;
                }
                else
                {
                    // Always refresh the connection status when the page appears
                    await RefreshDeviceConnectionStatusAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearingAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts a new stream by opening a new window with StreamStreamerPage
        /// </summary>
        private async Task StartStreamAsync()
        {
            try
            {
                // Refresh connection status first before proceeding
                await RefreshDeviceConnectionStatusAsync();

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

                // Make sure the connection manager knows about the device
                if (connectionManager != null && deviceManager.CurrentDevice != null &&
                    deviceManager.CurrentDevice.IsConnected)
                {
                    Debug.WriteLine("YourNexusPage: Ensuring device is registered with connection manager");
                    // Ensure the device is registered with the connection manager
                    connectionManager.RegisterDevice(deviceManager.CurrentDevice);
                }

                Debug.WriteLine("YourNexusPage: Opening StreamStreamerPage in new window");

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
        private async void OnDeviceConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            await MainThread.InvokeOnMainThreadAsync(async () => {
                IsConnected = e.NewState == ConnectionState.Connected;
                Debug.WriteLine($"YourNexusPage: Device connection state changed to {e.NewState}");

                // Update dependent properties
                OnPropertyChanged(nameof(IsNotConnected));
                OnPropertyChanged(nameof(HasConnectedDevice));

                // If newly connected and we have a connection manager, register the device
                if (IsConnected && connectionManager != null && sender is IBCIDevice device)
                {
                    connectionManager.RegisterDevice(device);
                    Debug.WriteLine($"YourNexusPage: Registered device with connection manager: {device.Name}");
                }
            });
        }

        /// <summary>
        /// Handles the DeviceListChanged event
        /// </summary>
        private void OnDeviceListChanged(object sender, System.Collections.Generic.List<IBCIDeviceInfo> devices)
        {
            Debug.WriteLine($"YourNexusPage: Device list changed - {devices.Count} devices available");
        }

        /// <summary>
        /// Handles the ConnectionStatusChanged event from the connection manager
        /// </summary>
        private async void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            Debug.WriteLine($"YourNexusPage: Connection status changed {e.OldStatus} -> {e.NewStatus}");

            // Refresh connection status on UI thread
            await MainThread.InvokeOnMainThreadAsync(async () => {
                await RefreshDeviceConnectionStatusAsync();
            });
        }

        /// <summary>
        /// Handles the DeviceConnected event from the connection manager
        /// </summary>
        private async void OnDeviceConnected(object sender, IBCIDevice device)
        {
            Debug.WriteLine($"YourNexusPage: Device connected event: {device.Name}");

            // Update connection status on UI thread
            await MainThread.InvokeOnMainThreadAsync(() => {
                IsConnected = true;
                OnPropertyChanged(nameof(IsNotConnected));
                OnPropertyChanged(nameof(HasConnectedDevice));
            });
        }

        /// <summary>
        /// Handles the DeviceDisconnected event from the connection manager
        /// </summary>
        private async void OnDeviceDisconnected(object sender, IBCIDevice device)
        {
            Debug.WriteLine($"YourNexusPage: Device disconnected event: {device.Name}");

            // Refresh connection status on UI thread
            await MainThread.InvokeOnMainThreadAsync(async () => {
                await RefreshDeviceConnectionStatusAsync();
            });
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
                    if (deviceManager != null)
                    {
                        deviceManager.DeviceListChanged -= OnDeviceListChanged;
                    }

                    if (deviceManager?.CurrentDevice != null)
                    {
                        deviceManager.CurrentDevice.ConnectionStateChanged -= OnDeviceConnectionStateChanged;
                    }

                    if (connectionManager != null)
                    {
                        connectionManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
                        connectionManager.DeviceConnected -= OnDeviceConnected;
                        connectionManager.DeviceDisconnected -= OnDeviceDisconnected;
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