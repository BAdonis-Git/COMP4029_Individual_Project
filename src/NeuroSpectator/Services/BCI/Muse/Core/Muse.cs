using NeuroSpectator.Services.BCI.Muse.Interop;
using System.Runtime.InteropServices;

// Define a minimal AOT namespace to support MonoPInvokeCallback
namespace AOT
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type t) { }
    }
}

namespace NeuroSpectator.Services.BCI.Muse.Core
{
    public class Muse
    {
        public string Name { get; set; }
        public string BluetoothMac { get; set; }
        public double RSSI { get; set; }

        private static readonly object listenerLock = new object();
        private static readonly ApiCallback onConnectionChanged = OnConnectionChanged;
        private static readonly DataCallback onArtifactReceived = OnArtifactReceived;
        private static readonly DataCallback onDataReceived = OnDataReceived;
        private static readonly ApiCallback onReceiveError = OnReceiveError;
        private readonly HashSet<IMuseDataListener> artifactListeners;
        private readonly HashSet<IMuseConnectionListener> connectionListeners;
        private readonly HashSet<IMuseErrorListener> errorListeners;
        private readonly Dictionary<MuseDataPacketType, HashSet<IMuseDataListener>> dataListeners;
        private static Muse instance = null;

        public static Muse GetInstance(MuseInfo info)
        {
            if (instance != null && instance.BluetoothMac != info.BluetoothMac)
            {
                instance.UnregisterAllListeners();
                instance = null;
            }

            if (instance == null)
            {
                instance = new Muse(info);
            }

            return instance;
        }

        private Muse(MuseInfo info)
        {
            Name = info.Name;
            BluetoothMac = info.BluetoothMac;
            RSSI = info.RSSI;
            this.connectionListeners = new HashSet<IMuseConnectionListener>();
            this.artifactListeners = new HashSet<IMuseDataListener>();
            this.errorListeners = new HashSet<IMuseErrorListener>();
            this.dataListeners = new Dictionary<MuseDataPacketType, HashSet<IMuseDataListener>>();
        }

        ~Muse()
        {
            if (this.connectionListeners.Count > 0 || this.dataListeners.Count > 0 || this.artifactListeners.Count > 0)
            {
                Console.WriteLine($"{Name} destroyed.");
                UnregisterAllListeners();
            }
        }

        [AOT.MonoPInvokeCallback(typeof(ApiCallback))]
        private static async void OnReceiveError(string json)
        {
            await Task.Run(() =>
            {
                var packet = MuseError.FromJson(json);
                if (packet.BluetoothMac == instance.BluetoothMac)
                {
                    lock (listenerLock)
                    {
                        foreach (var l in instance.errorListeners)
                        {
                            l.ReceiveError(packet, instance);
                        }
                    }
                }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(ApiCallback))]
        private static async void OnConnectionChanged(string json)
        {
            await Task.Run(() =>
            {
                var packet = MuseConnectionPacket.FromJson(json);
                if (packet.BluetoothMac == instance.BluetoothMac)
                {
                    lock (listenerLock)
                    {
                        foreach (var l in instance.connectionListeners)
                        {
                            l.ReceiveMuseConnectionPacket(packet, instance);
                        }
                    }
                }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(DataCallback))]
        private static async void OnDataReceived(MuseDataPacketType packetType, IntPtr valuesBuf, int numValues, long timestamp, string macAddress)
        {
            var valueArray = new double[numValues];
            Marshal.Copy(valuesBuf, valueArray, 0, numValues);
            await Task.Run(() =>
            {
                var packet = MuseDataPacket.FromNative(packetType, valueArray, timestamp, macAddress);
                lock (listenerLock)
                {
                    if (packet.BluetoothMac == instance.BluetoothMac && instance.dataListeners.ContainsKey(packet.PacketType))
                    {
                        foreach (var l in instance.dataListeners[packet.PacketType])
                        {
                            l.ReceiveMuseDataPacket(packet, instance);
                        }
                    }
                }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(DataCallback))]
        private static async void OnArtifactReceived(MuseDataPacketType packetType, IntPtr valuesBuf, int numValues, long timestamp, string macAddress)
        {
            var valueArray = new double[numValues];
            Marshal.Copy(valuesBuf, valueArray, 0, numValues);
            await Task.Run(() =>
            {
                var packet = MuseArtifactPacket.FromNative(valueArray, timestamp, macAddress);
                if (packet.BluetoothMac == instance.BluetoothMac)
                {
                    lock (listenerLock)
                    {
                        foreach (var l in instance.artifactListeners)
                        {
                            l.ReceiveMuseArtifactPacket(packet, instance);
                        }
                    }
                }
            });
        }

        public void RegisterConnectionListener(IMuseConnectionListener listener)
        {
            lock (listenerLock)
            {
                if (this.connectionListeners.Count == 0)
                {
                    Native.RegisterConnectionListener(BluetoothMac, onConnectionChanged);
                }
                this.connectionListeners.Add(listener);
            }
        }

        public void UnregisterConnectionListener(IMuseConnectionListener listener)
        {
            lock (listenerLock)
            {
                this.connectionListeners.Remove(listener);
                if (this.connectionListeners.Count == 0)
                {
                    Native.UnregisterConnectionListener(BluetoothMac, onConnectionChanged);
                }
            }
        }

        public void RegisterDataListener(IMuseDataListener listener, MuseDataPacketType type)
        {
            lock (listenerLock)
            {
                if (type == MuseDataPacketType.ARTIFACTS)
                {
                    if (this.artifactListeners.Count == 0)
                    {
                        Native.RegisterDataListener(BluetoothMac, onArtifactReceived, type);
                    }
                    this.artifactListeners.Add(listener);
                }
                else
                {
                    if (!this.dataListeners.ContainsKey(type))
                    {
                        this.dataListeners.Add(type, new HashSet<IMuseDataListener>());
                        Native.RegisterDataListener(BluetoothMac, onDataReceived, type);
                    }
                    this.dataListeners[type].Add(listener);
                }
            }
        }

        public void UnregisterDataListener(IMuseDataListener listener, MuseDataPacketType type)
        {
            lock (listenerLock)
            {
                if (type == MuseDataPacketType.ARTIFACTS)
                {
                    this.artifactListeners.Remove(listener);
                    if (this.artifactListeners.Count == 0)
                    {
                        Native.UnregisterDataListener(BluetoothMac, onArtifactReceived, type);
                    }
                }
                else if (this.dataListeners.ContainsKey(type))
                {
                    this.dataListeners[type].Remove(listener);
                    if (this.dataListeners[type].Count == 0)
                    {
                        Native.UnregisterDataListener(BluetoothMac, onDataReceived, type);
                        this.dataListeners.Remove(type);
                    }
                }
            }
        }

        public void RegisterErrorListener(IMuseErrorListener listener)
        {
            lock (listenerLock)
            {
                if (this.errorListeners.Count == 0)
                {
                    Native.RegisterErrorListener(BluetoothMac, onReceiveError);
                }
                this.errorListeners.Add(listener);
            }
        }

        public void UnregisterErrorListener(IMuseErrorListener listener)
        {
            lock (listenerLock)
            {
                this.errorListeners.Remove(listener);
                if (this.errorListeners.Count == 0)
                {
                    Native.UnregisterErrorListener(BluetoothMac, onReceiveError);
                }
            }
        }

        public void UnregisterAllListeners()
        {
            lock (listenerLock)
            {
                Native.UnregisterAllListeners(BluetoothMac);
                this.connectionListeners.Clear();
                this.artifactListeners.Clear();
                this.dataListeners.Clear();
            }
        }

        public void SetPreset(MusePreset preset)
        {
            Native.SetPreset(BluetoothMac, preset);
        }

        public void EnableDataTransmission(bool enable)
        {
            Native.EnableDataTransmission(BluetoothMac, enable);
        }

        public void Connect()
        {
            Native.Connect(BluetoothMac);
        }

        public void Disconnect()
        {
            Native.Disconnect(BluetoothMac);
        }

        public void RunAsynchronously()
        {
            Native.RunAsynchronously(BluetoothMac);
        }

        public MuseConfiguration GetMuseConfiguration()
        {
            var json = Native.GetMuseConfiguration(BluetoothMac);
            return MuseConfiguration.FromJson(json);
        }

        public MuseVersion GetMuseVersion()
        {
            var json = Native.GetMuseVersion(BluetoothMac);
            return MuseVersion.FromJson(json);
        }

        public ConnectionState GetConnectionState()
        {
            return Native.GetConnectionState(BluetoothMac);
        }

        public string GetMacAddress()
        {
            return BluetoothMac;
        }

        public string GetName()
        {
            return Name;
        }
    }
}