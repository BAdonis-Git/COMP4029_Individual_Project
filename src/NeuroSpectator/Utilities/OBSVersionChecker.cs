using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;

namespace NeuroSpectator.Utilities
{
    /// <summary>
    /// Utility class to check OBS version compatibility
    /// </summary>
    public static class OBSVersionChecker
    {
        /// <summary>
        /// Checks if the OBS WebSocket is accessible and returns version information
        /// </summary>
        public static async Task<(bool Success, string Message, string OBSVersion, string WebSocketVersion)> CheckOBSCompatibilityAsync(string url = "ws://localhost:4444", string password = "")
        {
            var obs = new OBSWebsocket();
            var tcs = new TaskCompletionSource<(bool Success, string Message, string OBSVersion, string WebSocketVersion)>();

            try
            {
                // Setup event handlers
                EventHandler onConnected = null;
                EventHandler<ObsDisconnectionInfo> onDisconnected = null;

                onConnected = async (s, e) =>
                {
                    obs.Connected -= onConnected;
                    obs.Disconnected -= onDisconnected;

                    try
                    {
                        // Get OBS version info
                        var version = obs.GetVersion();

                        string obsVersion = version.OBSStudioVersion;
                        string websocketVersion = version.PluginVersion;

                        tcs.TrySetResult((true, "Successfully connected", obsVersion, websocketVersion));
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetResult((false, $"Connected but failed to get version: {ex.Message}", "Unknown", "Unknown"));
                    }
                    finally
                    {
                        // Disconnect after getting info
                        try { obs.Disconnect(); } catch { }
                    }
                };

                onDisconnected = (s, e) =>
                {
                    obs.Connected -= onConnected;
                    obs.Disconnected -= onDisconnected;
                    tcs.TrySetResult((false, $"Connection failed: {e.DisconnectReason}", "N/A", "N/A"));
                };

                // Register one-time event handlers
                obs.Connected += onConnected;
                obs.Disconnected += onDisconnected;

                // Try to connect with a timeout
                obs.ConnectAsync(url, password);

                // Set a timeout of 5 seconds
                var timeoutTask = Task.Delay(5000);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Clean up
                    obs.Connected -= onConnected;
                    obs.Disconnected -= onDisconnected;

                    try { obs.Disconnect(); } catch { }

                    return (false, "Connection attempt timed out after 5 seconds", "N/A", "N/A");
                }

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                return (false, $"Error checking OBS compatibility: {ex.Message}", "N/A", "N/A");
            }
        }
    }
}
