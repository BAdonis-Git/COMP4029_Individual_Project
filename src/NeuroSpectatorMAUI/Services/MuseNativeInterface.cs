using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace NeuroSpectatorMAUI.Services
{
    // Delegate types for callbacks from native code
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void MuseListChangedCallback();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void ConnectionStateChangedCallback(int state, [MarshalAs(UnmanagedType.LPStr)] string name);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void DataReceivedCallback(int packetType, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] double[] data, int dataLength);

    internal static class MuseNativeInterface
    {
        private const string LibName = "MuseWrapper";
        private static IntPtr _libmuseHandle;
        private static IntPtr _museWrapperHandle;

        // Keep references to delegates to prevent garbage collection
        private static MuseListChangedCallback _museListChangedCallback;
        private static ConnectionStateChangedCallback _connectionStateChangedCallback;
        private static DataReceivedCallback _dataReceivedCallback;

        static MuseNativeInterface()
        {
            try
            {
                if (!PlatformHelpers.IsWindows)
                {
                    Debug.WriteLine($"Native Muse SDK only supported on Windows, current platform: {PlatformHelpers.GetPlatformName()}");
                    return;
                }

                // Determine the path to the native libraries
                string baseDir = AppContext.BaseDirectory;
                Debug.WriteLine($"Base Directory: {baseDir}");

                // Check Bluetooth status
                PlatformHelpers.CheckBluetoothStatus();

                // Check for required DLLs
                PlatformHelpers.CheckNativeLibraries();

                // Load libmuse.dll first since MuseWrapper depends on it
                string libmusePath = Path.Combine(baseDir, "libmuse.dll");
                Debug.WriteLine($"Loading libmuse.dll from: {libmusePath}");

                _libmuseHandle = NativeLibrary.Load(libmusePath);
                Debug.WriteLine($"Successfully loaded libmuse.dll with handle: {_libmuseHandle}");

                // Load MuseWrapper.dll
                string wrapperPath = Path.Combine(baseDir, "MuseWrapper.dll");
                Debug.WriteLine($"Loading MuseWrapper.dll from: {wrapperPath}");

                _museWrapperHandle = NativeLibrary.Load(wrapperPath);
                Debug.WriteLine($"Successfully loaded MuseWrapper.dll with handle: {_museWrapperHandle}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load libraries: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Manager functions
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetMuseManager();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool StartListening();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void StopListening();

        // Device enumeration
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetMuseCount();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public static extern string GetMuseName(int index);

        // Connection management
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ConnectToMuse([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DisconnectMuse();

        // Data registration
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool RegisterDataListener(int packetType);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool UnregisterDataListener(int packetType);

        // Callback registration
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetMuseListChangedCallback(MuseListChangedCallback callback);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetConnectionStateChangedCallback(ConnectionStateChangedCallback callback);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetDataReceivedCallback(DataReceivedCallback callback);

        // Cleanup
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void StopMuseManager();

        public static bool Initialize()
        {
            try
            {
                if (!PlatformHelpers.IsWindows)
                {
                    Debug.WriteLine("Cannot initialize Muse Manager: not on Windows platform");
                    return false;
                }

                Debug.WriteLine("Initializing MuseManager...");

                // Try initialization multiple ways
                Debug.WriteLine("Attempt 1: Standard initialization");
                IntPtr result = GetMuseManager();
                if (result != IntPtr.Zero)
                {
                    Debug.WriteLine($"MuseManager initialized successfully. Handle: {result}");
                    return true;
                }

                Debug.WriteLine("Standard initialization failed, trying alternative approach");

                // Try on a different thread
                Debug.WriteLine("Attempt 2: Thread-based initialization");
                bool threadSuccess = false;
                var thread = new Thread(() => {
                    try
                    {
                        var threadResult = GetMuseManager();
                        threadSuccess = (threadResult != IntPtr.Zero);
                        Debug.WriteLine($"Thread initialization result: {threadSuccess}, Handle: {threadResult}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Thread initialization exception: {ex.Message}");
                    }
                });

                thread.Start();
                thread.Join(5000); // Wait up to 5 seconds

                if (threadSuccess)
                {
                    Debug.WriteLine("Thread-based initialization succeeded");
                    return true;
                }

                Debug.WriteLine("All initialization attempts failed");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to initialize Muse Manager");
                Debug.WriteLine($"Exception: {ex.GetType().Name}");
                Debug.WriteLine($"Message: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        public static void RegisterCallbacks(
            MuseListChangedCallback museListChangedCallback,
            ConnectionStateChangedCallback connectionStateChangedCallback,
            DataReceivedCallback dataReceivedCallback)
        {
            if (!PlatformHelpers.IsWindows) return;

            // Store references to prevent delegates from being garbage collected
            _museListChangedCallback = museListChangedCallback;
            _connectionStateChangedCallback = connectionStateChangedCallback;
            _dataReceivedCallback = dataReceivedCallback;

            // Set callbacks in native code
            SetMuseListChangedCallback(_museListChangedCallback);
            SetConnectionStateChangedCallback(_connectionStateChangedCallback);
            SetDataReceivedCallback(_dataReceivedCallback);
        }

        public static void Cleanup()
        {
            if (!PlatformHelpers.IsWindows) return;

            try
            {
                StopMuseManager();

                // Clear delegate references
                _museListChangedCallback = null;
                _connectionStateChangedCallback = null;
                _dataReceivedCallback = null;

                if (_museWrapperHandle != IntPtr.Zero)
                {
                    NativeLibrary.Free(_museWrapperHandle);
                    _museWrapperHandle = IntPtr.Zero;
                }

                if (_libmuseHandle != IntPtr.Zero)
                {
                    NativeLibrary.Free(_libmuseHandle);
                    _libmuseHandle = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }
    }
}