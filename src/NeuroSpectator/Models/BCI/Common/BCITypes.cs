using System;

namespace NeuroSpectator.Models.BCI.Common
{
    /// <summary>
    /// Represents the type of BCI device
    /// </summary>
    public enum BCIDeviceType
    {
        Unknown = 0,
        MuseHeadband = 1,
        MendiHeadband = 2
        // Add more device types as needed
    }

    /// <summary>
    /// Represents the connection state of a BCI device
    /// </summary>
    public enum ConnectionState
    {
        Unknown = 0,
        Disconnected = 1,
        Connecting = 2,
        Connected = 3,
        NeedsUpdate = 4,
        NeedsLicense = 5
    }

    /// <summary>
    /// Represents the types of brain waves that can be monitored
    /// </summary>
    [Flags]
    public enum BrainWaveTypes
    {
        None = 0,
        Alpha = 1 << 0,
        Beta = 1 << 1,
        Delta = 1 << 2,
        Theta = 1 << 3,
        Gamma = 1 << 4,
        Raw = 1 << 5,
        All = Alpha | Beta | Delta | Theta | Gamma | Raw
    }

    /// <summary>
    /// Event arguments for connection state changes
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public ConnectionState OldState { get; }
        public ConnectionState NewState { get; }

        public ConnectionStateChangedEventArgs(ConnectionState oldState, ConnectionState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// Event arguments for brain wave data
    /// </summary>
    public class BrainWaveDataEventArgs : EventArgs
    {
        public BrainWaveData BrainWaveData { get; }

        public BrainWaveDataEventArgs(BrainWaveData brainWaveData)
        {
            BrainWaveData = brainWaveData;
        }
    }

    /// <summary>
    /// Event arguments for artifact detection
    /// </summary>
    public class ArtifactEventArgs : EventArgs
    {
        public bool Blink { get; }
        public bool JawClench { get; }
        public bool HeadbandTooLoose { get; }
        public DateTimeOffset Timestamp { get; }

        public ArtifactEventArgs(bool blink, bool jawClench, bool headbandTooLoose, DateTimeOffset timestamp)
        {
            Blink = blink;
            JawClench = jawClench;
            HeadbandTooLoose = headbandTooLoose;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Event arguments for BCI errors
    /// </summary>
    public class BCIErrorEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception Exception { get; }
        public BCIErrorType ErrorType { get; }

        public BCIErrorEventArgs(string message, Exception exception = null, BCIErrorType errorType = BCIErrorType.Unknown)
        {
            Message = message;
            Exception = exception;
            ErrorType = errorType;
        }
    }

    /// <summary>
    /// Represents the type of BCI error
    /// </summary>
    public enum BCIErrorType
    {
        Unknown = 0,
        ConnectionFailed = 1,
        ScanningFailed = 2,
        DeviceDisconnected = 3,
        PermissionDenied = 4,
        BluetoothNotEnabled = 5,
        DeviceNotSupported = 6,
        NativeLibraryError = 7
    }
}