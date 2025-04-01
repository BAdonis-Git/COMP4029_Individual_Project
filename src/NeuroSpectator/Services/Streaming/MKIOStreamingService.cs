using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Networking;
using NeuroSpectator.Services.Account;

namespace NeuroSpectator.Services.Streaming
{
    /// <summary>
    /// Implementation of the MK.IO streaming service
    /// </summary>
    public class MKIOStreamingService : IMKIOStreamingService
    {
        private readonly HttpClient httpClient;
        private readonly AccountService accountService;
        private readonly IConnectivity connectivity;
        private readonly string baseApiUrl = "https://api.mkio.cloud/v1/";
        private readonly string apiKey;
        private readonly string apiSecret;

        private StreamInfo currentStream;
        private StreamingStatus status = StreamingStatus.Idle;
        private CancellationTokenSource streamingCancellationSource;
        private Timer statisticsTimer;
        private bool isDisposed;

        /// <summary>
        /// Gets information about the currently active stream
        /// </summary>
        public StreamInfo CurrentStream => currentStream;

        /// <summary>
        /// Gets the current streaming status
        /// </summary>
        public StreamingStatus Status
        {
            get => status;
            private set
            {
                if (status != value)
                {
                    status = value;
                    StatusChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Event fired when streaming status changes
        /// </summary>
        public event EventHandler<StreamingStatus> StatusChanged;

        /// <summary>
        /// Event fired when an error occurs during streaming
        /// </summary>
        public event EventHandler<Exception> StreamingError;

        /// <summary>
        /// Event fired regularly with streaming statistics
        /// </summary>
        public event EventHandler<StreamingStatistics> StatisticsUpdated;

        /// <summary>
        /// Creates a new instance of the MKIOStreamingService
        /// </summary>
        public MKIOStreamingService(AccountService accountService, IConnectivity connectivity)
        {
            this.accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            this.connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));

            // In a real implementation, these would be securely stored or retrieved from a secure service
            this.apiKey = "your-mkio-api-key";
            this.apiSecret = "your-mkio-api-secret";

            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(baseApiUrl);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Setup authentication headers
            string authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        }

        /// <summary>
        /// Creates a new stream in MK.IO
        /// </summary>
        public async Task<StreamInfo> CreateStreamAsync(string title, string description, string game, List<string> tags = null)
        {
            if (!connectivity.NetworkAccess.HasFlag(NetworkAccess.Internet))
            {
                throw new InvalidOperationException("No internet connection available");
            }

            try
            {
                var currentUser = accountService.CurrentUser;
                if (currentUser == null)
                {
                    throw new InvalidOperationException("User must be signed in to create a stream");
                }

                var createRequest = new
                {
                    title = title,
                    description = description,
                    game = game,
                    tags = tags ?? new List<string>(),
                    streamer_id = currentUser.UserId,
                    streamer_name = currentUser.DisplayName
                };

                var response = await httpClient.PostAsJsonAsync("streams", createRequest);

                if (response.IsSuccessStatusCode)
                {
                    var streamResponse = await response.Content.ReadFromJsonAsync<MKIOStreamResponse>();
                    currentStream = MapToStreamInfo(streamResponse);
                    return currentStream;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to create stream: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating stream: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing stream
        /// </summary>
        public async Task<StreamInfo> UpdateStreamAsync(string streamId, string title = null, string description = null, string game = null, List<string> tags = null)
        {
            if (!connectivity.NetworkAccess.HasFlag(NetworkAccess.Internet))
            {
                throw new InvalidOperationException("No internet connection available");
            }

            try
            {
                var updateRequest = new
                {
                    title = title,
                    description = description,
                    game = game,
                    tags = tags
                };

                var response = await httpClient.PatchAsJsonAsync($"streams/{streamId}", updateRequest);

                if (response.IsSuccessStatusCode)
                {
                    var streamResponse = await response.Content.ReadFromJsonAsync<MKIOStreamResponse>();

                    // If this is the current stream, update it
                    if (currentStream != null && currentStream.Id == streamId)
                    {
                        currentStream = MapToStreamInfo(streamResponse);
                    }

                    return MapToStreamInfo(streamResponse);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to update stream: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating stream: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Starts streaming to MK.IO
        /// </summary>
        public async Task<bool> StartStreamingAsync(string streamId, StreamQualitySettings qualitySettings = null, CancellationToken cancellationToken = default)
        {
            if (!connectivity.NetworkAccess.HasFlag(NetworkAccess.Internet))
            {
                throw new InvalidOperationException("No internet connection available");
            }

            if (Status == StreamingStatus.Streaming || Status == StreamingStatus.Starting)
            {
                throw new InvalidOperationException("Already streaming or starting a stream");
            }

            try
            {
                Status = StreamingStatus.Starting;

                // Get stream details if not already set
                if (currentStream == null || currentStream.Id != streamId)
                {
                    currentStream = await GetStreamAsync(streamId);
                }

                // Start the streaming session
                var startRequest = new
                {
                    status = "live",
                    quality = qualitySettings != null
                        ? new
                        {
                            width = qualitySettings.Width,
                            height = qualitySettings.Height,
                            frame_rate = qualitySettings.FrameRate,
                            bitrate = qualitySettings.Bitrate,
                            preset = qualitySettings.Preset,
                            profile = qualitySettings.Profile
                        }
                        : null
                };

                var response = await httpClient.PostAsJsonAsync($"streams/{streamId}/session", startRequest);

                if (response.IsSuccessStatusCode)
                {
                    var sessionResponse = await response.Content.ReadFromJsonAsync<MKIOStreamSessionResponse>();

                    // Update current stream with session info
                    currentStream.IngestUrl = sessionResponse.ingest_url;
                    currentStream.StreamKey = sessionResponse.stream_key;
                    currentStream.PlaybackUrl = sessionResponse.playback_url;
                    currentStream.IsLive = true;
                    currentStream.StartTime = DateTime.Now;

                    // Create a new cancellation token source for this streaming session
                    streamingCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    // Start the statistics timer
                    StartStatisticsPolling();

                    Status = StreamingStatus.Streaming;
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to start streaming: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Status = StreamingStatus.Error;
                Console.WriteLine($"Error starting stream: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Stops the current stream
        /// </summary>
        public async Task<bool> StopStreamingAsync(string streamId)
        {
            if (Status != StreamingStatus.Streaming)
            {
                return false;
            }

            try
            {
                Status = StreamingStatus.Stopping;

                // Stop the statistics timer
                StopStatisticsPolling();

                // Stop the streaming session
                var stopRequest = new
                {
                    status = "ended"
                };

                var response = await httpClient.PatchAsJsonAsync($"streams/{streamId}/session", stopRequest);

                if (response.IsSuccessStatusCode)
                {
                    // Update current stream
                    if (currentStream != null && currentStream.Id == streamId)
                    {
                        currentStream.IsLive = false;
                        currentStream.EndTime = DateTime.Now;
                        currentStream.DurationSeconds = (long)(currentStream.EndTime.Value - currentStream.StartTime.Value).TotalSeconds;
                    }

                    Status = StreamingStatus.Idle;
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to stop streaming: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Status = StreamingStatus.Error;
                Console.WriteLine($"Error stopping stream: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
            finally
            {
                // Cancel the streaming token source
                if (streamingCancellationSource != null)
                {
                    streamingCancellationSource.Cancel();
                    streamingCancellationSource.Dispose();
                    streamingCancellationSource = null;
                }
            }
        }

        /// <summary>
        /// Creates a VOD from a live stream
        /// </summary>
        public async Task<StreamInfo> CreateVodFromStreamAsync(string streamId, string title = null)
        {
            if (!connectivity.NetworkAccess.HasFlag(NetworkAccess.Internet))
            {
                throw new InvalidOperationException("No internet connection available");
            }

            try
            {
                var vodRequest = new
                {
                    stream_id = streamId,
                    title = title
                };

                var response = await httpClient.PostAsJsonAsync("vods", vodRequest);

                if (response.IsSuccessStatusCode)
                {
                    var vodResponse = await response.Content.ReadFromJsonAsync<MKIOVodResponse>();
                    return MapToStreamInfo(vodResponse);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to create VOD: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating VOD: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets a list of available streams
        /// </summary>
        public async Task<List<StreamInfo>> GetAvailableStreamsAsync(bool includeLiveOnly = false, string game = null, string search = null)
        {
            if (!connectivity.NetworkAccess.HasFlag(NetworkAccess.Internet))
            {
                throw new InvalidOperationException("No internet connection available");
            }

            try
            {
                var queryParams = new List<string>();

                if (includeLiveOnly)
                {
                    queryParams.Add("status=live");
                }

                if (!string.IsNullOrEmpty(game))
                {
                    queryParams.Add($"game={Uri.EscapeDataString(game)}");
                }

                if (!string.IsNullOrEmpty(search))
                {
                    queryParams.Add($"search={Uri.EscapeDataString(search)}");
                }

                string url = "streams";
                if (queryParams.Count > 0)
                {
                    url += "?" + string.Join("&", queryParams);
                }

                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var streamResponses = await response.Content.ReadFromJsonAsync<List<MKIOStreamResponse>>();
                    return streamResponses.ConvertAll(MapToStreamInfo);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to get available streams: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting available streams: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets a specific stream by ID
        /// </summary>
        public async Task<StreamInfo> GetStreamAsync(string streamId)
        {
            if (!connectivity.NetworkAccess.HasFlag(NetworkAccess.Internet))
            {
                throw new InvalidOperationException("No internet connection available");
            }

            try
            {
                var response = await httpClient.GetAsync($"streams/{streamId}");

                if (response.IsSuccessStatusCode)
                {
                    var streamResponse = await response.Content.ReadFromJsonAsync<MKIOStreamResponse>();
                    return MapToStreamInfo(streamResponse);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to get stream: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting stream: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets a list of available VODs
        /// </summary>
        public async Task<List<StreamInfo>> GetAvailableVodsAsync(string userId = null, string game = null, string search = null)
        {
            if (!connectivity.NetworkAccess.HasFlag(NetworkAccess.Internet))
            {
                throw new InvalidOperationException("No internet connection available");
            }

            try
            {
                var queryParams = new List<string>();

                if (!string.IsNullOrEmpty(userId))
                {
                    queryParams.Add($"streamer_id={Uri.EscapeDataString(userId)}");
                }

                if (!string.IsNullOrEmpty(game))
                {
                    queryParams.Add($"game={Uri.EscapeDataString(game)}");
                }

                if (!string.IsNullOrEmpty(search))
                {
                    queryParams.Add($"search={Uri.EscapeDataString(search)}");
                }

                string url = "vods";
                if (queryParams.Count > 0)
                {
                    url += "?" + string.Join("&", queryParams);
                }

                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var vodResponses = await response.Content.ReadFromJsonAsync<List<MKIOVodResponse>>();
                    return vodResponses.ConvertAll(MapToStreamInfo);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to get available VODs: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting available VODs: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Uploads a thumbnail for a stream
        /// </summary>
        public async Task<bool> UploadThumbnailAsync(string streamId, Stream thumbnailStream)
        {
            if (!connectivity.NetworkAccess.HasFlag(NetworkAccess.Internet))
            {
                throw new InvalidOperationException("No internet connection available");
            }

            try
            {
                var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(thumbnailStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                content.Add(streamContent, "thumbnail", "thumbnail.jpg");

                var response = await httpClient.PostAsync($"streams/{streamId}/thumbnail", content);

                if (response.IsSuccessStatusCode)
                {
                    var thumbnailResponse = await response.Content.ReadFromJsonAsync<MKIOThumbnailResponse>();

                    // Update current stream if this is the current stream
                    if (currentStream != null && currentStream.Id == streamId)
                    {
                        currentStream.ThumbnailUrl = thumbnailResponse.thumbnail_url;
                    }

                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to upload thumbnail: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading thumbnail: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Generates a thumbnail from the current stream
        /// </summary>
        public async Task<bool> GenerateThumbnailAsync(string streamId)
        {
            if (!connectivity.NetworkAccess.HasFlag(NetworkAccess.Internet))
            {
                throw new InvalidOperationException("No internet connection available");
            }

            try
            {
                var response = await httpClient.PostAsync($"streams/{streamId}/thumbnail/generate", null);

                if (response.IsSuccessStatusCode)
                {
                    var thumbnailResponse = await response.Content.ReadFromJsonAsync<MKIOThumbnailResponse>();

                    // Update current stream if this is the current stream
                    if (currentStream != null && currentStream.Id == streamId)
                    {
                        currentStream.ThumbnailUrl = thumbnailResponse.thumbnail_url;
                    }

                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to generate thumbnail: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating thumbnail: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Starts polling for streaming statistics
        /// </summary>
        private void StartStatisticsPolling()
        {
            // Stop any existing timer
            StopStatisticsPolling();

            // Start a new timer to poll for statistics every 5 seconds
            statisticsTimer = new Timer(PollStatistics, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Stops polling for streaming statistics
        /// </summary>
        private void StopStatisticsPolling()
        {
            if (statisticsTimer != null)
            {
                statisticsTimer.Dispose();
                statisticsTimer = null;
            }
        }

        /// <summary>
        /// Polls for streaming statistics
        /// </summary>
        private async void PollStatistics(object state)
        {
            if (Status != StreamingStatus.Streaming || currentStream == null)
            {
                return;
            }

            try
            {
                var response = await httpClient.GetAsync($"streams/{currentStream.Id}/stats");

                if (response.IsSuccessStatusCode)
                {
                    var statsResponse = await response.Content.ReadFromJsonAsync<MKIOStreamStatsResponse>();

                    // Update current stream
                    currentStream.ViewerCount = statsResponse.viewer_count;

                    // Create statistics object
                    var statistics = new StreamingStatistics
                    {
                        ViewerCount = statsResponse.viewer_count,
                        DurationSeconds = statsResponse.duration_seconds,
                        CurrentBitrate = statsResponse.current_bitrate,
                        CurrentFps = statsResponse.current_fps,
                        CpuUsage = statsResponse.cpu_usage,
                        DroppedFrames = statsResponse.dropped_frames,
                        Timestamp = DateTime.Now
                    };

                    // Raise event
                    StatisticsUpdated?.Invoke(this, statistics);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error polling statistics: {ex.Message}");
            }
        }

        /// <summary>
        /// Maps an MK.IO stream response to a StreamInfo object
        /// </summary>
        private StreamInfo MapToStreamInfo(MKIOStreamResponse response)
        {
            return new StreamInfo
            {
                Id = response.id,
                Title = response.title,
                Description = response.description,
                StreamerName = response.streamer_name,
                StreamerUserId = response.streamer_id,
                Game = response.game,
                Tags = response.tags,
                IsLive = response.status == "live",
                StartTime = response.start_time != null ? DateTime.Parse(response.start_time) : null,
                EndTime = response.end_time != null ? DateTime.Parse(response.end_time) : null,
                DurationSeconds = response.duration_seconds,
                ThumbnailUrl = response.thumbnail_url,
                PlaybackUrl = response.playback_url,
                IngestUrl = response.ingest_url,
                StreamKey = response.stream_key,
                ViewerCount = response.viewer_count,
                Quality = response.quality,
                HasBrainData = response.tags?.Contains("brain-data") ?? false
            };
        }

        /// <summary>
        /// Maps an MK.IO VOD response to a StreamInfo object
        /// </summary>
        private StreamInfo MapToStreamInfo(MKIOVodResponse response)
        {
            return new StreamInfo
            {
                Id = response.id,
                Title = response.title,
                Description = response.description,
                StreamerName = response.streamer_name,
                StreamerUserId = response.streamer_id,
                Game = response.game,
                Tags = response.tags,
                IsLive = false,
                StartTime = response.start_time != null ? DateTime.Parse(response.start_time) : null,
                EndTime = response.end_time != null ? DateTime.Parse(response.end_time) : null,
                DurationSeconds = response.duration_seconds,
                ThumbnailUrl = response.thumbnail_url,
                PlaybackUrl = response.playback_url,
                ViewerCount = response.view_count,
                Quality = response.quality,
                HasBrainData = response.tags?.Contains("brain-data") ?? false
            };
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    // Stop any ongoing operations
                    StopStatisticsPolling();

                    if (streamingCancellationSource != null)
                    {
                        streamingCancellationSource.Cancel();
                        streamingCancellationSource.Dispose();
                        streamingCancellationSource = null;
                    }

                    httpClient.Dispose();
                }

                isDisposed = true;
            }
        }

        #region MK.IO API Response Classes

        /// <summary>
        /// Response from the MK.IO API for streams
        /// </summary>
        private class MKIOStreamResponse
        {
            public string id { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public string streamer_id { get; set; }
            public string streamer_name { get; set; }
            public string game { get; set; }
            public List<string> tags { get; set; }
            public string status { get; set; }
            public string start_time { get; set; }
            public string end_time { get; set; }
            public long duration_seconds { get; set; }
            public string thumbnail_url { get; set; }
            public string playback_url { get; set; }
            public string ingest_url { get; set; }
            public string stream_key { get; set; }
            public int viewer_count { get; set; }
            public string quality { get; set; }
        }

        /// <summary>
        /// Response from the MK.IO API for VODs
        /// </summary>
        private class MKIOVodResponse
        {
            public string id { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public string streamer_id { get; set; }
            public string streamer_name { get; set; }
            public string game { get; set; }
            public List<string> tags { get; set; }
            public string start_time { get; set; }
            public string end_time { get; set; }
            public long duration_seconds { get; set; }
            public string thumbnail_url { get; set; }
            public string playback_url { get; set; }
            public int view_count { get; set; }
            public string quality { get; set; }
        }

        /// <summary>
        /// Response from the MK.IO API for stream sessions
        /// </summary>
        private class MKIOStreamSessionResponse
        {
            public string id { get; set; }
            public string stream_id { get; set; }
            public string status { get; set; }
            public string ingest_url { get; set; }
            public string stream_key { get; set; }
            public string playback_url { get; set; }
        }

        /// <summary>
        /// Response from the MK.IO API for stream statistics
        /// </summary>
        private class MKIOStreamStatsResponse
        {
            public string id { get; set; }
            public string stream_id { get; set; }
            public int viewer_count { get; set; }
            public long duration_seconds { get; set; }
            public int current_bitrate { get; set; }
            public float current_fps { get; set; }
            public float cpu_usage { get; set; }
            public int dropped_frames { get; set; }
        }

        /// <summary>
        /// Response from the MK.IO API for thumbnails
        /// </summary>
        private class MKIOThumbnailResponse
        {
            public string id { get; set; }
            public string stream_id { get; set; }
            public string thumbnail_url { get; set; }
        }

        #endregion
    }
}