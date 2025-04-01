using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using NeuroSpectator.Services.Streaming;

namespace NeuroSpectator.Controls
{
    /// <summary>
    /// Implementation of the MK.IO player component using WebView
    /// </summary>
    public class MKIOPlayer : ContentView, IMKIOPlayerService
    {
        private readonly WebView webView;
        private bool isInitialized;
        private string streamId;
        private bool isLive;

        /// <summary>
        /// Event fired when the player state changes
        /// </summary>
        public event EventHandler<string> PlayerStateChanged;

        /// <summary>
        /// Event fired when an error occurs in the player
        /// </summary>
        public event EventHandler<Exception> PlayerError;

        /// <summary>
        /// Creates a new instance of the MKIOPlayer
        /// </summary>
        public MKIOPlayer()
        {
            webView = new WebView();
            webView.Navigated += WebView_Navigated;

            Content = webView;
        }

        /// <summary>
        /// Event handler for when the WebView navigates
        /// </summary>
        private void WebView_Navigated(object sender, WebNavigatedEventArgs e)
        {
            if (e.Result == WebNavigationResult.Success)
            {
                isInitialized = true;
                PlayerStateChanged?.Invoke(this, "ready");
            }
            else
            {
                isInitialized = false;
                PlayerError?.Invoke(this, new Exception($"Error loading player: {e.Result}"));
            }
        }

        /// <summary>
        /// Initializes the player with a stream URL
        /// </summary>
        /// <summary>
        /// Initializes the player with a stream URL
        /// </summary>
        public async Task InitializePlayerAsync(string streamId, bool isLive = true)
        {
            this.streamId = streamId;
            this.isLive = isLive;

            try
            {
                // Build the HTML with the MK.IO player
                var html = BuildPlayerHtml(streamId, isLive);

                // Load the HTML into the WebView
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    webView.Source = new HtmlWebViewSource
                    {
                        Html = html
                    };
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing player: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Plays the stream
        /// </summary>
        public async Task PlayAsync()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                await webView.EvaluateJavaScriptAsync("player.play();");
                PlayerStateChanged?.Invoke(this, "playing");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing stream: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Pauses the stream
        /// </summary>
        public async Task PauseAsync()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                await webView.EvaluateJavaScriptAsync("player.pause();");
                PlayerStateChanged?.Invoke(this, "paused");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pausing stream: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Seeks to a specific position in the stream (for VODs)
        /// </summary>
        public async Task SeekAsync(TimeSpan position)
        {
            if (!isInitialized || isLive)
            {
                throw new InvalidOperationException("Player not initialized or stream is live");
            }

            try
            {
                await webView.EvaluateJavaScriptAsync($"player.currentTime({position.TotalSeconds});");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeking stream: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Sets the volume
        /// </summary>
        public async Task SetVolumeAsync(double volume)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                await webView.EvaluateJavaScriptAsync($"player.volume({Math.Clamp(volume, 0, 1)});");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting volume: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the current playback state
        /// </summary>
        public async Task<bool> IsPlayingAsync()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                var result = await webView.EvaluateJavaScriptAsync("player.paused() ? 'false' : 'true';");
                return result?.Trim('"') == "true";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting playback state: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the current position in the stream
        /// </summary>
        public async Task<TimeSpan> GetCurrentPositionAsync()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                var result = await webView.EvaluateJavaScriptAsync("player.currentTime();");
                if (double.TryParse(result?.Trim('"'), out var seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }

                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting current position: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the duration of the stream
        /// </summary>
        public async Task<TimeSpan> GetDurationAsync()
        {
            if (!isInitialized || isLive)
            {
                throw new InvalidOperationException("Player not initialized or stream is live");
            }

            try
            {
                var result = await webView.EvaluateJavaScriptAsync("player.duration();");
                if (double.TryParse(result?.Trim('"'), out var seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }

                return TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting duration: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets whether the player is muted
        /// </summary>
        public async Task<bool> IsMutedAsync()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                var result = await webView.EvaluateJavaScriptAsync("player.muted();");
                return result?.Trim('"') == "true";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting muted state: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Sets whether the player is muted
        /// </summary>
        public async Task SetMutedAsync(bool muted)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                await webView.EvaluateJavaScriptAsync($"player.muted({(muted ? "true" : "false")});");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting muted state: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the current quality
        /// </summary>
        public async Task<string> GetCurrentQualityAsync()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                var result = await webView.EvaluateJavaScriptAsync("player.getQuality();");
                return result?.Trim('"');
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting current quality: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the available qualities
        /// </summary>
        public async Task<List<string>> GetAvailableQualitiesAsync()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                var result = await webView.EvaluateJavaScriptAsync("JSON.stringify(player.getQualities());");
                if (string.IsNullOrEmpty(result))
                {
                    return new List<string>();
                }

                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting available qualities: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Sets the player quality
        /// </summary>
        public async Task SetQualityAsync(string quality)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                await webView.EvaluateJavaScriptAsync($"player.setQuality('{quality}');");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting quality: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Builds the HTML for the MK.IO player
        /// </summary>
        private string BuildPlayerHtml(string streamId, bool isLive)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>MK.IO Player</title>
    <style>
        html, body {{ margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; }}
        #player-container {{ width: 100%; height: 100%; background-color: #000; }}
    </style>
    <script src='https://cdnjs.cloudflare.com/ajax/libs/mediakind-player/1.0.0/player.min.js'></script>
</head>
<body>
    <div id='player-container'></div>
    <script>
        // Initialize the player when the page loads
        document.addEventListener('DOMContentLoaded', function() {{
            // Create the player
            var player = new MediaKindPlayer({{
                container: document.getElementById('player-container'),
                stream: {{
                    id: '{streamId}',
                    type: '{(isLive ? "live" : "vod")}'
                }},
                autoplay: true,
                muted: true,
                controls: true,
                responsive: true,
                posterImage: '',
                settings: {{
                    quality: true,
                    speed: {(isLive ? "false" : "true")},
                    volume: true
                }}
            }});

            // Register event listeners for player events
            player.on('ready', function() {{
                console.log('Player is ready');
                window.webkit.messageHandlers.playerState.postMessage('ready');
            }});

            player.on('playing', function() {{
                console.log('Player is playing');
                window.webkit.messageHandlers.playerState.postMessage('playing');
            }});

            player.on('pause', function() {{
                console.log('Player is paused');
                window.webkit.messageHandlers.playerState.postMessage('paused');
            }});

            player.on('ended', function() {{
                console.log('Playback ended');
                window.webkit.messageHandlers.playerState.postMessage('ended');
            }});

            player.on('error', function(error) {{
                console.error('Player error:', error);
                window.webkit.messageHandlers.playerError.postMessage(error);
            }});

            // Make the player available globally
            window.player = player;
        }});
    </script>
</body>
</html>";
        }
    }
}