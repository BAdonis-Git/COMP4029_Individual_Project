using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NeuroSpectator.Services.BCI.Muse.Core;

namespace NeuroSpectator.Services.BCI.Muse.Interop
{
    internal delegate void ApiCallback(string jsonArgs);
    internal delegate void DataCallback(MuseDataPacketType packetType, nint valuesBuf, int numValues, long timestamp, string macAddress);

    internal class Native
    {
        private const string LibmuseDll = "Libmuse";

        private const int StringBufferDefaultLength = 512;
        private const int ErrorBufferLength = 256;
        private static int stringBufferLength = StringBufferDefaultLength;
        private static nint stringBuffer;
        private static nint errorBuffer;
        private static readonly object bufferLock = new object();

        static Native()
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            stringBuffer = Marshal.AllocHGlobal(StringBufferDefaultLength);
            errorBuffer = Marshal.AllocHGlobal(ErrorBufferLength);
            Marshal.WriteByte(errorBuffer, 0);
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            if (stringBuffer != nint.Zero)
            {
                Marshal.FreeHGlobal(stringBuffer);
                stringBuffer = nint.Zero;
                stringBufferLength = 0;
            }

            if (errorBuffer != nint.Zero)
            {
                Marshal.FreeHGlobal(errorBuffer);
                errorBuffer = nint.Zero;
            }
        }

        // DLL Imports for muse manager
        [DllImport(LibmuseDll)]
        private static extern int IxApiVersion(nint version, int versionLen, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxLibmuseVersion(nint version, int versionLen, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxInitialize(nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxStartListening(nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxStopListening(nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxSetMuseListener(ApiCallback listener, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxSetLogListener(ApiCallback listener, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxRemoveFromListAfter(long time, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetMuses(nint jsonOut, int jsonLen, nint errorOut, int errorLen);

        // DLL Imports for muse device
        [DllImport(LibmuseDll)]
        private static extern int IxRegisterConnectionListener(string macAddress, ApiCallback listener, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxUnregisterConnectionListener(string macAddress, ApiCallback listener, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxRegisterDataListener(string macAddress, DataCallback listener, MuseDataPacketType type, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxUnregisterDataListener(string macAddress, DataCallback listener, MuseDataPacketType type, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxRegisterErrorListener(string macAddress, ApiCallback listener, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxUnregisterErrorListener(string macAddress, ApiCallback listener, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxUnregisterAllListeners(string macAddress, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxSetPreset(string macAddress, MusePreset preset, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxEnableDataTransmission(string macAddress, bool enable, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxConnect(string macAddress, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxDisconnect(string macAddress, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxRunAsynchronously(string macAddress, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetConnectionState(string macAddress, out ConnectionState connectionState, nint errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetMuseConfiguration(string macAddress, nint jsonOut, int jsonLen, nint errorOut, int errorLen);

        // Public API properties
        public static string ApiVersion
        {
            get
            {
                lock (bufferLock)
                {
                    if (IxApiVersion(stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0)
                    {
                        throw ApiError();
                    }
                    return Marshal.PtrToStringAnsi(stringBuffer);
                }
            }
        }

        public static string LibmuseVersion
        {
            get
            {
                lock (bufferLock)
                {
                    if (IxLibmuseVersion(stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0)
                    {
                        throw ApiError();
                    }
                    return Marshal.PtrToStringAnsi(stringBuffer);
                }
            }
        }

        // Public API methods
        public static void Initialize()
        {
            lock (bufferLock)
            {
                if (IxInitialize(errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void StartListening()
        {
            lock (bufferLock)
            {
                if (IxStartListening(errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void StopListening()
        {
            lock (bufferLock)
            {
                if (IxStopListening(errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void SetMuseListener(ApiCallback listener)
        {
            lock (bufferLock)
            {
                if (IxSetMuseListener(listener, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void SetLogListener(ApiCallback listener)
        {
            lock (bufferLock)
            {
                if (IxSetLogListener(listener, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void RemoveFromListAfter(long time)
        {
            lock (bufferLock)
            {
                if (IxRemoveFromListAfter(time, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static string GetMuses()
        {
            lock (bufferLock)
            {
                var err = IxGetMuses(stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength);
                if (err == 0)
                {
                    return Marshal.PtrToStringAnsi(stringBuffer);
                }
                else if (err > 0)
                {
                    // buffer too small, returned error is required size
                    Marshal.FreeHGlobal(stringBuffer);
                    stringBufferLength = (int)(err * 1.5);
                    stringBuffer = Marshal.AllocHGlobal(stringBufferLength);
                    return GetMuses();
                }
                throw ApiError();
            }
        }

        public static void RegisterConnectionListener(string macAddress, ApiCallback listener)
        {
            lock (bufferLock)
            {
                if (IxRegisterConnectionListener(macAddress, listener, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void UnregisterConnectionListener(string macAddress, ApiCallback listener)
        {
            lock (bufferLock)
            {
                if (IxUnregisterConnectionListener(macAddress, listener, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void RegisterDataListener(string macAddress, DataCallback listener, MuseDataPacketType type)
        {
            lock (bufferLock)
            {
                if (IxRegisterDataListener(macAddress, listener, type, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void UnregisterDataListener(string macAddress, DataCallback listener, MuseDataPacketType type)
        {
            lock (bufferLock)
            {
                if (IxUnregisterDataListener(macAddress, listener, type, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void RegisterErrorListener(string macAddress, ApiCallback listener)
        {
            lock (bufferLock)
            {
                if (IxRegisterErrorListener(macAddress, listener, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void UnregisterErrorListener(string macAddress, ApiCallback listener)
        {
            lock (bufferLock)
            {
                if (IxUnregisterErrorListener(macAddress, listener, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void UnregisterAllListeners(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxUnregisterAllListeners(macAddress, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void SetPreset(string macAddress, MusePreset preset)
        {
            lock (bufferLock)
            {
                if (IxSetPreset(macAddress, preset, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void EnableDataTransmission(string macAddress, bool enable)
        {
            lock (bufferLock)
            {
                if (IxEnableDataTransmission(macAddress, enable, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void Connect(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxConnect(macAddress, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void Disconnect(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxDisconnect(macAddress, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void RunAsynchronously(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxRunAsynchronously(macAddress, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static ConnectionState GetConnectionState(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxGetConnectionState(macAddress, out var state, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
                return state;
            }
        }

        public static string GetMuseConfiguration(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxGetMuseConfiguration(macAddress, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
                return Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        private static ApiException ApiError([CallerMemberName] string callingMethod = null)
        {
            var err = Marshal.PtrToStringAnsi(errorBuffer);
            string message;
            if (string.IsNullOrEmpty(err))
            {
                message = "An exception was raised from the native dll but no error message was found";
            }
            else
            {
                message = err;
            }
            Marshal.WriteByte(errorBuffer, 0);
            return new ApiException($"Error calling {callingMethod}. {message}");
        }
    }

    public class ApiException : Exception
    {
        public ApiException(string message) : base(message) { }
    }
}