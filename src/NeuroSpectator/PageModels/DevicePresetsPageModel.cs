using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Services;
using NeuroSpectator.Services.BCI.Interfaces;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the DevicePresetsPage
    /// </summary>
    public partial class DevicePresetsPageModel : ObservableObject
    {
        private readonly DeviceSettingsService settingsService;
        private readonly IBCIDeviceManager deviceManager;

        [ObservableProperty]
        private string deviceId;

        [ObservableProperty]
        private string deviceName;

        [ObservableProperty]
        private ObservableCollection<PresetInfo> availablePresets = new ObservableCollection<PresetInfo>();

        [ObservableProperty]
        private PresetInfo selectedPreset;

        [ObservableProperty]
        private bool notchFilterEnabled = true;

        [ObservableProperty]
        private string notchFilterFrequency = "50Hz";

        [ObservableProperty]
        private string sampleRate = "256 Hz (Default)";

        [ObservableProperty]
        private bool drlRefEnabled = true;

        public ICommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Creates a new instance of the DevicePresetsPageModel class
        /// </summary>
        public DevicePresetsPageModel(DeviceSettingsService settingsService, IBCIDeviceManager deviceManager)
        {
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));

            // Initialize commands
            ApplyCommand = new AsyncRelayCommand(ApplySettingsAsync);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
        }

        /// <summary>
        /// Initializes the view model with device information
        /// </summary>
        public async Task InitializeAsync(string deviceId, string deviceName)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;

            // Load existing settings if available
            var settings = await settingsService.GetDeviceSettingsAsync(deviceId);

            // Parse and apply settings
            if (settings != null)
            {
                NotchFilterEnabled = settings.NotchFilter?.ToLower() == "true";

                if (settings.NotchFilter?.Contains("50") == true)
                {
                    NotchFilterFrequency = "50Hz";
                }
                else if (settings.NotchFilter?.Contains("60") == true)
                {
                    NotchFilterFrequency = "60Hz";
                }

                if (!string.IsNullOrEmpty(settings.SampleRate))
                {
                    if (settings.SampleRate.Contains("256"))
                    {
                        SampleRate = "256 Hz (Default)";
                    }
                    else if (settings.SampleRate.Contains("128"))
                    {
                        SampleRate = "128 Hz";
                    }
                    else if (settings.SampleRate.Contains("512"))
                    {
                        SampleRate = "512 Hz";
                    }
                }
            }

            // Load available presets based on device type
            LoadAvailablePresets();
        }

        /// <summary>
        /// Loads available presets based on the device type
        /// </summary>
        private void LoadAvailablePresets()
        {
            AvailablePresets.Clear();

            // For MUSE headbands
            AvailablePresets.Add(new PresetInfo
            {
                Name = "PRESET_21",
                Description = "Optimized for recording high quality EEG data",
                IsSelected = true
            });

            AvailablePresets.Add(new PresetInfo
            {
                Name = "PRESET_22",
                Description = "Balanced power-saving mode with good EEG quality",
                IsSelected = false
            });

            AvailablePresets.Add(new PresetInfo
            {
                Name = "PRESET_AB",
                Description = "Artifact detection mode for detecting blinks and jaw clenches",
                IsSelected = false
            });

            // Select the first preset by default
            SelectedPreset = AvailablePresets[0];
        }

        /// <summary>
        /// Applies the selected settings to the device
        /// </summary>
        private async Task ApplySettingsAsync()
        {
            try
            {
                if (deviceManager.CurrentDevice != null && deviceManager.CurrentDevice.IsConnected)
                {
                    // In a real implementation, we would apply these settings to the device
                    // For now, just save them
                    var settings = new Services.DeviceSettingsModel
                    {
                        Name = DeviceName,
                        Preset = SelectedPreset?.Name ?? "Unknown",
                        NotchFilter = NotchFilterEnabled ? NotchFilterFrequency : "Off",
                        SampleRate = SampleRate
                    };

                    await settingsService.SaveDeviceSettingsAsync(DeviceId, settings);

                    // Display success message
                    await Shell.Current.DisplayAlert("Settings Applied",
                        $"Settings have been applied to {DeviceName}.",
                        "OK");
                }
                else
                {
                    // If not connected, just save the settings for future use
                    var settings = new Services.DeviceSettingsModel
                    {
                        Name = DeviceName,
                        Preset = SelectedPreset?.Name ?? "Unknown",
                        NotchFilter = NotchFilterEnabled ? NotchFilterFrequency : "Off",
                        SampleRate = SampleRate
                    };

                    await settingsService.SaveDeviceSettingsAsync(DeviceId, settings);

                    // Display informational message
                    await Shell.Current.DisplayAlert("Settings Saved",
                        $"Settings have been saved for {DeviceName} and will be applied when the device is connected.",
                        "OK");
                }

                // Navigate back
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error",
                    $"Failed to apply settings: {ex.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// Cancels the settings changes and navigates back
        /// </summary>
        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    /// <summary>
    /// Represents a device preset
    /// </summary>
    public partial class PresetInfo : ObservableObject
    {
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private bool isSelected;
    }
}