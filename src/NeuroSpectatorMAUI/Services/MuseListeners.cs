using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace NeuroSpectatorMAUI.Services
{
    internal class MuseListenerCallback
    {
        private readonly Action _onMuseListChanged;
        private readonly GCHandle _handle;

        public MuseListenerCallback(Action onMuseListChanged)
        {
            _onMuseListChanged = onMuseListChanged;
            _handle = GCHandle.Alloc(this);
        }

        public void OnMuseListChanged()
        {
            _onMuseListChanged?.Invoke();
        }

        public void Free()
        {
            if (_handle.IsAllocated)
                _handle.Free();
        }
    }

    internal class ConnectionListenerCallback
    {
        private readonly Action<IntPtr, ConnectionState> _onConnectionStateChanged;
        private readonly GCHandle _handle;

        public ConnectionListenerCallback(Action<IntPtr, ConnectionState> onConnectionStateChanged)
        {
            _onConnectionStateChanged = onConnectionStateChanged;
            _handle = GCHandle.Alloc(this);
        }

        public void OnConnectionStateChanged(IntPtr muse, ConnectionState state)
        {
            _onConnectionStateChanged?.Invoke(muse, state);
        }

        public void Free()
        {
            if (_handle.IsAllocated)
                _handle.Free();
        }
    }

    internal class DataListenerCallback
    {
        private readonly Action<IntPtr, IntPtr> _onDataReceived;
        private readonly GCHandle _handle;

        public DataListenerCallback(Action<IntPtr, IntPtr> onDataReceived)
        {
            _onDataReceived = onDataReceived;
            _handle = GCHandle.Alloc(this);
        }

        public void OnDataReceived(IntPtr muse, IntPtr data)
        {
            _onDataReceived?.Invoke(muse, data);
        }

        public void Free()
        {
            if (_handle.IsAllocated)
                _handle.Free();
        }
    }
}
