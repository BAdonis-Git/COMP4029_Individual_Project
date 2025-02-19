using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpectatorMAUI.Services
{
    public class MuseDevice
    {
        private IntPtr _manager;

        public bool Initialise()
        {
            try
            {
                _manager = MuseNativeInterface.GetMuseManager();
                var success = _manager != IntPtr.Zero;
                if (success)
                    System.Diagnostics.Debug.WriteLine("Muse Manager initialized successfully");
                else System.Diagnostics.Debug.WriteLine("Failed to initialise Muse Manager");
                return success;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Exception in Initialise: {ex.Message}");
                return false;
            }
        }
        public bool IsInitialised => _manager != IntPtr.Zero;
    }
}
