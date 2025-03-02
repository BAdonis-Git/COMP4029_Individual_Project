using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NeuroSpectator.Services.BCI.Muse.Core;

namespace NeuroSpectator.Services.BCI.Muse.Interop
{
    
    internal delegate void ApiCallback(string jsonArgs);
    internal delegate void DataCallback(MuseDataPacketType packetType, nint valuesBuf, int numValues, long timestamp, string macAddress);

    internal partial class Native
    {
        private const string LibmuseDll = "Libmuse.dll";

        private const int StringBufferDefaultLength = 512;
        private const int ErrorBufferLength = 256;
        private static int stringBufferLength = StringBufferDefaultLength;
        private static IntPtr stringBuffer;
        private static IntPtr errorBuffer;
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
            if (stringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(stringBuffer);
                stringBuffer = IntPtr.Zero;
                stringBufferLength = 0;
            }

            if (errorBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(errorBuffer);
                errorBuffer = IntPtr.Zero;
            }
        }

        [DllImport(LibmuseDll)]
        private static extern int IxApiVersion(IntPtr version, int versionLen, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxLibmuseVersion(IntPtr version, int versionLen, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxInitialize(IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriteLog(Severity severity, [MarshalAs(UnmanagedType.Bool)] bool raw, string tag, string message, IntPtr errorOut, int errorLen);

        // muse manager
        [DllImport(LibmuseDll)]
        private static extern int IxSetRecorderInfo(string recorderName, string recorderVersion, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxStartListening(IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxStopListening(IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxSetMuseListener(ApiCallback listener, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxSetLogListener(ApiCallback listener, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxGetAdvertisingStats(string macAddress, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxResetAdvertisingStats(IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxRemoveFromListAfter(long time, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxGetMuses(IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

        // muse
        [DllImport(LibmuseDll)]
        private static extern int IxRegisterConnectionListener(string macAddress, ApiCallback listener, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxUnregisterConnectionListener(string macAddress, ApiCallback listener, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxRegisterDataListener(string macAddress, DataCallback listener, MuseDataPacketType type, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxUnregisterDataListener(string macAddress, DataCallback listener, MuseDataPacketType type, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxRegisterErrorListener(string macAddress, ApiCallback listener, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxUnregisterErrorListener(string macAddress, ApiCallback listener, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxUnregisterAllListeners(string macAddress, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxSetPreset(string macAddress, MusePreset preset, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxEnableDataTransmission(string macAddress, bool enable, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxSetNotchFrequency(string macAddress, NotchFrequency frequency, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxEnableException(string macAddress, bool enable, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxEnableLedIndicator(string macAddress, bool enable, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxConnect(string macAddress, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxDisconnect(string macAddress, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxExecute(string macAddress, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxRunAsynchronously(string macAddress, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxSetNumConnectTries(string macAddress, int numTries, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxGetMuseConfiguration(string macAddress, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxGetMuseVersion(string macAddress, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxGetConnectionState(string macAddress, out ConnectionState connectionState, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxGetRssi(string macAddress, out double rssi, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxGetLastDiscoveredTime(string macAddress, out double time, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxGetName(string macAddress, IntPtr nameOut, int nameLen, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxGetModel(string macAddress, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxIsPaired(string macAddress, out bool isPaired, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxIsLowEnergy(string macAddress, out bool isLowEnergy, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxIsConnectable(string macAddress, out bool isConnectable, IntPtr errorOut, int errorLen);
        [DllImport(LibmuseDll)]
        private static extern int IxSetLicenseData(string macAddress, IntPtr dataBlob, int blobLen, IntPtr errorOut, int errorLen);


        // file reader
        [DllImport(LibmuseDll)]
        private static extern int IxOpenFileReader(string filePath, out int handle, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxCloseFileReader(int handle, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderNextMessage(int handle, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderMessageType(int handle, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderMessageId(int handle, out int messageId, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderMessageTime(int handle, out long timestamp, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderAnnotation(int handle, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderMuseConfiguration(int handle, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderVersion(int handle, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderDeviceConfiguration(int handle, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderDspData(int handle, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderDataPacket(int handle, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetReaderArtifactPacket(int handle, IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

        // file writer
        [DllImport(LibmuseDll)]
        private static extern int IxOpenFileWriter(string filePath, out int handle, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxCloseFileWriter(int handle, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriterDiscardBuffer(int handle, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriterFlush(int handle, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetWriterBufferedMessagesCount(int handle, out int count, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetWriterBufferedMessagesSize(int handle, out int size, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetWriterBytesWritten(int handle, out long byteCount, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriterAddArtifactPacket(int handle, int id, [MarshalAs(UnmanagedType.Bool)] bool headbandOn, [MarshalAs(UnmanagedType.Bool)] bool blink, [MarshalAs(UnmanagedType.Bool)] bool jawClench, long timestamp, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriterAddDataPacket(int handle, int id, MuseDataPacketType type, long timestamp, double[] values, long valuesSize, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriterAddAnnotationString(int handle, int id, string annotation, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriterAddAnnotation(int handle, int id, string annotationData, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriterAddMuseConfiguration(int handle, int id, string configuration, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriterAddVersion(int handle, int id, string version, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriterAddDeviceConfiguration(int handle, int id, string configuration, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxWriterAddDspData(int handle, int id, string dspData, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxSetWriterTimestampMode(int handle, TimestampMode mode, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxSetWriterTimestamp(int handle, long timestamp, IntPtr errorOut, int errorLen);

        [DllImport(LibmuseDll)]
        private static extern int IxGetComputingDeviceConfiguration(IntPtr jsonOut, int jsonLen, IntPtr errorOut, int errorLen);

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

        public static void SetRecorderInfo(string recorderName, string recorderVersion)
        {
            lock (bufferLock)
            {
                if (IxSetRecorderInfo(recorderName, recorderVersion, errorBuffer, ErrorBufferLength) != 0)
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

        // muse
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

        public static void SetNotchFrequency(string macAddress, NotchFrequency frequency)
        {
            lock (bufferLock)
            {
                if (IxSetNotchFrequency(macAddress, frequency, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void EnableException(string macAddress, bool enable)
        {
            lock (bufferLock)
            {
                if (IxEnableException(macAddress, enable, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void EnableLedIndicator(string macAddress, bool enable)
        {
            lock (bufferLock)
            {
                if (IxEnableLedIndicator(macAddress, enable, errorBuffer, ErrorBufferLength) != 0)
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

        public static void Execute(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxExecute(macAddress, errorBuffer, ErrorBufferLength) != 0)
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

        public static void SetNumConnectTries(string macAddress, int numTries)
        {
            lock (bufferLock)
            {
                if (IxSetNumConnectTries(macAddress, numTries, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
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

        public static string GetMuseVersion(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxGetMuseVersion(macAddress, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
                return Marshal.PtrToStringAnsi(stringBuffer);
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

        public static double GetRssi(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxGetRssi(macAddress, out var rssi, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
                return rssi;
            }
        }

        public static double GetLastDiscoveredTime(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxGetLastDiscoveredTime(macAddress, out var time, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
                return time;
            }
        }

        public static string GetName(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxGetName(macAddress, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
                return Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        public static string GetModel(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxGetModel(macAddress, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
                return Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        public static bool IsPaired(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxIsPaired(macAddress, out var isPaired, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
                return isPaired;
            }
        }

        public static bool IsLowEnergy(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxIsPaired(macAddress, out var isLowEnergy, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
                return isLowEnergy;
            }
        }

        public static bool IsConnectable(string macAddress)
        {
            lock (bufferLock)
            {
                if (IxIsConnectable(macAddress, out var isConnectable, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
                return isConnectable;
            }
        }

        public static void SetLicenseData(string macAddress, byte[] dataBlob)
        {
            lock (bufferLock)
            {
                var ptr = Marshal.AllocHGlobal(dataBlob.Length);
                try
                {
                    Marshal.Copy(dataBlob, 0, ptr, dataBlob.Length);
                    if (IxSetLicenseData(macAddress, ptr, dataBlob.Length, errorBuffer, ErrorBufferLength) != 0)
                    {
                        throw ApiError();
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        // file reader
        public static int OpenFileReader(string filePath)
        {
            lock (bufferLock)
            {
                return IxOpenFileReader(filePath, out var handle, errorBuffer, ErrorBufferLength) != 0 ? throw ApiError() : handle;
            }
        }

        public static void CloseFileReader(int handle)
        {
            lock (bufferLock)
            {
                if (IxCloseFileReader(handle, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static string GetReaderNextMessage(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderNextMessage(handle, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0
                    ? throw ApiError()
                    : Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        public static string GetReaderMessageType(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderMessageType(handle, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0
                    ? throw ApiError()
                    : Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        public static int GetReaderMessageId(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderMessageId(handle, out var messageId, errorBuffer, ErrorBufferLength) != 0 ? throw ApiError() : messageId;
            }
        }

        public static long GetReaderMessageTimestamp(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderMessageTime(handle, out var timestamp, errorBuffer, ErrorBufferLength) != 0 ? throw ApiError() : timestamp;
            }
        }

        public static string GetReaderAnnotation(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderAnnotation(handle, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0
                    ? throw ApiError()
                    : Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        public static string GetReaderMuseConfiguration(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderMuseConfiguration(handle, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0
                    ? throw ApiError()
                    : Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        public static string GetReaderVersion(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderVersion(handle, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0
                    ? throw ApiError()
                    : Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        public static string GetReaderDeviceConfiguration(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderDeviceConfiguration(handle, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0
                    ? throw ApiError()
                    : Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        public static string GetReaderDspData(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderDspData(handle, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0
                    ? throw ApiError()
                    : Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        public static string GetReaderDataPacket(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderDataPacket(handle, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0
                    ? throw ApiError()
                    : Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        public static string GetReaderArtifactPacket(int handle)
        {
            lock (bufferLock)
            {
                return IxGetReaderArtifactPacket(handle, stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0
                    ? throw ApiError()
                    : Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        // file writer
        public static int OpenFileWriter(string filePath)
        {
            lock (bufferLock)
            {
                return IxOpenFileWriter(filePath, out var handle, errorBuffer, ErrorBufferLength) != 0 ? throw ApiError() : handle;
            }
        }

        public static void CloseFileWriter(int handle)
        {
            lock (bufferLock)
            {
                if (IxCloseFileWriter(handle, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void WriterDiscardBuffer(int handle)
        {
            lock (bufferLock)
            {
                if (IxWriterDiscardBuffer(handle, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void WriterFlush(int handle)
        {
            lock (bufferLock)
            {
                if (IxWriterFlush(handle, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static int GetWriterBufferedMessagesCount(int handle)
        {
            lock (bufferLock)
            {
                return IxGetWriterBufferedMessagesCount(handle, out var count, errorBuffer, ErrorBufferLength) != 0 ? throw ApiError() : count;
            }
        }

        public static int GetWriterBufferedMessagesSize(int handle)
        {
            lock (bufferLock)
            {
                return IxGetWriterBufferedMessagesSize(handle, out var size, errorBuffer, ErrorBufferLength) != 0 ? throw ApiError() : size;
            }
        }

        public static long GetWriterBytesWritten(int handle)
        {
            lock (bufferLock)
            {
                return IxGetWriterBytesWritten(handle, out var byteCount, errorBuffer, ErrorBufferLength) != 0 ? throw ApiError() : byteCount;
            }
        }

        public static void WriterAddArtifactPacket(int handle, MuseArtifactPacket packet, int id = 0)
        {
            lock (bufferLock)
            {
                if (IxWriterAddArtifactPacket(handle, id, packet.HeadbandOn, packet.Blink, packet.JawClench, packet.Timestamp, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void WriterAddDataPacket(int handle, MuseDataPacket packet, int id = 0)
        {
            lock (bufferLock)
            {
                if (IxWriterAddDataPacket(handle, id, packet.PacketType, packet.Timestamp, packet.Values, packet.ValuesSize, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void WriterAddAnnotationString(int handle, string annotation, int id = 0)
        {
            lock (bufferLock)
            {
                if (IxWriterAddAnnotationString(handle, id, annotation, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        //public static void WriterAddAnnotation(int handle, AnnotationData annotation, int id = 0)
        //{
        //    lock (bufferLock)
        //    {
        //        if (IxWriterAddAnnotation(handle, id, annotation.ToJson(), errorBuffer, ErrorBufferLength) != 0)
        //        {
        //            throw ApiError();
        //        }
        //    }
        //}

        public static void WriterAddMuseConfiguration(int handle, MuseConfiguration configuration, int id = 0)
        {
            lock (bufferLock)
            {
                if (IxWriterAddMuseConfiguration(handle, id, configuration.ToJson(), errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void WriterAddVersion(int handle, MuseVersion version, int id = 0)
        {
            lock (bufferLock)
            {
                if (IxWriterAddVersion(handle, id, version.ToJson(), errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        //public static void WriterAddDeviceConfiguration(int handle, ComputingDeviceConfiguration configuration, int id = 0)
        //{
        //    lock (bufferLock)
        //    {
        //        if (IxWriterAddDeviceConfiguration(handle, id, configuration.ToJson(), errorBuffer, ErrorBufferLength) != 0)
        //        {
        //            throw ApiError();
        //        }
        //    }
        //}

        //public static void WriterAddDspData(int handle, DspData data, int id = 0)
        //{
        //    lock (bufferLock)
        //    {
        //        if (IxWriterAddDspData(handle, id, data.ToJson(), errorBuffer, ErrorBufferLength) != 0)
        //        {
        //            throw ApiError();
        //        }
        //    }
        //}

        public static void SetWriterTimestampMode(int handle, TimestampMode mode)
        {
            lock (bufferLock)
            {
                if (IxSetWriterTimestampMode(handle, mode, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static void SetWriterTimestamp(int handle, long timestamp)
        {
            lock (bufferLock)
            {
                if (IxSetWriterTimestamp(handle, timestamp, errorBuffer, ErrorBufferLength) != 0)
                {
                    throw ApiError();
                }
            }
        }

        public static string GetComputingDeviceConfiguration()
        {
            lock (bufferLock)
            {
                return IxGetComputingDeviceConfiguration(stringBuffer, stringBufferLength, errorBuffer, ErrorBufferLength) != 0
                    ? throw ApiError()
                    : Marshal.PtrToStringAnsi(stringBuffer);
            }
        }

        private static ApiException ApiError(string message = null, [CallerMemberName] string callingMethod = null)
        {
            var err = Marshal.PtrToStringAnsi(errorBuffer);
            if (string.IsNullOrEmpty(err) && message == null)
            {
                message = "An exception was raised from the native dll but no error message was found";
            }
            else if (message == null)
            {
                message = err;
            }
            else
            {
                message += $" {err}";
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