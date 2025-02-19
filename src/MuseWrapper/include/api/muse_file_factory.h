// Copyright 2016 Interaxon, Inc.
#pragma once

#include <memory>
#include <string>

namespace interaxon {
    namespace bridge {

        class MuseFile;
        class MuseFileWriter;
        class MuseFileReader;

        /**
        * Creates MuseFileWriter, MuseFileReader and MuseFile objects.
        */
        class MuseFileFactory {
        public:
            /**
            * Creates and returns MuseFileWriter object based on provided path.
            * Interaxon's MuseFile implementation is used in this case.
            *
            * Note that upon creation of MuseFileWriter, an Annotation is automatically written 
            * out to the file. The annotation contains the app's name and version and libmuse
            * version. If app's name and version can not be determined, they will be empty strings.
            *
            * \param file_path The absolute path of the file to write.
            * \return MuseFileWriter
            */
            static std::shared_ptr<MuseFileWriter> get_muse_file_writer(const std::string& file_path);

            /**
            * Creates and returns MuseFileReader object based on provided path.
            * Interaxon's MuseFile implementation is used in this case.
            * \param file_path The absolute path of the file to read.
            * \return MuseFileReader
            */
            static std::shared_ptr<MuseFileReader> get_muse_file_reader(const std::string& file_path);


            /**
            * Creates and returns MuseFile object, which uses Interaxon's implementation.
            * \param file_path The absolute path of the file.
            * \return MuseFile
            */
            static std::shared_ptr<MuseFile> get_muse_file(const std::string& file_path);

        };

    } // namespace bridge
} // namespace interaxon
