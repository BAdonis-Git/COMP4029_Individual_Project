using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the ModPage
    /// </summary>
    public partial class ModPageModel : ObservableObject
    {
        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        private string searchQuery;

        [ObservableProperty]
        private string selectedDeviceFilter;

        [ObservableProperty]
        private ObservableCollection<string> deviceFilters = new ObservableCollection<string>();

        [ObservableProperty]
        private ObservableCollection<ModInfo> availableMods = new ObservableCollection<ModInfo>();

        [ObservableProperty]
        private ObservableCollection<ModInfo> popularMods = new ObservableCollection<ModInfo>();

        [ObservableProperty]
        private ObservableCollection<ModDownloadInfo> userDownloadedMods = new ObservableCollection<ModDownloadInfo>();

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand DownloadModCommand { get; }
        public ICommand ViewModDetailsCommand { get; }

        /// <summary>
        /// Creates a new instance of the ModPageModel class
        /// </summary>
        public ModPageModel()
        {
            // Initialize commands
            SearchCommand = new AsyncRelayCommand(SearchModsAsync);
            DownloadModCommand = new AsyncRelayCommand<string>(DownloadModAsync);
            ViewModDetailsCommand = new AsyncRelayCommand<ModInfo>(ViewModDetailsAsync);

            // Initialize device filters
            DeviceFilters.Add("All Devices");
            DeviceFilters.Add("Muse Headband");
            DeviceFilters.Add("Mendi Headband");
            SelectedDeviceFilter = "All Devices";

            // Load placeholder data for demonstration
            LoadPlaceholderData();
        }

        /// <summary>
        /// Loads placeholder data for demonstration purposes
        /// </summary>
        private void LoadPlaceholderData()
        {
            // Popular mods
            var csgoMod = new ModInfo
            {
                Id = "CSGO-Mod",
                Name = "CS:GO Brain Integration",
                Version = "1.2.3",
                GameName = "Counter-Strike: Global Offensive",
                Description = "Integrates brain data visualization with CS:GO gameplay events",
                CompatibleDevices = new List<string> { "Muse Headband", "Mendi Headband" },
                DownloadCount = 5432,
                RatingAverage = 4.8,
                RatingCount = 245
            };
            PopularMods.Add(csgoMod);
            AvailableMods.Add(csgoMod);

            var valorantMod = new ModInfo
            {
                Id = "Valorant-Mod",
                Name = "Valorant Brain Metrics",
                Version = "2.0.1",
                GameName = "Valorant",
                Description = "Displays brain metrics during Valorant matches",
                CompatibleDevices = new List<string> { "Muse Headband" },
                DownloadCount = 3218,
                RatingAverage = 4.6,
                RatingCount = 178
            };
            PopularMods.Add(valorantMod);
            AvailableMods.Add(valorantMod);

            var lolMod = new ModInfo
            {
                Id = "LOL-Mod",
                Name = "LOL Brain Stats Overlay",
                Version = "1.5.0",
                GameName = "League of Legends",
                Description = "Shows brain activity statistics during League of Legends games",
                CompatibleDevices = new List<string> { "Muse Headband", "Mendi Headband" },
                DownloadCount = 2957,
                RatingAverage = 4.5,
                RatingCount = 162
            };
            PopularMods.Add(lolMod);
            AvailableMods.Add(lolMod);

            var minecraftMod = new ModInfo
            {
                Id = "Minecraft-Mod",
                Name = "Minecraft Focus Tracker",
                Version = "1.0.2",
                GameName = "Minecraft",
                Description = "Tracks and displays focus levels during Minecraft gameplay",
                CompatibleDevices = new List<string> { "Muse Headband" },
                DownloadCount = 1845,
                RatingAverage = 4.3,
                RatingCount = 98
            };
            PopularMods.Add(minecraftMod);
            AvailableMods.Add(minecraftMod);

            // Additional mods
            AvailableMods.Add(new ModInfo
            {
                Id = "Apex-Mod",
                Name = "Apex Legends Brain Stats",
                Version = "0.9.5 (Beta)",
                GameName = "Apex Legends",
                Description = "Beta version of brain statistics integration for Apex Legends",
                CompatibleDevices = new List<string> { "Muse Headband" },
                DownloadCount = 876,
                RatingAverage = 4.0,
                RatingCount = 42,
                IsBeta = true
            });

            AvailableMods.Add(new ModInfo
            {
                Id = "Fortnite-Mod",
                Name = "Fortnite Brain Monitor",
                Version = "1.1.0",
                GameName = "Fortnite",
                Description = "Monitors and displays brain activity during Fortnite battles",
                CompatibleDevices = new List<string> { "Muse Headband", "Mendi Headband" },
                DownloadCount = 1245,
                RatingAverage = 4.4,
                RatingCount = 87
            });

            AvailableMods.Add(new ModInfo
            {
                Id = "RocketLeague-Mod",
                Name = "Rocket League Stats",
                Version = "1.3.2",
                GameName = "Rocket League",
                Description = "Integrates brain data with Rocket League performance metrics",
                CompatibleDevices = new List<string> { "Muse Headband" },
                DownloadCount = 932,
                RatingAverage = 4.2,
                RatingCount = 56
            });

            AvailableMods.Add(new ModInfo
            {
                Id = "Dota2-Mod",
                Name = "Dota 2 Brain Interface",
                Version = "2.1.1",
                GameName = "Dota 2",
                Description = "Advanced brain data visualization for Dota 2 gameplay",
                CompatibleDevices = new List<string> { "Muse Headband" },
                DownloadCount = 1548,
                RatingAverage = 4.7,
                RatingCount = 123
            });

            // User's downloaded mods (for demonstration)
            UserDownloadedMods.Add(new ModDownloadInfo
            {
                ModId = "CSGO-Mod",
                DownloadDate = DateTime.Now.AddDays(-3),
                InstallStatus = ModInstallStatus.Installed,
                Version = "1.2.3"
            });

            UserDownloadedMods.Add(new ModDownloadInfo
            {
                ModId = "LOL-Mod",
                DownloadDate = DateTime.Now.AddDays(-7),
                InstallStatus = ModInstallStatus.Installed,
                Version = "1.5.0"
            });
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
                    // In a real implementation, this would fetch mods from a service
                    // For now, we're using placeholder data loaded in the constructor

                    IsInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearingAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Searches for mods based on the search query
        /// </summary>
        private async Task SearchModsAsync()
        {
            try
            {
                // In a real implementation, this would search for mods based on the search query
                // For now, just log the search query
                Debug.WriteLine($"Searching for mods with query: {SearchQuery}");

                // Here you would filter the AvailableMods collection based on the search query
                // For now, just display a message
                await Shell.Current.DisplayAlert("Search",
                    $"Searching for: {SearchQuery}",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching for mods: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads a mod
        /// </summary>
        private async Task DownloadModAsync(string modId)
        {
            if (string.IsNullOrEmpty(modId)) return;

            try
            {
                // Find the mod info
                var mod = AvailableMods.FirstOrDefault(m => m.Id == modId);
                if (mod == null) return;

                // Check if already downloaded
                var existingDownload = UserDownloadedMods.FirstOrDefault(d => d.ModId == modId);
                bool isUpdate = existingDownload != null;

                // In a real implementation, this would initiate a download process
                // For now, just show a placeholder alert
                string message = isUpdate
                    ? $"Updating {mod.Name} from v{existingDownload.Version} to v{mod.Version}"
                    : $"Downloading {mod.Name} v{mod.Version}";

                await Shell.Current.DisplayAlert("Download Mod", message, "OK");

                // Simulate download completion
                if (!isUpdate)
                {
                    UserDownloadedMods.Add(new ModDownloadInfo
                    {
                        ModId = mod.Id,
                        DownloadDate = DateTime.Now,
                        InstallStatus = ModInstallStatus.Downloaded,
                        Version = mod.Version
                    });
                }
                else
                {
                    existingDownload.Version = mod.Version;
                    existingDownload.DownloadDate = DateTime.Now;
                    existingDownload.InstallStatus = ModInstallStatus.Downloaded;
                }

                // Simulate installation
                await Task.Delay(500);
                await Shell.Current.DisplayAlert("Installation",
                    $"{mod.Name} v{mod.Version} has been installed successfully!",
                    "OK");

                // Update install status
                var downloadInfo = UserDownloadedMods.First(d => d.ModId == modId);
                downloadInfo.InstallStatus = ModInstallStatus.Installed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading mod: {ex.Message}");
                await Shell.Current.DisplayAlert("Download Error",
                    "An error occurred while trying to download the mod. Please try again.",
                    "OK");
            }
        }

        /// <summary>
        /// Views details for a mod
        /// </summary>
        private async Task ViewModDetailsAsync(ModInfo mod)
        {
            if (mod == null) return;

            // In a real implementation, this would navigate to a mod details page
            // For now, just show a placeholder alert
            await Shell.Current.DisplayAlert("Mod Details",
                $"Name: {mod.Name}\n" +
                $"Version: {mod.Version}\n" +
                $"Game: {mod.GameName}\n" +
                $"Description: {mod.Description}\n" +
                $"Compatible Devices: {string.Join(", ", mod.CompatibleDevices)}\n" +
                $"Rating: {mod.RatingAverage}/5 ({mod.RatingCount} ratings)\n" +
                $"Downloads: {mod.DownloadCount}",
                "OK");
        }
    }

    /// <summary>
    /// Model for mod information
    /// </summary>
    public class ModInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string GameName { get; set; }
        public string Description { get; set; }
        public List<string> CompatibleDevices { get; set; } = new List<string>();
        public int DownloadCount { get; set; }
        public double RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public bool IsBeta { get; set; }
    }

    /// <summary>
    /// Model for mod download information
    /// </summary>
    public class ModDownloadInfo
    {
        public string ModId { get; set; }
        public DateTime DownloadDate { get; set; }
        public ModInstallStatus InstallStatus { get; set; }
        public string Version { get; set; }
    }

    /// <summary>
    /// Enum for mod installation status
    /// </summary>
    public enum ModInstallStatus
    {
        Downloaded,
        Installing,
        Installed,
        Failed
    }
}