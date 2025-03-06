using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services;
using NeuroSpectator.Services.BCI.Interfaces;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the YourDashboardPage
    /// </summary>
    public partial class YourDashboardPageModel : ObservableObject, IDisposable
    {
        private readonly IBCIDeviceManager deviceManager;
        private IDispatcherTimer batteryUpdateTimer;
        private bool _disposed = false;
        private const int BatteryUpdateIntervalMs = 30000; // 30 seconds

        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotConnected))]
        private bool isConnected;

        [ObservableProperty]
        private double batteryPercent = 75; // Default placeholder value

        [ObservableProperty]
        private Services.DeviceSettingsModel currentDeviceSettings = new Services.DeviceSettingsModel();

        [ObservableProperty]
        private ObservableCollection<FeaturedStreamModel> featuredStreams = new ObservableCollection<FeaturedStreamModel>();

        [ObservableProperty]
        private ObservableCollection<RecentActivityModel> recentActivities = new ObservableCollection<RecentActivityModel>();

        [ObservableProperty]
        private ObservableCollection<CategoryModel> topCategories = new ObservableCollection<CategoryModel>();

        // Derived Properties
        public bool IsNotConnected => !IsConnected;
        public string BatteryPercentText => $"{BatteryPercent:F0}%";
        public bool BatteryPercentIsLargeArc => BatteryPercent > 50;

        // Commands
        public ICommand NavigateToDevicesCommand { get; }
        public ICommand ViewStreamCommand { get; }
        public ICommand ViewCategoryCommand { get; }

        /// <summary>
        /// Creates a new instance of the YourDashboardPageModel class
        /// </summary>
        public YourDashboardPageModel(IBCIDeviceManager deviceManager)
        {
            this.deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));

            // Initialize commands
            NavigateToDevicesCommand = new AsyncRelayCommand(NavigateToDevicesAsync);
            ViewStreamCommand = new AsyncRelayCommand<FeaturedStreamModel>(ViewStreamAsync);
            ViewCategoryCommand = new AsyncRelayCommand<CategoryModel>(ViewCategoryAsync);

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
            FeaturedStreams.Add(new FeaturedStreamModel
            {
                StreamerName = "ProGamer123",
                Title = "CS:GO Tournament Finals",
                IsLive = true,
                Game = "Counter-Strike: Global Offensive",
                ViewerCount = 1258,
                BrainMetrics = new Dictionary<string, string> { { "Focus", "87%" }, { "Alpha", "High" } }
            });

            FeaturedStreams.Add(new FeaturedStreamModel
            {
                StreamerName = "StrategyMaster",
                Title = "Starcraft 2 Ladder Grinding",
                IsLive = true,
                Game = "Starcraft 2",
                ViewerCount = 582,
                BrainMetrics = new Dictionary<string, string> { { "Focus", "92%" }, { "Alpha", "Medium" } }
            });

            FeaturedStreams.Add(new FeaturedStreamModel
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
                    // Check if a device is connected
                    IsConnected = deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected;

                    if (IsConnected)
                    {
                        // Load device settings and start battery monitoring
                        await LoadDeviceSettings();
                        StartBatteryMonitoring();
                    }

                    IsInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearingAsync: {ex.Message}");
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
                        BatteryPercent = await deviceManager.CurrentDevice.GetBatteryLevelAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting battery level: {ex.Message}");
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
                    try
                    {
                        var level = await deviceManager.CurrentDevice.GetBatteryLevelAsync();

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
                    IsConnected = e.NewState == ConnectionState.Connected;

                    if (IsConnected)
                    {
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
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in OnDeviceConnectionStateChanged: {ex.Message}");
                }
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

                    // Unsubscribe from events
                    if (deviceManager != null)
                    {
                        deviceManager.DeviceListChanged -= OnDeviceListChanged;
                    }

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