#include <iostream>
#include <string>
#include <Windows.h>
#include "../MuseWrapper/include/muse.h"

using namespace interaxon::bridge;

// Global manager instance
std::shared_ptr<MuseManagerWindows> g_manager;
std::shared_ptr<MyMuseListener> g_listener;

// Simple listener implementation
class MyMuseListener : public MuseListener {
public:
    void muse_list_changed() override {
        std::cout << "Muse list changed!" << std::endl;

        // Print available devices
        if (g_manager) {
            auto muses = g_manager->get_muses();
            std::cout << "Found " << muses.size() << " Muse devices:" << std::endl;

            for (const auto& muse : muses) {
                std::cout << " - " << muse->get_name() << std::endl;
            }
        }
    }
};

int main() {
    std::cout << "Testing Muse SDK..." << std::endl;

    try {
        // Initialize the manager
        std::cout << "Initializing Muse Manager..." << std::endl;
        g_manager = MuseManagerWindows::get_instance();

        if (!g_manager) {
            std::cout << "Failed to get Muse Manager instance!" << std::endl;
            return 1;
        }

        std::cout << "Muse Manager initialized successfully" << std::endl;

        // Create and set listener
        g_listener = std::make_shared<MyMuseListener>();
        g_manager->set_muse_listener(g_listener);

        // Start listening for devices
        std::cout << "Starting to listen for Muse devices..." << std::endl;
        g_manager->start_listening();

        // Wait for devices to be discovered
        std::cout << "Waiting for devices (10 seconds)..." << std::endl;
        Sleep(10000);

        // Stop listening
        std::cout << "Stopping listening..." << std::endl;
        g_manager->stop_listening();

        std::cout << "Test completed successfully" << std::endl;
        return 0;
    }
    catch (const std::exception& e) {
        std::cout << "Exception: " << e.what() << std::endl;
        return 1;
    }
    catch (...) {
        std::cout << "Unknown exception occurred" << std::endl;
        return 1;
    }
}