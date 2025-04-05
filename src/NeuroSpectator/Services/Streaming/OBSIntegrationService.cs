using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Dispatching;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;
using NeuroSpectator.Services.Visualisation;
using OBSWebsocketDotNet.Communication;

namespace NeuroSpectator.Services.Streaming
{
    /// <summary>
    /// Enhanced service for integrating with OBS Studio via WebSockets,
    /// specifically designed for brain data visualization in NeuroSpectator
    /// </summary>
    public class OBSIntegrationService : IDisposable
    {
        private readonly OBSWebsocket obsWebsocket;
        private readonly IDispatcher dispatcher;
        private readonly BrainDataVisualisationService visualizationService;
        private string currentScene;
        private string brainDataBrowserSourceName;
        private bool isDisposed;

        // Default browser source settings
        private const string DEFAULT_BRAIN_SOURCE_NAME = "NeuroSpectator Brain Data";
        private const int DEFAULT_WIDTH = 400;
        private const int DEFAULT_HEIGHT = 600;
        private const bool DEFAULT_USE_CUSTOM_FRAME_RATE = false;
        private const int DEFAULT_FPS = 30;

        /// <summary>
        /// Gets whether the service is connected to OBS
        /// </summary>
        public bool IsConnected => obsWebsocket.IsConnected;

        /// <summary>
        /// Gets the name of the current scene in OBS
        /// </summary>
        public string CurrentScene => currentScene;

        /// <summary>
        /// Gets the name of the brain data browser source
        /// </summary>
        public string BrainDataSourceName => brainDataBrowserSourceName ?? DEFAULT_BRAIN_SOURCE_NAME;

        /// <summary>
        /// Event fired when connection status changes
        /// </summary>
        public event EventHandler<bool> ConnectionStatusChanged;

        /// <summary>
        /// Event fired when an error occurs
        /// </summary>
        public event EventHandler<Exception> ConnectionError;

        /// <summary>
        /// Event fired when streaming status changes
        /// </summary>
        public event EventHandler<bool> StreamingStatusChanged;

        /// <summary>
        /// Event fired when a scene changes in OBS
        /// </summary>
        public event EventHandler<string> SceneChanged;

        /// <summary>
        /// Event fired when new brain data statistics are available
        /// </summary>
        public event EventHandler<Dictionary<string, string>> BrainDataStatsUpdated;

        /// <summary>
        /// Creates a new instance of the OBSIntegrationService
        /// </summary>
        public OBSIntegrationService(IDispatcher dispatcher, BrainDataVisualisationService visualizationService = null)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.visualizationService = visualizationService;
            obsWebsocket = new OBSWebsocket();

            // Subscribe to OBS WebSocket events
            obsWebsocket.Connected += OnConnected;
            obsWebsocket.Disconnected += OnDisconnected;
            obsWebsocket.StreamStateChanged += OnStreamingStateChanged;
            obsWebsocket.CurrentProgramSceneChanged += OnSceneChanged;
            obsWebsocket.SceneItemCreated += OnSceneItemCreated;
            obsWebsocket.SceneItemRemoved += OnSceneItemRemoved;
            obsWebsocket.SourceFilterCreated += OnSourceFilterCreated;

            // Set a default browser source name
            brainDataBrowserSourceName = DEFAULT_BRAIN_SOURCE_NAME;
        }

        /// <summary>
        /// Connects to OBS WebSockets
        /// </summary>
        public async Task ConnectAsync(string url = "ws://localhost:4444", string password = "")
        {
            try
            {
                // Using a task completion source to properly wait for async operation
                var tcs = new TaskCompletionSource<bool>();

                // Setup event handlers for one-time connection events
                EventHandler onConnected = null;
                EventHandler<ObsDisconnectionInfo> onDisconnected = null;

                onConnected = (s, e) => {
                    obsWebsocket.Connected -= onConnected;
                    obsWebsocket.Disconnected -= onDisconnected;
                    tcs.TrySetResult(true);
                };

                onDisconnected = (s, e) => {
                    obsWebsocket.Connected -= onConnected;
                    obsWebsocket.Disconnected -= onDisconnected;
                    tcs.TrySetException(new Exception($"Connection failed: {e.DisconnectReason}"));
                };

                // Register one-time event handlers
                obsWebsocket.Connected += onConnected;
                obsWebsocket.Disconnected += onDisconnected;

                // Start connection attempt
                obsWebsocket.ConnectAsync(url, password);

                // Set a timeout of 10 seconds
                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Clean up event handlers
                    obsWebsocket.Connected -= onConnected;
                    obsWebsocket.Disconnected -= onDisconnected;
                    throw new TimeoutException("Connection to OBS timed out after 10 seconds");
                }

                // Wait for the actual connection task to complete
                await tcs.Task;
            }
            catch (AuthFailureException)
            {
                throw new Exception("Authentication failed. Please check your password.");
            }
            catch (ErrorResponseException ex)
            {
                RaiseError(new Exception($"OBS error: {ex.Message}"));
                throw;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Disconnects from OBS WebSockets
        /// </summary>
        public async Task DisconnectAsync()
        {
            obsWebsocket.Disconnect();
            await Task.CompletedTask; // For async pattern consistency
        }

        /// <summary>
        /// Starts streaming in OBS
        /// </summary>
        public async Task StartStreamingAsync()
        {
            try
            {
                obsWebsocket.StartStream();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Stops streaming in OBS
        /// </summary>
        public async Task StopStreamingAsync()
        {
            try
            {
                obsWebsocket.StopStream();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Toggles streaming in OBS
        /// </summary>
        public async Task<bool> ToggleStreamingAsync()
        {
            try
            {
                return await Task.FromResult(obsWebsocket.ToggleStream());
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the current streaming status from OBS
        /// </summary>
        public async Task<OutputStatus> GetStreamStatusAsync()
        {
            try
            {
                return obsWebsocket.GetStreamStatus();
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets a list of available scenes in OBS
        /// </summary>
        public async Task<List<string>> GetScenesAsync()
        {
            try
            {
                var scenes = obsWebsocket.ListScenes();
                var sceneNames = new List<string>();

                foreach (var scene in scenes)
                {
                    sceneNames.Add(scene.Name);
                }

                return sceneNames;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets a list of sources in a scene
        /// </summary>
        public async Task<List<string>> GetSceneSourcesAsync(string sceneName)
        {
            try
            {
                var sources = obsWebsocket.GetSceneItemList(sceneName);
                var sourceNames = new List<string>();

                foreach (var source in sources)
                {
                    sourceNames.Add(source.SourceName);
                }

                return sourceNames;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Sets the visibility of a source in a scene
        /// </summary>
        public async Task SetSourceVisibilityAsync(string sceneName, string sourceName, bool isVisible)
        {
            try
            {
                // First we need to get the scene item ID
                int sceneItemId = obsWebsocket.GetSceneItemId(sceneName, sourceName, 0);

                // Now set the visibility using the scene item ID
                obsWebsocket.SetSceneItemEnabled(sceneName, sceneItemId, isVisible);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Sets the visibility of a source filter
        /// </summary>
        public async Task SetSourceFilterVisibilityAsync(string sourceName, string filterName, bool isVisible)
        {
            try
            {
                obsWebsocket.SetSourceFilterEnabled(sourceName, filterName, isVisible);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Updates a browser source with a new URL
        /// </summary>
        public async Task UpdateBrowserSourceAsync(string sourceName, string url)
        {
            try
            {
                var settings = obsWebsocket.GetInputSettings(sourceName);

                // Update the URL 
                var settingsObj = settings.Settings;
                settingsObj["url"] = url;
                settingsObj["refresh"] = true; // Force refresh

                obsWebsocket.SetInputSettings(sourceName, settingsObj);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Updates a browser source with additional settings
        /// </summary>
        public async Task UpdateBrowserSourceWithSettingsAsync(string sourceName, string url, int width = -1, int height = -1, bool refresh = true)
        {
            try
            {
                var settings = obsWebsocket.GetInputSettings(sourceName);

                // Update the URL 
                var settingsObj = settings.Settings;
                settingsObj["url"] = url;
                settingsObj["refresh"] = refresh;

                // Update dimensions if provided
                if (width > 0)
                    settingsObj["width"] = width;
                if (height > 0)
                    settingsObj["height"] = height;

                obsWebsocket.SetInputSettings(sourceName, settingsObj);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Switches to a different scene
        /// </summary>
        public async Task SwitchSceneAsync(string sceneName)
        {
            try
            {
                obsWebsocket.SetCurrentProgramScene(sceneName);
                currentScene = sceneName;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Sets streaming settings for RTMP
        /// </summary>
        public async Task SetStreamSettingsAsync(string rtmpUrl, string streamKey)
        {
            try
            {
                var streamingService = new StreamingService
                {
                    Type = "rtmp_custom",
                    Settings = new StreamingServiceSettings
                    {
                        Server = rtmpUrl,
                        Key = streamKey
                    }
                };

                obsWebsocket.SetStreamServiceSettings(streamingService);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Creates a browser source for brain data visualization if it doesn't exist
        /// </summary>
        public async Task<bool> CreateOrUpdateBrainDataSourceAsync(string sceneName, string url, string sourceName = null, int width = DEFAULT_WIDTH, int height = DEFAULT_HEIGHT)
        {
            try
            {
                // Use the provided source name or the default
                string targetSourceName = sourceName ?? brainDataBrowserSourceName;
                brainDataBrowserSourceName = targetSourceName;

                // Check if the source exists in the scene
                var sceneItems = obsWebsocket.GetSceneItemList(sceneName);
                bool sourceExists = false;

                foreach (var item in sceneItems)
                {
                    if (item.SourceName == targetSourceName)
                    {
                        sourceExists = true;
                        break;
                    }
                }

                if (!sourceExists)
                {
                    // Source doesn't exist, create it
                    var settings = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "url", url },
                        { "width", width },
                        { "height", height },
                        { "fps", DEFAULT_FPS },
                        { "reroute_audio", false },
                        { "restart_when_active", true },
                        { "shutdown", false },
                        { "use_custom_frame_rate", DEFAULT_USE_CUSTOM_FRAME_RATE }
                    };

                    // Convert to JObject
                    var settingsJObj = Newtonsoft.Json.Linq.JObject.FromObject(settings);

                    // Create the input in the scene
                    obsWebsocket.CreateInput(sceneName, targetSourceName, "browser_source", settingsJObj, true);
                    return true;
                }
                else
                {
                    // Source exists, update it
                    await UpdateBrowserSourceWithSettingsAsync(targetSourceName, url, width, height);
                    return true;
                }
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                return false;
            }
        }

        /// <summary>
        /// Adds a color correction filter to a source to highlight brain activity
        /// </summary>
        public async Task<bool> AddBrainActivityColorFilterAsync(string sourceName, string filterName)
        {
            try
            {
                // Check if the filter already exists
                var filters = obsWebsocket.GetSourceFilterList(sourceName);
                bool filterExists = false;

                foreach (var filter in filters)
                {
                    if (filter.Name == filterName)
                    {
                        filterExists = true;
                        break;
                    }
                }

                if (!filterExists)
                {
                    // Create a color correction filter with elevated saturation for brain activity
                    var settings = new Dictionary<string, object>
                    {
                        { "brightness", 0.0 },
                        { "color_add", 0.0 },
                        { "color_multiply", 16777215 }, // White (0xFFFFFF)
                        { "contrast", 0.0 },
                        { "gamma", 0.0 },
                        { "hue_shift", 0.0 },
                        { "opacity", 1.0 },
                        { "saturation", 0.5 } // Increased saturation
                    };

                    // Convert to JObject
                    var settingsJObj = Newtonsoft.Json.Linq.JObject.FromObject(settings);

                    // Create the filter
                    obsWebsocket.CreateSourceFilter(sourceName, filterName, "color_filter", settingsJObj);
                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                return false;
            }
        }

        /// <summary>
        /// Takes a screenshot of the current OBS output
        /// </summary>
        public async Task<string> TakeScreenshotAsync(string outputPath)
        {
            try
            {
                var currentScene = obsWebsocket.GetCurrentProgramScene();
                string screenshotPath = outputPath;

                // Generate default path if not provided
                if (string.IsNullOrEmpty(screenshotPath))
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                        "NeuroSpectator");

                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    screenshotPath = Path.Combine(dir, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                }

                // Take the screenshot
                obsWebsocket.SaveSourceScreenshot(currentScene, "png", screenshotPath);
                return screenshotPath;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Updates the color correction of a filter based on brain activity level
        /// </summary>
        public async Task UpdateBrainActivityFilterAsync(string sourceName, string filterName, double activityLevel)
        {
            try
            {
                // Ensure we're working with a value between 0 and 1
                activityLevel = Math.Clamp(activityLevel, 0.0, 1.0);

                // Get the current filter settings
                var filter = obsWebsocket.GetSourceFilter(sourceName, filterName);

                // Create updated settings with color based on activity level
                // Higher activity = more red/warm colors
                var settings = filter.Settings;

                // Calculate a color transition from blue (low) to red (high) based on activity level
                // RGB color math: blend from 0x0000FF (blue) to 0xFF0000 (red)
                int red = (int)(255 * activityLevel);
                int blue = (int)(255 * (1 - activityLevel));
                int colorValue = (red << 16) | blue; // Red and blue components

                settings["color_multiply"] = colorValue;
                settings["brightness"] = activityLevel * 0.2; // Slight brightness increase with activity
                settings["opacity"] = 0.3 + (activityLevel * 0.7); // Increase opacity with activity

                // Apply the updated filter settings
                obsWebsocket.SetSourceFilterSettings(sourceName, filterName, settings);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
            }
        }

        /// <summary>
        /// Pulses a source's visibility briefly to highlight a brain event
        /// </summary>
        public async Task PulseBrainEventHighlightAsync(string sceneName, string sourceName, int durationMs = 1000)
        {
            try
            {
                // Make the source visible
                await SetSourceVisibilityAsync(sceneName, sourceName, true);

                // Wait for the specified duration
                await Task.Delay(durationMs);

                // Hide the source again
                await SetSourceVisibilityAsync(sceneName, sourceName, false);
            }
            catch (Exception ex)
            {
                RaiseError(ex);
            }
        }

        /// <summary>
        /// Synchronizes the brain data with OBS by updating the browser source
        /// </summary>
        public async Task SyncBrainDataWithOBSAsync(Dictionary<string, string> brainMetrics)
        {
            try
            {
                if (visualizationService != null && IsConnected)
                {
                    // Update the visualization data
                    await visualizationService.UpdateBrainMetricsAsync(brainMetrics);

                    // If we have a browser source, update it to show the latest data
                    if (!string.IsNullOrEmpty(brainDataBrowserSourceName) && !string.IsNullOrEmpty(currentScene))
                    {
                        // Get the visualization URL
                        string url = visualizationService.VisualisationUrl + "/brain_data.html";

                        // Force the browser source to refresh
                        await UpdateBrowserSourceAsync(brainDataBrowserSourceName, url);

                        // If we have a focus metric, use it to update a highlight filter if it exists
                        if (brainMetrics.TryGetValue("Focus", out string focusValue))
                        {
                            double focusLevel = 0;
                            // Parse percentage value
                            if (focusValue.EndsWith("%"))
                            {
                                if (double.TryParse(focusValue.TrimEnd('%'), out double percent))
                                {
                                    focusLevel = percent / 100.0;
                                }
                            }

                            // Update a highlight filter if it exists
                            string filterName = "BrainActivityHighlight";
                            await UpdateBrainActivityFilterAsync(brainDataBrowserSourceName, filterName, focusLevel);
                        }
                    }

                    // Notify subscribers about the updated brain data
                    BrainDataStatsUpdated?.Invoke(this, brainMetrics);
                }
            }
            catch (Exception ex)
            {
                RaiseError(ex);
            }
        }

        /// <summary>
        /// Creates a highlight transition when a significant brain event occurs
        /// </summary>
        public async Task HighlightBrainEventAsync(string eventType, string eventDescription, double intensity = 1.0)
        {
            try
            {
                if (visualizationService != null && IsConnected)
                {
                    // Add an event marker in the brain data
                    await visualizationService.AddEventMarkerAsync(eventType, eventDescription);

                    // Do OBS-specific event highlighting
                    string currentScene = obsWebsocket.GetCurrentProgramScene();

                    // Option 1: Create a stinger transition effect
                    // (This would require a stinger transition to be set up in OBS)

                    // Option 2: Briefly show a highlight source
                    string highlightSourceName = "BrainEventHighlight";
                    await PulseBrainEventHighlightAsync(currentScene, highlightSourceName);

                    // Option 3: Take a screenshot to capture the moment
                    string screenshotDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                        "NeuroSpectator", "BrainEvents");

                    if (!Directory.Exists(screenshotDir))
                        Directory.CreateDirectory(screenshotDir);

                    string screenshotPath = Path.Combine(
                        screenshotDir,
                        $"BrainEvent_{eventType}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                    await TakeScreenshotAsync(screenshotPath);
                }
            }
            catch (Exception ex)
            {
                RaiseError(ex);
            }
        }

        /// <summary>
        /// Creates a set of sources for brain data visualization in the specified scene
        /// </summary>
        public async Task SetupBrainDataVisualizationAsync(string sceneName)
        {
            try
            {
                if (!IsConnected)
                    throw new InvalidOperationException("Not connected to OBS");

                // 1. Create the main brain data browser source
                string brainDataUrl = visualizationService?.VisualisationUrl + "/brain_data.html" ?? "about:blank";
                await CreateOrUpdateBrainDataSourceAsync(sceneName, brainDataUrl);

                // 2. Add a color correction filter for brain activity highlights
                await AddBrainActivityColorFilterAsync(brainDataBrowserSourceName, "BrainActivityHighlight");

                // 3. Create a brain event highlight source (initially hidden)
                string highlightSourceName = "BrainEventHighlight";
                bool highlightExists = false;

                var sceneItems = obsWebsocket.GetSceneItemList(sceneName);
                foreach (var item in sceneItems)
                {
                    if (item.SourceName == highlightSourceName)
                    {
                        highlightExists = true;
                        break;
                    }
                }

                if (!highlightExists)
                {
                    // Create a color source for highlights
                    var settings = new Dictionary<string, object>
                    {
                        { "color", 0xFF5500 }, // Orange-red highlight
                        { "width", 1920 },
                        { "height", 1080 }
                    };

                    // Convert to JObject
                    var settingsJObj = Newtonsoft.Json.Linq.JObject.FromObject(settings);

                    // Create the input
                    obsWebsocket.CreateInput(sceneName, highlightSourceName, "color_source", settingsJObj, false);

                    // Add a fade filter to the highlight
                    var fadeSettings = new Dictionary<string, object>
                    {
                        { "opacity", 0.3 }
                    };

                    var fadeSettingsJObj = Newtonsoft.Json.Linq.JObject.FromObject(fadeSettings);
                    obsWebsocket.CreateSourceFilter(highlightSourceName, "FadeFilter", "color_filter", fadeSettingsJObj);
                }

                // 4. Position the brain data browser source (get ID first, then transform)
                sceneItems = obsWebsocket.GetSceneItemList(sceneName);
                foreach (var item in sceneItems)
                {
                    if (item.SourceName == brainDataBrowserSourceName)
                    {
                        // Get the ID to apply transforms - fix property name
                        int sceneItemId = item.ItemId; // or the correct property from SceneItemDetails

                        // Create transform info with correct property names and enum value
                        var transform = new SceneItemTransformInfo
                        {
                            X = 1520,
                            Y = 0,
                            Alignnment = 5, // Using the property as it exists in class (double 'n')
                            BoundsWidth = 400,
                            BoundsHeight = 600,
                            BoundsType = SceneItemBoundsType.OBS_BOUNDS_STRETCH
                        };

                        // Apply the transform 
                        obsWebsocket.SetSceneItemTransform(sceneName, sceneItemId, transform);
                        break;
                    }
                }

                // Set current scene
                currentScene = sceneName;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        #region Event Handlers

        /// <summary>
        /// Event handler for OBS Connected event
        /// </summary>
        private void OnConnected(object sender, EventArgs e)
        {
            dispatcher.Dispatch(() =>
            {
                try
                {
                    // Get the current scene
                    currentScene = obsWebsocket.GetCurrentProgramScene();
                    ConnectionStatusChanged?.Invoke(this, true);
                }
                catch (Exception ex)
                {
                    RaiseError(ex);
                }
            });
        }

        /// <summary>
        /// Event handler for OBS Disconnected event
        /// </summary>
        private void OnDisconnected(object sender, ObsDisconnectionInfo e)
        {
            dispatcher.Dispatch(() =>
            {
                ConnectionStatusChanged?.Invoke(this, false);
            });
        }

        /// <summary>
        /// Event handler for streaming state changes
        /// </summary>
        private void OnStreamingStateChanged(object sender, StreamStateChangedEventArgs e)
        {
            dispatcher.Dispatch(() =>
            {
                StreamingStatusChanged?.Invoke(this, e.OutputState.State == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED);
            });
        }

        /// <summary>
        /// Event handler for scene changes
        /// </summary>
        private void OnSceneChanged(object sender, ProgramSceneChangedEventArgs e)
        {
            dispatcher.Dispatch(() =>
            {
                currentScene = e.SceneName;
                SceneChanged?.Invoke(this, e.SceneName);
            });
        }

        /// <summary>
        /// Event handler for scene item creation
        /// </summary>
        private void OnSceneItemCreated(object sender, SceneItemCreatedEventArgs e)
        {
            // Can be used to track when sources are added
        }

        /// <summary>
        /// Event handler for scene item removal
        /// </summary>
        private void OnSceneItemRemoved(object sender, SceneItemRemovedEventArgs e)
        {
            // Can be used to track when sources are removed
        }

        /// <summary>
        /// Event handler for source filter creation
        /// </summary>
        private void OnSourceFilterCreated(object sender, SourceFilterCreatedEventArgs e)
        {
            // Can be used to track when filters are added
        }

        #endregion

        /// <summary>
        /// Raises an error event
        /// </summary>
        private void RaiseError(Exception ex)
        {
            dispatcher.Dispatch(() =>
            {
                ConnectionError?.Invoke(this, ex);
            });
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
                    // Unsubscribe from events
                    obsWebsocket.Connected -= OnConnected;
                    obsWebsocket.Disconnected -= OnDisconnected;
                    obsWebsocket.StreamStateChanged -= OnStreamingStateChanged;
                    obsWebsocket.CurrentProgramSceneChanged -= OnSceneChanged;
                    obsWebsocket.SceneItemCreated -= OnSceneItemCreated;
                    obsWebsocket.SceneItemRemoved -= OnSceneItemRemoved;
                    obsWebsocket.SourceFilterCreated -= OnSourceFilterCreated;

                    // Disconnect from OBS
                    if (obsWebsocket.IsConnected)
                    {
                        obsWebsocket.Disconnect();
                    }
                }

                isDisposed = true;
            }
        }
    }
}