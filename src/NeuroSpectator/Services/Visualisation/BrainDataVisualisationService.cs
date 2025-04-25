using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Dispatching;
using NeuroSpectator.Models.BCI.Common;

namespace NeuroSpectator.Services.Visualisation
{
    /// <summary>
    /// Service for generating brain data visualisations for OBS overlays with enhanced diagnostics
    /// </summary>
    public class BrainDataVisualisationService : IDisposable
    {
        private readonly IDispatcher dispatcher;
        private readonly string visualisationDirectory;
        private Dictionary<string, string> currentBrainMetrics = new Dictionary<string, string>();
        private HttpListener httpListener;
        private CancellationTokenSource serverCancellationToken;
        private bool isDisposed;
        private int port = 8080;
        private string baseUrl;
        private DateTime lastDataUpdateTime = DateTime.MinValue;
        private int dataPointsReceived = 0;
        private bool hasReceivedData = false;
        private static HashSet<int> _usedPorts = new HashSet<int>();
        private static readonly object _portLock = new object();
        private int _serverPort;

        // Log of brain metrics for debugging
        private readonly Queue<DiagnosticLogEntry> metricLog = new Queue<DiagnosticLogEntry>(100); // Keep last 100 entries

        /// <summary>
        /// Represents a diagnostic log entry for tracking data flow
        /// </summary>
        private class DiagnosticLogEntry
        {
            public DateTime Timestamp { get; set; }
            public Dictionary<string, string> Metrics { get; set; }
            public string EventType { get; set; } = "Data";
            public string Description { get; set; } = "";
        }

        // Brain event handling
        private BrainDataEvent currentEvent = null;
        private List<BrainDataEvent> eventHistory = new List<BrainDataEvent>();
        private readonly int maxEventHistory = 10;

        /// <summary>
        /// Stores information about a brain data event
        /// </summary>
        private class BrainDataEvent
        {
            public DateTime Timestamp { get; set; }
            public string EventType { get; set; }
            public string Description { get; set; }
        }

        /// <summary>
        /// Gets whether the HTTP server is running
        /// </summary>
        public bool IsServerRunning => httpListener?.IsListening ?? false;

        /// <summary>
        /// Gets the URL of the visualisation server
        /// </summary>
        public string VisualisationUrl => baseUrl;

        /// <summary>
        /// Gets how many data points have been received
        /// </summary>
        public int DataPointsReceived => dataPointsReceived;

        /// <summary>
        /// Gets whether any data has been received
        /// </summary>
        public bool HasReceivedData => hasReceivedData;

        /// <summary>
        /// Event fired when the metrics are updated
        /// </summary>
        public event EventHandler<Dictionary<string, string>> MetricsUpdated;

        /// <summary>
        /// Creates a new instance of the BrainDataVisualisationService
        /// </summary>
        public BrainDataVisualisationService(IDispatcher dispatcher, string visualisationDirectory = null)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.visualisationDirectory = visualisationDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeuroSpectator", "Visualisations");

            // Ensure the directory exists
            if (!Directory.Exists(this.visualisationDirectory))
            {
                Directory.CreateDirectory(this.visualisationDirectory);
            }

            // Find an available port
            port = FindAvailablePort();
            baseUrl = $"http://localhost:{port}";
        }

        /// <summary>
        /// Starts the HTTP server for serving visualisations
        /// </summary>
        public async Task StartServerAsync()
        {
            // If server is already running, don't try to start it again
            if (IsServerRunning)
            {
                Console.WriteLine("Visualization server is already running, skipping start");
                return;
            }

            try
            {
                // Ensure the OBS template is available
                await EnsureOBSTemplateAvailableAsync();

                // Ensure the diagnostic template is available
                await EnsureDiagnosticTemplateAvailableAsync();

                // Generate initial visualisations
                await GenerateVisualisationsAsync();

                // Find an available port FIRST
                port = FindAvailablePort();
                baseUrl = $"http://localhost:{port}";

                // Start the HTTP server AFTER port is confirmed available
                httpListener = new HttpListener();
                httpListener.Prefixes.Add($"{baseUrl}/");

                try
                {
                    httpListener.Start();
                }
                catch (HttpListenerException ex)
                {
                    Console.WriteLine($"Could not start HTTP listener on port {port}: {ex.Message}");
                    // Remove from used ports registry since we couldn't use it
                    lock (_portLock)
                    {
                        if (_usedPorts.Contains(port))
                            _usedPorts.Remove(port);
                    }
                    throw; // Re-throw to signal caller
                }

                serverCancellationToken = new CancellationTokenSource();

                // Start listening for requests
                _ = Task.Run(() => ListenForRequestsAsync(serverCancellationToken.Token));

                Console.WriteLine($"Brain data visualisation server started at {baseUrl}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting visualisation server: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stops the HTTP server
        /// </summary>
        public async Task StopServerAsync()
        {
            if (!IsServerRunning)
                return;

            try
            {
                serverCancellationToken?.Cancel();

                // Important: Wait a moment before closing the listener
                await Task.Delay(200);

                httpListener.Stop();
                httpListener.Close();
                httpListener = null;

                // Release the port from our registry
                lock (_portLock)
                {
                    if (_serverPort > 0 && _usedPorts.Contains(_serverPort))
                        _usedPorts.Remove(_serverPort);
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping visualization server: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the brain metrics and regenerates visualisations
        /// </summary>
        public async Task UpdateBrainMetricsAsync(Dictionary<string, string> brainMetrics)
        {
            if (brainMetrics == null)
                return;

            currentBrainMetrics = new Dictionary<string, string>(brainMetrics);
            lastDataUpdateTime = DateTime.Now;
            dataPointsReceived++;
            hasReceivedData = true;

            // Add to metric log for diagnostics
            metricLog.Enqueue(new DiagnosticLogEntry { 
                Timestamp = DateTime.Now,
                Metrics = new Dictionary<string, string>(brainMetrics)
            });
            
            // Keep log limited to 100 entries
            if (metricLog.Count > 100)
                metricLog.Dequeue();

            // Notify listeners
            dispatcher.Dispatch(() => MetricsUpdated?.Invoke(this, currentBrainMetrics));

            // Regenerate visualisations
            await GenerateVisualisationsAsync();
        }

        /// <summary>
        /// Adds an event marker to the brain data visualization
        /// </summary>
        public async Task AddEventMarkerAsync(string eventType, string eventDescription)
        {
            try
            {
                // Create the event data
                var brainEvent = new BrainDataEvent
                {
                    Timestamp = DateTime.Now,
                    EventType = eventType,
                    Description = eventDescription
                };

                // Set as current event
                currentEvent = brainEvent;

                // Add to history
                eventHistory.Add(brainEvent);

                // Add to metric log for diagnostics
                metricLog.Enqueue(new DiagnosticLogEntry { 
                    Timestamp = DateTime.Now,
                    EventType = eventType,
                    Description = eventDescription
                });

                // Trim history if needed
                if (eventHistory.Count > maxEventHistory)
                {
                    eventHistory.RemoveAt(0);
                }

                // Update visualizations to show the event
                await GenerateVisualisationsAsync();

                // After a few seconds, clear the current event
                _ = Task.Run(async () => {
                    await Task.Delay(5000); // Show for 5 seconds
                    if (currentEvent == brainEvent) // Only clear if it's still the same event
                    {
                        currentEvent = null;
                        await GenerateVisualisationsAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding event marker: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates brain data visualisations
        /// </summary>
        public async Task GenerateVisualisationsAsync()
        {
            // Generate an HTML visualisation
            string htmlContent = GenerateHtmlVisualisation();
            string htmlPath = Path.Combine(visualisationDirectory, "brain_data.html");
            await File.WriteAllTextAsync(htmlPath, htmlContent);

            // Generate enhanced diagnostic visualization
            string diagnosticContent = GenerateDiagnosticHtmlVisualisation();
            string diagnosticPath = Path.Combine(visualisationDirectory, "brain_data_diagnostic.html");
            await File.WriteAllTextAsync(diagnosticPath, diagnosticContent);

            // Generate SVG visualisation
            string svgContent = GenerateSvgVisualisation();
            string svgPath = Path.Combine(visualisationDirectory, "brain_data.svg");
            await File.WriteAllTextAsync(svgPath, svgContent);
        }

        /// <summary>
        /// Ensures the diagnostic template is available on the server
        /// </summary>
        public async Task EnsureDiagnosticTemplateAvailableAsync()
        {
            string diagnosticHtmlPath = Path.Combine(visualisationDirectory, "brain_data_diagnostic.html");

            // Check if the template already exists
            if (!File.Exists(diagnosticHtmlPath))
            {
                // Generate an initial diagnostic visualization
                string content = GenerateDiagnosticHtmlVisualisation();
                await File.WriteAllTextAsync(diagnosticHtmlPath, content);
            }
        }

        /// <summary>
        /// Listens for HTTP requests
        /// </summary>
        private async Task ListenForRequestsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && httpListener.IsListening)
            {
                try
                {
                    // Wait for a request
                    var context = await httpListener.GetContextAsync();

                    // Handle the request in a separate task
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Console.WriteLine($"Error processing HTTP request: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles an HTTP request
        /// </summary>
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath.TrimStart('/');

                if (string.IsNullOrEmpty(path) || path == "/")
                {
                    // Serve the main visualisation
                    path = "brain_data.html";
                }

                string filePath = Path.Combine(visualisationDirectory, path);

                if (File.Exists(filePath))
                {
                    // Set the content type based on file extension
                    string contentType = GetContentType(Path.GetExtension(filePath));
                    context.Response.ContentType = contentType;

                    // Read the file and send it
                    byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                    context.Response.ContentLength64 = fileBytes.Length;
                    await context.Response.OutputStream.WriteAsync(fileBytes);
                }
                else if (path == "data")
                {
                    // Serve current brain metrics as JSON
                    string json = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        metrics = currentBrainMetrics,
                        currentEvent = currentEvent,
                        lastUpdate = lastDataUpdateTime,
                        dataPoints = dataPointsReceived,
                        hasData = hasReceivedData,
                        timeSinceLastUpdate = (DateTime.Now - lastDataUpdateTime).TotalMilliseconds
                    });
                    
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = jsonBytes.Length;
                    await context.Response.OutputStream.WriteAsync(jsonBytes);
                }
                else if (path == "events")
                {
                    // Serve event data as JSON
                    var eventData = new
                    {
                        currentEvent = currentEvent,
                        eventHistory = eventHistory
                    };
                    string json = System.Text.Json.JsonSerializer.Serialize(eventData);
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = jsonBytes.Length;
                    await context.Response.OutputStream.WriteAsync(jsonBytes);
                }
                else if (path == "diagnostic")
                {
                    // Serve diagnostic data (including the full metric log)
                    var diagnosticData = new
                    {
                        serverStartTime = DateTime.Now.AddSeconds(-dataPointsReceived),
                        currentTime = DateTime.Now,
                        metrics = currentBrainMetrics,
                        lastUpdate = lastDataUpdateTime,
                        dataPoints = dataPointsReceived,
                        hasData = hasReceivedData,
                        timeSinceLastUpdate = (DateTime.Now - lastDataUpdateTime).TotalMilliseconds,
                        metricLog = metricLog.ToArray(),
                        currentEvent = currentEvent,
                        eventHistory = eventHistory
                    };
                    
                    string json = System.Text.Json.JsonSerializer.Serialize(diagnosticData);
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = jsonBytes.Length;
                    await context.Response.OutputStream.WriteAsync(jsonBytes);
                }
                else
                {
                    // File not found
                    context.Response.StatusCode = 404;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling HTTP request: {ex.Message}");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }

        /// <summary>
        /// Gets the content type based on file extension
        /// </summary>
        private string GetContentType(string extension)
        {
            return extension.ToLower() switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Finds an available port for the HTTP server
        /// </summary>
        private int FindAvailablePort()
        {
            // Use a higher port range to avoid conflicts with MKIOPlayer (which uses 8080)
            // Start at 9000 to be well clear of the MKIOPlayer port
            for (int testPort = 9000; testPort < 9100; testPort++)
            {
                lock (_portLock)
                {
                    // Skip if port is already in our registry
                    if (_usedPorts.Contains(testPort))
                        continue;

                    var listener = new TcpListener(IPAddress.Loopback, testPort);
                    try
                    {
                        listener.Start();
                        listener.Stop();
                        // Port is available, register it
                        _usedPorts.Add(testPort);
                        _serverPort = testPort;
                        Console.WriteLine($"BrainDataVisualizationService: Selected port {testPort}");
                        return testPort;
                    }
                    catch
                    {
                        // Port unavailable, continue to next
                    }
                }
            }

            // If all ports are taken, throw exception to fail gracefully
            throw new InvalidOperationException("Could not find any available port in range 9000-9100");
        }

        /// <summary>
        /// Generates an HTML visualisation of brain data
        /// </summary>
        private string GenerateHtmlVisualisation()
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("  <title>NeuroSpectator Brain Data</title>");
            html.AppendLine("  <meta http-equiv=\"refresh\" content=\"2\">"); // Auto-refresh every 2 seconds
            html.AppendLine("  <style>");
            html.AppendLine("    body { font-family: 'Segoe UI', Arial, sans-serif; background-color: transparent; color: white; margin: 0; padding: 20px; overflow: hidden; }");
            html.AppendLine("    .container { background-color: rgba(0, 0, 0, 0.6); border-radius: 10px; padding: 15px; backdrop-filter: blur(5px); }");
            html.AppendLine("    .brain-data-title { font-size: 18px; font-weight: bold; margin-bottom: 15px; color: #B388FF; text-align: center; }");
            html.AppendLine("    .metrics-container { display: grid; grid-template-columns: 1fr 1fr; gap: 15px; }");
            html.AppendLine("    .metric { background-color: rgba(45, 45, 45, 0.7); border-radius: 8px; padding: 10px; }");
            html.AppendLine("    .metric-name { font-weight: bold; margin-bottom: 5px; font-size: 14px; }");
            html.AppendLine("    .metric-value { font-size: 24px; font-weight: bold; }");
            html.AppendLine("    .high { color: #92D36E; }");
            html.AppendLine("    .medium { color: #FFD740; }");
            html.AppendLine("    .low { color: #AAAAAA; }");
            html.AppendLine("    .focus-meter { width: 100%; height: 8px; background-color: #444; border-radius: 4px; overflow: hidden; margin-top: 10px; }");
            html.AppendLine("    .focus-value { height: 100%; background-color: #92D36E; transition: width 0.5s ease; }");
            html.AppendLine("    .brain-event { background-color: rgba(179, 136, 255, 0.3); animation: pulse 2s infinite; }");
            html.AppendLine("    @keyframes pulse { 0% {opacity: 0.7;} 50% {opacity: 1;} 100% {opacity: 0.7;} }");
            html.AppendLine("    .connection-status { font-size: 10px; text-align: right; margin-top: 5px; color: #92D36E; }");
            html.AppendLine("    .status-offline { color: #FF5252; }");
            html.AppendLine("    .status-warning { color: #FFD740; }");
            html.AppendLine("  </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("  <div class=\"container\">");
            html.AppendLine("    <div class=\"brain-data-title\">BRAIN METRICS</div>");
            html.AppendLine("    <div class=\"metrics-container\">");

            // Focus metric (always first and given special treatment)
            if (currentBrainMetrics.TryGetValue("Focus", out string focusValue))
            {
                int focusPercentage = 0;
                if (focusValue.EndsWith("%"))
                {
                    int.TryParse(focusValue.TrimEnd('%'), out focusPercentage);
                }

                html.AppendLine("      <div class=\"metric\" style=\"grid-column: span 2;\">");
                html.AppendLine("        <div class=\"metric-name\">FOCUS LEVEL</div>");
                html.AppendLine($"        <div class=\"metric-value high\">{focusValue}</div>");
                html.AppendLine("        <div class=\"focus-meter\">");
                html.AppendLine($"          <div class=\"focus-value\" style=\"width: {focusPercentage}%\"></div>");
                html.AppendLine("        </div>");
                html.AppendLine("      </div>");
            }

            // Other brain metrics
            foreach (var metric in currentBrainMetrics)
            {
                if (metric.Key == "Focus") continue; // Already handled above

                string colorClass = "medium";
                if (metric.Value.Contains("High")) colorClass = "high";
                else if (metric.Value.Contains("Low")) colorClass = "low";

                html.AppendLine("      <div class=\"metric\">");
                html.AppendLine($"        <div class=\"metric-name\">{metric.Key.ToUpper()}</div>");
                html.AppendLine($"        <div class=\"metric-value {colorClass}\">{metric.Value}</div>");
                html.AppendLine("      </div>");
            }

            // Add brain event if there is one
            if (currentEvent != null)
            {
                html.AppendLine("      <div class=\"metric brain-event\" style=\"grid-column: span 2;\">");
                html.AppendLine("        <div class=\"metric-name\">BRAIN EVENT DETECTED</div>");
                html.AppendLine($"        <div class=\"metric-value high\">{currentEvent.EventType}</div>");
                html.AppendLine($"        <div style=\"color: #FFFFFF; font-size: 14px;\">{currentEvent.Description}</div>");
                html.AppendLine($"        <div style=\"color: #AAAAAA; font-size: 12px; margin-top: 5px;\">{currentEvent.Timestamp:HH:mm:ss}</div>");
                html.AppendLine("      </div>");
            }

            html.AppendLine("    </div>");
            
            // Add connection status indicator
            string statusClass = "connection-status";
            string statusText = "Data connected";
            
            if (!hasReceivedData)
            {
                statusClass += " status-offline";
                statusText = "No data received";
            }
            else if ((DateTime.Now - lastDataUpdateTime).TotalSeconds > 5)
            {
                statusClass += " status-warning";
                statusText = $"Data stale ({(int)(DateTime.Now - lastDataUpdateTime).TotalSeconds}s)";
            }
            
            html.AppendLine($"    <div class=\"{statusClass}\">{statusText}</div>");
            
            html.AppendLine("  </div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            return html.ToString();
        }

        /// <summary>
        /// Generates an enhanced diagnostic HTML visualization
        /// </summary>
        private string GenerateDiagnosticHtmlVisualisation()
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("  <title>NeuroSpectator Brain Data Diagnostic</title>");
            html.AppendLine("  <meta http-equiv=\"refresh\" content=\"2\">"); // Auto-refresh every 2 seconds
            html.AppendLine("  <style>");
            html.AppendLine("    body { font-family: 'Segoe UI', Arial, sans-serif; background-color: #2D2D2D; color: white; margin: 20px; }");
            html.AppendLine("    h1, h2 { color: #B388FF; }");
            html.AppendLine("    .status { padding: 10px; border-radius: 5px; margin: 10px 0; }");
            html.AppendLine("    .good { background-color: rgba(146, 211, 110, 0.3); }");
            html.AppendLine("    .warning { background-color: rgba(255, 215, 64, 0.3); }");
            html.AppendLine("    .error { background-color: rgba(255, 82, 82, 0.3); }");
            html.AppendLine("    .data-table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
            html.AppendLine("    .data-table th { background-color: #333; text-align: left; padding: 8px; }");
            html.AppendLine("    .data-table td { padding: 8px; border-top: 1px solid #444; }");
            html.AppendLine("    .data-flow { height: 200px; overflow-y: auto; background-color: #333; padding: 10px; border-radius: 5px; font-family: monospace; }");
            html.AppendLine("    .data-point { margin-bottom: 5px; }");
            html.AppendLine("    .timestamp { color: #999; }");
            html.AppendLine("    .value-display { display: flex; align-items: center; }");
            html.AppendLine("    .pill { display: inline-block; padding: 3px 8px; border-radius: 12px; margin-left: 10px; font-size: 12px; }");
            html.AppendLine("    .high { background-color: #92D36E; color: #333; }");
            html.AppendLine("    .medium { background-color: #FFD740; color: #333; }");
            html.AppendLine("    .low { background-color: #999; color: #333; }");
            html.AppendLine("    .event { background-color: rgba(179, 136, 255, 0.3); }");
            html.AppendLine("  </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("  <h1>NeuroSpectator Brain Data Diagnostic</h1>");

            // Connection Status
            html.AppendLine("  <h2>Connection Status</h2>");
            
            if (!hasReceivedData)
            {
                html.AppendLine("  <div class=\"status error\">");
                html.AppendLine("    <strong>No data received</strong> - Device may not be connected or data is not flowing.");
                html.AppendLine("  </div>");
            }
            else if ((DateTime.Now - lastDataUpdateTime).TotalSeconds > 5)
            {
                html.AppendLine("  <div class=\"status warning\">");
                html.AppendLine($"    <strong>Data stale</strong> - Last update: {lastDataUpdateTime.ToString("HH:mm:ss.fff")} ({(int)(DateTime.Now - lastDataUpdateTime).TotalSeconds} seconds ago)");
                html.AppendLine("  </div>");
            }
            else
            {
                html.AppendLine("  <div class=\"status good\">");
                html.AppendLine($"    <strong>Data flowing</strong> - Last update: {lastDataUpdateTime.ToString("HH:mm:ss.fff")} (<1 second ago)");
                html.AppendLine("  </div>");
            }

            html.AppendLine($"  <p>Data points received: <strong>{dataPointsReceived}</strong></p>");
            
            if (hasReceivedData)
            {
                html.AppendLine($"  <p>Session start: <strong>{DateTime.Now.AddSeconds(-dataPointsReceived).ToString("HH:mm:ss")}</strong></p>");
                html.AppendLine($"  <p>Data rate: <strong>{Math.Round(dataPointsReceived / Math.Max(1, (DateTime.Now - DateTime.Now.AddSeconds(-dataPointsReceived)).TotalSeconds), 1)} points/second</strong></p>");
            }

            // Current Data
            html.AppendLine("  <h2>Current Brain Data</h2>");
            html.AppendLine("  <table class=\"data-table\">");
            html.AppendLine("    <tr><th>Metric</th><th>Value</th><th>Raw Value</th></tr>");

            // Add rows for each metric
            foreach (var metric in currentBrainMetrics)
            {
                string cssClass = "low";
                if (metric.Value.Contains("High")) cssClass = "high";
                else if (metric.Value.Contains("Medium")) cssClass = "medium";

                html.AppendLine("    <tr>");
                html.AppendLine($"      <td>{metric.Key}</td>");
                html.AppendLine($"      <td class=\"value-display\">{metric.Value} <span class=\"pill {cssClass}\">{GetLevel(metric.Value)}</span></td>");
                html.AppendLine($"      <td>{GetRawValue(metric.Value)}</td>");
                html.AppendLine("    </tr>");
            }

            html.AppendLine("  </table>");

            // Recent Events & Data Flow
            html.AppendLine("  <h2>Recent Events & Data Flow</h2>");
            html.AppendLine("  <div class=\"data-flow\">");
            
            // Show recent log entries
            var logEntries = metricLog.ToArray();
            Array.Reverse(logEntries); // Most recent first
            
            foreach (var entry in logEntries.Take(20)) // Show last 20 entries
            {
                string entryClass = entry.EventType == "Data" ? "data-point" : "data-point event";
                html.AppendLine($"    <div class=\"{entryClass}\">");
                html.AppendLine($"      <span class=\"timestamp\">[{entry.Timestamp.ToString("HH:mm:ss.fff")}]</span> ");
                
                if (entry.EventType == "Data")
                {
                    html.AppendLine($"      Data: ");
                    foreach (var metric in entry.Metrics)
                    {
                        html.AppendLine($"      {metric.Key}={metric.Value} ");
                    }
                }
                else
                {
                    html.AppendLine($"      Event: {entry.EventType} - {entry.Description}");
                }
                
                html.AppendLine("    </div>");
            }
            
            html.AppendLine("  </div>");

            // Debug Information
            html.AppendLine("  <h2>Debug Information</h2>");
            html.AppendLine($"  <p>Visualization Service URL: <code>{VisualisationUrl}</code></p>");
            html.AppendLine($"  <p>Server Status: <strong>{(IsServerRunning ? "Running" : "Stopped")}</strong></p>");
            
            html.AppendLine("  <h2>Technical Information</h2>");
            html.AppendLine("  <p>To use these diagnostic visualizations in OBS:</p>");
            html.AppendLine("  <ol>");
            html.AppendLine("    <li>Add a Browser source to your scene</li>");
            html.AppendLine("    <li>Main visualization: <code>" + VisualisationUrl + "/brain_data.html</code></li>");
            html.AppendLine("    <li>Diagnostic visualization: <code>" + VisualisationUrl + "/brain_data_diagnostic.html</code></li>");
            html.AppendLine("    <li>Set width: 800, height: 600 for diagnostic view</li>");
            html.AppendLine("    <li>Set width: 400, height: 600 for main view</li>");
            html.AppendLine("    <li>Check 'Refresh browser when scene becomes active'</li>");
            html.AppendLine("  </ol>");
            
            html.AppendLine("  <p>Raw data endpoints for developers:</p>");
            html.AppendLine("  <ul>");
            html.AppendLine($"    <li>Current data: <code>{VisualisationUrl}/data</code></li>");
            html.AppendLine($"    <li>Events: <code>{VisualisationUrl}/events</code></li>");
            html.AppendLine($"    <li>Diagnostic data: <code>{VisualisationUrl}/diagnostic</code></li>");
            html.AppendLine("  </ul>");

            html.AppendLine("</body>");
            html.AppendLine("</html>");
            return html.ToString();
        }

        /// <summary>
        /// Generates an SVG visualisation of brain data
        /// </summary>
        private string GenerateSvgVisualisation()
        {
            StringBuilder svg = new StringBuilder();
            svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"400\" height=\"300\" viewBox=\"0 0 400 300\">");

            // Background with transparency
            svg.AppendLine("  <rect width=\"400\" height=\"300\" fill=\"rgba(0,0,0,0.6)\" rx=\"10\" ry=\"10\"/>");

            // Title
            svg.AppendLine("  <text x=\"200\" y=\"30\" font-family=\"Arial\" font-size=\"20\" fill=\"#B388FF\" text-anchor=\"middle\" font-weight=\"bold\">BRAIN METRICS</text>");

            // Focus circle
            if (currentBrainMetrics.TryGetValue("Focus", out string focusValue))
            {
                int focusPercentage = 0;
                if (focusValue.EndsWith("%"))
                {
                    int.TryParse(focusValue.TrimEnd('%'), out focusPercentage);
                }

                // Draw focus meter
                svg.AppendLine("  <rect x=\"100\" y=\"60\" width=\"200\" height=\"20\" rx=\"10\" ry=\"10\" fill=\"#444444\"/>");
                svg.AppendLine($"  <rect x=\"100\" y=\"60\" width=\"{focusPercentage * 2}\" height=\"20\" rx=\"10\" ry=\"10\" fill=\"#92D36E\"/>");
                svg.AppendLine("  <text x=\"100\" y=\"50\" font-family=\"Arial\" font-size=\"16\" fill=\"white\">FOCUS LEVEL</text>");
                svg.AppendLine($"  <text x=\"300\" y=\"50\" font-family=\"Arial\" font-size=\"16\" fill=\"#92D36E\" text-anchor=\"end\" font-weight=\"bold\">{focusValue}</text>");
            }

            // Other brain metrics
            int yPos = 100;
            foreach (var metric in currentBrainMetrics)
            {
                if (metric.Key == "Focus") continue; // Already handled above

                string fillColor = "#FFD740"; // Default medium
                if (metric.Value.Contains("High")) fillColor = "#92D36E";
                else if (metric.Value.Contains("Low")) fillColor = "#AAAAAA";

                svg.AppendLine($"  <text x=\"100\" y=\"{yPos}\" font-family=\"Arial\" font-size=\"16\" fill=\"white\">{metric.Key.ToUpper()}</text>");
                svg.AppendLine($"  <text x=\"300\" y=\"{yPos}\" font-family=\"Arial\" font-size=\"16\" fill=\"{fillColor}\" text-anchor=\"end\" font-weight=\"bold\">{metric.Value}</text>");

                yPos += 40;
            }

            // Add brain event if there is one
            if (currentEvent != null)
            {
                yPos += 20; // Add some space
                svg.AppendLine($"  <rect x=\"100\" y=\"{yPos - 15}\" width=\"200\" height=\"50\" rx=\"5\" ry=\"5\" fill=\"rgba(179, 136, 255, 0.3)\">");
                svg.AppendLine($"    <animate attributeName=\"opacity\" values=\"0.7;1;0.7\" dur=\"2s\" repeatCount=\"indefinite\" />");
                svg.AppendLine($"  </rect>");
                svg.AppendLine($"  <text x=\"200\" y=\"{yPos}\" font-family=\"Arial\" font-size=\"16\" fill=\"white\" text-anchor=\"middle\" font-weight=\"bold\">BRAIN EVENT: {currentEvent.EventType}</text>");
                svg.AppendLine($"  <text x=\"200\" y=\"{yPos + 20}\" font-family=\"Arial\" font-size=\"12\" fill=\"white\" text-anchor=\"middle\">{currentEvent.Description}</text>");
            }
            
            // Add connection status indicator
            string statusColor = "#92D36E";
            string statusText = "Data connected";
            
            if (!hasReceivedData)
            {
                statusColor = "#FF5252";
                statusText = "No data received";
            }
            else if ((DateTime.Now - lastDataUpdateTime).TotalSeconds > 5)
            {
                statusColor = "#FFD740";
                statusText = "Data stale";
            }
            
            svg.AppendLine($"  <text x=\"380\" y=\"290\" font-family=\"Arial\" font-size=\"10\" fill=\"{statusColor}\" text-anchor=\"end\">{statusText}</text>");

            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        /// <summary>
        /// Gets the level (High, Medium, Low) from a value
        /// </summary>
        private string GetLevel(string value)
        {
            if (value.Contains("High")) return "HIGH";
            if (value.Contains("Medium")) return "MED";
            if (value.Contains("Low")) return "LOW";
            
            // For focus percentage values
            if (value.EndsWith("%"))
            {
                if (int.TryParse(value.TrimEnd('%'), out int percent))
                {
                    if (percent >= 80) return "HIGH";
                    if (percent >= 50) return "MED";
                    return "LOW";
                }
            }
            
            return "N/A";
        }

        /// <summary>
        /// Extracts the raw numeric value from a formatted value
        /// </summary>
        private string GetRawValue(string value)
        {
            // For percentage values
            if (value.EndsWith("%"))
            {
                if (int.TryParse(value.TrimEnd('%'), out int percent))
                {
                    return (percent / 100.0).ToString("F2");
                }
            }
            
            // For μV values
            if (value.EndsWith("μV"))
            {
                if (double.TryParse(value.TrimEnd("μV".ToCharArray()), out double microvolt))
                {
                    return microvolt.ToString("F2");
                }
            }
            
            // For text values (High, Medium, Low)
            if (value == "High") return "0.9";
            if (value == "Medium") return "0.5";
            if (value == "Low") return "0.1";
            
            return "N/A";
        }

        /// <summary>
        /// Ensures the OBS template is available on the server
        /// </summary>
        public async Task EnsureOBSTemplateAvailableAsync()
        {
            string obsTemplateHtmlPath = Path.Combine(visualisationDirectory, "brain_data_obs.html");

            // Check if the template already exists
            if (!File.Exists(obsTemplateHtmlPath))
            {
                // Create the OBS-optimized template
                await File.WriteAllTextAsync(obsTemplateHtmlPath, GetOBSBrainDataTemplate());
            }
        }

        /// <summary>
        /// Gets the preview URL for OBS virtual camera preview
        /// </summary>
        public string GetPreviewUrl()
        {
            return $"{VisualisationUrl}/obs_preview.html";
        }

        /// <summary>
        /// Ensures the OBS preview template is available on the server
        /// </summary>
        public async Task EnsureOBSPreviewTemplateAvailableAsync()
        {
            string previewTemplateHtmlPath = Path.Combine(visualisationDirectory, "obs_preview.html");

            // Check if the template already exists
            if (!File.Exists(previewTemplateHtmlPath))
            {
                // Create the OBS preview template
                await File.WriteAllTextAsync(previewTemplateHtmlPath, GetOBSPreviewTemplate());
            }
        }

        /// <summary>
        /// Gets the OBS preview template HTML
        /// </summary>
        private string GetOBSPreviewTemplate()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>NeuroSpectator OBS Preview</title>
    <style>
        body {
            font-family: 'Segoe UI', Arial, sans-serif;
            background-color: #1E1E1E;
            color: white;
            margin: 0;
            padding: 20px;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            flex-direction: column;
        }
        
        .preview-container {
            width: 100%;
            max-width: 800px;
            background-color: rgba(45, 45, 45, 0.7);
            border-radius: 10px;
            padding: 20px;
            text-align: center;
        }
        
        .preview-title {
            font-size: 24px;
            font-weight: bold;
            color: #B388FF;
            margin-bottom: 20px;
        }
        
        .preview-content {
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 300px;
            border: 2px dashed #444;
            border-radius: 5px;
            margin-bottom: 20px;
        }
        
        .preview-status {
            margin-top: 10px;
            font-size: 14px;
            color: #AAAAAA;
        }
        
        .active {
            color: #92D36E;
        }
        
        .inactive {
            color: #FF5252;
        }
    </style>
</head>
<body>
    <div class=""preview-container"">
        <div class=""preview-title"">OBS Virtual Camera Preview</div>
        
        <div class=""preview-content"">
            <p>OBS Virtual Camera Output Would Appear Here</p>
        </div>
        
        <div class=""preview-status"">
            <p>OBS Status: <span id=""obsStatus"" class=""inactive"">Not Connected</span></p>
            <p>Virtual Camera: <span id=""cameraStatus"" class=""inactive"">Not Active</span></p>
        </div>
    </div>
    
    <script>
        // Auto-refresh every 5 seconds to update status
        setInterval(function() {
            // In a real implementation, this would fetch status from an API
            fetch('/status')
                .then(response => response.json())
                .then(data => {
                    const obsStatus = document.getElementById('obsStatus');
                    const cameraStatus = document.getElementById('cameraStatus');
                    
                    // Update OBS status
                    if (data && data.obsConnected) {
                        obsStatus.textContent = 'Connected';
                        obsStatus.className = 'active';
                    } else {
                        obsStatus.textContent = 'Not Connected';
                        obsStatus.className = 'inactive';
                    }
                    
                    // Update camera status
                    if (data && data.virtualCameraActive) {
                        cameraStatus.textContent = 'Active';
                        cameraStatus.className = 'active';
                    } else {
                        cameraStatus.textContent = 'Not Active';
                        cameraStatus.className = 'inactive';
                    }
                })
                .catch(error => {
                    console.error('Error fetching status:', error);
                });
        }, 5000);
    </script>
</body>
</html>";
        }

        /// <summary>
        /// Gets the OBS-optimized brain data visualization template
        /// </summary>
        private string GetOBSBrainDataTemplate()
        {
            // [existing HTML template stays the same]
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>NeuroSpectator Brain Data</title>
    <style>
        body {
            font-family: 'Segoe UI', Arial, sans-serif;
            background-color: transparent;
            color: white;
            margin: 0;
            padding: 0;
            overflow: hidden;
        }
        
        .container {
            width: 100%;
            height: 100%;
            background-color: rgba(0, 0, 0, 0.5);
            border-radius: 10px;
            backdrop-filter: blur(5px);
            padding: 15px;
            box-sizing: border-box;
        }
        
        .title {
            font-size: 18px;
            font-weight: bold;
            color: #B388FF;
            margin-bottom: 15px;
            text-align: center;
            text-transform: uppercase;
        }
        
        .metrics {
            display: grid;
            grid-template-columns: 1fr;
            gap: 10px;
        }
        
        .metric {
            background-color: rgba(30, 30, 30, 0.7);
            border-radius: 8px;
            padding: 10px;
            display: flex;
            flex-direction: column;
        }
        
        .metric-name {
            font-size: 14px;
            font-weight: bold;
            color: #AAAAAA;
            margin-bottom: 5px;
        }
        
        .metric-value {
            font-size: 24px;
            font-weight: bold;
        }
        
        .focus {
            grid-column: span 1;
        }
        
        .high {
            color: #92D36E;
        }
        
        .medium {
            color: #FFD740;
        }
        
        .low {
            color: #AAAAAA;
        }
        
        .focus-meter {
            width: 100%;
            height: 8px;
            background-color: #222;
            border-radius: 4px;
            overflow: hidden;
            margin-top: 5px;
        }
        
        .focus-value {
            height: 100%;
            background-color: #92D36E;
            transition: width 0.5s ease;
        }
        
        .event-log {
            margin-top: 15px;
            font-size: 12px;
            color: #CCCCCC;
            max-height: 80px;
            overflow-y: auto;
        }
        
        .event {
            margin-bottom: 5px;
            padding: 5px;
            background-color: rgba(30, 30, 30, 0.5);
            border-radius: 4px;
        }
        
        .event.highlight {
            background-color: rgba(179, 136, 255, 0.2);
            color: #B388FF;
            font-weight: bold;
        }
        
        /* Animation for brain events */
        @keyframes pulse {
            0% { opacity: 1; }
            50% { opacity: 0.5; }
            100% { opacity: 1; }
        }
        
        .pulse {
            animation: pulse 1s ease-in-out;
        }
        
        .connection-status {
            font-size: 10px;
            margin-top: 10px;
            color: #92D36E;
            text-align: right;
        }
        
        .status-offline {
            color: #FF5252;
        }
        
        .status-warning {
            color: #FFD740;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""title"">Brain Metrics</div>
        <div class=""metrics"">
            <!-- Focus metric -->
            <div class=""metric focus"">
                <div class=""metric-name"">FOCUS LEVEL</div>
                <div class=""metric-value high"" id=""focus-value"">0%</div>
                <div class=""focus-meter"">
                    <div class=""focus-value"" id=""focus-bar"" style=""width: 0%""></div>
                </div>
            </div>
            
            <!-- Alpha Wave -->
            <div class=""metric"">
                <div class=""metric-name"">ALPHA WAVE</div>
                <div class=""metric-value low"" id=""alpha-value"">Low</div>
            </div>
            
            <!-- Beta Wave -->
            <div class=""metric"">
                <div class=""metric-name"">BETA WAVE</div>
                <div class=""metric-value low"" id=""beta-value"">Low</div>
            </div>
            
            <!-- Theta Wave -->
            <div class=""metric"">
                <div class=""metric-name"">THETA WAVE</div>
                <div class=""metric-value low"" id=""theta-value"">Low</div>
            </div>
            
            <!-- Recent Events -->
            <div class=""event-log"" id=""event-log"">
                <!-- Events will be added here dynamically -->
            </div>
            
            <!-- Connection Status -->
            <div class=""connection-status"" id=""connection-status"">
                Data connected
            </div>
        </div>
    </div>
    
    <script>
        // Auto-refresh data every second
        const REFRESH_INTERVAL = 1000;
        
        // Keep track of last values for change detection
        let lastMetrics = {};
        
        // Elements
        const focusValue = document.getElementById('focus-value');
        const focusBar = document.getElementById('focus-bar');
        const alphaValue = document.getElementById('alpha-value');
        const betaValue = document.getElementById('beta-value');
        const thetaValue = document.getElementById('theta-value');
        const eventLog = document.getElementById('event-log');
        const connectionStatus = document.getElementById('connection-status');
        
        // Set value class based on level
        function setValueClass(element, value) {
            element.classList.remove('high', 'medium', 'low');
            
            if (value === 'High') {
                element.classList.add('high');
            } else if (value === 'Medium') {
                element.classList.add('medium');
            } else {
                element.classList.add('low');
            }
        }
        
        // Add an event to the log
        function addEvent(text, highlight = false) {
            const event = document.createElement('div');
            event.className = 'event' + (highlight ? ' highlight' : '');
            event.textContent = text;
            
            // Add to the top of the log
            eventLog.insertBefore(event, eventLog.firstChild);
            
            // Limit to 5 events
            if (eventLog.children.length > 5) {
                eventLog.removeChild(eventLog.lastChild);
            }
            
            // Apply pulse animation to the container
            document.querySelector('.container').classList.add('pulse');
            setTimeout(() => {
                document.querySelector('.container').classList.remove('pulse');
            }, 1000);
        }
        
        // Check for significant changes
        function checkForSignificantChanges(newMetrics) {
            if (!lastMetrics.Focus && newMetrics.Focus) {
                // First data received
                lastMetrics = {...newMetrics};
                return;
            }
            
            // Check focus changes
            if (lastMetrics.Focus && newMetrics.Focus) {
                const oldFocus = parseInt(lastMetrics.Focus.replace('%', ''));
                const newFocus = parseInt(newMetrics.Focus.replace('%', ''));
                
                if (Math.abs(newFocus - oldFocus) >= 20) {
                    // Change of 20% or more is significant
                    const direction = newFocus > oldFocus ? 'increased' : 'decreased';
                    addEvent(`Focus ${direction} to ${newFocus}%`, true);
                }
            }
            
            // Check wave changes
            const waveTypes = ['Alpha Wave', 'Beta Wave', 'Theta Wave'];
            waveTypes.forEach(wave => {
                if (lastMetrics[wave] !== newMetrics[wave] && 
                    lastMetrics[wave] !== undefined && 
                    newMetrics[wave] !== undefined) {
                    
                    if ((lastMetrics[wave] === 'Low' && newMetrics[wave] === 'High') || 
                        (lastMetrics[wave] === 'High' && newMetrics[wave] === 'Low')) {
                        // Significant wave change
                        addEvent(`${wave} changed to ${newMetrics[wave]}`);
                    }
                }
            });
            
            // Update last metrics
            lastMetrics = {...newMetrics};
        }
        
        // Update the display with new data
        function updateDisplay(data) {
            if (!data) return;
            
            // Update connection status
            if (!data.hasData) {
                connectionStatus.textContent = 'No data received';
                connectionStatus.classList.add('status-offline');
            } else if (data.timeSinceLastUpdate > 5000) {
                connectionStatus.textContent = `Data stale (${Math.round(data.timeSinceLastUpdate/1000)}s)`;
                connectionStatus.classList.remove('status-offline');
                connectionStatus.classList.add('status-warning');
            } else {
                connectionStatus.textContent = 'Data connected';
                connectionStatus.classList.remove('status-offline');
                connectionStatus.classList.remove('status-warning');
            }
            
            // Update focus
            if (data.metrics.Focus) {
                focusValue.textContent = data.metrics.Focus;
                const focusPercent = parseInt(data.metrics.Focus.replace('%', ''));
                focusBar.style.width = `${focusPercent}%`;
            }
            
            // Update alpha wave
            if (data.metrics['Alpha Wave']) {
                alphaValue.textContent = data.metrics['Alpha Wave'];
                setValueClass(alphaValue, data.metrics['Alpha Wave']);
            }
            
            // Update beta wave
            if (data.metrics['Beta Wave']) {
                betaValue.textContent = data.metrics['Beta Wave'];
                setValueClass(betaValue, data.metrics['Beta Wave']);
            }
            
            // Update theta wave
            if (data.metrics['Theta Wave']) {
                thetaValue.textContent = data.metrics['Theta Wave'];
                setValueClass(thetaValue, data.metrics['Theta Wave']);
            }
            
            // Check for significant changes
            checkForSignificantChanges(data.metrics);
            
            // If there's a current event, display it
            if (data.currentEvent) {
                addEvent(`${data.currentEvent.EventType}: ${data.currentEvent.Description}`, true);
            }
        }
        
        // Fetch data periodically
        function fetchData() {
            fetch('/data')
                .then(response => response.json())
                .then(data => {
                    updateDisplay(data);
                })
                .catch(error => {
                    console.error('Error fetching data:', error);
                    connectionStatus.textContent = 'Connection error';
                    connectionStatus.classList.add('status-offline');
                })
                .finally(() => {
                    // Schedule next update
                    setTimeout(fetchData, REFRESH_INTERVAL);
                });
        }
        
        // Start fetching data
        fetchData();
    </script>
</body>
</html>";
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
                    // Stop the HTTP server
                    if (IsServerRunning)
                    {
                        StopServerAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    }

                    serverCancellationToken?.Dispose();
                }

                isDisposed = true;
            }
        }
    }
}