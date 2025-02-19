using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Data;
using System.Diagnostics;
using System.Reflection;

namespace NeuroSpectatorMAUI.Services
{
    internal static class MuseNativeInterface
    {
        private const string LibName = "MuseWrapper";

        static MuseNativeInterface()
        {
            string baseDir = AppContext.BaseDirectory;
            string dllPath = Path.Combine(baseDir, "x64",
#if DEBUG
            "Debug"
#else
                "Release"
#endif
            );

            // Debug logging
            Debug.WriteLine($"Base Directory: {baseDir}");
            Debug.WriteLine($"DLL Directory: {dllPath}");

            // Set DLL directory
            if (!SetDllDirectory(dllPath))
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to set DLL directory. Error: {error}");
            }

            // Verify DLL locations
            string museDll = Path.Combine(dllPath, "libmuse.dll");
            string wrapperDll = Path.Combine(dllPath, "MuseWrapper.dll");
            Debug.WriteLine($"libmuse.dll exists: {File.Exists(museDll)}");
            Debug.WriteLine($"MuseWrapper.dll exists: {File.Exists(wrapperDll)}");

            NativeLibrary.SetDllImportResolver(typeof(MuseNativeInterface).Assembly, ResolveDllImport);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private static IntPtr ResolveDllImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName == LibName)
            {
                try
                {
                    string configuration =
#if DEBUG
                    "Debug"
#else
                        "Release"
#endif
                        ;

                    string dllPath = Path.Combine(AppContext.BaseDirectory, "x64", configuration, "MuseWrapper.dll");
                    Debug.WriteLine($"Attempting to load DLL from: {dllPath}");

                    if (!File.Exists(dllPath))
                    {
                        Debug.WriteLine($"DLL not found at {dllPath}");
                        throw new FileNotFoundException($"Could not find {LibName}.dll");
                    }

                    return NativeLibrary.Load(dllPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load {LibName}: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw;
                }
            }
            return IntPtr.Zero;
        }

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern IntPtr GetMuseManager();
    }
}
