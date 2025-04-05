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
    /// Service for generating brain data visualisations for OBS overlays
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
            if (IsServerRunning)
                return;

            try
            {
                // Ensure the OBS template is available
                await EnsureOBSTemplateAvailableAsync();

                // Generate initial visualisations
                await GenerateVisualisationsAsync();

                // Start the HTTP server
                httpListener = new HttpListener();
                httpListener.Prefixes.Add($"{baseUrl}/");
                httpListener.Start();

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
                httpListener.Stop();
                httpListener.Close();
                httpListener = null;

                await Task.CompletedTask; // For async pattern consistency
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping visualisation server: {ex.Message}");
                throw;
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

            // Generate SVG visualisation
            string svgContent = GenerateSvgVisualisation();
            string svgPath = Path.Combine(visualisationDirectory, "brain_data.svg");
            await File.WriteAllTextAsync(svgPath, svgContent);
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
                    string json = System.Text.Json.JsonSerializer.Serialize(currentBrainMetrics);
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
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
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
            html.AppendLine("  </div>");
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

            svg.AppendLine("</svg>");
            return svg.ToString();
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

        // Add these methods to BrainDataVisualisationService.cs

        /// <summary>
        /// Ensures the OBS overlay template is available on the server
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
        /// Gets the OBS-optimized brain data visualization template
        /// </summary>
        private string GetOBSBrainDataTemplate()
        {
            // This is the OBS-optimized template with transparent background and responsive layout
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
            
            // Update focus
            if (data.Focus) {
                focusValue.textContent = data.Focus;
                const focusPercent = parseInt(data.Focus.replace('%', ''));
                focusBar.style.width = `${focusPercent}%`;
            }
            
            // Update alpha wave
            if (data['Alpha Wave']) {
                alphaValue.textContent = data['Alpha Wave'];
                setValueClass(alphaValue, data['Alpha Wave']);
            }
            
            // Update beta wave
            if (data['Beta Wave']) {
                betaValue.textContent = data['Beta Wave'];
                setValueClass(betaValue, data['Beta Wave']);
            }
            
            // Update theta wave
            if (data['Theta Wave']) {
                thetaValue.textContent = data['Theta Wave'];
                setValueClass(thetaValue, data['Theta Wave']);
            }
            
            // Check for significant changes
            checkForSignificantChanges(data);
        }
        
        // Fetch data periodically
        function fetchData() {
            fetch('/data')
                .then(response => response.json())
                .then(data => {
                    updateDisplay(data.metrics);
                    
                    // If there's a current event, display it
                    if (data.currentEvent) {
                        addEvent(`${data.currentEvent.EventType}: ${data.currentEvent.Description}`, true);
                    }
                })
                .catch(error => console.error('Error fetching data:', error))
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

    }
}