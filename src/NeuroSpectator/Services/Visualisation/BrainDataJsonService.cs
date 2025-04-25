using System.Text.Json;

namespace NeuroSpectator.Services.Visualisation
{
    /// <summary>
    /// Service for generating real-time JSON brain data for visualization
    /// This data can be consumed by OBS overlays and other visualization tools
    /// </summary>
    public class BrainDataJsonService : IDisposable
    {
        private readonly IDispatcher dispatcher;
        private readonly string dataDirectory;
        private readonly string jsonFilePath;
        private readonly string historyFilePath;
        private bool isDisposed;
        private List<BrainDataSnapshot> dataHistory = new List<BrainDataSnapshot>();
        private readonly int maxHistoryItems = 600; // 10 minutes at 1 update per second

        /// <summary>
        /// Creates a new instance of the BrainDataJsonService
        /// </summary>
        public BrainDataJsonService(IDispatcher dispatcher, string dataDirectory = null)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            this.dataDirectory = dataDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeuroSpectator", "BrainData");

            // Ensure the directory exists
            if (!Directory.Exists(this.dataDirectory))
            {
                Directory.CreateDirectory(this.dataDirectory);
            }

            jsonFilePath = Path.Combine(this.dataDirectory, "current_data.json");
            historyFilePath = Path.Combine(this.dataDirectory, "data_history.json");

            // Create initial empty files if they don't exist
            if (!File.Exists(jsonFilePath))
            {
                File.WriteAllText(jsonFilePath, "{}");
            }

            if (!File.Exists(historyFilePath))
            {
                File.WriteAllText(historyFilePath, "[]");
            }

            // Load existing history
            LoadHistory();
        }

        /// <summary>
        /// Gets the current brain data file path
        /// </summary>
        public string JsonFilePath => jsonFilePath;

        /// <summary>
        /// Gets the brain data history file path
        /// </summary>
        public string HistoryFilePath => historyFilePath;

        /// <summary>
        /// Updates the brain data file with new metrics
        /// </summary>
        public async Task UpdateDataAsync(Dictionary<string, string> brainMetrics)
        {
            if (brainMetrics == null || brainMetrics.Count == 0)
                return;

            try
            {
                // Create a new data snapshot
                var snapshot = new BrainDataSnapshot
                {
                    Timestamp = DateTime.Now,
                    Metrics = new Dictionary<string, string>(brainMetrics)
                };

                // Add to history
                dataHistory.Add(snapshot);

                // Trim history if needed
                if (dataHistory.Count > maxHistoryItems)
                {
                    dataHistory.RemoveAt(0);
                }

                // Save current data
                var currentJson = JsonSerializer.Serialize(new
                {
                    timestamp = snapshot.Timestamp,
                    metrics = snapshot.Metrics,
                    focusLevel = ParseFocusLevel(brainMetrics),
                    alphaLevel = ParseWaveLevel(brainMetrics, "Alpha Wave"),
                    betaLevel = ParseWaveLevel(brainMetrics, "Beta Wave"),
                    thetaLevel = ParseWaveLevel(brainMetrics, "Theta Wave"),
                    deltaLevel = ParseWaveLevel(brainMetrics, "Delta Wave"),
                    gammaLevel = ParseWaveLevel(brainMetrics, "Gamma Wave")
                }, new JsonSerializerOptions { WriteIndented = true });

                await File.WriteAllTextAsync(jsonFilePath, currentJson);

                // Save history periodically (every 10 updates)
                if (dataHistory.Count % 10 == 0)
                {
                    await SaveHistoryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating brain data JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds an event marker to the brain data
        /// </summary>
        public async Task AddEventMarkerAsync(string eventType, string eventDescription)
        {
            try
            {
                // Create event data
                var eventData = new BrainDataEvent
                {
                    Timestamp = DateTime.Now,
                    EventType = eventType,
                    Description = eventDescription
                };

                // Read current data
                var currentData = await ReadCurrentDataAsync();

                // Add event to current data
                var newData = new
                {
                    timestamp = currentData.timestamp,
                    metrics = currentData.metrics,
                    focusLevel = currentData.focusLevel,
                    alphaLevel = currentData.alphaLevel,
                    betaLevel = currentData.betaLevel,
                    thetaLevel = currentData.thetaLevel,
                    deltaLevel = currentData.deltaLevel,
                    gammaLevel = currentData.gammaLevel,
                    currentEvent = eventData
                };

                // Save updated data
                var json = JsonSerializer.Serialize(newData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(jsonFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding event marker: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads history from file
        /// </summary>
        private void LoadHistory()
        {
            try
            {
                if (File.Exists(historyFilePath))
                {
                    var json = File.ReadAllText(historyFilePath);
                    var history = JsonSerializer.Deserialize<List<BrainDataSnapshot>>(json);

                    if (history != null)
                    {
                        dataHistory = history;

                        // Trim if it's too large
                        if (dataHistory.Count > maxHistoryItems)
                        {
                            dataHistory = dataHistory.GetRange(dataHistory.Count - maxHistoryItems, maxHistoryItems);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading brain data history: {ex.Message}");
                dataHistory = new List<BrainDataSnapshot>();
            }
        }

        /// <summary>
        /// Saves history to file
        /// </summary>
        private async Task SaveHistoryAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(dataHistory, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(historyFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving brain data history: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the current brain data from file
        /// </summary>
        private async Task<dynamic> ReadCurrentDataAsync()
        {
            try
            {
                var json = await File.ReadAllTextAsync(jsonFilePath);
                return JsonSerializer.Deserialize<dynamic>(json);
            }
            catch
            {
                // Return an empty object if the file can't be read
                return new
                {
                    timestamp = DateTime.Now,
                    metrics = new Dictionary<string, string>(),
                    focusLevel = 0,
                    alphaLevel = 0,
                    betaLevel = 0,
                    thetaLevel = 0,
                    deltaLevel = 0,
                    gammaLevel = 0
                };
            }
        }

        /// <summary>
        /// Parses the focus level from brain metrics
        /// </summary>
        private int ParseFocusLevel(Dictionary<string, string> metrics)
        {
            if (metrics.TryGetValue("Focus", out string focusValue))
            {
                if (focusValue.EndsWith("%"))
                {
                    if (int.TryParse(focusValue.TrimEnd('%'), out int value))
                    {
                        return value;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Parses a wave level from brain metrics
        /// </summary>
        private int ParseWaveLevel(Dictionary<string, string> metrics, string waveKey)
        {
            if (metrics.TryGetValue(waveKey, out string levelValue))
            {
                return levelValue switch
                {
                    "High" => 3,
                    "Medium" => 2,
                    "Low" => 1,
                    _ => 0
                };
            }

            return 0;
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
                    // Save history one last time
                    SaveHistoryAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }

                isDisposed = true;
            }
        }
    }

    /// <summary>
    /// Represents a snapshot of brain data at a specific point in time
    /// </summary>
    public class BrainDataSnapshot
    {
        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the brain metrics
        /// </summary>
        public Dictionary<string, string> Metrics { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Represents a brain data event
    /// </summary>
    public class BrainDataEvent
    {
        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the event type
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// Gets or sets the event description
        /// </summary>
        public string Description { get; set; }
    }
}