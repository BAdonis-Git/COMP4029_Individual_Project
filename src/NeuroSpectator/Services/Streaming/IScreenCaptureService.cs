using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Dispatching;

namespace NeuroSpectator.Services.Streaming
{
    /// <summary>
    /// Implementation of the screen capture service
    /// </summary>
    public class ScreenCaptureService : IScreenCaptureService
    {
        private readonly IDispatcher dispatcher;
        private CancellationTokenSource captureCancellationSource;
        private Task captureTask;
        private int frameRate = 30;
        private bool isDisposed;

        /// <summary>
        /// Gets whether capture is active
        /// </summary>
        public bool IsCapturing => captureTask != null && !captureTask.IsCompleted;

        /// <summary>
        /// Event fired when a new frame is captured
        /// </summary>
        public event EventHandler<byte[]> FrameCaptured;

        /// <summary>
        /// Event fired when the capture fails
        /// </summary>
        public event EventHandler<Exception> CaptureFailed;

        /// <summary>
        /// Creates a new instance of the ScreenCaptureService
        /// </summary>
        public ScreenCaptureService(IDispatcher dispatcher)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// Starts capturing the screen
        /// </summary>
        public async Task StartCaptureAsync(int frameRate = 30, string targetWindow = null)
        {
            if (IsCapturing)
            {
                throw new InvalidOperationException("Capture is already in progress");
            }

            this.frameRate = frameRate;
            captureCancellationSource = new CancellationTokenSource();

            // Start the capture task
            captureTask = Task.Run(() => CaptureLoopAsync(targetWindow, captureCancellationSource.Token));
        }

        /// <summary>
        /// Stops capturing the screen
        /// </summary>
        public async Task StopCaptureAsync()
        {
            if (!IsCapturing)
            {
                return;
            }

            // Cancel the capture task
            captureCancellationSource?.Cancel();

            try
            {
                // Wait for the capture task to complete
                await captureTask;
            }
            catch (OperationCanceledException)
            {
                // This is expected when cancelling
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping capture: {ex.Message}");
            }
            finally
            {
                captureCancellationSource?.Dispose();
                captureCancellationSource = null;
                captureTask = null;
            }
        }

        /// <summary>
        /// Capture loop for all platforms
        /// </summary>
        private async Task CaptureLoopAsync(string targetWindow, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    using (var screenshotStream = await TakeScreenshotAsync("jpg"))
                    {
                        // Convert to byte array
                        var memoryStream = new MemoryStream();
                        await screenshotStream.CopyToAsync(memoryStream, cancellationToken);
                        var frameData = memoryStream.ToArray();

                        // Raise the event
                        await dispatcher.DispatchAsync(() =>
                        {
                            FrameCaptured?.Invoke(this, frameData);
                        });
                    }

                    // Wait for next frame
                    await Task.Delay(1000 / frameRate, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when cancelling
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in capture loop: {ex.Message}");
                await dispatcher.DispatchAsync(() =>
                {
                    CaptureFailed?.Invoke(this, ex);
                });
            }
        }

        /// <summary>
        /// Takes a screenshot
        /// </summary>
        public async Task<Stream> TakeScreenshotAsync(string outputFormat = "jpg")
        {
#if WINDOWS
            return await TakeWindowsScreenshotAsync(outputFormat);
#elif ANDROID
            return await TakeAndroidScreenshotAsync(outputFormat);
#elif IOS || MACCATALYST
            return await TakeAppleScreenshotAsync(outputFormat);
#else
            return await CreatePlaceholderScreenshotAsync(outputFormat);
#endif
        }

#if WINDOWS
        /// <summary>
        /// Takes a screenshot on Windows
        /// </summary>
        private async Task<Stream> TakeWindowsScreenshotAsync(string outputFormat = "jpg")
        {
            try
            {
                // Create a placeholder bitmap for now (actual implementation would use Windows APIs)
                return await CreatePlaceholderScreenshotAsync(outputFormat, "Windows");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error taking Windows screenshot: {ex.Message}");
                CaptureFailed?.Invoke(this, ex);
                throw;
            }
        }
#endif

#if ANDROID
        /// <summary>
        /// Takes a screenshot on Android
        /// </summary>
        private async Task<Stream> TakeAndroidScreenshotAsync(string outputFormat = "jpg")
        {
            try
            {
                // Create a placeholder bitmap for now (actual implementation would use MediaProjection API)
                return await CreatePlaceholderScreenshotAsync(outputFormat, "Android");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error taking Android screenshot: {ex.Message}");
                CaptureFailed?.Invoke(this, ex);
                throw;
            }
        }
#endif

#if IOS || MACCATALYST
        /// <summary>
        /// Takes a screenshot on iOS/Mac
        /// </summary>
        private async Task<Stream> TakeAppleScreenshotAsync(string outputFormat = "jpg")
        {
            try
            {
                // Create a placeholder bitmap for now (actual implementation would use ReplayKit or UIScreen)
                return await CreatePlaceholderScreenshotAsync(outputFormat, "iOS/Mac");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error taking iOS/Mac screenshot: {ex.Message}");
                CaptureFailed?.Invoke(this, ex);
                throw;
            }
        }
#endif

        /// <summary>
        /// Creates a placeholder screenshot (for development/testing)
        /// </summary>
        private async Task<Stream> CreatePlaceholderScreenshotAsync(string outputFormat = "jpg", string platform = "Generic")
        {
            // This creates a simple colored rectangle with text to simulate a screenshot
            var memoryStream = new MemoryStream();

            // Create a simple text representation
            string placeholderText =
                $"--- {platform} Screenshot Placeholder ---\r\n" +
                $"Timestamp: {DateTime.Now}\r\n" +
                $"Format: {outputFormat}\r\n" +
                $"This is a placeholder for actual screen capture";

            var bytes = System.Text.Encoding.UTF8.GetBytes(placeholderText);
            await memoryStream.WriteAsync(bytes, 0, bytes.Length);
            memoryStream.Position = 0;

            return memoryStream;
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
                    // Stop any ongoing capture
                    if (IsCapturing)
                    {
                        StopCaptureAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    }

                    captureCancellationSource?.Dispose();
                    captureCancellationSource = null;
                }

                isDisposed = true;
            }
        }
    }
}