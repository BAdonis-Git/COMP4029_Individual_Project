// Copyright 2016 Interaxon Inc.
#pragma once

#include <string>

namespace interaxon {
    namespace bridge {

        /**
         * A utility class that provides conversions to and from C++ and UWP objects.
         */
        class Convert {
        public:
            /**
             * Converts a std::string object to a Platform::String object.
             *
             * \param str  The std::string object to convert.
             *
             * \return The Platform::String object with the same text representation.
             */
            static Platform::String^ to_platform_string(const std::string &str);

            /**
            * Converts a Platform::String object to a std::string object.
            *
            * \param str  The Platform::String object to convert.
            *
            * \return The std::string object with the same text representation.
            */
            static std::string to_std_string(Platform::String^ str);

        };

    }
}