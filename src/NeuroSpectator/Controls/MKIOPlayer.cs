namespace NeuroSpectator.Controls
{
    /// <summary>
    /// Simple WebView-based player for MK.IO streams
    /// </summary>
    public class MKIOPlayer : ContentView
    {
        private readonly WebView webView;
        private bool isInitialized;
        private string currentStreamId;
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
            // Handle errors in the Navigated event

            Content = webView;
        }

        /// <summary>
        /// Event handler for when the WebView completes navigation (success or failure)
        /// </summary>
        private void WebView_Navigated(object sender, WebNavigatedEventArgs e)
        {
            if (e.Result == WebNavigationResult.Success)
            {
                Console.WriteLine($"WebView successfully loaded: {e.Url}");
                isInitialized = true;
                PlayerStateChanged?.Invoke(this, "ready");
            }
            else
            {
                Console.WriteLine($"WebView navigation failed: {e.Url}, Result: {e.Result}");
                isInitialized = false;
                PlayerError?.Invoke(this, new Exception($"Error loading player: {e.Result}"));
            }
        }

        /// <summary>
        /// Initialises the player with a stream ID
        /// </summary>
        public async Task InitializePlayerAsync(string streamId, bool isLive = true)
        {
            this.currentStreamId = streamId;
            this.isLive = isLive;

            try
            {
                Console.WriteLine($"Initializing player with stream ID: {streamId}, isLive: {isLive}");

                // Build the HTML with a basic player for the stream URL
                string playerHtml = BuildPlayerHtml(streamId, isLive);

                // Load the HTML into the WebView
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    webView.Source = new HtmlWebViewSource
                    {
                        Html = playerHtml
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
        /// Initialises the player with a direct stream URL
        /// </summary>
        public async Task InitializeWithUrlAsync(string streamUrl, bool isLive = true)
        {
            try
            {
                Console.WriteLine($"Initializing player with URL: {streamUrl}, isLive: {isLive}");
                this.isLive = isLive;

                // Build the HTML with a basic player for the stream URL
                string playerHtml = BuildPlayerHtmlWithDirectUrl(streamUrl, isLive);

                // Load the HTML into the WebView
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    webView.Source = new HtmlWebViewSource
                    {
                        Html = playerHtml
                    };
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing player with URL: {ex.Message}");
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
                Console.WriteLine("Cannot play: Player not initialized");
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                Console.WriteLine("Playing stream");
                await webView.EvaluateJavaScriptAsync("if (typeof player !== 'undefined') { player.play(); }");
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
                Console.WriteLine("Cannot pause: Player not initialized");
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                Console.WriteLine("Pausing stream");
                await webView.EvaluateJavaScriptAsync("if (typeof player !== 'undefined') { player.pause(); }");
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
        /// Sets the muted state
        /// </summary>
        public async Task SetMutedAsync(bool muted)
        {
            if (!isInitialized)
            {
                Console.WriteLine("Cannot set muted: Player not initialized");
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                Console.WriteLine($"Setting muted state: {muted}");
                await webView.EvaluateJavaScriptAsync($"if (typeof player !== 'undefined') {{ player.muted = {(muted ? "true" : "false")}; }}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting muted state: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Sets the quality level
        /// </summary>
        public async Task SetQualityAsync(string quality)
        {
            if (!isInitialized)
            {
                Console.WriteLine("Cannot set quality: Player not initialized");
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                Console.WriteLine($"Setting quality: {quality}");
                // Note: This is a simplified approach - actual quality selection would depend on the player implementation
                await webView.EvaluateJavaScriptAsync($"console.log('Quality set to {quality}');");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting quality: {ex.Message}");
                PlayerError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Builds HTML for the player using a stream ID
        /// </summary>
        private string BuildPlayerHtml(string streamId, bool isLive)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Stream Player</title>
    <style>
        html, body {{ margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background-color: #000; }}
        #player-container {{ width: 100%; height: 100%; background-color: #000; }}
        .loading {{ color: white; text-align: center; position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); }}
    </style>
</head>
<body>
    <div id='player-container'>
        <div class='loading'>Loading stream...</div>
    </div>
    
    <script>
        // Simple video player initialization
        document.addEventListener('DOMContentLoaded', function() {{
            try {{
                // Create video element
                var player = document.createElement('video');
                player.id = 'player';
                player.controls = true;
                player.autoplay = true;
                player.muted = true;
                player.style.width = '100%';
                player.style.height = '100%';
                player.style.backgroundColor = '#000';
                player.playsInline = true;
                
                player.src = '{streamId}';
                
                // Add to container
                var container = document.getElementById('player-container');
                container.innerHTML = '';
                container.appendChild(player);
                
                // Event listeners
                player.addEventListener('playing', function() {{
                    console.log('Playing');
                    try {{ window.webkit.messageHandlers.playerState.postMessage('playing'); }} catch(e) {{}}
                }});
                
                player.addEventListener('pause', function() {{
                    console.log('Paused');
                    try {{ window.webkit.messageHandlers.playerState.postMessage('paused'); }} catch(e) {{}}
                }});
                
                player.addEventListener('ended', function() {{
                    console.log('Ended');
                    try {{ window.webkit.messageHandlers.playerState.postMessage('ended'); }} catch(e) {{}}
                }});
                
                player.addEventListener('error', function(e) {{
                    console.error('Player error:', e);
                    try {{ window.webkit.messageHandlers.playerError.postMessage(e.message); }} catch(e) {{}}
                }});
                
                // Expose player to global scope for external control
                window.player = player;
                
                console.log('Player initialized');
                try {{ window.webkit.messageHandlers.playerState.postMessage('ready'); }} catch(e) {{}}
            }} catch(e) {{
                console.error('Error initializing player:', e);
                try {{ window.webkit.messageHandlers.playerError.postMessage(e.message); }} catch(e) {{}}
            }}
        }});
    </script>
</body>
</html>";
        }

        /// <summary>
        /// Builds HTML for the player using a direct stream URL
        /// </summary>
        private string BuildPlayerHtmlWithDirectUrl(string streamUrl, bool isLive)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>Stream Player</title>
    <style>
        html, body {{ margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background-color: #000; }}
        #player-container {{ width: 100%; height: 100%; background-color: #000; }}
        .loading {{ color: white; text-align: center; position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); }}
    </style>
</head>
<body>
    <div id='player-container'>
        <div class='loading'>Loading stream...</div>
    </div>
    
    <script>
        // Simple video player initialization
        document.addEventListener('DOMContentLoaded', function() {{
            try {{
                // Create video element
                var player = document.createElement('video');
                player.id = 'player';
                player.controls = true;
                player.autoplay = true;
                player.muted = true;
                player.style.width = '100%';
                player.style.height = '100%';
                player.style.backgroundColor = '#000';
                player.playsInline = true;
                
                // Set the source directly
                player.src = '{streamUrl}';
                
                // Add to container
                var container = document.getElementById('player-container');
                container.innerHTML = '';
                container.appendChild(player);
                
                // Event listeners
                player.addEventListener('playing', function() {{
                    console.log('Playing');
                    try {{ window.webkit.messageHandlers.playerState.postMessage('playing'); }} catch(e) {{}}
                }});
                
                player.addEventListener('pause', function() {{
                    console.log('Paused');
                    try {{ window.webkit.messageHandlers.playerState.postMessage('paused'); }} catch(e) {{}}
                }});
                
                player.addEventListener('ended', function() {{
                    console.log('Ended');
                    try {{ window.webkit.messageHandlers.playerState.postMessage('ended'); }} catch(e) {{}}
                }});
                
                player.addEventListener('error', function(e) {{
                    console.error('Player error:', e);
                    try {{ window.webkit.messageHandlers.playerError.postMessage(e.message); }} catch(e) {{}}
                }});
                
                // Expose player to global scope for external control
                window.player = player;
                
                console.log('Player initialized with URL: {streamUrl}');
                try {{ window.webkit.messageHandlers.playerState.postMessage('ready'); }} catch(e) {{}}
            }} catch(e) {{
                console.error('Error initializing player:', e);
                try {{ window.webkit.messageHandlers.playerError.postMessage(e.message); }} catch(e) {{}}
            }}
        }});
    </script>
</body>
</html>";
        }
    }
}