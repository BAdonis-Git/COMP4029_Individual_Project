#pragma once

#include "include/muse.h"

namespace interaxon {
    namespace bridge {
        class MuseManagerWindows;
        class MuseListener;
    }
}

// Callback function types
typedef void(__stdcall* MuseListChangedCallback)();
typedef void(__stdcall* ConnectionStateChangedCallback)(int state, const char* name);
typedef void(__stdcall* DataReceivedCallback)(int packetType, double* data, int dataLength);

#ifdef __cplusplus
extern "C" {
#endif

    // Manager functions
    __declspec(dllexport) void* GetMuseManager();
    __declspec(dllexport) bool StartListening();
    __declspec(dllexport) void StopListening();

    // Device enumeration
    __declspec(dllexport) int GetMuseCount();
    __declspec(dllexport) const char* GetMuseName(int index);

    // Connection management
    __declspec(dllexport) bool ConnectToMuse(const char* name);
    __declspec(dllexport) void DisconnectMuse();

    // Data registration
    __declspec(dllexport) bool RegisterDataListener(int packetType);
    __declspec(dllexport) bool UnregisterDataListener(int packetType);

    // Callback registration
    __declspec(dllexport) void SetMuseListChangedCallback(MuseListChangedCallback callback);
    __declspec(dllexport) void SetConnectionStateChangedCallback(ConnectionStateChangedCallback callback);
    __declspec(dllexport) void SetDataReceivedCallback(DataReceivedCallback callback);

    // Cleanup
    __declspec(dllexport) void StopMuseManager();

#ifdef __cplusplus
}
#endif