using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using NeuroSpectator.Services.Streaming;

namespace NeuroSpectator.Controls
{
    /// <summary>
    /// MKIOPlayer that uses the official MKPlayer SDK from MediaKind with improved JS-to-native communication
    /// and local resource loading instead of CDN-based loading
    /// </summary>
    public class MKIOPlayer : Microsoft.Maui.Controls.ContentView, IDisposable
    {
        private readonly WebView webView;
        private readonly MKIOConfig mkioConfig;
        private bool isInitialized;
        private string currentStreamUrl;
        private bool isLive;
        private TaskCompletionSource<bool> initializationTcs;
        private int initializationTimeoutSeconds = 30;
        private HttpListener httpListener;
        private CancellationTokenSource serverCancellationTokenSource;
        private int serverPort = 8080;
        private string serverUrl => $"http://localhost:{serverPort}";
        private Dictionary<string, object> javaScriptFunctions = new Dictionary<string, object>();
        private bool isWebViewReady = false;
        private readonly string logTag = "MKIOPlayer";
        private readonly HttpClient httpClient = new HttpClient(); // For URL checking
        private static string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MKIOPlayer.log");

        // Local resource paths instead of CDN URLs
        private readonly string localPlayerJsResourceName = "NeuroSpectator.Resources.MKPlayer.mkplayer.js";
        private readonly string localPlayerCssResourceName = "NeuroSpectator.Resources.MKPlayer.mkplayer-ui.css";
        private readonly string localPlayerJsPath = "mkplayer.js";
        private readonly string localPlayerCssPath = "mkplayer-ui.css";

        /// <summary>
        /// Event fired when the player state changes
        /// </summary>
        public event EventHandler<string> PlayerStateChanged;

        /// <summary>
        /// Event fired when an error occurs in the player
        /// </summary>
        public event EventHandler<Exception> PlayerError;

        /// <summary>
        /// Event fired when debug information is received from JavaScript
        /// </summary>
        public event EventHandler<string> DebugInfoReceived;

        /// <summary>
        /// Creates a new instance of the MKIOPlayer with default constructor for XAML
        /// </summary>
        public MKIOPlayer() : this(null)
        {
            // This parameterless constructor is required for XAML instantiation
        }

        /// <summary>
        /// Creates a new instance of the MKIOPlayer
        /// </summary>
        public MKIOPlayer(MKIOConfig config = null)
        {
            LogToFile("Creating new MKIOPlayer instance");

            // List available resources for debugging
            LogAvailableResources();

            // Store the config if provided, otherwise try to get it from MauiProgram.Services
            this.mkioConfig = config ?? MauiProgram.Services.GetService<MKIOConfig>();

            if (this.mkioConfig == null)
            {
                LogInfo("WARNING: MKIOConfig not provided to MKIOPlayer. Player license may not work.");
            }
            else
            {
                LogInfo($"MKIOPlayer initialized with license key: {(string.IsNullOrEmpty(this.mkioConfig.LicenseKey) ? "NOT FOUND" : "FOUND (hidden)")}");
            }

            // Create the WebView with a blank page first to initialize it
            webView = new WebView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Source = "about:blank" // Start with blank page to initialize WebView
            };

            // Configure WebView handlers
            webView.Navigated += WebView_Navigated;
            webView.Navigating += WebView_Navigating;

            // Add WebView message handlers for different platforms
            SetupWebViewMessageHandlers();

            // Add the WebView to this control
            this.Content = webView;

            // Start the HTTP server in a background thread
            Task.Run(async () => {
                try
                {
                    await StartHttpServerAsync();
                }
                catch (Exception ex)
                {
                    LogError($"HTTP Server error: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Lists all embedded resources available in the assembly for debugging
        /// </summary>
        private void LogAvailableResources()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resources = assembly.GetManifestResourceNames();

                LogInfo($"Available embedded resources ({resources.Length}):");
                foreach (var resource in resources)
                {
                    LogInfo($"  - {resource}");
                }
            }
            catch (Exception ex)
            {
                LogError("Error listing resources", ex);
            }
        }

        /// <summary>
        /// Sets up the WebView message handlers for different platforms
        /// </summary>
        private void SetupWebViewMessageHandlers()
        {
            // Add JavaScript evaluation handlers
            // These will be called from JavaScript via window.chrome.webview.postMessage or WebKit handlers
            javaScriptFunctions["onPlayerReady"] = new Action<string>(_ => {
                LogInfo("JS -> Native: Player is ready");
                CompleteInitialization();
            });

            javaScriptFunctions["onPlayerStateChanged"] = new Action<string>(state => {
                LogInfo($"JS -> Native: Player state changed to {state}");
                MainThread.BeginInvokeOnMainThread(() => {
                    PlayerStateChanged?.Invoke(this, state);
                });
            });

            javaScriptFunctions["onPlayerError"] = new Action<string>(errorMessage => {
                LogError($"JS -> Native: Player error: {errorMessage}", null);
                var exception = new Exception(errorMessage);
                MainThread.BeginInvokeOnMainThread(() => {
                    PlayerError?.Invoke(this, exception);
                });

                // Also fail the initialization if it's still pending
                if (initializationTcs != null && !initializationTcs.Task.IsCompleted)
                {
                    initializationTcs.TrySetException(exception);
                }
            });

            javaScriptFunctions["onDebugInfo"] = new Action<string>(debugInfo => {
                LogInfo($"JS Debug: {debugInfo}");
                MainThread.BeginInvokeOnMainThread(() => {
                    DebugInfoReceived?.Invoke(this, debugInfo);
                });
            });
        }

        /// <summary>
        /// Ensures all UI operations run on the main thread
        /// </summary>
        private async Task EnsureMainThreadAsync(Func<Task> action)
        {
            if (MainThread.IsMainThread)
            {
                await action();
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(action);
            }
        }

        /// <summary>
        /// Logs a message to Debug output and file
        /// </summary>
        private void LogInfo(string message)
        {
            Debug.WriteLine($"{logTag}: {message}");
            LogToFile($"INFO: {message}");
        }

        /// <summary>
        /// Logs an error message to Debug output and file
        /// </summary>
        private void LogError(string message, Exception ex)
        {
            Debug.WriteLine($"{logTag}: {message}");
            if (ex != null)
            {
                Debug.WriteLine($"{logTag}: Exception: {ex.Message}");
                Debug.WriteLine($"{logTag}: Stack trace: {ex.StackTrace}");
                LogToFile($"ERROR: {message} - Exception: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
            else
            {
                LogToFile($"ERROR: {message}");
            }
        }

        /// <summary>
        /// Logs a message to a file for persistent debugging
        /// </summary>
        private void LogToFile(string message)
        {
            try
            {
                File.AppendAllText(logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
            }
            catch
            {
                // Ignore file writing errors
            }
        }

        /// <summary>
        /// Serves embedded resources to the WebView
        /// </summary>
        /// <summary>
        /// Serves embedded resources to the WebView
        /// </summary>
        private async Task ServeEmbeddedResourceAsync(string resourceName, string contentType, HttpListenerResponse response, CancellationToken token)
        {
            try
            {
                // Get the assembly where resources are embedded
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                // Map requested resource name to the actual embedded resource name
                string embeddedResourceName = null;

                LogInfo($"Looking for embedded resource: {resourceName}");

                // Map the requested resource to our specific logical names
                if (resourceName == localPlayerJsPath || resourceName.EndsWith(localPlayerJsPath))
                {
                    embeddedResourceName = localPlayerJsResourceName;
                    LogInfo($"Using specific logical name for JS: {embeddedResourceName}");
                }
                else if (resourceName == localPlayerCssPath || resourceName.EndsWith(localPlayerCssPath))
                {
                    embeddedResourceName = localPlayerCssResourceName;
                    LogInfo($"Using specific logical name for CSS: {embeddedResourceName}");
                }
                else
                {
                    // For other resources, try different patterns
                    string[] possibleResourceNames = new string[]
                    {
                $"NeuroSpectator.Resources.{resourceName}",
                resourceName
                    };

                    // Try each possible name
                    foreach (var name in possibleResourceNames)
                    {
                        LogInfo($"Trying resource name: {name}");
                        var testStream = assembly.GetManifestResourceStream(name);
                        if (testStream != null)
                        {
                            embeddedResourceName = name;
                            testStream.Dispose();
                            break;
                        }
                    }
                }

                // If no specific match, log all available resources for debugging
                if (embeddedResourceName == null)
                {
                    var allResources = assembly.GetManifestResourceNames();
                    LogInfo($"Resource not found with predefined patterns. Searching all {allResources.Length} resources");

                    foreach (var res in allResources)
                    {
                        if (res.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase))
                        {
                            LogInfo($"Found matching resource: {res}");
                            embeddedResourceName = res;
                            break;
                        }
                    }
                }

                // Check if we found a resource name
                if (embeddedResourceName == null)
                {
                    LogError($"Embedded resource not found: {resourceName}", null);
                    SendResponse(response, 404, $"Resource not found: {resourceName}");
                    return;
                }

                // Get the resource stream using the exact logical name
                Stream stream = assembly.GetManifestResourceStream(embeddedResourceName);

                if (stream == null)
                {
                    LogError($"Failed to load resource: {embeddedResourceName}", null);
                    SendResponse(response, 404, $"Resource not found: {embeddedResourceName}");
                    return;
                }

                LogInfo($"Found resource: {embeddedResourceName}, Length: {stream.Length} bytes");

                // Set response headers
                response.ContentType = contentType;
                response.ContentLength64 = stream.Length;
                response.StatusCode = 200;

                // Copy the resource to the response
                await stream.CopyToAsync(response.OutputStream, token);
                await response.OutputStream.FlushAsync(token);
                response.Close();

                LogInfo($"Successfully served embedded resource: {resourceName} ({stream.Length} bytes)");
            }
            catch (Exception ex)
            {
                LogError($"Error serving embedded resource {resourceName}", ex);
                try
                {
                    SendResponse(response, 500, $"Internal Server Error: {ex.Message}");
                }
                catch
                {
                    // Ignore errors in error handling
                }
            }
        }

        /// <summary>
        /// Starts a local HTTP server to serve player content
        /// </summary>
        private async Task StartHttpServerAsync()
        {
            try
            {
                LogInfo("Starting local HTTP server...");

                serverCancellationTokenSource = new CancellationTokenSource();
                var token = serverCancellationTokenSource.Token;

                // Try different ports if the default one is in use
                bool serverStarted = false;
                for (int port = 8080; port < 8090; port++)
                {
                    try
                    {
                        serverPort = port;
                        httpListener = new HttpListener();
                        httpListener.Prefixes.Add($"http://localhost:{serverPort}/");
                        httpListener.Start();
                        serverStarted = true;
                        LogInfo($"HTTP server started on port {serverPort}");
                        break;
                    }
                    catch (HttpListenerException ex)
                    {
                        LogInfo($"Failed to start server on port {port}: {ex.Message}");
                        httpListener?.Close();
                    }
                }

                if (!serverStarted)
                {
                    throw new InvalidOperationException("Could not start HTTP server on any port");
                }

                // Start listening for requests
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            var context = await httpListener.GetContextAsync().ConfigureAwait(false);
                            _ = HandleRequestAsync(context, token);
                        }
                    }
                    catch (Exception ex) when (token.IsCancellationRequested)
                    {
                        // Server was stopped, this is expected
                        LogInfo("HTTP server stopped");
                    }
                    catch (Exception ex)
                    {
                        LogError("HTTP server error", ex);
                        MainThread.BeginInvokeOnMainThread(() => {
                            PlayerError?.Invoke(this, ex);
                        });
                    }
                }, token);
            }
            catch (Exception ex)
            {
                LogError("Error starting HTTP server", ex);
                MainThread.BeginInvokeOnMainThread(() => {
                    PlayerError?.Invoke(this, ex);
                });
                throw;
            }
        }

        /// <summary>
        /// Handles HTTP requests to the local server
        /// </summary>
        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                string path = request.Url.LocalPath;

                LogInfo($"HTTP request: {request.HttpMethod} {request.Url.PathAndQuery}");

                // Handle MKPlayer JS file
                if (path == "/" + localPlayerJsPath)
                {
                    await ServeEmbeddedResourceAsync(localPlayerJsPath, "application/javascript", response, token);
                    return;
                }
                // Handle MKPlayer CSS file
                else if (path == "/" + localPlayerCssPath)
                {
                    await ServeEmbeddedResourceAsync(localPlayerCssPath, "text/css", response, token);
                    return;
                }
                // Handle player page
                else if (path == "/" || path == "/index.html" || path == "/player.html")
                {
                    if (string.IsNullOrEmpty(currentStreamUrl))
                    {
                        SendResponse(response, 404, "No stream URL provided");
                        return;
                    }

                    // Determine URL type (DASH or HLS)
                    bool isDash = currentStreamUrl.Contains("manifest(format=mpd") || currentStreamUrl.EndsWith(".mpd");
                    bool isHls = currentStreamUrl.Contains("manifest(format=m3u8") || currentStreamUrl.EndsWith(".m3u8");

                    // Build the player HTML
                    string playerHtml = BuildMKPlayerHtml(currentStreamUrl, isLive, isDash, isHls);
                    byte[] buffer = Encoding.UTF8.GetBytes(playerHtml);

                    response.ContentType = "text/html";
                    response.ContentLength64 = buffer.Length;
                    response.StatusCode = 200;

                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, token);
                    response.Close();
                }
                // Handle proxy requests for CORS issues
                else if (path.StartsWith("/proxy"))
                {
                    await HandleProxyRequestAsync(request, response, token);
                }
                // Handle bridge requests (from JavaScript to C#)
                else if (path.StartsWith("/bridge/"))
                {
                    string command = path.Substring("/bridge/".Length);

                    if (command == "ready")
                    {
                        CompleteInitialization();
                    }
                    else if (command.StartsWith("error/"))
                    {
                        string errorMsg = Uri.UnescapeDataString(command.Substring("error/".Length));
                        LogError($"Received JS error: {errorMsg}", null);

                        // Complete the task with failure
                        if (initializationTcs != null && !initializationTcs.Task.IsCompleted)
                        {
                            initializationTcs.SetException(new Exception(errorMsg));
                        }

                        MainThread.BeginInvokeOnMainThread(() => {
                            PlayerError?.Invoke(this, new Exception(errorMsg));
                        });
                    }
                    else
                    {
                        // Handle other state changes
                        MainThread.BeginInvokeOnMainThread(() => {
                            PlayerStateChanged?.Invoke(this, command);
                        });
                    }

                    SendResponse(response, 200, "OK");
                }
                // Handle debug logging
                else if (path.StartsWith("/debug/"))
                {
                    string message = Uri.UnescapeDataString(path.Substring("/debug/".Length));
                    LogInfo($"JS Debug: {message}");

                    MainThread.BeginInvokeOnMainThread(() => {
                        DebugInfoReceived?.Invoke(this, message);
                    });

                    SendResponse(response, 200, "Debug message received");
                }
                else
                {
                    SendResponse(response, 404, "Not Found");
                }
            }
            catch (Exception ex) when (token.IsCancellationRequested)
            {
                // Server was stopped, this is expected
                LogInfo("HTTP server stopped during request handling");
            }
            catch (Exception ex)
            {
                LogError("Error handling HTTP request", ex);
                try
                {
                    SendResponse(context.Response, 500, "Internal Server Error");
                }
                catch
                {
                    // Ignore errors during error handling
                }
            }
        }

        /// <summary>
        /// Handles proxy requests for CORS issues
        /// </summary>
        private async Task HandleProxyRequestAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken token)
        {
            try
            {
                // Get the target URL from the query string
                string targetUrl = request.QueryString["url"];

                if (string.IsNullOrEmpty(targetUrl))
                {
                    SendResponse(response, 400, "Missing 'url' parameter");
                    return;
                }

                LogInfo($"Proxying request to {targetUrl}");

                // Create the HTTP request
                var proxyRequest = new HttpRequestMessage(HttpMethod.Get, targetUrl);

                // Copy relevant headers
                foreach (string headerName in request.Headers.AllKeys)
                {
                    if (headerName.StartsWith("Accept") ||
                        headerName.Equals("Range", StringComparison.OrdinalIgnoreCase) ||
                        headerName.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                    {
                        proxyRequest.Headers.TryAddWithoutValidation(headerName, request.Headers[headerName]);
                    }
                }

                // Send the request
                var proxyResponse = await httpClient.SendAsync(proxyRequest, token);

                // Copy response status code
                response.StatusCode = (int)proxyResponse.StatusCode;

                // Copy relevant response headers
                foreach (var header in proxyResponse.Headers)
                {
                    response.Headers.Add(header.Key, string.Join(",", header.Value));
                }
                foreach (var header in proxyResponse.Content.Headers)
                {
                    response.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                // Copy the response content
                var responseStream = await proxyResponse.Content.ReadAsStreamAsync(token);
                await responseStream.CopyToAsync(response.OutputStream, token);

                response.Close();
            }
            catch (Exception ex)
            {
                LogError("Error handling proxy request", ex);
                SendResponse(response, 500, $"Proxy error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a URL is accessible
        /// </summary>
        private async Task<bool> IsUrlAccessibleAsync(string url)
        {
            try
            {
                // Create a HEAD request to check if the URL is accessible
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await httpClient.SendAsync(request);

                LogInfo($"URL accessibility check for {url}: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LogError($"URL accessibility check failed for {url}", ex);
                return false;
            }
        }

        /// <summary>
        /// Sends an HTTP response
        /// </summary>
        private void SendResponse(HttpListenerResponse response, int statusCode, string statusDescription)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(statusDescription);
            response.ContentType = "text/plain";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = statusCode;

            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        /// <summary>
        /// Event handler for when the WebView is navigating
        /// </summary>
        private void WebView_Navigating(object sender, WebNavigatingEventArgs e)
        {
            LogInfo($"WebView navigating to: {e.Url}");
        }

        /// <summary>
        /// Event handler for when the WebView completes navigation
        /// </summary>
        private async void WebView_Navigated(object sender, WebNavigatedEventArgs e)
        {
            LogInfo($"WebView navigation completed: {e.Url}, Result: {e.Result}");

            if (e.Result == WebNavigationResult.Success)
            {
                // WebView loaded successfully
                LogInfo("WebView HTML loaded successfully");
                isWebViewReady = true;

                // If this is the initial blank page, do nothing
                if (e.Url == "about:blank")
                {
                    // Nothing to do on blank page
                    return;
                }

                // If this is our player page, inject script to check status
                if (e.Url.Contains("/player.html"))
                {
                    // Wait a brief moment to ensure WebView is fully loaded
                    await Task.Delay(500);

                    try
                    {
                        await EnsureMainThreadAsync(async () => {
                            // Inject communication script to help debug the player status
                            string checkScript = @"
                                try {
                                    console.log('Checking MKPlayer status...');
                                    const statusMsg = 'MKPlayer check: ' + 
                                        (typeof mkplayer !== 'undefined' ? 'SDK available' : 'SDK NOT FOUND') + 
                                        ', Player: ' + (typeof player !== 'undefined' ? 'initialized' : 'NOT initialized');
                                    console.log(statusMsg);
                                    
                                    // Try to notify native code
                                    if (typeof window.webkit !== 'undefined' && window.webkit.messageHandlers) {
                                        console.log('Using WebKit message handlers to send debug info');
                                        if (window.webkit.messageHandlers.onDebugInfo) {
                                            window.webkit.messageHandlers.onDebugInfo.postMessage(statusMsg);
                                        }
                                    } else if (typeof window.chrome !== 'undefined' && window.chrome.webview) {
                                        console.log('Using WebView message handlers to send debug info');
                                        window.chrome.webview.postMessage(JSON.stringify({type: 'onDebugInfo', data: statusMsg}));
                                    }
                                    
                                    // Always try HTTP fallback
                                    fetch('/debug/' + encodeURIComponent(statusMsg)).catch(() => console.log('HTTP debug failed'));

                                    // Force initialization if needed
                                    if (typeof window.forcePlayerInit === 'function') {
                                        console.log('Explicitly calling force initialization');
                                        window.forcePlayerInit();
                                    }
                                } catch(e) {
                                    console.error('Error in status check: ' + e);
                                    fetch('/debug/' + encodeURIComponent('Error: ' + e.toString())).catch(() => {});
                                }";

                            await webView.EvaluateJavaScriptAsync(checkScript);
                            LogInfo("Injected player status check script");
                        });
                    }
                    catch (Exception ex)
                    {
                        LogError("Error injecting status check", ex);
                    }
                }
            }
            else
            {
                LogError($"WebView navigation failed: {e.Url}, Result: {e.Result}", null);

                // Complete the task with failure
                if (initializationTcs != null && !initializationTcs.Task.IsCompleted)
                {
                    initializationTcs.SetException(new Exception($"WebView navigation failed: {e.Result}"));
                }

                MainThread.BeginInvokeOnMainThread(() => {
                    PlayerError?.Invoke(this, new Exception($"WebView navigation failed: {e.Result}"));
                });
            }
        }

        /// <summary>
        /// Completes the initialization process and marks the player as ready
        /// </summary>
        private void CompleteInitialization()
        {
            if (!isInitialized)
            {
                isInitialized = true;
                LogInfo("Player initialization completed successfully");

                // Complete the initialization task if it exists
                MainThread.BeginInvokeOnMainThread(() => {
                    if (initializationTcs != null && !initializationTcs.Task.IsCompleted)
                    {
                        initializationTcs.SetResult(true);
                    }

                    PlayerStateChanged?.Invoke(this, "ready");
                });
            }
        }

        /// <summary>
        /// Initializes the player with a stream URL
        /// </summary>
        public async Task<bool> InitializeWithUrlAsync(string streamUrl, bool isLive = true)
        {
            try
            {
                LogInfo($"Initializing player with URL: {streamUrl}, isLive: {isLive}");

                // Reset state
                isInitialized = false;
                this.currentStreamUrl = streamUrl;
                this.isLive = isLive;

                if (string.IsNullOrEmpty(streamUrl))
                {
                    throw new ArgumentException("Stream URL cannot be null or empty");
                }

                // Check if the URL is accessible before proceeding
                bool isAccessible = await IsUrlAccessibleAsync(streamUrl);
                if (!isAccessible)
                {
                    LogInfo($"Stream URL accessibility check failed, will try to use proxy");
                    // We'll continue anyway and let the player handle it
                }

                // Create a new initialization task completion source
                initializationTcs = new TaskCompletionSource<bool>();

                // Load the player page from our local HTTP server
                string playerUrl = $"{serverUrl}/player.html?t={DateTime.Now.Ticks}";

                // Load the URL into the WebView (on the main thread)
                await EnsureMainThreadAsync(async () => {
                    // Clear any previous page
                    webView.Source = "about:blank";
                    await Task.Delay(100); // Brief pause

                    // Load the player page
                    webView.Source = playerUrl;
                });

                LogInfo($"WebView source set to {playerUrl}");

                // Wait for the initialization to complete with a timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(initializationTimeoutSeconds));
                var completedTask = await Task.WhenAny(initializationTcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    LogError($"Player initialization timed out", null);
                    throw new TimeoutException($"Player initialization timed out after {initializationTimeoutSeconds} seconds");
                }

                // Wait for the actual initialization task to complete
                await initializationTcs.Task;

                LogInfo("Player initialization completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogError("Error initializing with URL", ex);
                MainThread.BeginInvokeOnMainThread(() => {
                    PlayerError?.Invoke(this, ex);
                });
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
                LogError("Cannot play: Player not initialized", null);
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                LogInfo("Playing stream");
                await EnsureMainThreadAsync(async () => {
                    await webView.EvaluateJavaScriptAsync("try { if (typeof player !== 'undefined' && player) { player.play(); } } catch(e) { console.error('Play error:', e); }");
                });
            }
            catch (Exception ex)
            {
                LogError("Error playing stream", ex);
                MainThread.BeginInvokeOnMainThread(() => {
                    PlayerError?.Invoke(this, ex);
                });
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
                LogError("Cannot pause: Player not initialized", null);
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                LogInfo("Pausing stream");
                await EnsureMainThreadAsync(async () => {
                    await webView.EvaluateJavaScriptAsync("try { if (typeof player !== 'undefined' && player) { player.pause(); } } catch(e) { console.error('Pause error:', e); }");
                });
            }
            catch (Exception ex)
            {
                LogError("Error pausing stream", ex);
                MainThread.BeginInvokeOnMainThread(() => {
                    PlayerError?.Invoke(this, ex);
                });
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
                LogError("Cannot set muted: Player not initialized", null);
                throw new InvalidOperationException("Player not initialized");
            }

            try
            {
                LogInfo($"Setting muted state: {muted}");
                await EnsureMainThreadAsync(async () => {
                    string script = $"try {{ if (typeof player !== 'undefined' && player) {{ player.setMuted({(muted ? "true" : "false")}); }} }} catch(e) {{ console.error('Mute error:', e); }}";
                    await webView.EvaluateJavaScriptAsync(script);
                });
            }
            catch (Exception ex)
            {
                LogError("Error setting muted state", ex);
                MainThread.BeginInvokeOnMainThread(() => {
                    PlayerError?.Invoke(this, ex);
                });
                throw;
            }
        }

        /// <summary>
        /// Builds HTML with MKPlayer SDK for playing streams
        /// </summary>
        private string BuildMKPlayerHtml(string streamUrl, bool isLive, bool isDash, bool isHls)
        {
            // Use local server URLs for player files
            string sdkUrl = $"{serverUrl}/mkplayer.js";
            string cssUrl = $"{serverUrl}/mkplayer-ui.css";

            // Get the license key from config if available, or use default placeholder
            string licenseKey = mkioConfig?.LicenseKey;

            if (string.IsNullOrEmpty(licenseKey))
            {
                LogInfo("WARNING: No valid license key found for MKPlayer. Using placeholder.");
                licenseKey = "d0167b1c-9767-4287-9ddc-e0fa09d31e02"; // Default to config value from appsettings.json
            }
            else
            {
                LogInfo("Using configured license key for MKPlayer");
            }

            // Check if stream URL needs proxying
            bool useProxy = !IsUrlAccessibleAsync(streamUrl).Result;
            string effectiveStreamUrl = useProxy ?
                $"{serverUrl}/proxy?url={Uri.EscapeDataString(streamUrl)}" :
                streamUrl;

            // Configure source based on stream type
            string sourceConfig = "";
            if (isDash)
            {
                sourceConfig = $"dash: \"{effectiveStreamUrl}\"";
            }
            else if (isHls)
            {
                sourceConfig = $"hls: \"{effectiveStreamUrl}\"";
            }
            else
            {
                // Default to both - MKPlayer will choose the appropriate one
                sourceConfig = $"dash: \"{effectiveStreamUrl}\", hls: \"{effectiveStreamUrl}\"";
            }

            // Create a timestamp for cache busting
            string timestamp = DateTime.Now.Ticks.ToString();

            // Build the HTML content with enhanced error handling and debugging
            string html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <meta http-equiv='Content-Security-Policy' content='default-src * data: blob: gap: https: http: localhost 127.0.0.1 ws: wss: ""unsafe-eval"" ""unsafe-inline""; connect-src * data: blob: http: https: localhost 127.0.0.1 ws: wss:;'>
    <title>MK.IO Stream Player</title>
    <style>
        html, body {{ margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background-color: #000; }}
        #video-container {{ width: 100%; height: 100%; }}
        #error-container {{ 
            position: absolute; 
            top: 10px; 
            left: 10px; 
            right: 10px; 
            background-color: rgba(0,0,0,0.7); 
            color: #ff5555; 
            padding: 10px; 
            font-family: monospace; 
            font-size: 12px; 
            z-index: 1000; 
            display: none; 
            max-height: 200px; 
            overflow-y: auto; 
            border: 1px solid #ff5555; 
            border-radius: 4px; 
        }}
        #debug-container {{ 
            position: absolute; 
            bottom: 10px; 
            left: 10px; 
            right: 10px; 
            background-color: rgba(0,0,0,0.5); 
            color: #55ff55; 
            padding: 5px; 
            font-family: monospace; 
            font-size: 10px; 
            z-index: 999; 
            max-height: 150px; 
            overflow-y: auto; 
            border: 1px solid #333; 
            border-radius: 4px; 
        }}
        #loading {{ 
            position: absolute; 
            top: 50%; 
            left: 50%; 
            transform: translate(-50%, -50%);
            background-color: rgba(0,0,0,0.7);
            color: white;
            padding: 20px;
            border-radius: 10px;
            z-index: 999;
        }}
        /* Added loader animation */
        .loader {{
            display: inline-block;
            width: 20px;
            height: 20px;
            margin-left: 10px;
            border: 3px solid rgba(255,255,255,.3);
            border-radius: 50%;
            border-top-color: #fff;
            animation: spin 1s ease-in-out infinite;
        }}
        @keyframes spin {{
            to {{ transform: rotate(360deg); }}
        }}
        #retry-button {{
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, 50px);
            background-color: #B388FF;
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 5px;
            cursor: pointer;
            font-weight: bold;
            display: none;
            z-index: 1001;
        }}
        /* Add status indicator */
        #status-indicator {{
            position: absolute;
            top: 8px;
            right: 8px;
            background-color: rgba(0,0,0,0.5);
            color: white;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 12px;
            z-index: 1000;
        }}
    </style>
    
    <!-- Debug container initialization - always visible during development -->
    <script>
        // Global error handler - capture all JS errors
        window.onerror = function(message, source, lineno, colno, error) {{
            console.error('JS ERROR:', message, 'at', source, ':', lineno);
            logDebug('ERROR: ' + message + ' at ' + source + ':' + lineno);
            
            // Show in error container
            showError('JavaScript error: ' + message);
            
            // Send to native
            try {{
                fetch('/bridge/error/' + encodeURIComponent('JS Error: ' + message))
                    .catch(e => console.error('Failed to send error to bridge'));
            }} catch(e) {{}}
            
            return false; // Let default handler run too
        }};
    </script>
</head>
<body>
    <div id='loading'>Loading player... <span class='loader'></span></div>
    <div id='error-container'></div>
    <div id='debug-container'></div>
    <div id='status-indicator'>Initializing...</div>
    <div id='video-container'></div>
    <button id='retry-button'>Retry Loading Player</button>
    
    <script>
        // Enhanced debugging system
        const DEBUG = true;
        const debugContainer = document.getElementById('debug-container');
        debugContainer.style.display = DEBUG ? 'block' : 'none';
        
        let loadStartTime = Date.now();
        let sdkLoadTime = null;
        let playerInitTime = null;
        
        // Status tracking
        let sdkLoaded = false;
        let playerCreated = false;
        let sourceLoaded = false;
        
        // Function to update status indicator
        function updateStatus(status) {{
            const indicator = document.getElementById('status-indicator');
            indicator.textContent = status;
            logDebug('Status: ' + status);
        }}
        
        // Enhanced debug logging
        function logDebug(message) {{
            if (!DEBUG) return;
            
            const timestamp = new Date().toISOString().slice(11, 19);
            console.log('[DEBUG]', message);
            
            if (debugContainer) {{
                const entry = document.createElement('div');
                entry.textContent = timestamp + ': ' + message;
                debugContainer.appendChild(entry);
                debugContainer.scrollTop = debugContainer.scrollHeight;
            }}
            
            // Try to send to native code through multiple channels
            try {{
                fetch('/debug/' + encodeURIComponent(message))
                    .catch(e => console.warn('HTTP debug channel failed'));
                
                if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.onDebugInfo) {{
                    window.webkit.messageHandlers.onDebugInfo.postMessage(message);
                }} else if (window.chrome && window.chrome.webview) {{
                    window.chrome.webview.postMessage(JSON.stringify({{ type: 'onDebugInfo', data: message }}));
                }}
            }} catch(e) {{
                console.warn('Failed to send debug message to native', e);
            }}
        }}
        
        // Enhanced error handling
        function showError(message) {{
            console.error(message);
            
            const errorContainer = document.getElementById('error-container');
            errorContainer.style.display = 'block';
            
            const entry = document.createElement('div');
            entry.textContent = new Date().toISOString().slice(11, 19) + ': ' + message;
            errorContainer.appendChild(entry);
            errorContainer.scrollTop = errorContainer.scrollHeight;
            
            // Show retry button
            document.getElementById('retry-button').style.display = 'block';
            
            // Send to native code
            try {{
                fetch('/bridge/error/' + encodeURIComponent(message))
                    .catch(e => console.warn('Failed to send error to bridge'));
                    
                if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.onPlayerError) {{
                    window.webkit.messageHandlers.onPlayerError.postMessage(message);
                }} else if (window.chrome && window.chrome.webview) {{
                    window.chrome.webview.postMessage(JSON.stringify({{ type: 'onPlayerError', data: message }}));
                }}
            }} catch(e) {{
                console.error('Failed to send error to native', e);
            }}
        }}
        
        // Test MKPlayer file accessibility
        function checkMKPlayerAccess() {{
            updateStatus('Checking MKPlayer files...');
            logDebug('Testing MKPlayer files accessibility: {sdkUrl}');
            
            // Test the JS file
            fetch('{sdkUrl}', {{ method: 'HEAD' }})
                .then(response => {{
                    logDebug(`MKPlayer JS status: ${{response.status}} ${{response.statusText}}`);
                    if (response.ok) {{
                        updateStatus('MKPlayer JS found');
                        
                        // Test the CSS file
                        return fetch('{cssUrl}', {{ method: 'HEAD' }});
                    }} else {{
                        showError(`MKPlayer JS access failed: ${{response.status}} ${{response.statusText}}`);
                        updateStatus('MKPlayer JS not found');
                        return Promise.reject('MKPlayer JS not found');
                    }}
                }})
                .then(response => {{
                    logDebug(`MKPlayer CSS status: ${{response.status}} ${{response.statusText}}`);
                    if (response.ok) {{
                        updateStatus('MKPlayer files found');
                        // Continue with loading MKPlayer
                        loadMKPlayerSDK();
                    }} else {{
                        showError(`MKPlayer CSS access failed: ${{response.status}} ${{response.statusText}}`);
                        updateStatus('MKPlayer CSS not found');
                    }}
                }})
                .catch(error => {{
                    logDebug(`MKPlayer files access error: ${{error.message}}`);
                    showError(`MKPlayer files access error: ${{error.message}}`);
                    updateStatus('MKPlayer files unreachable');
                }});
        }}
        
        // Check if stream URL is accessible
        function checkStreamUrl() {{
            updateStatus('Checking stream URL...');
            logDebug('Testing stream URL accessibility: {effectiveStreamUrl}');
            
            // For DASH/HLS manifests, just do a HEAD request
            fetch('{effectiveStreamUrl}', {{ method: 'HEAD' }})
                .then(response => {{
                    logDebug(`Stream URL status: ${{response.status}} ${{response.statusText}}`);
                    if (response.ok) {{
                        updateStatus('Stream URL accessible');
                    }} else {{
                        showError(`Stream URL check failed: ${{response.status}} ${{response.statusText}}`);
                        updateStatus('Stream URL inaccessible');
                    }}
                }})
                .catch(error => {{
                    logDebug(`Stream URL error: ${{error.message}}`);
                    showError(`Stream URL error: ${{error.message}}`);
                    updateStatus('Stream URL unreachable');
                }});
        }}
        
        // Load MKPlayer SDK from local files
        function loadMKPlayerSDK() {{
            updateStatus('Loading MKPlayer SDK...');
            logDebug('Loading MKPlayer SDK from local server: {sdkUrl}');
            
            const script = document.createElement('script');
            script.src = '{sdkUrl}';
            script.async = true;
            
            script.onload = () => {{
                logDebug('MKPlayer SDK script loaded successfully');
                sdkLoaded = true;
                sdkLoadTime = Date.now() - loadStartTime;
                logDebug(`SDK loaded in ${{sdkLoadTime}}ms`);
                
                // Check if MKPlayer object exists after loading
                if (typeof mkplayer === 'undefined') {{
                    showError('MKPlayer SDK script loaded but mkplayer object not found');
                    updateStatus('MKPlayer SDK load error');
                    return;
                }}
                
                updateStatus('MKPlayer SDK loaded');
                
                // Also load CSS
                const link = document.createElement('link');
                link.rel = 'stylesheet';
                link.href = '{cssUrl}';
                document.head.appendChild(link);
                
                // Continue with player initialization
                initializePlayer();
            }};
            
            script.onerror = (e) => {{
                logDebug('Failed to load MKPlayer SDK script from local server');
                showError('MKPlayer SDK script loading failed. Check local server.');
                updateStatus('SDK load failed');
            }};
            
            document.head.appendChild(script);
        }}
        
        // Initialize MKPlayer
        function initializePlayer() {{
            try {{
                updateStatus('Initializing player...');
                logDebug('Creating MKPlayer instance with license: ' + '{licenseKey}'.substring(0, 4) + '...');
                
                // Get video container
                const videoContainer = document.getElementById('video-container');
                if (!videoContainer) {{
                    showError('Video container element not found');
                    return;
                }}
                
                // Create player configuration with verbose logging
                const playerConfig = {{
                    key: '{licenseKey}',
                    analytics: false,  // Disable analytics to reduce issues
                    debug: true,       // Enable debug mode if supported
                    playback: {{
                        muted: true,
                        autoplay: {(isLive ? "true" : "false")},
                        // Try progressive loading for VOD
                        progressive: {(!isLive ? "true" : "false")}
                    }},
                    // Detailed event handling
                    events: {{
                        Error: (event) => {{
                            const errorMsg = JSON.stringify(event);
                            logDebug('MKPlayer error event: ' + errorMsg);
                            showError('MKPlayer error: ' + errorMsg);
                            updateStatus('Player error');
                        }},
                        Ready: (event) => {{
                            logDebug('MKPlayer Ready event received');
                            playerCreated = true;
                            playerInitTime = Date.now() - loadStartTime;
                            logDebug(`Player initialized in ${{playerInitTime}}ms`);
                            updateStatus('Player ready');
                            
                            // Hide loading indicator
                            document.getElementById('loading').style.display = 'none';
                            
                            // Send ready event to native
                            try {{
                                if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.onPlayerReady) {{
                                    window.webkit.messageHandlers.onPlayerReady.postMessage('');
                                }} else if (window.chrome && window.chrome.webview) {{
                                    window.chrome.webview.postMessage(JSON.stringify({{ type: 'onPlayerReady' }}));
                                }}
                                
                                // Always try HTTP fallback
                                fetch('/bridge/ready')
                                    .catch(e => logDebug('Ready HTTP notification failed: ' + e.message));
                            }} catch(e) {{
                                logDebug('Error sending ready event: ' + e.message);
                                // Last resort
                                fetch('/bridge/ready')
                                    .catch(() => {{}});
                            }}
                        }},
                        SourceLoaded: (event) => {{
                            logDebug('Source loaded successfully');
                            sourceLoaded = true;
                            updateStatus('Source loaded');
                            
                            // Send state update
                            sendStateChange('sourceLoaded');
                        }},
                        TimeChanged: (event) => {{
                            // Optional: Log playback position (can be noisy)
                            // logDebug(`Playback position: ${{event.time}}`);
                        }},
                        Playing: () => {{
                            logDebug('Playback started');
                            updateStatus('Playing');
                            sendStateChange('playing');
                        }},
                        Paused: () => {{
                            logDebug('Playback paused');
                            updateStatus('Paused');
                            sendStateChange('paused');
                        }},
                        Ended: () => {{
                            logDebug('Playback ended');
                            updateStatus('Ended');
                            sendStateChange('ended');
                        }},
                        // Additional events to track
                        Destroy: () => logDebug('Player destroyed'),
                        SourceUnloaded: () => logDebug('Source unloaded'),
                        VolumeChanged: (e) => logDebug(`Volume changed: ${{e.muted ? 'Muted' : 'Unmuted'}}, level: ${{e.volume}}`)
                    }}
                }};
                
                try {{
                    // Important - log pre-creation status
                    logDebug('Pre-creation check - MKPlayer SDK exists: ' + (typeof mkplayer !== 'undefined'));
                    
                    // Create player instance
                    logDebug('Creating player instance now');
                    window.player = new mkplayer.MKPlayer(videoContainer, playerConfig);
                    player = window.player;
                    
                    // Verify player instance
                    if (!player) {{
                        showError('Player instance creation failed - player is null');
                        updateStatus('Creation failed');
                        return;
                    }}
                    
                    logDebug('MKPlayer instance created successfully, loading source...');
                    
                    // Setup source configuration
                    const sourceConfig = {{
                        {sourceConfig}
                    }};
                    
                    // Check for empty URL
                    if (!'{effectiveStreamUrl}') {{
                        showError('Stream URL is empty');
                        updateStatus('Empty URL');
                        return;
                    }}
                    
                    // Load the source
                    logDebug('Loading source URL: {effectiveStreamUrl}');
                    updateStatus('Loading source...');
                    
                    player.load(sourceConfig)
                        .then(() => {{
                            logDebug('Source loaded successfully via promise');
                            sourceLoaded = true;
                            updateStatus('Source loaded (promise)');
                        }})
                        .catch((error) => {{
                            const errorMsg = error.message || error.toString();
                            logDebug('Error loading source: ' + errorMsg);
                            showError('Source loading error: ' + errorMsg);
                            updateStatus('Source load error');
                        }});
                }} catch (err) {{
                    const errorMsg = err.message || err.toString();
                    logDebug('Error creating player: ' + errorMsg);
                    showError('Player creation error: ' + errorMsg);
                    updateStatus('Creation error');
                    
                    // Additional stack trace if available
                    if (err.stack) {{
                        logDebug('Stack trace: ' + err.stack);
                    }}
                }}
            }} catch (err) {{
                const errorMsg = err.message || err.toString();
                logDebug('Initialization error: ' + errorMsg);
                showError('Player initialization error: ' + errorMsg);
                updateStatus('Init error');
                
                // Additional stack trace if available
                if (err.stack) {{
                    logDebug('Stack trace: ' + err.stack);
                }}
            }}
        }}
        
        // Send state change to native code
        function sendStateChange(state) {{
            logDebug('Player state changed to: ' + state);
            try {{
                if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.onPlayerStateChanged) {{
                    window.webkit.messageHandlers.onPlayerStateChanged.postMessage(state);
                }} else if (window.chrome && window.chrome.webview) {{
                    window.chrome.webview.postMessage(JSON.stringify({{ type: 'onPlayerStateChanged', data: state }}));
                }}
                
                // Always try HTTP fallback
                fetch('/bridge/' + state)
                    .catch(e => logDebug('HTTP state change notification failed: ' + e.message));
            }} catch(e) {{
                logDebug('Error sending state change: ' + e.message);
                // Last resort
                fetch('/bridge/' + state)
                    .catch(() => {{}});
            }}
        }}
        
        // Setup retry button
        document.getElementById('retry-button').addEventListener('click', () => {{
            const button = document.getElementById('retry-button');
            button.style.display = 'none';
            document.getElementById('error-container').style.display = 'none';
            
            logDebug('Manual retry requested');
            
            // Reset status tracking
            sdkLoaded = false;
            playerCreated = false;
            sourceLoaded = false;
            
            // Reload the page with a new timestamp
            window.location.href = '/player.html?t=' + Date.now();
        }});
        
        // Start checking MKPlayer accessibility
        window.addEventListener('load', () => {{
            logDebug('Window loaded, starting initialization');
            checkMKPlayerAccess();
            checkStreamUrl();
        }});
        
        // Initialize immediately if document is already loaded
        if (document.readyState === 'complete') {{
            logDebug('Document already complete, starting initialization');
            checkMKPlayerAccess();
            checkStreamUrl();
        }}
        
        // Define explicit force initialization function for external calling
        window.forcePlayerInit = function() {{
            logDebug('Force initialization requested');
            if (!sdkLoaded) {{
                loadMKPlayerSDK();
            }} else if (!playerCreated) {{
                initializePlayer();
            }}
        }};
        
        // Final stats logging
        logDebug('Environment check:');
        logDebug('- User Agent: ' + navigator.userAgent);
        logDebug('- Platform: ' + navigator.platform);
        logDebug('- Language: ' + navigator.language);
        logDebug('- Content URL: {serverUrl}/player.html?t={timestamp}');
        logDebug('- Stream URL: {effectiveStreamUrl}');
        logDebug('- MKPlayer JS: {sdkUrl}');
        logDebug('- MKPlayer CSS: {cssUrl}');
    </script>
</body>
</html>";

            return html;
        }

        /// <summary>
        /// Cleans up resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Stop the local HTTP server
                if (serverCancellationTokenSource != null)
                {
                    serverCancellationTokenSource.Cancel();
                    serverCancellationTokenSource.Dispose();
                    serverCancellationTokenSource = null;
                }

                if (httpListener != null)
                {
                    try
                    {
                        httpListener.Stop();
                        httpListener.Close();
                    }
                    catch (Exception ex)
                    {
                        LogError("Error stopping HTTP listener", ex);
                    }
                    httpListener = null;
                }

                LogInfo("MKIOPlayer resources disposed");
            }
            catch (Exception ex)
            {
                LogError("Error disposing MKIOPlayer", ex);
            }
        }
    }
}