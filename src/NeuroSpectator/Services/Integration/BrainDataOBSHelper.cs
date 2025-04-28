using NeuroSpectator.Models.BCI.Common;
using NeuroSpectator.Services.BCI.Interfaces;
using NeuroSpectator.Services.Streaming;
using NeuroSpectator.Services.Visualisation;

namespace NeuroSpectator.Services.Integration
{
    /// <summary>
    /// Helper class for processing BCI data and integrating it with OBS
    /// </summary>
    public class BrainDataOBSHelper : IDisposable
    {
        private readonly IBCIDevice bciDevice;
        private readonly OBSIntegrationService obsService;
        private readonly BrainDataVisualisationService visualizationService;
        private readonly BrainDataJsonService jsonService;

        private CancellationTokenSource monitoringCancellationSource;
        private Task monitoringTask;
        private Dictionary<string, string> currentBrainMetrics = new Dictionary<string, string>();
        private bool isDisposed;

        // Thresholds for significant brain events
        private const double FOCUS_THRESHOLD_HIGH = 0.8; // 80%
        private const double FOCUS_THRESHOLD_JUMP = 0.2; // 20% sudden increase
        private const double ALPHA_THRESHOLD_HIGH = 70.0;
        private const double BETA_THRESHOLD_HIGH = 60.0;

        // Last values for change detection
        private double lastFocusLevel = 0;
        private double lastAlphaLevel = 0;
        private double lastBetaLevel = 0;

        // Event handlers
        public event EventHandler<Dictionary<string, string>> BrainMetricsUpdated;
        public event EventHandler<string> SignificantBrainEventDetected;
        public event EventHandler<Exception> ErrorOccurred;

        /// <summary>
        /// Gets the visualization service used by this helper
        /// </summary>
        public BrainDataVisualisationService VisualizationService => visualizationService;

        /// <summary>
        /// Gets the JSON service used by this helper
        /// </summary>
        public BrainDataJsonService JsonService => jsonService;

        /// <summary>
        /// Creates a new instance of the BrainDataOBSHelper
        /// </summary>
        public BrainDataOBSHelper(
            IBCIDevice bciDevice,
            OBSIntegrationService obsService,
            BrainDataVisualisationService visualizationService,
            BrainDataJsonService jsonService)
        {
            this.bciDevice = bciDevice ?? throw new ArgumentNullException(nameof(bciDevice));
            this.obsService = obsService ?? throw new ArgumentNullException(nameof(obsService));
            this.visualizationService = visualizationService ?? throw new ArgumentNullException(nameof(visualizationService));
            this.jsonService = jsonService ?? throw new ArgumentNullException(nameof(jsonService));

            // Initialize default brain metrics
            InitializeDefaultBrainMetrics();
        }

        /// <summary>
        /// Initializes default brain metrics
        /// </summary>
        private void InitializeDefaultBrainMetrics()
        {
            currentBrainMetrics["Focus"] = "0%";
            currentBrainMetrics["Alpha Wave"] = "Low";
            currentBrainMetrics["Beta Wave"] = "Low";
            currentBrainMetrics["Theta Wave"] = "Low";
            currentBrainMetrics["Delta Wave"] = "Low";
            currentBrainMetrics["Gamma Wave"] = "Low";
        }

        /// <summary>
        /// Starts monitoring brain data and integrating with OBS
        /// </summary>
        public async Task StartMonitoringAsync(bool setupVisualizationSources = false)
        {
            if (monitoringTask != null && !monitoringTask.IsCompleted)
                return;

            try
            {
                // Make sure the device is connected
                if (!bciDevice.IsConnected)
                {
                    await bciDevice.ConnectAsync();
                }

                // Start the HTTP server for visualizations if not already running
                if (visualizationService != null && !visualizationService.IsServerRunning)
                {
                    await visualizationService.StartServerAsync();
                }

                // Set up OBS browser sources if needed
                if (setupVisualizationSources && obsService.IsConnected)
                {
                    string currentScene = obsService.CurrentScene;
                    if (!string.IsNullOrEmpty(currentScene))
                    {
                        await obsService.SetupBrainDataVisualizationAsync(currentScene);
                    }
                }

                // Register for all brain wave types
                bciDevice.RegisterForBrainWaveData(BrainWaveTypes.All);

                // Subscribe to brain wave data events
                bciDevice.BrainWaveDataReceived += OnBrainWaveDataReceived;
                bciDevice.ArtifactDetected += OnArtifactDetected;
                bciDevice.ErrorOccurred += OnBciErrorOccurred;

                // Start the monitoring task
                monitoringCancellationSource = new CancellationTokenSource();
                monitoringTask = Task.Run(() => MonitoringLoopAsync(monitoringCancellationSource.Token));
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
            if (monitoringTask == null || monitoringTask.IsCompleted)
                return;

            try
            {
                // Cancel the monitoring task
                monitoringCancellationSource?.Cancel();

                // Wait for the task to complete
                await monitoringTask;

                // Unregister from brain wave data events
                if (bciDevice != null)
                {
                    bciDevice.BrainWaveDataReceived -= OnBrainWaveDataReceived;
                    bciDevice.ArtifactDetected -= OnArtifactDetected;
                    bciDevice.ErrorOccurred -= OnBciErrorOccurred;

                    // Unregister from all brain wave types
                    bciDevice.UnregisterFromBrainWaveData(BrainWaveTypes.All);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw;
            }
            finally
            {
                monitoringCancellationSource?.Dispose();
                monitoringCancellationSource = null;
                monitoringTask = null;
            }
        }

        /// <summary>
        /// Main monitoring loop that updates visualizations
        /// </summary>
        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Update the visualizations
                    try
                    {
                        // Update visualization services with current brain metrics
                        if (visualizationService != null)
                        {
                            await visualizationService.UpdateBrainMetricsAsync(currentBrainMetrics);
                        }

                        if (jsonService != null)
                        {
                            await jsonService.UpdateDataAsync(currentBrainMetrics);
                        }

                        // Sync with OBS if connected
                        if (obsService != null && obsService.IsConnected)
                        {
                            await obsService.SyncBrainDataWithOBSAsync(currentBrainMetrics);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorOccurred?.Invoke(this, ex);
                    }

                    // Wait for the next update interval (100ms)
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when cancellation is requested
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Handles significant brain events detected during monitoring
        /// </summary>
        public async Task HandleSignificantBrainEventAsync(string eventType, string description)
        {
            try
            {
                // Log the event
                Console.WriteLine($"Brain event: {eventType} - {description}");

                // Notify subscribers
                SignificantBrainEventDetected?.Invoke(this, description);

                // Add an event marker to the brain data visualization
                if (jsonService != null)
                {
                    await jsonService.AddEventMarkerAsync(eventType, description);
                }

                // Highlight the event in OBS if connected
                if (obsService != null && obsService.IsConnected)
                {
                    await obsService.HighlightBrainEventAsync(eventType, description);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Processes received brain wave data
        /// </summary>
        private void OnBrainWaveDataReceived(object sender, BrainWaveDataEventArgs e)
        {
            try
            {
                // Process the brain wave data based on type
                ProcessBrainWaveData(e.BrainWaveData);

                // Check for significant events
                CheckForSignificantEvents();

                // Notify subscribers of updated metrics
                BrainMetricsUpdated?.Invoke(this, currentBrainMetrics);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Processes a brain wave data packet
        /// </summary>
        private void ProcessBrainWaveData(BrainWaveData data)
        {
            switch (data.WaveType)
            {
                case BrainWaveTypes.Alpha:
                    double alphaValue = data.AverageValue;
                    lastAlphaLevel = alphaValue;
                    currentBrainMetrics["Alpha Wave"] = ClassifyWaveLevel(alphaValue);
                    break;

                case BrainWaveTypes.Beta:
                    double betaValue = data.AverageValue;
                    lastBetaLevel = betaValue;
                    currentBrainMetrics["Beta Wave"] = ClassifyWaveLevel(betaValue);
                    break;

                case BrainWaveTypes.Theta:
                    double thetaValue = data.AverageValue;
                    currentBrainMetrics["Theta Wave"] = ClassifyWaveLevel(thetaValue);
                    break;

                case BrainWaveTypes.Delta:
                    double deltaValue = data.AverageValue;
                    currentBrainMetrics["Delta Wave"] = ClassifyWaveLevel(deltaValue);
                    break;

                case BrainWaveTypes.Gamma:
                    double gammaValue = data.AverageValue;
                    currentBrainMetrics["Gamma Wave"] = ClassifyWaveLevel(gammaValue);
                    break;
            }

            // Calculate focus based on alpha and beta waves
            // This is a simplified approach - focus could be calculated in different ways
            if (lastAlphaLevel > 0 && lastBetaLevel > 0)
            {
                // Higher beta-to-alpha ratio generally indicates higher focus
                double focusLevel = Math.Min(1.0, Math.Max(0.0, lastBetaLevel / (lastAlphaLevel + lastBetaLevel)));

                // Store the previous focus level for change detection
                double previousFocus = lastFocusLevel;
                lastFocusLevel = focusLevel;

                // Convert to percentage for display
                int focusPercent = (int)(focusLevel * 100);
                currentBrainMetrics["Focus"] = $"{focusPercent}%";

                // Check for significant focus change
                double focusChange = focusLevel - previousFocus;
                if (Math.Abs(focusChange) >= FOCUS_THRESHOLD_JUMP)
                {
                    string direction = focusChange > 0 ? "increase" : "decrease";
                    string description = $"Significant focus {direction} detected: {focusPercent}%";

                    // Handle asynchronously to avoid blocking the data processing
                    Task.Run(() => HandleSignificantBrainEventAsync("FocusChange", description));
                }
            }
        }

        /// <summary>
        /// Classifies a wave level as Low, Medium, or High
        /// </summary>
        private string ClassifyWaveLevel(double value)
        {
            // These thresholds would need to be calibrated for specific BCI device
            if (value < 30)
                return "Low";
            else if (value < 60)
                return "Medium";
            else
                return "High";
        }

        /// <summary>
        /// Checks for significant brain events based on current metrics
        /// </summary>
        private void CheckForSignificantEvents()
        {
            // Check for high focus
            if (lastFocusLevel >= FOCUS_THRESHOLD_HIGH &&
                double.TryParse(currentBrainMetrics["Focus"].TrimEnd('%'), out double focusPercent) &&
                focusPercent >= FOCUS_THRESHOLD_HIGH * 100)
            {
                string description = $"High focus level detected: {focusPercent}%";
                Task.Run(() => HandleSignificantBrainEventAsync("HighFocus", description));
            }

            // Check for high alpha activity
            if (lastAlphaLevel >= ALPHA_THRESHOLD_HIGH &&
                currentBrainMetrics["Alpha Wave"] == "High")
            {
                string description = $"High alpha wave activity detected: {lastAlphaLevel:F1}μV";
                Task.Run(() => HandleSignificantBrainEventAsync("HighAlpha", description));
            }

            // Check for high beta activity
            if (lastBetaLevel >= BETA_THRESHOLD_HIGH &&
                currentBrainMetrics["Beta Wave"] == "High")
            {
                string description = $"High beta wave activity detected: {lastBetaLevel:F1}μV";
                Task.Run(() => HandleSignificantBrainEventAsync("HighBeta", description));
            }
        }

        /// <summary>
        /// Handles artifact detection from the BCI device
        /// </summary>
        private void OnArtifactDetected(object sender, ArtifactEventArgs e)
        {
            try
            {
                // Process artifacts like blinks, jaw clenches
                if (e.Blink)
                {
                    Task.Run(() => HandleSignificantBrainEventAsync("Blink", "Eye blink detected"));
                }

                if (e.JawClench)
                {
                    Task.Run(() => HandleSignificantBrainEventAsync("JawClench", "Jaw clench detected"));
                }

                if (e.HeadbandTooLoose)
                {
                    Task.Run(() => HandleSignificantBrainEventAsync("HeadbandLoose", "Headband adjustment needed"));
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// Handles errors from the BCI device
        /// </summary>
        private void OnBciErrorOccurred(object sender, BCIErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e.Exception ?? new Exception(e.Message));
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
                    // Stop monitoring if active
                    if (monitoringTask != null && !monitoringTask.IsCompleted)
                    {
                        StopMonitoringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    }

                    monitoringCancellationSource?.Dispose();
                }

                isDisposed = true;
            }
        }
    }
}