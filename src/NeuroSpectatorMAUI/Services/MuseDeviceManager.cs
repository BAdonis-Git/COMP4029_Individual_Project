using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using System.Runtime.InteropServices;

namespace NeuroSpectatorMAUI.Services
{
    public class MuseDeviceManager : IDisposable
    {
        #region Private Fields
        private readonly IntPtr _manager;
        private readonly List<MuseDevice> _devices;
        private MuseListenerCallback? _museListenerCallback;
        private ConnectionListenerCallback? _connectionListenerCallback;
        private readonly Dictionary<MuseDataPacketType, DataListenerCallback> _dataListenerCallbacks;
        private MuseDevice? _currentDevice;
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
        private bool _isScanning;
        private bool _isDisposed;
        #endregion

        #region Public Events
        public event EventHandler<MuseDevice>? DeviceDiscovered;
        public event EventHandler<MuseDevice>? DeviceStatusChanged;
        public event EventHandler<string>? ConnectionError;
        public event EventHandler<(MuseDevice device, double[] data)>? DataReceived;
        #endregion

        #region Public Properties
        public ObservableCollection<MuseDevice> AvailableDevices { get; }
        public bool IsScanning => _isScanning;
        public MuseDevice? CurrentDevice => _currentDevice;
        #endregion

        #region Constructor
        public MuseDeviceManager()
        {

            try {
                _devices = new List<MuseDevice>();
                _dataListenerCallbacks = new Dictionary<MuseDataPacketType, DataListenerCallback>();
                AvailableDevices = new ObservableCollection<MuseDevice>();

                _manager = MuseNativeInterface.GetMuseManager();
                if (_manager == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create Muse Manager");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in MuseDeviceManager constructor: {ex.Message}");
            }
            
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Public Methods

        #endregion

        #region Private Methods

        #endregion

        #region IDisposable Implementation

        #endregion
    }
}