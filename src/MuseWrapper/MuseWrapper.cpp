#include "pch.h"
#include "MuseWrapper.h"
#include <Windows.h>
#include <sstream>
#include <vector>

using namespace interaxon::bridge;

void LogDebug(const char* msg) {
    OutputDebugStringA(msg);
    OutputDebugStringA("\n");
}

// Manager instance
std::shared_ptr<interaxon::bridge::MuseManagerWindows> g_manager = nullptr;

// Callback function pointers
typedef void(__stdcall* MuseListChangedCallback)();
typedef void(__stdcall* ConnectionStateChangedCallback)(int state, const char* name);
typedef void(__stdcall* DataReceivedCallback)(int packetType, double* data, int dataLength);

// Callback instances
MuseListChangedCallback g_museListChangedCallback = nullptr;
ConnectionStateChangedCallback g_connectionStateChangedCallback = nullptr;
DataReceivedCallback g_dataReceivedCallback = nullptr;

// Currently connected muse
std::shared_ptr<interaxon::bridge::Muse> g_currentMuse = nullptr;

// Class to listen for Muse list changes
class MuseListenerImpl : public MuseListener {
public:
    void muse_list_changed() override {
        LogDebug("MuseListenerImpl::muse_list_changed called");
        if (g_manager) {
            auto muses = g_manager->get_muses();
            std::stringstream ss;
            ss << "Found " << muses.size() << " Muse devices";
            LogDebug(ss.str().c_str());

            // Call the C# callback if registered
            if (g_museListChangedCallback) {
                g_museListChangedCallback();
            }
        }
    }
};

// Class to listen for connection state changes
class ConnectionListenerImpl : public MuseConnectionListener {
public:
    void receive_muse_connection_packet(const MuseConnectionPacket& packet, const std::shared_ptr<Muse>& muse) override {
        std::string name = muse->get_name();
        LogDebug(("ConnectionListenerImpl::receive_muse_connection_packet: " + name).c_str());

        // Call the C# callback if registered
        if (g_connectionStateChangedCallback) {
            g_connectionStateChangedCallback((int)packet.current_connection_state, name.c_str());
        }
    }
};

// Class to listen for data packets
class DataListenerImpl : public MuseDataListener {
public:
    void receive_muse_data_packet(const std::shared_ptr<MuseDataPacket>& packet, const std::shared_ptr<Muse>& muse) override {
        if (!g_dataReceivedCallback) return;

        // Prepare data buffer based on packet type
        double dataBuffer[6] = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };

        switch (packet->packet_type()) {
        case MuseDataPacketType::ACCELEROMETER:
            dataBuffer[0] = packet->get_accelerometer_value(Accelerometer::X);
            dataBuffer[1] = packet->get_accelerometer_value(Accelerometer::Y);
            dataBuffer[2] = packet->get_accelerometer_value(Accelerometer::Z);
            break;
        case MuseDataPacketType::BATTERY:
            dataBuffer[0] = packet->get_battery_value(Battery::CHARGE_PERCENTAGE_REMAINING);
            dataBuffer[1] = packet->get_battery_value(Battery::MILLIVOLTS);
            dataBuffer[2] = packet->get_battery_value(Battery::TEMPERATURE_CELSIUS);
            break;
        case MuseDataPacketType::EEG:
            dataBuffer[0] = packet->get_eeg_channel_value(Eeg::EEG1);
            dataBuffer[1] = packet->get_eeg_channel_value(Eeg::EEG2);
            dataBuffer[2] = packet->get_eeg_channel_value(Eeg::EEG3);
            dataBuffer[3] = packet->get_eeg_channel_value(Eeg::EEG4);
            dataBuffer[4] = packet->get_eeg_channel_value(Eeg::AUX_LEFT);
            dataBuffer[5] = packet->get_eeg_channel_value(Eeg::AUX_RIGHT);
            break;
            // Add cases for other data types as needed
        default:
            break;
        }

        // Call the C# callback
        g_dataReceivedCallback((int)packet->packet_type(), dataBuffer, 6);
    }

    void receive_muse_artifact_packet(const MuseArtifactPacket& packet, const std::shared_ptr<Muse>& muse) override {
        if (!g_dataReceivedCallback) return;

        double dataBuffer[6] = { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };
        dataBuffer[0] = packet.headband_on ? 1.0 : 0.0;
        dataBuffer[1] = packet.blink ? 1.0 : 0.0;
        dataBuffer[2] = packet.jaw_clench ? 1.0 : 0.0;

        // Call the C# callback
        g_dataReceivedCallback((int)MuseDataPacketType::ARTIFACTS, dataBuffer, 6);
    }
};

// Listener instances
std::shared_ptr<MuseListenerImpl> g_listener = nullptr;
std::shared_ptr<ConnectionListenerImpl> g_connectionListener = nullptr;
std::shared_ptr<DataListenerImpl> g_dataListener = nullptr;

extern "C" {
    // Get the MuseManager instance
    __declspec(dllexport) void* GetMuseManager() {
        try {
            LogDebug("GetMuseManager: Starting initialization");

            // Check if already initialized
            if (g_manager) {
                LogDebug("GetMuseManager: Already initialized");
                return g_manager.get();
            }

            try {
                // Get manager instance
                g_manager = MuseManagerWindows::get_instance();
                if (!g_manager) {
                    LogDebug("GetMuseManager: get_instance() returned null");
                    return nullptr;
                }

                // Create listener if not exists
                if (!g_listener) {
                    g_listener = std::make_shared<MuseListenerImpl>();
                }

                // Set listener
                g_manager->set_muse_listener(g_listener);
                LogDebug("GetMuseManager: Listener set");

                return g_manager.get();
            }
            catch (const std::exception& e) {
                LogDebug("Failed during initialization:");
                LogDebug(e.what());
                g_manager = nullptr;
                return nullptr;
            }
        }
        catch (const std::exception& e) {
            LogDebug("Exception in GetMuseManager:");
            LogDebug(e.what());
            return nullptr;
        }
        catch (...) {
            LogDebug("Unknown exception in GetMuseManager");
            return nullptr;
        }
    }

    // Start listening for Muse devices
    __declspec(dllexport) bool StartListening() {
        try {
            if (!g_manager) {
                LogDebug("StartListening: Manager not initialized");
                return false;
            }

            g_manager->start_listening();
            LogDebug("StartListening: Started listening");
            return true;
        }
        catch (const std::exception& e) {
            LogDebug("Exception in StartListening:");
            LogDebug(e.what());
            return false;
        }
        catch (...) {
            LogDebug("Unknown exception in StartListening");
            return false;
        }
    }

    // Stop listening for Muse devices
    __declspec(dllexport) void StopListening() {
        try {
            if (g_manager) {
                g_manager->stop_listening();
                LogDebug("StopListening: Stopped listening");
            }
        }
        catch (...) {
            LogDebug("Error in StopListening");
        }
    }

    // Get available Muse devices
    __declspec(dllexport) int GetMuseCount() {
        try {
            if (!g_manager) return 0;

            auto muses = g_manager->get_muses();
            return (int)muses.size();
        }
        catch (...) {
            return 0;
        }
    }

    // Get Muse name by index
    __declspec(dllexport) const char* GetMuseName(int index) {
        try {
            if (!g_manager) return "";

            auto muses = g_manager->get_muses();
            if (index < 0 || index >= muses.size()) return "";

            // Note: This is not thread-safe. In a real implementation,
            // you'd want to manage this string more carefully
            static std::string name;
            name = muses[index]->get_name();
            return name.c_str();
        }
        catch (...) {
            return "";
        }
    }

    // Connect to a Muse device by name
    __declspec(dllexport) bool ConnectToMuse(const char* name) {
        try {
            if (!g_manager) return false;

            auto muses = g_manager->get_muses();
            for (auto muse : muses) {
                if (muse->get_name() == name) {
                    // Disconnect previous muse if exists
                    if (g_currentMuse) {
                        g_currentMuse->disconnect();
                    }

                    // Create listeners if not exists
                    if (!g_connectionListener) {
                        g_connectionListener = std::make_shared<ConnectionListenerImpl>();
                    }
                    if (!g_dataListener) {
                        g_dataListener = std::make_shared<DataListenerImpl>();
                    }

                    // Register listeners
                    muse->register_connection_listener(g_connectionListener);

                    // Connect to the muse
                    g_currentMuse = muse;
                    g_currentMuse->run_asynchronously();

                    LogDebug(("ConnectToMuse: Connecting to " + std::string(name)).c_str());
                    return true;
                }
            }

            LogDebug(("ConnectToMuse: Muse not found - " + std::string(name)).c_str());
            return false;
        }
        catch (const std::exception& e) {
            LogDebug(("Exception in ConnectToMuse: " + std::string(e.what())).c_str());
            return false;
        }
        catch (...) {
            LogDebug("Unknown exception in ConnectToMuse");
            return false;
        }
    }

    // Disconnect from the current Muse device
    __declspec(dllexport) void DisconnectMuse() {
        try {
            if (g_currentMuse) {
                g_currentMuse->disconnect();
                g_currentMuse = nullptr;
                LogDebug("DisconnectMuse: Disconnected");
            }
        }
        catch (...) {
            LogDebug("Error in DisconnectMuse");
        }
    }

    // Register for data of a specific type
    __declspec(dllexport) bool RegisterDataListener(int packetType) {
        try {
            if (!g_currentMuse || !g_dataListener) return false;

            g_currentMuse->register_data_listener(g_dataListener,
                static_cast<MuseDataPacketType>(packetType));

            LogDebug(("RegisterDataListener: Registered for packet type " +
                std::to_string(packetType)).c_str());
            return true;
        }
        catch (...) {
            LogDebug("Error in RegisterDataListener");
            return false;
        }
    }

    // Unregister for data of a specific type
    __declspec(dllexport) bool UnregisterDataListener(int packetType) {
        try {
            if (!g_currentMuse || !g_dataListener) return false;

            g_currentMuse->unregister_data_listener(g_dataListener,
                static_cast<MuseDataPacketType>(packetType));

            return true;
        }
        catch (...) {
            LogDebug("Error in UnregisterDataListener");
            return false;
        }
    }

    // Set callbacks from C#
    __declspec(dllexport) void SetMuseListChangedCallback(MuseListChangedCallback callback) {
        g_museListChangedCallback = callback;
    }

    __declspec(dllexport) void SetConnectionStateChangedCallback(ConnectionStateChangedCallback callback) {
        g_connectionStateChangedCallback = callback;
    }

    __declspec(dllexport) void SetDataReceivedCallback(DataReceivedCallback callback) {
        g_dataReceivedCallback = callback;
    }

    // Clean up resources
    __declspec(dllexport) void StopMuseManager() {
        try {
            // Disconnect current muse if any
            if (g_currentMuse) {
                g_currentMuse->disconnect();
                g_currentMuse = nullptr;
            }

            // Stop listening
            if (g_manager) {
                g_manager->stop_listening();
            }

            // Clear callbacks
            g_museListChangedCallback = nullptr;
            g_connectionStateChangedCallback = nullptr;
            g_dataReceivedCallback = nullptr;

            // Clear listeners
            g_listener = nullptr;
            g_connectionListener = nullptr;
            g_dataListener = nullptr;

            // Clear manager
            g_manager = nullptr;

            LogDebug("Manager stopped and cleaned up");
        }
        catch (...) {
            LogDebug("Error stopping manager");
        }
    }
}