using NeuroSpectator.Models.BCI.Common;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace NeuroSpectator.Services
{
    /// <summary>
    /// Service for managing device settings persistence
    /// </summary>
    public class DeviceSettingsService
    {
        private readonly string StoredDevicesKey = "stored_devices";
        private readonly string DeviceSettingsKeyPrefix = "device_settings_";

        /// <summary>
        /// Gets all stored devices
        /// </summary>
        public async Task<ObservableCollection<StoredDeviceInfo>> GetStoredDevicesAsync()
        {
            try
            {
                var json = await SecureStorage.GetAsync(StoredDevicesKey);
                if (string.IsNullOrEmpty(json))
                {
                    return new ObservableCollection<StoredDeviceInfo>();
                }

                var devices = JsonSerializer.Deserialize<List<StoredDeviceInfo>>(json);
                return new ObservableCollection<StoredDeviceInfo>(devices);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting stored devices: {ex.Message}");
                return new ObservableCollection<StoredDeviceInfo>();
            }
        }

        /// <summary>
        /// Saves the stored devices list
        /// </summary>
        public async Task SaveStoredDevicesAsync(IEnumerable<StoredDeviceInfo> devices)
        {
            try
            {
                var json = JsonSerializer.Serialize(devices.ToList());
                await SecureStorage.SetAsync(StoredDevicesKey, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving stored devices: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets device settings for a specific device
        /// </summary>
        public async Task<DeviceSettingsModel> GetDeviceSettingsAsync(string deviceId)
        {
            try
            {
                var json = await SecureStorage.GetAsync($"{DeviceSettingsKeyPrefix}{deviceId}");
                if (string.IsNullOrEmpty(json))
                {
                    return new DeviceSettingsModel();
                }

                return JsonSerializer.Deserialize<DeviceSettingsModel>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting device settings: {ex.Message}");
                return new DeviceSettingsModel();
            }
        }

        /// <summary>
        /// Saves settings for a specific device
        /// </summary>
        public async Task SaveDeviceSettingsAsync(string deviceId, DeviceSettingsModel settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings);
                await SecureStorage.SetAsync($"{DeviceSettingsKeyPrefix}{deviceId}", json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving device settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds or updates a device in the stored devices list
        /// </summary>
        public async Task AddOrUpdateStoredDeviceAsync(StoredDeviceInfo deviceInfo)
        {
            try
            {
                var devices = await GetStoredDevicesAsync();
                var existingDevice = devices.FirstOrDefault(d => d.DeviceId == deviceInfo.DeviceId);

                if (existingDevice != null)
                {
                    // Update existing device
                    existingDevice.Name = deviceInfo.Name;
                    existingDevice.DeviceType = deviceInfo.DeviceType;
                    existingDevice.LastConnected = deviceInfo.LastConnected;
                }
                else
                {
                    // Add new device
                    devices.Add(deviceInfo);
                }

                await SaveStoredDevicesAsync(devices);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding/updating device: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a stored device
        /// </summary>
        public async Task DeleteStoredDeviceAsync(string deviceId)
        {
            try
            {
                var devices = await GetStoredDevicesAsync();
                var deviceToRemove = devices.FirstOrDefault(d => d.DeviceId == deviceId);

                if (deviceToRemove != null)
                {
                    devices.Remove(deviceToRemove);
                    await SaveStoredDevicesAsync(devices);

                    // Also remove settings
                    SecureStorage.Remove($"{DeviceSettingsKeyPrefix}{deviceId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting device: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Model for stored device information
    /// </summary>
    public class StoredDeviceInfo
    {
        public string Name { get; set; }
        public string DeviceId { get; set; }
        public BCIDeviceType DeviceType { get; set; }
        public DateTime LastConnected { get; set; }
    }

    /// <summary>
    /// Model for device settings
    /// </summary>
    public class DeviceSettingsModel
    {
        public string Name { get; set; } = "Unknown";
        public string Model { get; set; } = "Unknown";
        public string SerialNumber { get; set; } = "Unknown";
        public string Preset { get; set; } = "Unknown";
        public string NotchFilter { get; set; } = "Unknown";
        public string SampleRate { get; set; } = "Unknown";
        public string EegChannels { get; set; } = "Unknown";

        // Add preset-specific settings
        public Dictionary<string, object> PresetSettings { get; set; } = new Dictionary<string, object>();
    }
}