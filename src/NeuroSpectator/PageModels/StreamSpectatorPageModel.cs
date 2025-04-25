using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using NeuroSpectator.Controls;
using NeuroSpectator.Models.Stream;
using NeuroSpectator.Services.Streaming;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the StreamSpectatorPage
    /// </summary>
    [QueryProperty(nameof(StreamId), "streamId")]
    public partial class StreamSpectatorPageModel : ObservableObject, IDisposable
    {
        private readonly IMKIOStreamingService streamingService;
        private MKIOPlayer player;
        private bool _disposed = false;
        private string streamId;

        [ObservableProperty]
        private bool isProcessingStreamId = false;

        public string StreamId
        {
            get => streamId;
            set
            {
                if (streamId != value)
                {
                    Debug.WriteLine($"StreamId property set to: {value}");
                    streamId = value;
                    OnPropertyChanged(nameof(StreamId));

                    if (!string.IsNullOrEmpty(value) && !IsProcessingStreamId)
                    {
                        IsProcessingStreamId = true;
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            try
                            {
                                await ProcessStreamIdAsync(value);
                            }
                            finally
                            {
                                IsProcessingStreamId = false;
                            }
                        });
                    }
                }
            }
        }

        #region Observable Properties

        [ObservableProperty]
        private bool isInitialized;

        [ObservableProperty]
        private StreamInfo stream;

        [ObservableProperty]
        private string statusMessage = "Initializing...";

        [ObservableProperty]
        private string selectedQuality = "Auto";

        [ObservableProperty]
        private ObservableCollection<string> qualityOptions = new ObservableCollection<string>
        {
            "1080p",
            "720p",
            "480p",
            "360p",
            "Auto"
        };

        [ObservableProperty]
        private bool isLoading = true;

        [ObservableProperty]
        private bool hasError = false;

        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        private bool isPlayerReady = false;

        [ObservableProperty]
        private bool isPlaying = false;

        [ObservableProperty]
        private bool isMuted = false;

        [ObservableProperty]
        private string playPauseButtonText = "Play";

        #endregion

        #region Commands

        public ICommand TogglePlayCommand { get; }
        public ICommand ToggleMuteCommand { get; }
        public ICommand CloseStreamCommand { get; }

        #endregion

        /// <summary>
        /// Creates a new instance of the StreamSpectatorPageModel class
        /// </summary>
        public StreamSpectatorPageModel(IMKIOStreamingService streamingService)
        {
            this.streamingService = streamingService ?? throw new ArgumentNullException(nameof(streamingService));

            // Initialize default stream info
            Stream = new StreamInfo
            {
                Id = "",
                Title = "Loading...",
                StreamerName = "Unknown",
                IsLive = false,
                ViewerCount = 0
            };

            // Initialize commands
            TogglePlayCommand = new AsyncRelayCommand(TogglePlayAsync);
            ToggleMuteCommand = new AsyncRelayCommand(ToggleMuteAsync);
            CloseStreamCommand = new AsyncRelayCommand(CloseStreamAsync);

            Debug.WriteLine("StreamSpectatorPageModel initialized");
        }

        /// <summary>
        /// Process the stream ID received from QueryProperty
        /// </summary>
        private async Task ProcessStreamIdAsync(string id)
        {
            Debug.WriteLine($"Processing stream ID: {id}");
            try
            {
                // Load the stream with the received ID
                await LoadStreamDirectlyAsync(id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing stream ID: {ex.Message}");
                SetError($"Error processing stream: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the player instance
        /// </summary>
        public void SetPlayer(MKIOPlayer player)
        {
            Debug.WriteLine($"Setting player instance: {player != null}");
            this.player = player;

            // Subscribe to player events
            if (player != null)
            {
                player.PlayerStateChanged += OnPlayerStateChanged;
                player.PlayerError += OnPlayerError;
                Debug.WriteLine("Player events subscribed");

                // If we already have a stream with a playback URL, initialize the player
                if (Stream != null && !string.IsNullOrEmpty(Stream.PlaybackUrl))
                {
                    Debug.WriteLine($"Player set with existing stream: {Stream.Id}, initializing...");
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            await player.InitializeWithUrlAsync(Stream.PlaybackUrl, Stream.IsLive);
                            IsPlayerReady = true;
                            StatusMessage = "Player ready";
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error initializing player with existing stream: {ex.Message}");
                            SetError($"Player initialization error: {ex.Message}");
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Initializes the stream when the page appears
        /// </summary>
        public async Task OnAppearingAsync()
        {
            try
            {
                if (!IsInitialized)
                {
                    Debug.WriteLine($"OnAppearingAsync - StreamId: {StreamId}");
                    // If the StreamId was set via QueryProperty before OnAppearing, it will already be handled
                    // If not, we need to manually process it from navigation parameters as a fallback
                    if (string.IsNullOrEmpty(StreamId))
                    {
                        string navigStreamId = await GetStreamIdFromNavigationAsync();
                        if (!string.IsNullOrEmpty(navigStreamId))
                        {
                            // This will trigger the setter which will load the stream
                            StreamId = navigStreamId;
                        }
                        else
                        {
                            SetError("No stream ID specified");
                        }
                    }

                    IsInitialized = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnAppearingAsync: {ex.Message}");
                SetError($"Failed to load stream: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up resources when the page disappears
        /// </summary>
        public async Task OnDisappearingAsync()
        {
            try
            {
                // Pause playback
                if (player != null && IsPlayerReady)
                {
                    await player.PauseAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnDisappearingAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the stream ID from navigation parameters as a fallback
        /// </summary>
        private async Task<string> GetStreamIdFromNavigationAsync()
        {
            try
            {
                Debug.WriteLine("Attempting to get stream ID from navigation");

                // Get the current query string
                var queryString = Shell.Current.CurrentState?.Location?.Query;
                if (!string.IsNullOrEmpty(queryString))
                {
                    // Parse the query string
                    var queryDictionary = System.Web.HttpUtility.ParseQueryString(queryString);
                    var streamId = queryDictionary["streamId"];

                    if (!string.IsNullOrEmpty(streamId))
                    {
                        Debug.WriteLine($"Found stream ID in query: {streamId}");
                        return streamId;
                    }
                }

                // Alternative approach: parse from the full URL if query parsing failed
                var fullUrl = Shell.Current.CurrentState?.Location?.ToString();
                if (!string.IsNullOrEmpty(fullUrl) && fullUrl.Contains("streamId="))
                {
                    var parts = fullUrl.Split(new[] { "streamId=" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        var streamId = parts[1];
                        if (streamId.Contains("&"))
                        {
                            streamId = streamId.Split('&')[0];
                        }

                        Debug.WriteLine($"Found stream ID in URL: {streamId}");
                        return streamId;
                    }
                }

                // Direct check of QueryProperty
                if (!string.IsNullOrEmpty(StreamId))
                {
                    Debug.WriteLine($"Using existing StreamId from QueryProperty: {StreamId}");
                    return StreamId;
                }

                // If we get here, no stream ID was found
                Debug.WriteLine("No stream ID found in navigation parameters");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting stream ID: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Loads the stream directly using the streamId
        /// </summary>
        private async Task LoadStreamDirectlyAsync(string streamId)
        {
            try
            {
                IsLoading = true;
                HasError = false;
                StatusMessage = "Loading stream...";
                Debug.WriteLine($"Directly loading stream: {streamId}");

                // Get stream info from the streaming service
                try
                {
                    Stream = await streamingService.GetStreamAsync(streamId);
                }
                catch (MK.IO.ApiException apiEx)
                {
                    var errorDetails = $"API Exception: {apiEx.Message}\n" +
                                       $"Status code: {apiEx.StatusCode}\n" +
                                       $"Response body: {apiEx.Response}";

                    Debug.WriteLine(errorDetails);
                    // Log to file for examination
                    File.WriteAllText(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MKIOApiError.log"),
                        errorDetails);
                }
                if (Stream == null)
                {
                    SetError("Stream not found");
                    return;
                }

                StatusMessage = $"Loaded: {Stream.Title}";
                Debug.WriteLine($"Stream loaded successfully: {Stream.Title}, PlaybackUrl: {(string.IsNullOrEmpty(Stream.PlaybackUrl) ? "NOT SET" : "Available")}");

                // Initialize the player
                if (player != null && !string.IsNullOrEmpty(Stream.PlaybackUrl))
                {
                    StatusMessage = "Initializing player...";
                    Debug.WriteLine("Initializing player with stream URL");

                    try
                    {
                        // Wait for the player to initialize fully before proceeding
                        bool initSuccess = await player.InitializeWithUrlAsync(Stream.PlaybackUrl, Stream.IsLive);

                        if (!initSuccess)
                        {
                            Debug.WriteLine("Player initialization returned false");
                            SetError("Player initialization failed");
                            return;
                        }

                        // Player is now initialized, we can interact with it
                        Debug.WriteLine("Player initialization successful");

                        // Set initial mute state after initialization is confirmed
                        await player.SetMutedAsync(true);

                        // Auto-play if it's a live stream
                        if (Stream.IsLive)
                        {
                            Debug.WriteLine("Stream is live - auto-playing");
                            await player.PlayAsync();
                            IsPlaying = true;
                            PlayPauseButtonText = "Pause";
                        }

                        IsPlayerReady = true;
                        IsLoading = false;
                        StatusMessage = Stream.IsLive ? "Live stream" : "VOD playback";
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Player initialization error: {ex.Message}");
                        SetError($"Player initialization error: {ex.Message}");
                    }
                }
                else
                {
                    string errorReason = player == null ? "Player not available" : "No playback URL";
                    Debug.WriteLine($"Failed to initialize player: {errorReason}");
                    SetError($"Failed to initialize player: {errorReason}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading stream directly: {ex.Message}");
                SetError($"Failed to load stream: {ex.Message}");
            }
        }

        /// <summary>
        /// Legacy method to maintain backward compatibility
        /// Now just calls LoadStreamDirectlyAsync
        /// </summary>
        private async Task LoadStreamAsync(string streamId)
        {
            await LoadStreamDirectlyAsync(streamId);
        }

        /// <summary>
        /// Sets an error message and updates UI state
        /// </summary>
        private void SetError(string message)
        {
            ErrorMessage = message;
            HasError = true;
            IsLoading = false;
            StatusMessage = "Error: " + message;
            Debug.WriteLine("Stream error: " + message);
        }

        #region Player Event Handlers

        /// <summary>
        /// Handles player state changes
        /// </summary>
        private void OnPlayerStateChanged(object sender, string state)
        {
            try
            {
                Debug.WriteLine($"Player state changed: {state}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    switch (state)
                    {
                        case "ready":
                            IsPlayerReady = true;
                            StatusMessage = "Player ready";

                            // If we have a Stream but the player just became ready, try to autoplay for live content
                            if (Stream?.IsLive == true && !IsPlaying)
                            {
                                MainThread.BeginInvokeOnMainThread(async () => {
                                    try
                                    {
                                        await Task.Delay(500);
                                        await player.PlayAsync();
                                        IsPlaying = true;
                                        PlayPauseButtonText = "Pause";
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Error auto-playing: {ex.Message}");
                                    }
                                });
                            }
                            break;

                        case "playing":
                            IsPlaying = true;
                            PlayPauseButtonText = "Pause";
                            StatusMessage = Stream?.IsLive == true ? "Live stream playing" : "VOD playing";
                            break;

                        case "paused":
                            IsPlaying = false;
                            PlayPauseButtonText = "Play";
                            StatusMessage = "Paused";
                            break;

                        case "ended":
                            IsPlaying = false;
                            PlayPauseButtonText = "Replay";
                            StatusMessage = "Playback ended";
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling player state change: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles player errors
        /// </summary>
        private void OnPlayerError(object sender, Exception ex)
        {
            Debug.WriteLine($"Player error: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetError($"Player error: {ex.Message}");
            });
        }

        #endregion

        #region Command Implementations

        /// <summary>
        /// Toggles play/pause state
        /// </summary>
        private async Task TogglePlayAsync()
        {
            if (player == null || !IsPlayerReady)
            {
                Debug.WriteLine("Cannot toggle play: player not ready");
                return;
            }

            try
            {
                if (IsPlaying)
                {
                    Debug.WriteLine("Pausing playback");
                    await player.PauseAsync();
                    IsPlaying = false;
                    PlayPauseButtonText = "Play";
                    StatusMessage = "Paused";
                }
                else
                {
                    Debug.WriteLine("Starting playback");
                    await player.PlayAsync();
                    IsPlaying = true;
                    PlayPauseButtonText = "Pause";
                    StatusMessage = Stream?.IsLive == true ? "Live stream playing" : "VOD playing";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling playback: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Toggles mute state
        /// </summary>
        private async Task ToggleMuteAsync()
        {
            if (player == null || !IsPlayerReady) return;

            try
            {
                IsMuted = !IsMuted;
                Debug.WriteLine($"Setting muted state: {IsMuted}");
                await player.SetMutedAsync(IsMuted);
                StatusMessage = IsMuted ? "Muted" : "Unmuted";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error toggling mute: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Closes the stream and navigates back
        /// </summary>
        private async Task CloseStreamAsync()
        {
            try
            {
                Debug.WriteLine("Closing stream and navigating back");
                // Stop player if active
                if (player != null && IsPlayerReady)
                {
                    try
                    {
                        await player.PauseAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Non-critical error pausing player during close: {ex.Message}");
                        // Ignore errors during cleanup
                    }
                }

                // Navigate back
                await Shell.Current.Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error closing stream: {ex.Message}");
                // Still try to navigate back even if there's an error
                await Shell.Current.Navigation.PopAsync();
            }
        }

        #endregion

        /// <summary>
        /// Cleans up resources
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
            if (!_disposed)
            {
                if (disposing)
                {
                    Debug.WriteLine("Disposing StreamSpectatorPageModel");
                    // Unsubscribe from player events
                    if (player != null)
                    {
                        player.PlayerStateChanged -= OnPlayerStateChanged;
                        player.PlayerError -= OnPlayerError;
                    }
                }

                _disposed = true;
            }
        }
    }
}