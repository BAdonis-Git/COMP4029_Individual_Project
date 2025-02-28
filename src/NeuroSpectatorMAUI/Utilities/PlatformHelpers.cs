using System.Diagnostics;
#if WINDOWS
using Windows.Devices.Bluetooth;
#endif

namespace NeuroSpectatorMAUI.Services
{
    /// <summary>
    /// Helper class for platform-specific operations
    /// </summary>
    public static class PlatformHelpers
    {
        /// <summary>
        /// Detects the current platform
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

        public static string GetPlatformName()
        {
            if (IsWindows) return "Windows";
            if (IsAndroid) return "Android";
            if (IsIOS) return "iOS";
            if (IsMacCatalyst) return "macOS";
            return "Unknown";
        }

        /// <summary>
        /// Checks the Bluetooth status in a platform-specific way
        /// </summary>
        public static void CheckBluetoothStatus()
        {
            try
            {
                Debug.WriteLine($"Checking Bluetooth status on {GetPlatformName()}...");

                if (IsWindows)
                {
                    CheckBluetoothStatusWindows();
                }
                else if (IsAndroid)
                {
                    CheckBluetoothStatusAndroid();
                }
                else if (IsIOS || IsMacCatalyst)
                {
                    Debug.WriteLine("Bluetooth status check not implemented for iOS/macOS");
                }
                else
                {
                    Debug.WriteLine("Bluetooth status check not implemented for this platform");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Bluetooth status: {ex.Message}");
            }
        }

        private static async Task CheckBluetoothStatusWindows()
        {
#if WINDOWS
            try
            {
                Debug.WriteLine("Checking Windows Bluetooth availability...");

                BluetoothAdapter adapter = await BluetoothAdapter.GetDefaultAsync();
                if (adapter != null)
                {
                    Debug.WriteLine("Bluetooth adapter found");
                    var radio = await adapter.GetRadioAsync();
                    if (radio != null)
                    {
                        Debug.WriteLine($"Bluetooth radio state: {radio.State}");
                    }
                }
                else
                {
                    Debug.WriteLine("No Bluetooth adapter found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accessing Bluetooth: {ex.Message}");
            }
#endif
        }

#if WINDOWS
        private static void TryGetPropertyValue(object obj, string propertyName, string displayName)
        {
            try
            {
                var property = obj.GetType().GetProperty(propertyName);
                if (property != null)
                {
                    var value = property.GetValue(obj);
                    Debug.WriteLine($"Bluetooth {displayName}: {value}");
                }
                else
                {
                    Debug.WriteLine($"Property '{propertyName}' not available on this version of the SDK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting {propertyName}: {ex.Message}");
            }
        }
#endif

        private static void CheckBluetoothStatusAndroid()
        {
#if ANDROID
            try
            {
                var adapter = Android.Bluetooth.BluetoothAdapter.DefaultAdapter;
                if (adapter != null)
                {
                    Debug.WriteLine($"Bluetooth adapter found, enabled: {adapter.IsEnabled}");
                    Debug.WriteLine($"Bluetooth adapter name: {adapter.Name}");
                    
                    // Check for Bluetooth permissions
                    var context = Android.App.Application.Context;
                    var permissionCheck = context.CheckSelfPermission(Android.Manifest.Permission.Bluetooth);
                    
                    // These permissions might not be available on all API levels
                    try
                    {
                        var scanPermissionCheck = context.CheckSelfPermission("android.permission.BLUETOOTH_SCAN");
                        var connectPermissionCheck = context.CheckSelfPermission("android.permission.BLUETOOTH_CONNECT");
                        
                        Debug.WriteLine($"Has BluetoothScan permission: {scanPermissionCheck == Android.Content.PM.Permission.Granted}");
                        Debug.WriteLine($"Has BluetoothConnect permission: {connectPermissionCheck == Android.Content.PM.Permission.Granted}");
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine("BLUETOOTH_SCAN and BLUETOOTH_CONNECT permissions only available on newer Android versions");
                    }
                    
                    Debug.WriteLine($"Has Bluetooth permission: {permissionCheck == Android.Content.PM.Permission.Granted}");
                }
                else
                {
                    Debug.WriteLine("No Bluetooth adapter found on this Android device!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Android Bluetooth status: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Checks if all required native libraries are present
        /// </summary>
        public static bool CheckNativeLibraries()
        {
            if (!IsWindows)
            {
                Debug.WriteLine("Native library check is only relevant on Windows");
                return true;
            }

            string baseDir = AppContext.BaseDirectory;
            string[] requiredLibraries = new[]
            {
                "libmuse.dll",
                "MuseWrapper.dll",
                "msvcp140.dll",
                "vcruntime140.dll"
            };

            bool allPresent = true;
            foreach (var lib in requiredLibraries)
            {
                string path = Path.Combine(baseDir, lib);
                bool exists = File.Exists(path);
                Debug.WriteLine($"Library {lib}: {(exists ? "Found" : "Missing")}");
                allPresent &= exists;
            }

            return allPresent;
        }
    }
}