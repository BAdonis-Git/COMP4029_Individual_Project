﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeuroSpectator.Controls;
using NeuroSpectator.Models.Stream;
using NeuroSpectator.Services.Streaming;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace NeuroSpectator.PageModels
{
    /// <summary>
    /// View model for the StreamSpectatorPage
    /// </summary>
    public partial class StreamSpectatorPageModel : ObservableObject, IDisposable
    {
        private readonly IMKIOStreamingService streamingService;
        private MKIOPlayer player;
        private bool _disposed = false;

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

        [ObservableProperty]
        private string streamId;

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
        }

        /// <summary>
        /// Sets the player instance
        /// </summary>
        public void SetPlayer(MKIOPlayer player)
        {
            this.player = player;

            // Subscribe to player events
            if (player != null)
            {
                player.PlayerStateChanged += OnPlayerStateChanged;
                player.PlayerError += OnPlayerError;
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
                    // Use the StreamId that was set from the query parameter
                    if (string.IsNullOrEmpty(StreamId))
                    {
                        // As a fallback, try to get it from navigation
                        StreamId = await GetStreamIdFromNavigationAsync();
                    }

                    if (string.IsNullOrEmpty(StreamId))
                    {
                        SetError("No stream ID specified");
                        return;
                    }

                    Debug.WriteLine($"Initializing player with stream ID: {StreamId}");

                    // Load the stream information
                    await LoadStreamAsync(StreamId);

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
        /// Gets the stream ID from navigation parameters as a fallback mechanism
        /// </summary>
        private async Task<string> GetStreamIdFromNavigationAsync()
        {
            try
            {
                // Get the current query string
                var queryString = Shell.Current.CurrentState?.Location?.Query;

                if (!string.IsNullOrEmpty(queryString))
                {
                    // Simple parsing approach for query string
                    if (queryString.Contains("streamId="))
                    {
                        int start = queryString.IndexOf("streamId=") + "streamId=".Length;
                        int end = queryString.IndexOf('&', start);
                        string streamId = end > 0 && end > start ?
                            queryString.Substring(start, end - start) :
                            queryString.Substring(start);

                        // Decode the value
                        streamId = Uri.UnescapeDataString(streamId);
                        Debug.WriteLine($"Found stream ID in query string: {streamId}");
                        return streamId;
                    }
                }

                // Try to get from the full URI path if query parsing failed
                var fullPath = Shell.Current.CurrentState?.Location?.ToString();
                if (!string.IsNullOrEmpty(fullPath))
                {
                    // Check for presence of streamId parameter
                    if (fullPath.Contains("?streamId="))
                    {
                        int start = fullPath.IndexOf("?streamId=") + "?streamId=".Length;
                        int end = fullPath.IndexOf('&', start);
                        string streamId = end > 0 && end > start ?
                            fullPath.Substring(start, end - start) :
                            fullPath.Substring(start);

                        // Decode the value
                        streamId = Uri.UnescapeDataString(streamId);
                        Debug.WriteLine($"Found stream ID in full path: {streamId}");
                        return streamId;
                    }
                }

                // If still don't have a stream ID, the user might have navigated 
                // to this page without parameters
                Debug.WriteLine("No stream ID found in navigation parameters");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting stream ID from navigation: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Loads the stream information and initializes the player
        /// </summary>
        private async Task LoadStreamAsync(string streamId)
        {
            try
            {
                IsLoading = true;
                HasError = false;
                StatusMessage = "Loading stream...";

                // Get stream info from the streaming service
                Stream = await streamingService.GetStreamAsync(streamId);

                if (Stream == null)
                {
                    SetError("Stream not found");
                    return;
                }

                StatusMessage = $"Loaded: {Stream.Title}";

                // Initialize the player
                if (player != null && !string.IsNullOrEmpty(Stream.PlaybackUrl))
                {
                    StatusMessage = "Initializing player...";
                    await player.InitializeWithUrlAsync(Stream.PlaybackUrl, Stream.IsLive);

                    // Set initial mute state
                    await player.SetMutedAsync(true);

                    // Auto-play if it's a live stream
                    if (Stream.IsLive)
                    {
                        await Task.Delay(1000); // Wait for player to initialize
                        await player.PlayAsync();
                        IsPlaying = true;
                        PlayPauseButtonText = "Pause";
                    }

                    IsPlayerReady = true;
                    IsLoading = false;
                    StatusMessage = Stream.IsLive ? "Live stream" : "VOD playback";
                }
                else
                {
                    SetError("Failed to initialize player: No playback URL");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading stream: {ex.Message}");
                SetError($"Failed to load stream: {ex.Message}");
            }
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
            if (player == null || !IsPlayerReady) return;

            try
            {
                if (IsPlaying)
                {
                    await player.PauseAsync();
                    IsPlaying = false;
                    PlayPauseButtonText = "Play";
                    StatusMessage = "Paused";
                }
                else
                {
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
                // Stop player if active
                if (player != null && IsPlayerReady)
                {
                    try
                    {
                        await player.PauseAsync();
                    }
                    catch
                    {
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