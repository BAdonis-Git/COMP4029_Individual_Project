namespace NeuroSpectator.Models.Stream
{
    /// <summary>
    /// Represents information about a stream for display in the UI.
    /// This is a simplified model that represents what's needed for the UI rather than
    /// mapping directly to the MK.IO API models.
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
    /// Defines quality settings for a stream
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
    /// Streaming status enum
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
    /// Contains streaming statistics
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
}