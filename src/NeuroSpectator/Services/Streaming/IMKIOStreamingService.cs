using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NeuroSpectator.Models.Stream;

namespace NeuroSpectator.Services.Streaming
{
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
}
