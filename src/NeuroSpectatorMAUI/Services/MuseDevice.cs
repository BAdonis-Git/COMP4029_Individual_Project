using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NeuroSpectatorMAUI.Services
{
    // Define ConnectionState enum to match the C++ values
    public enum ConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        NeedsUpdate = 3,
        NeedsLicense = 4,
        Unknown = 5
    }

    // Define MuseDataPacketType enum to match the C++ values
    public enum MuseDataPacketType
    {
        Accelerometer = 0,
        Gyro = 1,
        Eeg = 2,
        Quantization = 3,
        Battery = 4,
        DrlRef = 5,
        Alpha_Absolute = 6,
        Beta_Absolute = 7,
        Delta_Absolute = 8,
        Theta_Absolute = 9,
        Gamma_Absolute = 10,
        Alpha_Relative = 11,
        Beta_Relative = 12,
        Delta_Relative = 13,
        Theta_Relative = 14,
        Gamma_Relative = 15,
        Alpha_Score = 16,
        Beta_Score = 17,
        Delta_Score = 18,
        Theta_Score = 19,
        Gamma_Score = 20,
        IsGood = 21,
        Hsi = 22,
        HsiPrecision = 23,
        Artifacts = 24
    }

    public class MuseDevice
    {
        // Events to notify subscribers of state changes
        public event EventHandler<ConnectionState> ConnectionStateChanged;
        public event EventHandler<(MuseDataPacketType packetType, double[] data)> DataReceived;

        // Device properties
        public string Name { get; }
        public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;
        public bool IsConnected => ConnectionState == ConnectionState.Connected;
        public bool IsInitialized { get; private set; }

        // Callbacks for native interface
        private MuseListChangedCallback _museListChangedCallback;
        private ConnectionStateChangedCallback _connectionStateChangedCallback;
        private DataReceivedCallback _dataReceivedCallback;

        // Constructor for discovered devices
        public MuseDevice(string name)
        {
            Name = name;
            InitializeCallbacks();
        }

        // Initialize the device manager and callbacks
        public bool Initialize()
        {
            try
            {
                Debug.WriteLine("Attempting to initialize Muse Manager...");

                // Initialize the native interface
                bool success = MuseNativeInterface.Initialize();
                if (!success)
                {
                    Debug.WriteLine("Failed to initialize Muse Manager");
                    return false;
                }

                // Register callbacks
                MuseNativeInterface.RegisterCallbacks(
                    _museListChangedCallback,
                    _connectionStateChangedCallback,
                    _dataReceivedCallback);

                IsInitialized = true;
                Debug.WriteLine("Muse Manager initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in Initialize: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        // Start scanning for devices
        public bool StartScanning()
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("Cannot start scanning: not initialized");
                return false;
            }

            try
            {
                return MuseNativeInterface.StartListening();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in StartScanning: {ex.Message}");
                return false;
            }
        }

        // Stop scanning for devices
        public void StopScanning()
        {
            try
            {
                MuseNativeInterface.StopListening();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in StopScanning: {ex.Message}");
            }
        }

        // Connect to this device
        public bool Connect()
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("Cannot connect: not initialized");
                return false;
            }

            try
            {
                return MuseNativeInterface.ConnectToMuse(Name);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in Connect: {ex.Message}");
                return false;
            }
        }

        // Disconnect from this device
        public void Disconnect()
        {
            try
            {
                MuseNativeInterface.DisconnectMuse();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in Disconnect: {ex.Message}");
            }
        }

        // Register for data of a specific type
        public bool RegisterForData(MuseDataPacketType packetType)
        {
            try
            {
                return MuseNativeInterface.RegisterDataListener((int)packetType);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in RegisterForData: {ex.Message}");
                return false;
            }
        }

        // Unregister for data of a specific type
        public bool UnregisterForData(MuseDataPacketType packetType)
        {
            try
            {
                return MuseNativeInterface.UnregisterDataListener((int)packetType);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in UnregisterForData: {ex.Message}");
                return false;
            }
        }

        // Clean up resources
        public void Cleanup()
        {
            try
            {
                Disconnect();
                MuseNativeInterface.Cleanup();
                IsInitialized = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in Cleanup: {ex.Message}");
            }
        }

        // Initialize callbacks for native interface
        private void InitializeCallbacks()
        {
            _museListChangedCallback = new MuseListChangedCallback(OnMuseListChanged);
            _connectionStateChangedCallback = new ConnectionStateChangedCallback(OnConnectionStateChanged);
            _dataReceivedCallback = new DataReceivedCallback(OnDataReceived);
        }

        // Callback when muse list changes
        private void OnMuseListChanged()
        {
            Debug.WriteLine("Muse list changed");
        }

        // Callback when connection state changes
        private void OnConnectionStateChanged(int state, string name)
        {
            if (name != Name) return;

            ConnectionState = (ConnectionState)state;
            Debug.WriteLine($"Connection state changed to {ConnectionState} for {name}");

            // Notify subscribers
            ConnectionStateChanged?.Invoke(this, ConnectionState);
        }

        // Callback when data is received
        private void OnDataReceived(int packetType, double[] data, int dataLength)
        {
            MuseDataPacketType type = (MuseDataPacketType)packetType;

            // Copy the data to a new array to avoid issues if the native code reuses the array
            double[] dataCopy = new double[dataLength];
            Array.Copy(data, dataCopy, dataLength);

            // Notify subscribers
            DataReceived?.Invoke(this, (type, dataCopy));
        }

        // Get all available Muse devices
        public static List<MuseDevice> GetAvailableDevices()
        {
            List<MuseDevice> devices = new List<MuseDevice>();

            try
            {
                // Ensure the native interface is initialized
                MuseNativeInterface.Initialize();

                // Get the number of available devices
                int count = MuseNativeInterface.GetMuseCount();
                Debug.WriteLine($"Found {count} Muse devices");

                // Get device names and create device objects
                for (int i = 0; i < count; i++)
                {
                    string name = MuseNativeInterface.GetMuseName(i);
                    if (!string.IsNullOrEmpty(name))
                    {
                        devices.Add(new MuseDevice(name));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetAvailableDevices: {ex.Message}");
            }

            return devices;
        }
    }
}