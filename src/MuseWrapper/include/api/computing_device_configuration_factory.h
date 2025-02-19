// Copyright 2017 Interaxon Inc.
#pragma once

#include <memory>
#include "api/bridge_computing_device_configuration.h"

namespace interaxon {
    namespace bridge {

        /**
         * Provides access to the ComputingDeviceConfiguration object containing
         * information about the computing device.
         */
        class ComputingDeviceConfigurationFactory {
        public:

            /**
            * Static constructor for the singleton object.
            *
            * \return An instance of the ComputingDeviceConfigurationFactory object.
            */
            static std::shared_ptr<ComputingDeviceConfigurationFactory> get_instance();

            ComputingDeviceConfigurationFactory(const ComputingDeviceConfigurationFactory& rhs) = delete;
            ComputingDeviceConfigurationFactory& operator=(const ComputingDeviceConfigurationFactory& rhs) = delete;

            /**
            * Retrieves the appropriate ComputingDeviceConfiguration structure for the current
            * computing device.
            *
            * \return A ComputingDeviceConfiguration structure for the current computing device.
            */
            ComputingDeviceConfiguration get_computing_device_configuration();

        private:
            ComputingDeviceConfigurationFactory();
        };

    } // namespace bridge
} // namespace interaxon
