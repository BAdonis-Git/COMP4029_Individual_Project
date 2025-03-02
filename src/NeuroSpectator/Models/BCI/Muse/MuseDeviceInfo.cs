using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services.BCI.Interfaces;
using Newtonsoft.Json;

namespace NeuroSpectator.Models.BCI.Muse
{
    /// <summary>
    /// Represents information about a discovered Muse headband
    /// </summary>
    public class MuseDeviceInfo : IBCIDeviceInfo
    {
        /// <summary>
        /// Gets the name of the Muse device
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets the Bluetooth MAC address of the Muse device
        /// </summary>
        [JsonProperty("bluetoothMac")]
        public string BluetoothMac { get; set; }

        /// <summary>
        /// Gets the RSSI (signal strength) of the Muse device
        /// </summary>
        [JsonProperty("rssi")]
        public double RSSI { get; set; }

        /// <summary>
        /// Gets the device ID (identical to the Bluetooth MAC address for Muse)
        /// </summary>
        public string DeviceId => BluetoothMac;

        /// <summary>
        /// Gets the signal strength (identical to RSSI for Muse)
        /// </summary>
        public double SignalStrength => RSSI;

        /// <summary>
        /// Gets the device type (always MuseHeadband for Muse devices)
        /// </summary>
        public BCIDeviceType DeviceType => BCIDeviceType.MuseHeadband;
    }
}