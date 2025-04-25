using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Models.Stream;
using NeuroSpectator.Services;
using NeuroSpectator.Services.BCI;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.Streaming;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the YourDashboardPage
    /// </summary>
    public partial class YourDashboardPageModel : ObservableObject, IDisposable
    {
        private readonly IMKIOStreamingService streamingService;
        private readonly IBCIDeviceManager deviceManager;
        private readonly DeviceConnectionManager connectionManager;
        private IDispatcherTimer batteryUpdateTimer;
        private IDispatcherTimer connectionCheckTimer;
        private bool _disposed = false;
        private const int BatteryUpdateIntervalMs = 30000; // 30 seconds
        private const int ConnectionCheckIntervalMs = 5000; // 5 seconds

        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotConnected))]
        [NotifyPropertyChangedFor(nameof(HasConnectedDevice))]
        private bool isConnected;

        [ObservableProperty]
        private double batteryPercent = 75; // Default placeholder value

        [ObservableProperty]
        private string deviceName = "Unknown Device";

        [ObservableProperty]
        private Services.DeviceSettingsModel currentDeviceSettings = new Services.DeviceSettingsModel();

        [ObservableProperty]
        private ObservableCollection<RecentActivityModel> recentActivities = new ObservableCollection<RecentActivityModel>();

        [ObservableProperty]
        private ObservableCollection<CategoryModel> topCategories = new ObservableCollection<CategoryModel>();

        [ObservableProperty]
        private string connectionStatusMessage = "No device connected";

        [ObservableProperty]
        private bool isCheckingConnection = false;

        [ObservableProperty]
        private ObservableCollection<StreamInfo> featuredStreams = new ObservableCollection<StreamInfo>();

        [ObservableProperty]
        private ObservableCollection<StreamInfo> recentVods = new ObservableCollection<StreamInfo>();

        [ObservableProperty]
        private bool isLoadingStreams = false;

        [ObservableProperty]
        private bool isRefreshingStreams = false;



        // Derived Properties
        public bool IsNotConnected => !IsConnected;
        public bool HasConnectedDevice => IsConnected && deviceManager?.CurrentDevice != null;
        public string BatteryPercentText => $"{BatteryPercent:F0}%";
        public bool BatteryPercentIsLargeArc => BatteryPercent > 50;

        // Commands
        public ICommand NavigateToDevicesCommand { get; }
        public ICommand ViewStreamCommand { get; }
        public ICommand ViewCategoryCommand { get; }
        public ICommand RefreshConnectionCommand { get; }
        public ICommand RefreshStreamsCommand { get; }

        /// <summary>
        /// Creates a new instance of the YourDashboardPageModel class
        /// </summary>
        public YourDashboardPageModel(IBCIDeviceManager deviceManager, DeviceConnectionManager connectionManager = null, IMKIOStreamingService streamingService = null)
        {
            this.deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            this.connectionManager = connectionManager;
            this.streamingService = streamingService;

            // Initialize commands
            NavigateToDevicesCommand = new AsyncRelayCommand(NavigateToDevicesAsync);
            ViewStreamCommand = new AsyncRelayCommand<StreamInfo>(ViewStreamAsync);
            ViewCategoryCommand = new AsyncRelayCommand<CategoryModel>(ViewCategoryAsync);
            RefreshConnectionCommand = new AsyncRelayCommand(RefreshConnectionStatusAsync);
            RefreshStreamsCommand = new AsyncRelayCommand(LoadMkioStreamsAsync);

            // Subscribe to device manager events
            deviceManager.DeviceListChanged += OnDeviceListChanged;

            // Subscribe to connection manager events if available
            if (connectionManager != null)
            {
                connectionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
                connectionManager.DeviceConnected += OnDeviceConnected;
                connectionManager.DeviceDisconnected += OnDeviceDisconnected;
            }

            // Check if a device is already connected
            IsConnected = deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected;

            // Subscribe to device manager events
            deviceManager.DeviceListChanged += OnDeviceListChanged;

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
            // Featured streams
            FeaturedStreams.Add(new StreamInfo
            {
                StreamerName = "ProGamer123",
                Title = "CS:GO Tournament Finals",
                IsLive = true,
                Game = "Counter-Strike: Global Offensive",
                ViewerCount = 1258,
                BrainMetrics = new Dictionary<string, string> { { "Focus", "87%" }, { "Alpha", "High" } }
            });

            FeaturedStreams.Add(new StreamInfo
            {
                StreamerName = "StrategyMaster",
                Title = "Starcraft 2 Ladder Grinding",
                IsLive = true,
                Game = "Starcraft 2",
                ViewerCount = 582,
                BrainMetrics = new Dictionary<string, string> { { "Focus", "92%" }, { "Alpha", "Medium" } }
            });

            FeaturedStreams.Add(new StreamInfo
            {
                StreamerName = "SpeedRunChamp",
                Title = "Elden Ring Speedrun Attempts",
                IsLive = true,
                Game = "Elden Ring",
                ViewerCount = 843,
                BrainMetrics = new Dictionary<string, string> { { "Focus", "76%" }, { "Alpha", "High" } }
            });

            // Recent activities
            RecentActivities.Add(new RecentActivityModel
            {
                StreamerName = "TacticalPlayer",
                Title = "Valorant Ranked Matches",
                IsLive = true,
                Game = "Valorant",
                TimeWatched = "Watching now",
                BrainMetrics = new Dictionary<string, string> { { "Focus", "81%" }, { "Beta", "Medium" } }
            });

            RecentActivities.Add(new RecentActivityModel
            {
                StreamerName = "RPGEnthusiast",
                Title = "Baldur's Gate 3 Playthrough",
                IsLive = false,
                Game = "Baldur's Gate 3",
                TimeWatched = "2 days ago",
                BrainMetrics = new Dictionary<string, string>()
            });

            RecentActivities.Add(new RecentActivityModel
            {
                StreamerName = "CasualGamer",
                Title = "Minecraft Building Stream",
                IsLive = false,
                Game = "Minecraft",
                TimeWatched = "3 days ago",
                BrainMetrics = new Dictionary<string, string>()
            });

            // Top categories
            TopCategories.Add(new CategoryModel { Name = "FPS Games", WatchTime = "32 Hours" });
            TopCategories.Add(new CategoryModel { Name = "Strategy", WatchTime = "18 Hours" });
            TopCategories.Add(new CategoryModel { Name = "eSports", WatchTime = "12 Hours" });
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
                    Debug.WriteLine("Dashboard: OnAppearingAsync - First initialization");

                    // Check device connection status
                    await RefreshConnectionStatusAsync();

                    // Start connection monitoring
                    StartConnectionMonitoring();

                    // Load MK.IO streams if streaming service is available
                    if (streamingService != null)
                    {
                        await LoadMkioStreamsAsync();
                    }
                    else
                    {
                        // Fall back to placeholder data if no streaming service
                        LoadPlaceholderData();
                    }

                    IsInitialized = true;
                }
                else
                {
                    // Refresh connection status for subsequent appearances
                    await RefreshConnectionStatusAsync();

                    // Refresh streams
                    if (streamingService != null)
                    {
                        await LoadMkioStreamsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearingAsync: {ex.Message}");
                ConnectionStatusMessage = "Error checking connection status";
            }
        }

        /// <summary>
        /// Starts periodic connection status monitoring
        /// </summary>
        private void StartConnectionMonitoring()
        {
            try
            {
                // Setup connection check timer
                if (connectionCheckTimer != null)
                {
                    connectionCheckTimer.Stop();
                }

                connectionCheckTimer = Application.Current.Dispatcher.CreateTimer();
                connectionCheckTimer.Interval = TimeSpan.FromMilliseconds(ConnectionCheckIntervalMs);
                connectionCheckTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        // Simple check that doesn't update UI directly
                        await QuickConnectionCheckAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in connection check timer: {ex.Message}");
                    }
                };
                connectionCheckTimer.Start();

                Debug.WriteLine("Dashboard: Started connection monitoring timer");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting connection monitoring: {ex.Message}");
            }
        }

        // Method to load MK.IO streams
        // Update this method to handle the refreshing state
        private async Task LoadMkioStreamsAsync()
        {
            try
            {
                IsRefreshingStreams = true;
                IsLoadingStreams = true;

                // Clear existing collections
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    FeaturedStreams.Clear();
                    RecentVods.Clear();
                });

                // Get live streams
                var liveStreams = await streamingService.GetAvailableStreamsAsync(true);

                // Update the featured streams collection on UI thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var stream in liveStreams.Take(5)) // Limit to 5 streams
                    {
                        FeaturedStreams.Add(stream);
                    }
                });

                // Get VODs
                var vods = await streamingService.GetAvailableVodsAsync();

                // Update the recent VODs collection on UI thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    foreach (var vod in vods.Take(5)) // Limit to 5 VODs
                    {
                        RecentVods.Add(vod);
                    }
                });

                // If no streams were found, load placeholder data only if requested
                if (FeaturedStreams.Count == 0 && RecentVods.Count == 0)
                {
                    // Optional: load placeholder data or show "No streams available" message
                    Debug.WriteLine("No streams or VODs found from MK.IO");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading MK.IO streams: {ex.Message}");
                // Show an error message to the user
                await Shell.Current.DisplayAlert("Error", $"Failed to load streams: {ex.Message}", "OK");
            }
            finally
            {
                IsLoadingStreams = false;
                IsRefreshingStreams = false;
            }
        }

        // Method to view a stream
        private async Task ViewStreamAsync(StreamInfo stream)
        {
            if (stream == null) return;

            try
            {
                Debug.WriteLine($"Opening stream: {stream.Id}, Title: {stream.Title}");

                // Better approach - use query parameters directly
                var navigationParameter = new Dictionary<string, object>
        {
            { "streamId", stream.Id }
        };

                // Navigate to the stream viewer page with the stream ID
                await Shell.Current.GoToAsync("StreamSpectatorPage", navigationParameter);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to stream: {ex.Message}");
                await Shell.Current.DisplayAlert("Error", $"Could not open stream: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Quick check of connection status without updating UI
        /// </summary>
        private async Task QuickConnectionCheckAsync()
        {
            // Avoid overlapping checks
            if (IsCheckingConnection)
                return;

            try
            {
                IsCheckingConnection = true;

                bool wasConnected = IsConnected;

                // Check device manager first
                bool deviceManagerConnected = deviceManager.CurrentDevice != null &&
                                             deviceManager.CurrentDevice.IsConnected;

                // Also check connection manager if available
                bool connectionManagerConnected = false;
                if (connectionManager != null)
                {
                    var statusInfo = await connectionManager.RefreshConnectionStatusAsync();
                    connectionManagerConnected = statusInfo.IsConnected;
                }

                // Determine overall connection status
                bool isNowConnected = deviceManagerConnected || connectionManagerConnected;

                // If connection status changed, do a full refresh
                if (wasConnected != isNowConnected)
                {
                    Debug.WriteLine($"Dashboard: Connection status changed: {wasConnected} -> {isNowConnected}");
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await RefreshConnectionStatusAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in QuickConnectionCheckAsync: {ex.Message}");
            }
            finally
            {
                IsCheckingConnection = false;
            }
        }

        /// <summary>
        /// Comprehensive refresh of connection status with UI updates
        /// </summary>
        public async Task RefreshConnectionStatusAsync()
        {
            // Avoid overlapping refreshes
            if (IsCheckingConnection)
                return;

            try
            {
                IsCheckingConnection = true;
                Debug.WriteLine("Dashboard: Refreshing connection status");

                // Check device manager first
                if (deviceManager.CurrentDevice != null)
                {
                    Debug.WriteLine($"Dashboard: Current device from device manager: {deviceManager.CurrentDevice.Name}");

                    // Update connection status
                    IsConnected = deviceManager.CurrentDevice.IsConnected;
                    DeviceName = deviceManager.CurrentDevice.Name ?? "Unknown Device";

                    if (IsConnected)
                    {
                        ConnectionStatusMessage = $"Connected to {DeviceName}";

                        // Load device settings and start battery monitoring
                        await LoadDeviceSettings();
                        StartBatteryMonitoring();

                        Debug.WriteLine($"Dashboard: Device is connected: {DeviceName}");
                    }
                    else
                    {
                        ConnectionStatusMessage = $"Device {DeviceName} is disconnected";
                        Debug.WriteLine($"Dashboard: Device is NOT connected: {DeviceName}");
                    }
                }
                else if (connectionManager != null)
                {
                    // Fall back to connection manager
                    var statusInfo = await connectionManager.RefreshConnectionStatusAsync();
                    IsConnected = statusInfo.IsConnected;

                    if (IsConnected)
                    {
                        DeviceName = statusInfo.DeviceName ?? "Unknown Device";
                        ConnectionStatusMessage = $"Connected to {DeviceName}";

                        // Try to find the device in the device manager
                        deviceManager.DeviceListChanged -= OnDeviceListChanged;
                        await deviceManager.StartScanningAsync();
                        await Task.Delay(1000); // Give it a moment to find devices
                        await deviceManager.StopScanningAsync();
                        deviceManager.DeviceListChanged += OnDeviceListChanged;
                    }
                    else
                    {
                        DeviceName = "No Device";
                        ConnectionStatusMessage = "No device connected";
                        BatteryPercent = 0;
                    }
                }
                else
                {
                    // No device manager or connection manager
                    IsConnected = false;
                    DeviceName = "No Device";
                    ConnectionStatusMessage = "Device service unavailable";
                    BatteryPercent = 0;
                }

                Debug.WriteLine($"Dashboard: Connection status refresh complete. Connected: {IsConnected}, Device: {DeviceName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing connection status: {ex.Message}");
                ConnectionStatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsCheckingConnection = false;
            }
        }

        /// <summary>
        /// Loads device settings from the connected device
        /// </summary>
        private async Task LoadDeviceSettings()
        {
            try
            {
                if (deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected)
                {
                    // Set default device name so UI doesn't show "Unknown"
                    CurrentDeviceSettings = new Services.DeviceSettingsModel
                    {
                        Name = deviceManager.CurrentDevice.Name ?? "Muse Headband",
                        Model = "Muse Headband",
                        Preset = "Default",
                        NotchFilter = "Default",
                        SampleRate = "Default",
                        EegChannels = "4"
                    };

                    // Try to get battery level
                    try
                    {
                        //Debug.WriteLine("Dashboard: Getting battery level");
                        BatteryPercent = await deviceManager.CurrentDevice.GetBatteryLevelAsync();
                        //Debug.WriteLine($"Dashboard: Battery level: {BatteryPercent}%");
                    }
                    catch (Exception ex)
                    {
                        //Debug.WriteLine($"Error getting battery level: {ex.Message}");
                        BatteryPercent = 75; // Use default value
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading device settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts periodic battery level monitoring
        /// </summary>
        private void StartBatteryMonitoring()
        {
            try
            {
                // Check if we have a current device first
                if (deviceManager.CurrentDevice == null)
                {
                    Debug.WriteLine("Dashboard: Cannot start battery monitoring - no current device");
                    return;
                }

                if (batteryUpdateTimer != null)
                {
                    batteryUpdateTimer.Stop();
                }

                // Only check every 30 seconds to reduce API calls
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
                        // Stop timer if it's causing continuous exceptions
                        batteryUpdateTimer?.Stop();
                        Debug.WriteLine($"Stopped battery timer due to errors: {ex.Message}");
                    }
                };
                batteryUpdateTimer.Start();
                Debug.WriteLine("Dashboard: Started battery monitoring timer");
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
                    Debug.WriteLine("Dashboard: Updating battery level");
                    try
                    {
                        var level = await deviceManager.CurrentDevice.GetBatteryLevelAsync();
                        Debug.WriteLine($"Dashboard: New battery level: {level}%");

                        // Update on UI thread
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            BatteryPercent = level;
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in GetBatteryLevelAsync: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating battery level: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigates to the devices page
        /// </summary>
        private async Task NavigateToDevicesAsync()
        {
            await Shell.Current.GoToAsync("//YourDevicesPage");
        }

        /// <summary>
        /// Views a stream
        /// </summary>
        private async Task ViewStreamAsync(FeaturedStreamModel stream)
        {
            if (stream == null) return;

            // In a real implementation, you would pass stream ID to the stream viewer page
            // For now, just show a placeholder alert
            await Shell.Current.DisplayAlert("Stream Selected",
                $"Opening stream: {stream.Title} by {stream.StreamerName}",
                "OK");
        }

        /// <summary>
        /// Views streams in a category
        /// </summary>
        private async Task ViewCategoryAsync(CategoryModel category)
        {
            if (category == null) return;

            // In a real implementation, you would navigate to Browse page with category filter
            // For now, just show a placeholder alert
            await Shell.Current.DisplayAlert("Category Selected",
                $"Viewing category: {category.Name}",
                "OK");
        }

        /// <summary>
        /// Handles the DeviceListChanged event
        /// </summary>
        private void OnDeviceListChanged(object sender, System.Collections.Generic.List<IBCIDeviceInfo> devices)
        {
            // This is more relevant for the YourDevicesPage, but we might want to update UI elements
            // if a new device is detected while on the dashboard
            Debug.WriteLine($"Dashboard: Device list changed - {devices.Count} devices available");
        }

        /// <summary>
        /// Handles the ConnectionStateChanged event
        /// </summary>
        private async void OnDeviceConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            // Update on UI thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    Debug.WriteLine($"Dashboard: Device connection state changed {e.OldState} -> {e.NewState}");
                    IsConnected = e.NewState == ConnectionState.Connected;

                    if (IsConnected)
                    {
                        if (sender is IBCIDevice device)
                        {
                            DeviceName = device.Name ?? "Unknown Device";
                            ConnectionStatusMessage = $"Connected to {DeviceName}";
                        }

                        // When connected, load device settings and start battery monitoring
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await LoadDeviceSettings();
                            StartBatteryMonitoring();
                        });
                    }
                    else
                    {
                        // Stop battery monitoring
                        batteryUpdateTimer?.Stop();
                        batteryUpdateTimer = null;

                        // Reset battery level
                        BatteryPercent = 0;
                        ConnectionStatusMessage = "Device disconnected";
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in OnDeviceConnectionStateChanged: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Handles the ConnectionStatusChanged event from the connection manager
        /// </summary>
        private async void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            Debug.WriteLine($"Dashboard: Connection status changed {e.OldStatus} -> {e.NewStatus}");

            // Refresh connection status on UI thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await RefreshConnectionStatusAsync();
            });
        }

        /// <summary>
        /// Handles the DeviceConnected event from the connection manager
        /// </summary>
        private async void OnDeviceConnected(object sender, IBCIDevice device)
        {
            Debug.WriteLine($"Dashboard: Device connected event: {device.Name}");

            // Refresh connection status on UI thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await RefreshConnectionStatusAsync();
            });
        }

        /// <summary>
        /// Handles the DeviceDisconnected event from the connection manager
        /// </summary>
        private async void OnDeviceDisconnected(object sender, IBCIDevice device)
        {
            Debug.WriteLine($"Dashboard: Device disconnected event: {device.Name}");

            // Refresh connection status on UI thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await RefreshConnectionStatusAsync();
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
                    // Dispose managed resources
                    if (batteryUpdateTimer != null)
                    {
                        batteryUpdateTimer.Stop();
                        batteryUpdateTimer = null;
                    }

                    if (connectionCheckTimer != null)
                    {
                        connectionCheckTimer.Stop();
                        connectionCheckTimer = null;
                    }

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
                    FeaturedStreams.Clear();
                    RecentVods.Clear();
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Model for featured stream information
    /// </summary>
    public class FeaturedStreamModel
    {
        public string StreamerName { get; set; }
        public string Title { get; set; }
        public bool IsLive { get; set; }
        public string Game { get; set; }
        public int ViewerCount { get; set; }
        public Dictionary<string, string> BrainMetrics { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Model for recent activity information
    /// </summary>
    public class RecentActivityModel
    {
        public string StreamerName { get; set; }
        public string Title { get; set; }
        public bool IsLive { get; set; }
        public string Game { get; set; }
        public string TimeWatched { get; set; }
        public Dictionary<string, string> BrainMetrics { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Model for category information
    /// </summary>
    public class CategoryModel
    {
        public string Name { get; set; }
        public string WatchTime { get; set; }
    }
}