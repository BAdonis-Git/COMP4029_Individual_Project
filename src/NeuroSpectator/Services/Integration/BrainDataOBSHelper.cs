using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Dispatching;
using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.Streaming;
using NeuroSpectator.Services.Visualisation;

namespace NeuroSpectator.Services.Integration
{
    /// <summary>
    /// Service to coordinate brain data acquisition and OBS integration
    /// </summary>
    public class BrainDataOBSHelper : IDisposable
    {
        private readonly IDispatcher dispatcher;
        private readonly IBCIDevice bciDevice;
        private readonly OBSIntegrationService obsService;
        private readonly BrainDataVisualisationService visualizationService;
        private readonly BrainDataJsonService jsonService;

        private Dictionary<string, string> currentBrainMetrics = new Dictionary<string, string>();
        private List<string> significantEvents = new List<string>();
        private bool isMonitoring = false;
        private bool isDisposed = false;

        // Thresholds for brain events
        private const double FOCUS_THRESHOLD_HIGH = 0.8;    // 80% focus is high
        private const double FOCUS_THRESHOLD_LOW = 0.3;     // 30% focus is low
        private const double ALPHA_THRESHOLD_HIGH = 0.7;    // 70% alpha is high
        private const double ALPHA_THRESHOLD_LOW = 0.3;     // 30% alpha is low

        // OBS scene configuration
        private string activeScene;
        private string brainDataSourceName = "NeuroSpectator Brain Data";
        private string eventHighlightSourceName = "BrainEventHighlight";

        /// <summary>
        /// Gets the current brain metrics
        /// </summary>
        public Dictionary<string, string> CurrentBrainMetrics => new Dictionary<string, string>(currentBrainMetrics);

        /// <summary>
        /// Event fired when brain metrics are updated
        /// </summary>
        public event EventHandler<Dictionary<string, string>> BrainMetricsUpdated;

        /// <summary>
        /// Event fired when a significant brain event is detected
        /// </summary>
        public event EventHandler<string> SignificantBrainEventDetected;

        /// <summary>
        /// Event fired when an error occurs
        /// </summary>
        public event EventHandler<Exception> ErrorOccurred;

        /// <summary>
        /// Creates a new instance of the BrainDataOBSHelper
        /// </summary>
        public BrainDataOBSHelper(
            IDispatcher dispatcher,
            IBCIDevice bciDevice,
            OBSIntegrationService obsService,
            BrainDataVisualisationService visualizationService,
            BrainDataJsonService jsonService)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.bciDevice = bciDevice ?? throw new ArgumentNullException(nameof(bciDevice));
            this.obsService = obsService ?? throw new ArgumentNullException(nameof(obsService));
            this.visualizationService = visualizationService ?? throw new ArgumentNullException(nameof(visualizationService));
            this.jsonService = jsonService ?? throw new ArgumentNullException(nameof(jsonService));

            // Subscribe to BCI device events
            this.bciDevice.BrainWaveDataReceived += OnBrainWaveDataReceived;
            this.bciDevice.ArtifactDetected += OnArtifactDetected;
            this.bciDevice.ErrorOccurred += OnBCIErrorOccurred;

            // Subscribe to OBS events
            this.obsService.SceneChanged += OnOBSSceneChanged;
            this.obsService.ConnectionStatusChanged += OnOBSConnectionStatusChanged;

            // Initialize brain metrics with default values
            InitializeBrainMetrics();
        }

        /// <summary>
        /// Initializes brain metrics with default values
        /// </summary>
        private void InitializeBrainMetrics()
        {
            currentBrainMetrics["Focus"] = "0%";
            currentBrainMetrics["Alpha Wave"] = "Low";
            currentBrainMetrics["Beta Wave"] = "Low";
            currentBrainMetrics["Theta Wave"] = "Low";
            currentBrainMetrics["Delta Wave"] = "Low";
            currentBrainMetrics["Gamma Wave"] = "Low";
        }

        /// <summary>
        /// Starts monitoring brain data and syncing with OBS
        /// </summary>
        public async Task StartMonitoringAsync(bool setupOBSScene = true)
        {
            if (isMonitoring)
                return;

            try
            {
                // Check if we're connected to the device and OBS
                if (!bciDevice.IsConnected)
                {
                    throw new InvalidOperationException("BCI device is not connected");
                }

                // Set up OBS integration if requested
                if (setupOBSScene && obsService.IsConnected)
                {
                    // Get active scene
                    activeScene = obsService.CurrentScene;

                    // Set up OBS scene with brain data visualization
                    await obsService.SetupBrainDataVisualizationAsync(activeScene);
                    brainDataSourceName = obsService.BrainDataSourceName;
                }

                // Start the visualization server
                await visualizationService.StartServerAsync();

                // Register for brain wave data
                bciDevice.RegisterForBrainWaveData(BrainWaveTypes.All);

                isMonitoring = true;

                // Initial sync of data
                await SyncBrainDataWithOBSAsync();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Stops monitoring brain data
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            if (!isMonitoring)
                return;

            try
            {
                // Unregister from brain wave data
                bciDevice.UnregisterFromBrainWaveData(BrainWaveTypes.All);

                // Stop the visualization server
                await visualizationService.StopServerAsync();

                isMonitoring = false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Syncs the current brain data with OBS
        /// </summary>
        public async Task SyncBrainDataWithOBSAsync()
        {
            if (!isMonitoring || !obsService.IsConnected)
                return;

            try
            {
                // First update the visualization data
                await visualizationService.UpdateBrainMetricsAsync(currentBrainMetrics);

                // Then update the OBS sources
                await obsService.SyncBrainDataWithOBSAsync(currentBrainMetrics);

                // Also update the JSON data for other integrations
                await jsonService.UpdateDataAsync(currentBrainMetrics);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Handles a significant brain event by updating OBS
        /// </summary>
        public async Task HandleSignificantBrainEventAsync(string eventType, string description)
        {
            try
            {
                // Add to significant events
                significantEvents.Add($"[{DateTime.Now:HH:mm:ss}] {eventType}: {description}");

                // Only keep the last 10 events
                if (significantEvents.Count > 10)
                {
                    significantEvents.RemoveAt(0);
                }

                // Update the JSON data with an event marker
                await jsonService.AddEventMarkerAsync(eventType, description);

                // Update OBS with the event
                if (obsService.IsConnected)
                {
                    await obsService.HighlightBrainEventAsync(eventType, description);
                }

                // Notify subscribers
                SignificantBrainEventDetected?.Invoke(this, $"{eventType}: {description}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Updates brain metrics based on raw brain wave data
        /// </summary>
        private async Task ProcessBrainWaveDataAsync(BrainWaveTypes waveType, double value)
        {
            // Update the corresponding metric
            switch (waveType)
            {
                case BrainWaveTypes.Alpha:
                    UpdateWaveMetric("Alpha Wave", value);
                    break;

                case BrainWaveTypes.Beta:
                    UpdateWaveMetric("Beta Wave", value);
                    break;

                case BrainWaveTypes.Theta:
                    UpdateWaveMetric("Theta Wave", value);
                    break;

                case BrainWaveTypes.Delta:
                    UpdateWaveMetric("Delta Wave", value);
                    break;

                case BrainWaveTypes.Gamma:
                    UpdateWaveMetric("Gamma Wave", value);
                    break;
            }

            // Calculate focus level based on beta/alpha ratio
            if (currentBrainMetrics.TryGetValue("Beta Wave", out string betaLevel) &&
                currentBrainMetrics.TryGetValue("Alpha Wave", out string alphaLevel))
            {
                double beta = ConvertLevelToValue(betaLevel);
                double alpha = ConvertLevelToValue(alphaLevel);

                // Focus is roughly beta/alpha ratio, normalized to 0-100%
                double betaAlphaRatio = alpha > 0 ? Math.Min(beta / alpha, 3.0) : 1.0;
                double focusPercent = Math.Round(betaAlphaRatio / 3.0 * 100);

                // Update focus metric
                string oldFocusValue = currentBrainMetrics["Focus"];
                currentBrainMetrics["Focus"] = $"{focusPercent}%";

                // Detect significant focus changes
                if (!oldFocusValue.Equals(currentBrainMetrics["Focus"]))
                {
                    double focusValue = ParseFocusPercentage(currentBrainMetrics["Focus"]);
                    double oldFocusValueNum = ParseFocusPercentage(oldFocusValue);
                    double focusDelta = focusValue - oldFocusValueNum;

                    // Check for significant changes (>20% change)
                    if (Math.Abs(focusDelta) > 20)
                    {
                        string eventType = focusDelta > 0 ? "FocusIncrease" : "FocusDecrease";
                        string description = $"Focus level changed from {oldFocusValue} to {currentBrainMetrics["Focus"]}";
                        await HandleSignificantBrainEventAsync(eventType, description);
                    }
                    // Check for high/low thresholds being crossed
                    else if ((focusValue >= FOCUS_THRESHOLD_HIGH && oldFocusValueNum < FOCUS_THRESHOLD_HIGH) ||
                             (focusValue <= FOCUS_THRESHOLD_LOW && oldFocusValueNum > FOCUS_THRESHOLD_LOW))
                    {
                        string eventType = focusValue >= FOCUS_THRESHOLD_HIGH ? "HighFocus" : "LowFocus";
                        string description = $"Focus level is now {currentBrainMetrics["Focus"]}";
                        await HandleSignificantBrainEventAsync(eventType, description);
                    }
                }
            }

            // Notify subscribers
            BrainMetricsUpdated?.Invoke(this, currentBrainMetrics);

            // Sync with OBS if connected
            await SyncBrainDataWithOBSAsync();
        }

        /// <summary>
        /// Updates a wave metric based on its value
        /// </summary>
        private void UpdateWaveMetric(string metricName, double value)
        {
            // For each wave type, categorize as Low, Medium, or High
            // This is a simplified approach - real EEG would need more sophisticated analysis
            string level;

            if (value < 0.3)
                level = "Low";
            else if (value < 0.7)
                level = "Medium";
            else
                level = "High";

            // Update the metric
            currentBrainMetrics[metricName] = level;
        }

        /// <summary>
        /// Converts a level string (Low, Medium, High) to a numeric value
        /// </summary>
        private double ConvertLevelToValue(string level)
        {
            return level switch
            {
                "Low" => 0.2,
                "Medium" => 0.5,
                "High" => 0.8,
                _ => 0.0
            };
        }

        /// <summary>
        /// Parses a focus percentage string to a double
        /// </summary>
        private double ParseFocusPercentage(string focusValue)
        {
            if (focusValue.EndsWith("%"))
            {
                if (double.TryParse(focusValue.TrimEnd('%'), out double percent))
                {
                    return percent;
                }
            }

            return 0.0;
        }

        #region Event Handlers

        /// <summary>
        /// Event handler for brain wave data received
        /// </summary>
        private async void OnBrainWaveDataReceived(object sender, BrainWaveDataEventArgs e)
        {
            dispatcher.Dispatch(async () =>
            {
                await ProcessBrainWaveDataAsync(e.BrainWaveData.WaveType, e.BrainWaveData.AverageValue);
            });
        }

        /// <summary>
        /// Event handler for artifacts detected
        /// </summary>
        private async void OnArtifactDetected(object sender, ArtifactEventArgs e)
        {
            if (!isMonitoring)
                return;

            dispatcher.Dispatch(async () =>
            {
                // Handle blinks, jaw clenches, etc.
                if (e.Blink)
                {
                    await HandleSignificantBrainEventAsync("Blink", "Eye blink detected");
                }

                if (e.JawClench)
                {
                    await HandleSignificantBrainEventAsync("JawClench", "Jaw clench detected");
                }

                if (e.HeadbandTooLoose)
                {
                    // Update a status indicator
                    currentBrainMetrics["Signal Quality"] = "Poor";
                    await SyncBrainDataWithOBSAsync();
                }
            });
        }

        /// <summary>
        /// Event handler for BCI device errors
        /// </summary>
        private void OnBCIErrorOccurred(object sender, BCIErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e.Exception ?? new Exception(e.Message));
        }

        /// <summary>
        /// Event handler for OBS scene changes
        /// </summary>
        private async void OnOBSSceneChanged(object sender, string sceneName)
        {
            activeScene = sceneName;

            // If we're monitoring, update the scene with brain data visualization
            if (isMonitoring)
            {
                await SyncBrainDataWithOBSAsync();
            }
        }

        /// <summary>
        /// Event handler for OBS connection status changes
        /// </summary>
        private async void OnOBSConnectionStatusChanged(object sender, bool connected)
        {
            if (connected && isMonitoring)
            {
                // OBS just connected, set up the scene
                activeScene = obsService.CurrentScene;
                await obsService.SetupBrainDataVisualizationAsync(activeScene);
                brainDataSourceName = obsService.BrainDataSourceName;
                await SyncBrainDataWithOBSAsync();
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
        protected virtual async void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    // Stop monitoring
                    if (isMonitoring)
                    {
                        await StopMonitoringAsync();
                    }

                    // Unsubscribe from events
                    if (bciDevice != null)
                    {
                        bciDevice.BrainWaveDataReceived -= OnBrainWaveDataReceived;
                        bciDevice.ArtifactDetected -= OnArtifactDetected;
                        bciDevice.ErrorOccurred -= OnBCIErrorOccurred;
                    }

                    if (obsService != null)
                    {
                        obsService.SceneChanged -= OnOBSSceneChanged;
                        obsService.ConnectionStatusChanged -= OnOBSConnectionStatusChanged;
                    }
                }

                isDisposed = true;
            }
        }
    }
}