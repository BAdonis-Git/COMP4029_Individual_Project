using NeuroSpectator.Services.BCI.Muse.Core;
using System.Collections.ObjectModel;

namespace NeuroSpectator.Services.BCI.Muse
{
    public class MuseService : IDisposable, IMuseListener, IMuseConnectionListener, IMuseDataListener
    {
        private readonly MuseManager museManager;
        private Core.Muse currentMuse;
        private readonly List<MuseDataPacketType> registeredDataTypes;
        private bool isDisposed;

        public ObservableCollection<MuseInfo> AvailableMuses { get; } = new ObservableCollection<MuseInfo>();

        // Events
        public event EventHandler<List<MuseInfo>> DeviceListChanged; // Renamed from MuseListChanged
        public event EventHandler<MuseConnectionPacket> ConnectionStateChanged;
        public event EventHandler<MuseDataPacket> DataReceived;
        public event EventHandler<MuseArtifactPacket> ArtifactReceived;
        public event EventHandler<string> ErrorOccurred;

        public bool IsConnected => currentMuse != null &&
                                   currentMuse.GetConnectionState() == ConnectionState.CONNECTED;

        public MuseService()
        {
            try
            {
                museManager = MuseManager.GetInstance();
                museManager.SetMuseListener(this);
                registeredDataTypes = new List<MuseDataPacketType>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing MuseService: {ex.Message}");
                ErrorOccurred?.Invoke(this, $"Failed to initialize Muse service: {ex.Message}");
            }
        }

        public async Task StartScanningAsync()
        {
            try
            {
                await Task.Run(() => museManager.StartListening());
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error scanning for devices: {ex.Message}");
            }
        }

        public async Task StopScanningAsync()
        {
            try
            {
                await Task.Run(() => museManager.StopListening());
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error stopping scan: {ex.Message}");
            }
        }

        public async Task ConnectAsync(MuseInfo museInfo)
        {
            try
            {
                if (currentMuse != null)
                {
                    await DisconnectAsync();
                }

                await Task.Run(() =>
                {
                    currentMuse = Core.Muse.GetInstance(museInfo);
                    currentMuse.RegisterConnectionListener(this);

                    // Register for all requested data types
                    foreach (var dataType in registeredDataTypes)
                    {
                        currentMuse.RegisterDataListener(this, dataType);
                    }

                    // Set preset for best data quality
                    currentMuse.SetPreset(MusePreset.PRESET_51);

                    // Start connection asynchronously
                    currentMuse.RunAsynchronously();
                });
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error connecting to device: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (currentMuse != null)
                {
                    await Task.Run(() =>
                    {
                        currentMuse.UnregisterAllListeners();
                        currentMuse.Disconnect();
                        currentMuse = null;
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error disconnecting: {ex.Message}");
            }
        }

        public void RegisterForDataType(MuseDataPacketType dataType)
        {
            if (!registeredDataTypes.Contains(dataType))
            {
                registeredDataTypes.Add(dataType);
                if (currentMuse != null)
                {
                    currentMuse.RegisterDataListener(this, dataType);
                }
            }
        }

        public void UnregisterForDataType(MuseDataPacketType dataType)
        {
            if (registeredDataTypes.Contains(dataType))
            {
                registeredDataTypes.Remove(dataType);
                if (currentMuse != null)
                {
                    currentMuse.UnregisterDataListener(this, dataType);
                }
            }
        }

        // IMuseListener implementation
        public void MuseListChanged()
        {
            try
            {
                var muses = museManager.GetMuses();

                // Dispatch UI updates to the main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AvailableMuses.Clear();
                    foreach (var muse in muses)
                    {
                        AvailableMuses.Add(muse);
                    }

                    // Raise event
                    DeviceListChanged?.Invoke(this, muses); // Changed from MuseListChanged
                });
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error processing muse list: {ex.Message}");
            }
        }

        // IMuseConnectionListener implementation
        public void ReceiveMuseConnectionPacket(MuseConnectionPacket packet, Core.Muse muse)
        {
            try
            {
                // Dispatch to main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ConnectionStateChanged?.Invoke(this, packet);
                });
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error processing connection packet: {ex.Message}");
            }
        }

        // IMuseDataListener implementation
        public void ReceiveMuseDataPacket(MuseDataPacket packet, Core.Muse muse)
        {
            try
            {
                // Dispatch to main thread for UI updates
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DataReceived?.Invoke(this, packet);
                });
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error processing data packet: {ex.Message}");
            }
        }

        public void ReceiveMuseArtifactPacket(MuseArtifactPacket packet, Core.Muse muse)
        {
            try
            {
                // Dispatch to main thread for UI updates
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ArtifactReceived?.Invoke(this, packet);
                });
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error processing artifact packet: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    DisconnectAsync().Wait();
                }

                isDisposed = true;
            }
        }
    }
}