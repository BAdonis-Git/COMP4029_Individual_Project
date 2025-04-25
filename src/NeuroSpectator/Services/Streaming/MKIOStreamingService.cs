using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Networking;
using MK.IO;
using MK.IO.Models;
using NeuroSpectator.Models.Stream;
using NeuroSpectator.Services.Account;

namespace NeuroSpectator.Services.Streaming
{
    /// <summary>
    /// Implementation of the MK.IO streaming service
    /// </summary>
    public class MKIOStreamingService : IMKIOStreamingService
    {
        private readonly MKIOClient mkioClient;
        private readonly MKIOConfig mkioConfig;
        private readonly AccountService accountService;
        private readonly IConnectivity connectivity;

        private StreamInfo currentStream;
        private StreamingStatus status = StreamingStatus.Idle;
        private CancellationTokenSource streamingCancellationSource;
        private Timer statisticsTimer;
        private bool isDisposed;

        // MK.IO resource tracking
        private string currentLiveEventName;
        private string currentLiveOutputName;
        private string currentAssetName;
        private string currentLocatorName;
        private string currentStreamingEndpointName;

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
        public MKIOStreamingService(MKIOConfig mkioConfig, IConfiguration configuration, AccountService accountService, IConnectivity connectivity)
        {
            this.mkioConfig = mkioConfig ?? throw new ArgumentNullException(nameof(mkioConfig));
            this.accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            this.connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));

            // Ensure we have valid configuration
            if (!mkioConfig.IsValid)
            {
                throw new InvalidOperationException("MK.IO configuration is invalid. Check MKIOSubscriptionName, MKIOToken, and StorageName settings.");
            }

            // Initialize MK.IO client
            mkioClient = new MKIOClient(mkioConfig.SubscriptionName, mkioConfig.ApiToken);

            // Log successful initialization
            Debug.WriteLine($"MKIOStreamingService: Initialized with subscription {mkioConfig.SubscriptionName}");
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
                // Validate the current user
                var currentUser = accountService.CurrentUser;
                if (currentUser == null)
                {
                    throw new InvalidOperationException("User must be signed in to create a stream");
                }

                // First ensure we're in a clean state
                Debug.WriteLine("CreateStreamAsync: Ensuring clean state before creating stream");
                await ResetStatusAsync();

                // Add a delay to ensure reset is complete
                await Task.Delay(3000);

                // Double-check status after reset
                if (Status != StreamingStatus.Idle)
                {
                    Debug.WriteLine($"CreateStreamAsync: Status is not Idle after reset! Current status: {Status}");
                    // Force it to idle
                    Status = StreamingStatus.Idle;
                }

                // Create a unique ID for this stream
                string streamerId = currentUser.UserId ?? "anonymous";

                // CRITICAL FIX: Do NOT set status to Starting here - leave it as Idle
                // Let StartStreamingAsync handle the state transition
                // This was causing the "Stream is already starting" error

                // Create a live event in MK.IO
                currentLiveEventName = mkioConfig.GenerateLiveEventName(streamerId);
                Debug.WriteLine($"CreateStreamAsync: Creating live event: {currentLiveEventName}");

                var location = await mkioClient.Account.GetSubscriptionLocationAsync();

                if (location == null)
                {
                    throw new InvalidOperationException("Could not determine MK.IO location");
                }

                var liveEvent = await mkioClient.LiveEvents.CreateAsync(
                    currentLiveEventName,
                    location.Name,
                    new LiveEventProperties
                    {
                        Input = new LiveEventInput { StreamingProtocol = LiveEventInputProtocol.RTMP },
                        StreamOptions = new List<string> { "Default" },
                        Encoding = new LiveEventEncoding { EncodingType = LiveEventEncodingType.PassthroughBasic }
                    });

                Debug.WriteLine($"CreateStreamAsync: Live event {currentLiveEventName} created successfully");

                // Create an asset for the live output
                currentAssetName = mkioConfig.GenerateOutputAssetName(streamerId);
                Debug.WriteLine($"CreateStreamAsync: Creating asset {currentAssetName}");

                // Create the asset with deletion policy set to Delete
                await mkioClient.Assets.CreateOrUpdateAsync(
                    currentAssetName,
                    null,
                    mkioConfig.StorageName,
                    $"Live output asset for stream: {title}",
                    AssetContainerDeletionPolicyType.Delete
                );

                Debug.WriteLine($"CreateStreamAsync: Asset '{currentAssetName}' created for live output");

                // Wait a moment to ensure the asset is fully created
                await Task.Delay(2000);

                // Create a live output
                currentLiveOutputName = $"output-{streamerId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                Debug.WriteLine($"CreateStreamAsync: Creating live output {currentLiveOutputName}");

                var liveOutput = await mkioClient.LiveOutputs.CreateAsync(
                    currentLiveEventName,
                    currentLiveOutputName,
                    new LiveOutputProperties
                    {
                        ArchiveWindowLength = new TimeSpan(0, 5, 0),
                        AssetName = currentAssetName
                    });

                Debug.WriteLine($"CreateStreamAsync: Live output {currentLiveOutputName} created successfully");

                // Wait a moment to ensure the live output is fully created
                await Task.Delay(2000);

                // Create a streaming locator for clear streaming
                currentLocatorName = mkioConfig.GenerateLocatorName(streamerId);
                Debug.WriteLine($"CreateStreamAsync: Creating streaming locator {currentLocatorName}");

                var locator = await mkioClient.StreamingLocators.CreateAsync(
                    currentLocatorName,
                    new StreamingLocatorProperties
                    {
                        AssetName = currentAssetName,
                        StreamingPolicyName = PredefinedStreamingPolicy.ClearStreamingOnly
                    });

                Debug.WriteLine($"CreateStreamAsync: Streaming locator {currentLocatorName} created successfully");

                // Wait a moment for the locator to be fully created before trying to get the URLs
                await Task.Delay(3000);

                // Ensure we have a streaming endpoint
                await EnsureStreamingEndpointAvailableAsync();

                // Create a StreamInfo object to represent this stream
                currentStream = new StreamInfo
                {
                    Id = currentLiveEventName, // Use the live event name as the ID
                    Title = title,
                    Description = description,
                    StreamerName = currentUser.DisplayName,
                    StreamerUserId = currentUser.UserId,
                    Game = game,
                    Tags = tags ?? new List<string>(),
                    IsLive = false, // Not live yet
                    StartTime = null, // Will be set when the stream starts
                    EndTime = null,
                    DurationSeconds = 0,
                    ThumbnailUrl = null, // Will be generated later
                    IngestUrl = liveEvent.Properties.Input.Endpoints.FirstOrDefault()?.Url,
                    StreamKey = "stream", // Standard suffix for RTMP
                    ViewerCount = 0,
                    Quality = "HD", // Default quality
                    HasBrainData = true
                };

                Debug.WriteLine($"CreateStreamAsync: Stream object created with ID {currentStream.Id}");

                // Set the playback URL based on the streaming locator
                // This might fail, but we're handling that in the method
                await UpdatePlaybackUrlAsync();

                // Ensure we're still in Idle state before returning
                Debug.WriteLine("CreateStreamAsync: Ensuring Idle state before returning");
                Status = StreamingStatus.Idle;

                return currentStream;
            }
            catch (Exception ex)
            {
                Status = StreamingStatus.Error;
                Debug.WriteLine($"Error creating stream: {ex.Message}");
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
                // Check if this is the current stream
                if (currentStream == null || currentStream.Id != streamId)
                {
                    throw new InvalidOperationException("Cannot update a stream that is not currently active");
                }

                // Update the stream info
                if (!string.IsNullOrEmpty(title))
                    currentStream.Title = title;

                if (!string.IsNullOrEmpty(description))
                    currentStream.Description = description;

                if (!string.IsNullOrEmpty(game))
                    currentStream.Game = game;

                if (tags != null)
                    currentStream.Tags = tags;

                // Note: In MK.IO, we can't directly update the live event properties after creation
                // So we just update our local StreamInfo object

                return currentStream;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating stream: {ex.Message}");
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

            try
            {
                // This is where the issue is - we need to make sure status is properly reset
                // and check it more carefully

                // First, check if we're already in the streaming or starting state
                if (Status == StreamingStatus.Streaming)
                {
                    throw new InvalidOperationException("A stream is already in progress");
                }

                if (Status == StreamingStatus.Starting)
                {
                    // If we're already in Starting state but it's for a different stream,
                    // we should reset first
                    if (currentStream?.Id != streamId)
                    {
                        Debug.WriteLine($"StartStreamingAsync: Already in Starting state but for a different stream. Resetting first.");
                        // Force reset status before trying to start new stream
                        await ResetStatusAsync();
                        // Add a delay to ensure reset completes
                        await Task.Delay(2000);
                    }
                    else
                    {
                        Debug.WriteLine($"StartStreamingAsync: Stream {streamId} is already starting.");
                        throw new InvalidOperationException("Stream is already starting");
                    }
                }

                Debug.WriteLine($"StartStreamingAsync: Starting stream {streamId}");

                // Now we can update status to Starting
                Status = StreamingStatus.Starting;

                // Check if this is the current stream
                if (currentStream == null || currentStream.Id != streamId)
                {
                    Debug.WriteLine($"StartStreamingAsync: Cannot start stream {streamId} - not current stream");
                    throw new InvalidOperationException("Cannot start a stream that was not created with CreateStreamAsync");
                }

                // Start the live event
                Debug.WriteLine($"StartStreamingAsync: Starting live event: {currentLiveEventName}");
                try
                {
                    await mkioClient.LiveEvents.StartAsync(currentLiveEventName);
                    Debug.WriteLine("StartStreamingAsync: Successfully sent StartAsync command to MK.IO");
                }
                catch (Exception ex)
                {
                    // If we get an error about the stream already running, this is actually okay
                    if (ex.Message.Contains("already running") || ex.Message.Contains("already started"))
                    {
                        Debug.WriteLine("StartStreamingAsync: Live event appears to be already running - continuing");
                    }
                    else
                    {
                        Debug.WriteLine($"StartStreamingAsync: Error starting live event: {ex.Message}");
                        // For other errors, rethrow
                        throw;
                    }
                }

                // Create a new cancellation token source for this streaming session
                streamingCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Update stream info
                currentStream.IsLive = true;
                currentStream.StartTime = DateTime.Now;

                // Start the statistics timer
                StartStatisticsPolling();

                Status = StreamingStatus.Streaming;
                Debug.WriteLine($"StartStreamingAsync: Stream {streamId} started successfully");
                return true;
            }
            catch (Exception ex)
            {
                Status = StreamingStatus.Error;
                Debug.WriteLine($"StartStreamingAsync: Error starting stream: {ex.Message}");
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

                // Check if this is the current stream
                if (currentStream == null || currentStream.Id != streamId)
                {
                    throw new InvalidOperationException("Cannot stop a stream that is not currently active");
                }

                // First delete the live output to ensure the asset is finalized
                if (!string.IsNullOrEmpty(currentLiveOutputName))
                {
                    Debug.WriteLine($"Deleting live output: {currentLiveOutputName}");
                    await mkioClient.LiveOutputs.DeleteAsync(currentLiveEventName, currentLiveOutputName);
                    currentLiveOutputName = null;
                }

                // Then stop the live event
                if (!string.IsNullOrEmpty(currentLiveEventName))
                {
                    Debug.WriteLine($"Stopping live event: {currentLiveEventName}");
                    await mkioClient.LiveEvents.StopAsync(currentLiveEventName);
                }

                // Update stream info
                currentStream.IsLive = false;
                currentStream.EndTime = DateTime.Now;
                currentStream.DurationSeconds = (long)(currentStream.EndTime.Value - currentStream.StartTime.Value).TotalSeconds;

                Status = StreamingStatus.Idle;
                return true;
            }
            catch (Exception ex)
            {
                Status = StreamingStatus.Error;
                Debug.WriteLine($"Error stopping stream: {ex.Message}");
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
                // Check if this is the current stream
                if (currentStream == null || currentStream.Id != streamId)
                {
                    throw new InvalidOperationException("Cannot create VOD from a stream that is not currently active");
                }

                // Make sure the stream has ended
                if (currentStream.IsLive)
                {
                    throw new InvalidOperationException("Cannot create VOD from an active stream. Stop the stream first.");
                }

                // Get user information
                var currentUser = accountService.CurrentUser;
                if (currentUser == null)
                {
                    throw new InvalidOperationException("User must be signed in to create a VOD");
                }

                string streamerId = currentUser.UserId ?? "anonymous";
                string vodTitle = title ?? $"{currentStream.Title} (VOD)";

                // Create an MP4 asset for the VOD
                string mp4AssetName = mkioConfig.GenerateAssetName("vod", streamerId);
                await mkioClient.Assets.CreateOrUpdateAsync(
                    mp4AssetName,
                    null,
                    mkioConfig.StorageName,
                    $"VOD asset for stream: {vodTitle}",
                    AssetContainerDeletionPolicyType.Delete
                );

                Debug.WriteLine($"VOD asset '{mp4AssetName}' created");

                // Create or update the converter transform
                string transformName = MKIOConfig.CopyAllBitrateTransformName;
                var preset = mkioConfig.GetConverterPreset();

                await CreateOrUpdateConverterTransformAsync(transformName, preset);

                // Submit the conversion job
                string jobName = mkioConfig.GenerateJobName("vod", streamerId);
                await SubmitJobAsync(transformName, jobName, currentAssetName, mp4AssetName, "*");

                // Wait for the job to finish
                var job = await WaitForJobToFinishAsync(transformName, jobName);

                if (job.Properties.State != JobState.Finished)
                {
                    throw new InvalidOperationException($"VOD conversion job failed: {job.Properties.State}");
                }

                // Create a locator for the VOD
                string vodLocatorName = mkioConfig.GenerateLocatorName($"vod-{streamerId}");
                await mkioClient.StreamingLocators.CreateAsync(
                    vodLocatorName,
                    new StreamingLocatorProperties
                    {
                        AssetName = mp4AssetName,
                        StreamingPolicyName = PredefinedStreamingPolicy.ClearStreamingOnly
                    });

                // Create a StreamInfo object for the VOD
                var vodStream = new StreamInfo
                {
                    Id = mp4AssetName,
                    Title = vodTitle,
                    Description = currentStream.Description,
                    StreamerName = currentStream.StreamerName,
                    StreamerUserId = currentStream.StreamerUserId,
                    Game = currentStream.Game,
                    Tags = currentStream.Tags,
                    IsLive = false,
                    StartTime = currentStream.StartTime,
                    EndTime = currentStream.EndTime,
                    DurationSeconds = currentStream.DurationSeconds,
                    ThumbnailUrl = currentStream.ThumbnailUrl,
                    ViewerCount = currentStream.ViewerCount,
                    Quality = currentStream.Quality,
                    HasBrainData = currentStream.HasBrainData
                };

                // Set the playback URL for the VOD
                await SetVodPlaybackUrlAsync(mp4AssetName, vodLocatorName, vodStream);

                return vodStream;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating VOD: {ex.Message}");
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
                var streams = new List<StreamInfo>();

                // Get all live events
                var liveEvents = await mkioClient.LiveEvents.ListAsync();

                foreach (var liveEvent in liveEvents)
                {
                    // Skip events that aren't running if we only want live streams
                    if (includeLiveOnly && liveEvent.Properties.ResourceState != LiveEventResourceState.Running)
                    {
                        continue;
                    }

                    // Get the live outputs for this event
                    var liveOutputs = await mkioClient.LiveOutputs.ListAsync(liveEvent.Name);

                    foreach (var liveOutput in liveOutputs)
                    {
                        // Filter by game if specified
                        if (!string.IsNullOrEmpty(game) && !liveOutput.Name.Contains(game, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Filter by search term if specified
                        if (!string.IsNullOrEmpty(search) && !liveOutput.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Try to get the asset
                        try
                        {
                            var asset = await mkioClient.Assets.GetAsync(liveOutput.Properties.AssetName);

                            // Create a StreamInfo object for this live output
                            var stream = new StreamInfo
                            {
                                Id = liveEvent.Name,
                                Title = asset.Properties.Description ?? liveOutput.Properties.AssetName,
                                Description = asset.Properties.Description,
                                StreamerName = ExtractStreamerNameFromAsset(asset.Name),
                                StreamerUserId = null,
                                Game = game ?? ExtractGameInfoFromAsset(asset.Name),
                                Tags = new List<string>(),
                                IsLive = liveEvent.Properties.ResourceState == LiveEventResourceState.Running,
                                StartTime = liveEvent.Properties.ResourceState == LiveEventResourceState.Running ?
                                    DateTime.UtcNow.AddHours(-1) : null, // Estimate
                                EndTime = null,
                                DurationSeconds = 0,
                                ThumbnailUrl = null,
                                ViewerCount = new Random().Next(10, 100), // Simulated for now
                                Quality = "HD",
                                HasBrainData = true // Assume all NeuroSpectator streams have brain data
                            };

                            // Get the streaming locators for this asset - CORRECT WAY
                            var assetLocators = await mkioClient.Assets.ListStreamingLocatorsAsync(asset.Name);

                            if (assetLocators.Any())
                            {
                                var locator = assetLocators.First();

                                // Now we have the locator name, we can get the streaming URLs
                                await SetStreamPlaybackUrlAsync(asset.Name, locator.Name, stream);
                            }

                            streams.Add(stream);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error getting asset for live output {liveOutput.Name}: {ex.Message}");
                            // Continue to the next live output
                        }
                    }
                }

                return streams;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting available streams: {ex.Message}");
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
                // Check if this is the current stream
                if (currentStream != null && currentStream.Id == streamId)
                {
                    return currentStream;
                }

                // Try to get the live event
                try
                {
                    var liveEvent = await mkioClient.LiveEvents.GetAsync(streamId);

                    // Get the live outputs for this event
                    var liveOutputs = await mkioClient.LiveOutputs.ListAsync(liveEvent.Name);

                    if (liveOutputs.Any())
                    {
                        var liveOutput = liveOutputs.First();

                        // Get the asset
                        var asset = await mkioClient.Assets.GetAsync(liveOutput.Properties.AssetName);

                        // Create a StreamInfo object for this live output
                        var stream = new StreamInfo
                        {
                            Id = liveEvent.Name,
                            Title = asset.Properties.Description ?? liveOutput.Properties.AssetName,
                            Description = asset.Properties.Description,
                            StreamerName = "Unknown", // Try to parse streamer name from asset name
                            StreamerUserId = null,
                            Game = null,
                            Tags = new List<string>(),
                            IsLive = liveEvent.Properties.ResourceState == LiveEventResourceState.Running,
                            StartTime = liveEvent.Properties.ResourceState == LiveEventResourceState.Running ?
                                DateTime.UtcNow.AddHours(-1) : null, // Estimate
                            EndTime = null,
                            DurationSeconds = 0,
                            ThumbnailUrl = null,
                            ViewerCount = 0,
                            Quality = "HD",
                            HasBrainData = true // Assume all NeuroSpectator streams have brain data
                        };

                        // Get the streaming locators for this asset - CORRECT WAY
                        var assetLocators = await mkioClient.Assets.ListStreamingLocatorsAsync(asset.Name);

                        // The return is already a list of locators, no need to access .StreamingLocators property
                        if (assetLocators.Any())
                        {
                            var locator = assetLocators.First();

                            // Set the playback URL - using locator.Name
                            await SetStreamPlaybackUrlAsync(asset.Name, locator.Name, stream);
                        }

                        return stream;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting live event: {ex.Message}");
                    // If not found as a live event, try as an asset (VOD)
                    try
                    {
                        var asset = await mkioClient.Assets.GetAsync(streamId);

                        // Create a StreamInfo object for this VOD
                        var stream = new StreamInfo
                        {
                            Id = asset.Name,
                            Title = asset.Properties.Description ?? asset.Name,
                            Description = asset.Properties.Description,
                            StreamerName = "Unknown", // Try to parse streamer name from asset name
                            StreamerUserId = null,
                            Game = null,
                            Tags = new List<string>(),
                            IsLive = false,
                            StartTime = asset.Properties.Created,
                            EndTime = asset.Properties.LastModified,
                            DurationSeconds = 0, // Unknown duration
                            ThumbnailUrl = null,
                            ViewerCount = 0,
                            Quality = "HD",
                            HasBrainData = true // Assume all NeuroSpectator streams have brain data
                        };

                        // Get the streaming locators for this asset - CORRECT WAY
                        var assetLocators = await mkioClient.Assets.ListStreamingLocatorsAsync(asset.Name);

                        if (assetLocators.Any())
                        {
                            var locator = assetLocators.First();

                            // Set the playback URL - using locator.Name
                            await SetStreamPlaybackUrlAsync(asset.Name, locator.Name, stream);
                        }

                        return stream;
                    }
                    catch (Exception assetEx)
                    {
                        Debug.WriteLine($"Error getting asset: {assetEx.Message}");
                        // Not found as asset either
                        throw new InvalidOperationException($"Stream not found: {streamId}", assetEx);
                    }
                }

                throw new InvalidOperationException($"Stream not found: {streamId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting stream: {ex.Message}");
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
                var vods = new List<StreamInfo>();

                // Get all assets
                var assets = await mkioClient.Assets.ListAsync();

                foreach (var asset in assets)
                {
                    // Skip assets that are not VODs (use labels or naming convention to identify)
                    if (!IsVodAsset(asset))
                    {
                        continue;
                    }

                    // Filter by user ID if specified
                    if (!string.IsNullOrEmpty(userId) && !asset.Name.Contains(userId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Filter by game if specified
                    if (!string.IsNullOrEmpty(game) &&
                        (asset.Properties.Description == null ||
                        !asset.Properties.Description.Contains(game, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Filter by search term if specified
                    if (!string.IsNullOrEmpty(search) &&
                        !asset.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                        (asset.Properties.Description == null ||
                        !asset.Properties.Description.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Create a StreamInfo object for this VOD
                    var stream = new StreamInfo
                    {
                        Id = asset.Name,
                        Title = asset.Properties.Description ?? asset.Name,
                        Description = asset.Properties.Description,
                        StreamerName = ExtractStreamerNameFromAsset(asset.Name),
                        StreamerUserId = null,
                        Game = ExtractGameInfoFromAsset(asset.Name),
                        Tags = new List<string>(),
                        IsLive = false,
                        StartTime = asset.Properties.Created,
                        EndTime = asset.Properties.LastModified,
                        DurationSeconds = (long)(asset.Properties.LastModified - asset.Properties.Created).Value.TotalSeconds,
                        ThumbnailUrl = null,
                        ViewerCount = 0,
                        Quality = "HD",
                        HasBrainData = true // Assume all NeuroSpectator streams have brain data
                    };

                    // Get the streaming locators for this asset - CORRECT WAY
                    var assetLocators = await mkioClient.Assets.ListStreamingLocatorsAsync(asset.Name);

                    if (assetLocators.Any())
                    {
                        var locator = assetLocators.First();

                        // Now we have the locator name, we can get the streaming URLs
                        await SetStreamPlaybackUrlAsync(asset.Name, locator.Name, stream);
                    }

                    vods.Add(stream);
                }

                return vods;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting available VODs: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Determines if an asset is a VOD
        /// </summary>
        private bool IsVodAsset(AssetSchema asset)
        {
            // Check if this is a VOD asset based on naming convention or labels
            return asset.Name.Contains("vod", StringComparison.OrdinalIgnoreCase) ||
                   (asset.Labels != null && asset.Labels.ContainsKey("typeAsset") &&
                    asset.Labels["typeAsset"].Equals("encoded", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Extracts streamer name from asset name using naming conventions
        /// </summary>
        private string ExtractStreamerNameFromAsset(string assetName)
        {
            // Try to extract streamer name from asset name based on your naming conventions
            // Example: neurospectator-live-username-12345 -> username
            if (assetName.Contains("-"))
            {
                var parts = assetName.Split('-');
                if (parts.Length >= 3)
                {
                    return parts[2]; // Adjust index based on your naming convention
                }
            }
            return "Unknown Streamer";
        }

        /// <summary>
        /// Extracts game info from asset name or description
        /// </summary>
        private string ExtractGameInfoFromAsset(string assetName)
        {
            // In a real implementation, you might store game info in labels or description
            // For now, return default value
            return "Game";
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
                // Currently, MK.IO doesn't directly support thumbnail uploads via API
                // In a real implementation, we would:
                // 1. Upload the thumbnail to blob storage
                // 2. Update the asset metadata with the thumbnail URL

                // For now, we'll just update the current stream's thumbnail URL if this is the current stream
                if (currentStream != null && currentStream.Id == streamId)
                {
                    // Simulate a thumbnail URL
                    currentStream.ThumbnailUrl = $"https://{mkioConfig.StorageName}.blob.core.windows.net/thumbnails/{streamId}.jpg";
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error uploading thumbnail: {ex.Message}");
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
                // Currently, MK.IO doesn't directly support thumbnail generation via API
                // In a real implementation, we would use the thumbnail transform to generate thumbnails

                // For now, we'll just update the current stream's thumbnail URL if this is the current stream
                if (currentStream != null && currentStream.Id == streamId)
                {
                    // Simulate a thumbnail URL
                    currentStream.ThumbnailUrl = $"https://{mkioConfig.StorageName}.blob.core.windows.net/thumbnails/{streamId}.jpg";
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating thumbnail: {ex.Message}");
                StreamingError?.Invoke(this, ex);
                throw;
            }
        }

        #region Helper Methods

        /// <summary>
        /// Updates the playback URL for the current stream
        /// </summary>
        private async Task UpdatePlaybackUrlAsync()
        {
            try
            {
                if (currentStream == null || string.IsNullOrEmpty(currentLocatorName))
                {
                    return;
                }

                Debug.WriteLine($"Attempting to update playback URL for locator: {currentLocatorName}");

                // Add a delay to give time for the container to be fully created
                await Task.Delay(5000);  // 5 second delay

                try
                {
                    // Get the playback URLs
                    var paths = await mkioClient.StreamingLocators.ListUrlPathsAsync(currentLocatorName);
                    var streamingEndpoints = await mkioClient.StreamingEndpoints.ListAsync();

                    if (streamingEndpoints.Any() && paths.StreamingPaths.Any())
                    {
                        // Find the first suitable path
                        var path = paths.StreamingPaths.FirstOrDefault();

                        if (path != null && path.Paths.Any())
                        {
                            var endpoint = streamingEndpoints.First();
                            currentStream.PlaybackUrl = $"https://{endpoint.Properties.HostName}{path.Paths.First()}";
                            Debug.WriteLine($"Playback URL updated: {currentStream.PlaybackUrl}");
                        }
                        else
                        {
                            Debug.WriteLine("No streaming paths found in the response");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("No streaming endpoints or paths available");
                    }
                }
                catch (Exception ex)
                {
                    // Instead of throwing, just log the error and continue
                    Debug.WriteLine($"Non-critical error updating playback URL: {ex.Message}");
                    // Don't rethrow - the playback URL is not critical for starting the stream
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdatePlaybackUrlAsync: {ex.Message}");
                // Don't throw here - just log the error
            }
        }

        /// <summary>
        /// Sets the playback URL for a specific stream
        /// </summary>
        private async Task SetStreamPlaybackUrlAsync(string assetName, string locatorName, StreamInfo stream)
        {
            try
            {
                // Get the playback URLs
                var paths = await mkioClient.StreamingLocators.ListUrlPathsAsync(locatorName);
                var streamingEndpoints = await mkioClient.StreamingEndpoints.ListAsync();

                if (streamingEndpoints.Any() && paths.StreamingPaths.Any())
                {
                    // Find the first suitable path
                    var path = paths.StreamingPaths.FirstOrDefault();

                    if (path != null && path.Paths.Any())
                    {
                        var endpoint = streamingEndpoints.First();
                        stream.PlaybackUrl = $"https://{endpoint.Properties.HostName}{path.Paths.First()}";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting playback URL: {ex.Message}");
                // Don't throw here - just log the error
            }
        }

        /// <summary>
        /// Sets the playback URL for a VOD
        /// </summary>
        private async Task SetVodPlaybackUrlAsync(string assetName, string locatorName, StreamInfo vodStream)
        {
            try
            {
                // Get the playback URLs
                var paths = await mkioClient.StreamingLocators.ListUrlPathsAsync(locatorName);
                var streamingEndpoints = await mkioClient.StreamingEndpoints.ListAsync();

                if (streamingEndpoints.Any() && paths.StreamingPaths.Any())
                {
                    // Find the first suitable path
                    var path = paths.StreamingPaths.FirstOrDefault();

                    if (path != null && path.Paths.Any())
                    {
                        var endpoint = streamingEndpoints.First();
                        vodStream.PlaybackUrl = $"https://{endpoint.Properties.HostName}{path.Paths.First()}";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting VOD playback URL: {ex.Message}");
                // Don't throw here - just log the error
            }
        }

        /// <summary>
        /// Ensures a streaming endpoint is available and running
        /// </summary>
        private async Task EnsureStreamingEndpointAvailableAsync()
        {
            try
            {
                var streamingEndpoints = await mkioClient.StreamingEndpoints.ListAsync();

                if (streamingEndpoints.Any())
                {
                    // Use the first available streaming endpoint
                    var endpoint = streamingEndpoints.First();
                    currentStreamingEndpointName = endpoint.Name;

                    // If the endpoint is not running, start it
                    if (endpoint.Properties.ResourceState != StreamingEndpointResourceState.Running)
                    {
                        Debug.WriteLine($"Starting streaming endpoint '{endpoint.Name}'");
                        await mkioClient.StreamingEndpoints.StartAsync(endpoint.Name);
                    }
                }
                else
                {
                    // Create a new streaming endpoint
                    var locationName = await mkioClient.Account.GetSubscriptionLocationAsync();

                    if (locationName != null)
                    {
                        var endpointName = $"endpoint-{Guid.NewGuid():N}".Substring(0, 24);

                        var endpoint = await mkioClient.StreamingEndpoints.CreateAsync(
                            endpointName,
                            locationName.Name,
                            new StreamingEndpointProperties
                            {
                                Description = "Streaming endpoint for NeuroSpectator"
                            },
                            true // Auto-start
                        );

                        currentStreamingEndpointName = endpoint.Name;
                        Debug.WriteLine($"Streaming endpoint '{endpoint.Name}' created and started");
                    }
                    else
                    {
                        throw new InvalidOperationException("No location found for MK.IO subscription");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring streaming endpoint: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates or updates a converter transform
        /// </summary>
        private async Task<TransformSchema> CreateOrUpdateConverterTransformAsync(string transformName, ConverterNamedPreset preset)
        {
            try
            {
                var transform = await mkioClient.Transforms.CreateOrUpdateAsync(
                    transformName,
                    new TransformProperties
                    {
                        Description = $"Converter with {preset} preset",
                        Outputs = new List<TransformOutput>
                        {
                            new TransformOutput
                            {
                                Preset = new BuiltInAssetConverterPreset(preset),
                                RelativePriority = TransformOutputPriorityType.Normal
                            }
                        }
                    }
                );

                Debug.WriteLine($"Transform '{transform.Name}' created/updated");
                return transform;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating/updating transform: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Submits a job to convert an asset
        /// </summary>
        private async Task<JobSchema> SubmitJobAsync(string transformName, string jobName, string inputAssetName, string outputAssetName, string fileName)
        {
            try
            {
                var job = await mkioClient.Jobs.CreateAsync(
                    transformName,
                    jobName,
                    new JobProperties
                    {
                        Description = $"Job to process '{inputAssetName}' to '{outputAssetName}' with '{transformName}' transform",
                        Priority = JobPriorityType.Normal,
                        Input = new JobInputAsset(
                            inputAssetName,
                            new List<string> { fileName }
                        ),
                        Outputs = new List<JobOutputAsset>
                        {
                            new JobOutputAsset
                            {
                                AssetName = outputAssetName
                            }
                        }
                    }
                );

                Debug.WriteLine($"Job '{job.Name}' submitted");
                return job;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error submitting job: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Waits for a job to finish
        /// </summary>
        private async Task<JobSchema> WaitForJobToFinishAsync(string transformName, string jobName)
        {
            const int SleepIntervalMs = 5000;
            JobSchema job;

            do
            {
                await Task.Delay(SleepIntervalMs);
                job = await mkioClient.Jobs.GetAsync(transformName, jobName);
                Debug.WriteLine($"Job '{jobName}' state: {job.Properties.State}" +
                    (job.Properties.Outputs.First().Progress != null ?
                        $" Progress: {job.Properties.Outputs.First().Progress}%" :
                        string.Empty));
            }
            while (job.Properties.State == JobState.Queued ||
                   job.Properties.State == JobState.Scheduled ||
                   job.Properties.State == JobState.Processing);

            return job;
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
                // Currently, MK.IO doesn't provide real-time streaming statistics via API
                // In a real implementation, we would get stats from the streaming endpoint

                // For now, we'll simulate statistics
                var random = new Random();

                // Create statistics object
                var statistics = new StreamingStatistics
                {
                    ViewerCount = random.Next(10, 100),
                    DurationSeconds = (long)(DateTime.Now - currentStream.StartTime.Value).TotalSeconds,
                    CurrentBitrate = 3_000_000 + random.Next(-500_000, 500_000),
                    CurrentFps = 29.97f + (float)random.NextDouble(),
                    CpuUsage = 20f + (float)random.Next(0, 30),
                    DroppedFrames = random.Next(0, 10),
                    Timestamp = DateTime.Now
                };

                // Update current stream
                currentStream.ViewerCount = statistics.ViewerCount;

                // Raise event
                StatisticsUpdated?.Invoke(this, statistics);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error polling statistics: {ex.Message}");
                // Don't raise error event for statistics issues
            }
        }

        #endregion

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
                }

                isDisposed = true;
            }
        }

        /// <summary>
        /// Resets the streaming service status to Idle
        /// </summary>
        public async Task ResetStatusAsync()
        {
            try
            {
                Debug.WriteLine("MKIOStreamingService: Beginning status reset");

                // Stop statistics polling
                StopStatisticsPolling();

                // Cancel and dispose streaming token source
                if (streamingCancellationSource != null)
                {
                    try
                    {
                        streamingCancellationSource.Cancel();
                        streamingCancellationSource.Dispose();
                    }
                    catch (ObjectDisposedException) { /* Already disposed, ignore */ }
                    streamingCancellationSource = null;
                }

                // Reset all state variables
                currentLiveEventName = null;
                currentLiveOutputName = null;
                currentAssetName = null;
                currentLocatorName = null;

                // Reset stream state
                if (currentStream != null)
                {
                    // Check if the stream is actually running and needs to be stopped
                    if (currentStream.IsLive || Status == StreamingStatus.Streaming)
                    {
                        try
                        {
                            string id = currentStream.Id;
                            Debug.WriteLine($"MKIOStreamingService: Stopping active stream {id}");

                            // First set stream to not live to avoid recursive call issues
                            currentStream.IsLive = false;

                            try
                            {
                                // Directly access mkioClient to stop the live event without going through potentially recursive call
                                if (!string.IsNullOrEmpty(currentLiveEventName))
                                {
                                    Debug.WriteLine($"MKIOStreamingService: Stopping live event {currentLiveEventName} directly");
                                    await mkioClient.LiveEvents.StopAsync(currentLiveEventName);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error stopping live event directly: {ex.Message}");
                                // Continue with reset even if stop fails
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error stopping existing stream during reset: {ex.Message}");
                            // Continue with reset even if stop fails
                        }
                    }

                    // Now nullify the stream
                    currentStream = null;
                }

                // Wait a moment to ensure all async operations complete
                await Task.Delay(1000);

                // Finally, explicitly set status to Idle (this will trigger the StatusChanged event)
                Debug.WriteLine("MKIOStreamingService: Setting status to Idle");
                Status = StreamingStatus.Idle;

                Debug.WriteLine("MKIOStreamingService: Status reset completed successfully");
                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ResetStatusAsync: {ex.Message}");
                // Even if there's an error, force status to Idle
                Status = StreamingStatus.Idle;
            }
        }
    }
}
