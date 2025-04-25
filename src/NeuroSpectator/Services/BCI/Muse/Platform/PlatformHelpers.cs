using System.Diagnostics;
using System.Reflection;

namespace NeuroSpectator.Services.BCI.Muse.Platform
{
    /// <summary>
    /// Helpers for platform-specific operations
    /// </summary>
    public static class PlatformHelpers
    {
        /// <summary>
        /// Gets a value indicating whether this is running on Windows
        /// </summary>
        public static bool IsWindows
        {
            get
            {
#if WINDOWS
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is running on Android
        /// </summary>
        public static bool IsAndroid
        {
            get
            {
#if ANDROID
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is running on iOS
        /// </summary>
        public static bool IsIOS
        {
            get
            {
#if IOS
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets a value indicating whether this is running on Mac Catalyst
        /// </summary>
        public static bool IsMacCatalyst
        {
            get
            {
#if MACCATALYST
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets the name of the current platform
        /// </summary>
        public static string GetPlatformName()
        {
            if (IsWindows) return "Windows";
            if (IsAndroid) return "Android";
            if (IsIOS) return "iOS";
            if (IsMacCatalyst) return "macOS";
            return "Unknown";
        }

        /// <summary>
        /// Checks if Bluetooth permissions are granted
        /// </summary>
        public static bool CheckBluetoothPermissions()
        {
#if ANDROID
            try
            {
                // Android 12+ requires BLUETOOTH_CONNECT and BLUETOOTH_SCAN
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
                {
                    var status1 = Android.App.Application.Context.CheckSelfPermission(Android.Manifest.Permission.BluetoothConnect);
                    var status2 = Android.App.Application.Context.CheckSelfPermission(Android.Manifest.Permission.BluetoothScan);
                    return status1 == Android.Content.PM.Permission.Granted &&
                           status2 == Android.Content.PM.Permission.Granted;
                }
                else
                {
                    var status1 = Android.App.Application.Context.CheckSelfPermission(Android.Manifest.Permission.Bluetooth);
                    var status2 = Android.App.Application.Context.CheckSelfPermission(Android.Manifest.Permission.BluetoothAdmin);
                    var status3 = Android.App.Application.Context.CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation);
                    return status1 == Android.Content.PM.Permission.Granted &&
                           status2 == Android.Content.PM.Permission.Granted &&
                           status3 == Android.Content.PM.Permission.Granted;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Bluetooth permissions: {ex.Message}");
                return false;
            }
#elif IOS || MACCATALYST
            // iOS and macOS don't require runtime permissions for Bluetooth
            // but check NSBluetoothAlwaysUsageDescription in Info.plist
            return true;
#elif WINDOWS
            // Windows doesn't require runtime permissions for Bluetooth
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Requests Bluetooth permissions on platforms that require it
        /// </summary>
        public static async Task RequestBluetoothPermissionsAsync()
        {
#if ANDROID
            try
            {
                var requiredPermissions = new List<string>();

                // Android 12+ requires BLUETOOTH_CONNECT and BLUETOOTH_SCAN
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
                {
                    requiredPermissions.Add(Android.Manifest.Permission.BluetoothConnect);
                    requiredPermissions.Add(Android.Manifest.Permission.BluetoothScan);
                }
                else
                {
                    requiredPermissions.Add(Android.Manifest.Permission.Bluetooth);
                    requiredPermissions.Add(Android.Manifest.Permission.BluetoothAdmin);
                    requiredPermissions.Add(Android.Manifest.Permission.AccessFineLocation);
                }

                await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<BluetoothPermission>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error requesting Bluetooth permissions: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Extracts native libraries from embedded resources
        /// </summary>
        public static void ExtractNativeLibraries()
        {
            if (!IsWindows) return;

            try
            {
                string appDirectory = AppContext.BaseDirectory;
                string libmusePath = Path.Combine(appDirectory, "libmuse.dll");

                // Skip if file already exists
                if (File.Exists(libmusePath))
                {
                    Debug.WriteLine($"libmuse.dll already exists at {libmusePath}");
                    return;
                }

                // Extract the embedded libmuse.dll based on build configuration
#if DEBUG
                string resourcePath = "NeuroSpectator.Resources.Raw.Debug.libmuse.dll";
                Debug.WriteLine("Extracting DEBUG version of libmuse.dll");
#else
                string resourcePath = "NeuroSpectator.Resources.Raw.Release.libmuse.dll";
                Debug.WriteLine("Extracting RELEASE version of libmuse.dll");
#endif

                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream resourceStream = assembly.GetManifestResourceStream(resourcePath))
                {
                    if (resourceStream != null)
                    {
                        using (FileStream fileStream = new FileStream(libmusePath, FileMode.Create))
                        {
                            resourceStream.CopyTo(fileStream);
                            Debug.WriteLine($"Successfully extracted libmuse.dll to {libmusePath}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to find embedded libmuse.dll resource at {resourcePath}");

                        // List available resources to help debugging
                        var resources = assembly.GetManifestResourceNames();
                        Debug.WriteLine("Available resources:");
                        foreach (var resource in resources)
                        {
                            Debug.WriteLine($"  {resource}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting native libraries: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Checks if Bluetooth is enabled on the device
        /// </summary>
        public static bool IsBluetoothEnabled()
        {
#if ANDROID
            try
            {
                var bluetoothAdapter = Android.Bluetooth.BluetoothAdapter.DefaultAdapter;
                return bluetoothAdapter != null && bluetoothAdapter.IsEnabled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Bluetooth enabled state: {ex.Message}");
                return false;
            }
#elif WINDOWS
            try
            {
                // This is a simplified check for the example
                // In a real implementation, you would use Windows.Devices.Bluetooth APIs
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Windows Bluetooth state: {ex.Message}");
                return false;
            }
#else
            // For other platforms, we'll assume Bluetooth is enabled
            return true;
#endif
        }

        /// <summary>
        /// Lists all available embedded resources - useful for debugging
        /// </summary>
        public static void ListAllEmbeddedResources()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                var resources = assembly.GetManifestResourceNames();

                Debug.WriteLine("All embedded resources:");
                foreach (var resource in resources)
                {
                    Debug.WriteLine($"  {resource}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error listing embedded resources: {ex.Message}");
            }
        }
    }

#if ANDROID
    /// <summary>
    /// Custom Bluetooth permission for Android
    /// </summary>
    public class BluetoothPermission : Microsoft.Maui.ApplicationModel.Permissions.BasePlatformPermission
    {
        public override (string, bool)[] RequiredPermissions
        {
            get
            {
                var permissions = new List<(string, bool)>();

                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
                {
                    permissions.Add((Android.Manifest.Permission.BluetoothConnect, false));
                    permissions.Add((Android.Manifest.Permission.BluetoothScan, false));
                }
                else
                {
                    permissions.Add((Android.Manifest.Permission.Bluetooth, false));
                    permissions.Add((Android.Manifest.Permission.BluetoothAdmin, false));
                    permissions.Add((Android.Manifest.Permission.AccessFineLocation, false));
                }

                return permissions.ToArray();
            }
        }
    }
#endif
}