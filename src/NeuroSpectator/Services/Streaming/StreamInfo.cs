using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpectator.Services.Streaming
{
    /// <summary>
    /// Represents information about a stream
    /// </summary>
    public class StreamInfo
    {
        /// <summary>
        /// Unique identifier for the stream
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Title of the stream
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Description of the stream
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Name of the streamer
        /// </summary>
        public string StreamerName { get; set; }

        /// <summary>
        /// User ID of the streamer
        /// </summary>
        public string StreamerUserId { get; set; }

        /// <summary>
        /// Game or category of the stream
        /// </summary>
        public string Game { get; set; }

        /// <summary>
        /// Tags associated with the stream
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Whether the stream is currently live
        /// </summary>
        public bool IsLive { get; set; }

        /// <summary>
        /// Start time of the stream
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// End time of the stream (if it's a VOD)
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Duration of the stream in seconds
        /// </summary>
        public long DurationSeconds { get; set; }

        /// <summary>
        /// URL for the stream thumbnail
        /// </summary>
        public string ThumbnailUrl { get; set; }

        /// <summary>
        /// URL for the stream playback
        /// </summary>
        public string PlaybackUrl { get; set; }

        /// <summary>
        /// URL for ingest/streaming
        /// </summary>
        public string IngestUrl { get; set; }

        /// <summary>
        /// Stream key for ingest
        /// </summary>
        public string StreamKey { get; set; }

        /// <summary>
        /// Current viewer count
        /// </summary>
        public int ViewerCount { get; set; }

        /// <summary>
        /// Video quality settings (e.g., 1080p60)
        /// </summary>
        public string Quality { get; set; }

        /// <summary>
        /// Metadata for BCI/brain metrics
        /// </summary>
        public Dictionary<string, string> BrainMetrics { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Whether this stream includes brain data
        /// </summary>
        public bool HasBrainData { get; set; }
    }

    /// <summary>
    /// Defines methods for screen capture
    /// </summary>
    public interface IScreenCaptureService : IDisposable
    {
        /// <summary>
        /// Starts capturing the screen
        /// </summary>
        Task StartCaptureAsync(int frameRate = 30, string targetWindow = null);

        /// <summary>
        /// Stops capturing the screen
        /// </summary>
        Task StopCaptureAsync();

        /// <summary>
        /// Takes a screenshot
        /// </summary>
        Task<Stream> TakeScreenshotAsync(string outputFormat = "jpg");

        /// <summary>
        /// Gets whether capture is active
        /// </summary>
        bool IsCapturing { get; }

        /// <summary>
        /// Event fired when a new frame is captured
        /// </summary>
        event EventHandler<byte[]> FrameCaptured;

        /// <summary>
        /// Event fired when the capture fails
        /// </summary>
        event EventHandler<Exception> CaptureFailed;
    }

    /// <summary>
    /// Streaming service status
    /// </summary>
    public enum StreamingStatus
    {
        Idle,
        Starting,
        Streaming,
        Stopping,
        Error
    }

    /// <summary>
    /// Stream quality settings
    /// </summary>
    public class StreamQualitySettings
    {
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int FrameRate { get; set; } = 30;
        public int Bitrate { get; set; } = 4500000; // 4.5 Mbps
        public string Preset { get; set; } = "medium"; // x264 preset: ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow
        public string Profile { get; set; } = "high"; // baseline, main, high
    }

    /// <summary>
    /// Defines methods for streaming to MK.IO
    /// </summary>
    public interface IMKIOStreamingService : IDisposable
    {
        /// <summary>
        /// Creates a new stream
        /// </summary>
        Task<StreamInfo> CreateStreamAsync(string title, string description, string game, List<string> tags = null);

        /// <summary>
        /// Updates an existing stream
        /// </summary>
        Task<StreamInfo> UpdateStreamAsync(string streamId, string title = null, string description = null, string game = null, List<string> tags = null);

        /// <summary>
        /// Starts streaming to MK.IO
        /// </summary>
        Task<bool> StartStreamingAsync(string streamId, StreamQualitySettings qualitySettings = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the current stream
        /// </summary>
        Task<bool> StopStreamingAsync(string streamId);

        /// <summary>
        /// Creates a VOD from a live stream
        /// </summary>
        Task<StreamInfo> CreateVodFromStreamAsync(string streamId, string title = null);

        /// <summary>
        /// Gets the current streaming status
        /// </summary>
        StreamingStatus Status { get; }

        /// <summary>
        /// Gets information about the currently active stream
        /// </summary>
        StreamInfo CurrentStream { get; }

        /// <summary>
        /// Gets a list of available streams
        /// </summary>
        Task<List<StreamInfo>> GetAvailableStreamsAsync(bool includeLiveOnly = false, string game = null, string search = null);

        /// <summary>
        /// Gets a specific stream by ID
        /// </summary>
        Task<StreamInfo> GetStreamAsync(string streamId);

        /// <summary>
        /// Gets a list of available VODs
        /// </summary>
        Task<List<StreamInfo>> GetAvailableVodsAsync(string userId = null, string game = null, string search = null);

        /// <summary>
        /// Uploads a thumbnail for a stream
        /// </summary>
        Task<bool> UploadThumbnailAsync(string streamId, Stream thumbnailStream);

        /// <summary>
        /// Generates a thumbnail from the current stream
        /// </summary>
        Task<bool> GenerateThumbnailAsync(string streamId);

        /// <summary>
        /// Event fired when streaming status changes
        /// </summary>
        event EventHandler<StreamingStatus> StatusChanged;

        /// <summary>
        /// Event fired when an error occurs during streaming
        /// </summary>
        event EventHandler<Exception> StreamingError;

        /// <summary>
        /// Event fired regularly with streaming statistics
        /// </summary>
        event EventHandler<StreamingStatistics> StatisticsUpdated;
    }

    /// <summary>
    /// Statistics for streaming
    /// </summary>
    public class StreamingStatistics
    {
        /// <summary>
        /// Current viewer count
        /// </summary>
        public int ViewerCount { get; set; }

        /// <summary>
        /// Stream duration in seconds
        /// </summary>
        public long DurationSeconds { get; set; }

        /// <summary>
        /// Current bitrate in bits per second
        /// </summary>
        public int CurrentBitrate { get; set; }

        /// <summary>
        /// Current FPS being streamed
        /// </summary>
        public float CurrentFps { get; set; }

        /// <summary>
        /// CPU usage percentage
        /// </summary>
        public float CpuUsage { get; set; }

        /// <summary>
        /// Number of dropped frames
        /// </summary>
        public int DroppedFrames { get; set; }

        /// <summary>
        /// Timestamp when the statistics were collected
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Defines methods for the player component
    /// </summary>
    public interface IMKIOPlayerService
    {
        /// <summary>
        /// Initializes the player with a stream URL
        /// </summary>
        Task InitializePlayerAsync(string streamId, bool isLive = true);

        /// <summary>
        /// Plays the stream
        /// </summary>
        Task PlayAsync();

        /// <summary>
        /// Pauses the stream
        /// </summary>
        Task PauseAsync();

        /// <summary>
        /// Seeks to a specific position in the stream (for VODs)
        /// </summary>
        Task SeekAsync(TimeSpan position);

        /// <summary>
        /// Sets the volume
        /// </summary>
        Task SetVolumeAsync(double volume);

        /// <summary>
        /// Gets the current playback state
        /// </summary>
        Task<bool> IsPlayingAsync();

        /// <summary>
        /// Gets the current position in the stream
        /// </summary>
        Task<TimeSpan> GetCurrentPositionAsync();

        /// <summary>
        /// Gets the duration of the stream
        /// </summary>
        Task<TimeSpan> GetDurationAsync();

        /// <summary>
        /// Gets or sets whether the player is muted
        /// </summary>
        Task<bool> IsMutedAsync();
        Task SetMutedAsync(bool muted);

        /// <summary>
        /// Gets or sets the player quality
        /// </summary>
        Task<string> GetCurrentQualityAsync();
        Task<List<string>> GetAvailableQualitiesAsync();
        Task SetQualityAsync(string quality);

        /// <summary>
        /// Event fired when the player state changes
        /// </summary>
        event EventHandler<string> PlayerStateChanged;

        /// <summary>
        /// Event fired when an error occurs in the player
        /// </summary>
        event EventHandler<Exception> PlayerError;
    }
}