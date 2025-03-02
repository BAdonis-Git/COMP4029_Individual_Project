namespace NeuroSpectator.Services.BCI.Muse.Core
{
    public enum MuseModel : int
    {
        MU_01,
        MU_02,
        MU_03,
        MU_04,
        MU_05,
        MU_06,
        MS_03,
    }

    public enum ConnectionState : int
    {
        UNKNOWN,
        CONNECTED,
        CONNECTING,
        DISCONNECTED,
        NEEDS_UPDATE,
        NEEDS_LICENSE,
    }

    public enum MuseDataPacketType : int
    {
        ACCELEROMETER,
        GYRO,
        EEG,
        DROPPED_ACCELEROMETER,
        DROPPED_EEG,
        QUANTIZATION,
        BATTERY,
        DRL_REF,
        ALPHA_ABSOLUTE,
        BETA_ABSOLUTE,
        DELTA_ABSOLUTE,
        THETA_ABSOLUTE,
        GAMMA_ABSOLUTE,
        ALPHA_RELATIVE,
        BETA_RELATIVE,
        DELTA_RELATIVE,
        THETA_RELATIVE,
        GAMMA_RELATIVE,
        ALPHA_SCORE,
        BETA_SCORE,
        DELTA_SCORE,
        THETA_SCORE,
        GAMMA_SCORE,
        IS_GOOD,
        HSI,
        HSI_PRECISION,
        ARTIFACTS,
        MAGNETOMETER,
        PRESSURE,
        TEMPERATURE,
        ULTRA_VIOLET,
        NOTCH_FILTERED_EEG,
        VARIANCE_EEG,
        VARIANCE_NOTCH_FILTERED_EEG,
        PPG,
        IS_PPG_GOOD,
        IS_HEART_GOOD,
        THERMISTOR,
        IS_THERMISTOR_GOOD,
        AVG_BODY_TEMPERATURE,
        CLOUD_COMPUTED,
    }

    public enum MusePreset : int
    {
        PRESET_10,
        PRESET_12,
        PRESET_14,
        PRESET_20,
        PRESET_21,
        PRESET_22,
        PRESET_23,
        PRESET_AB,
        PRESET_AD,
        PRESET_31,
        PRESET_32,
        PRESET_50,
        PRESET_51,
        PRESET_52,
        PRESET_53,
        PRESET_54,
        PRESET_55,
        PRESET_60,
        PRESET_61,
        PRESET_63,
    }

    public enum NotchFrequency : int
    {
        NOTCH_NONE,
        NOTCH_50HZ,
        NOTCH_60HZ,
    }

    public enum Severity : int
    {
        SEV_VERBOSE,
        SEV_INFO,
        SEV_WARN,
        SEV_ERROR,
        SEV_FATAL,
        SEV_DEBUG,
    }

    public enum ErrorType : int
    {
        FAILURE,
        TIMEOUT,
        OVERLOADED,
        UNIMPLEMENTED,
    }

    public enum TimestampMode : int
    {
        /**
         * Legacy mode.
         *
         * Use the current time for everything except data packets. Use the data
         * packet's timestamp field for those.
         */
        LEGACY,
        /** Use the current time for timestamps. */
        CURRENT,
        /** Use set_timestamp for timestamps. */
        EXPLICIT,
    }

    public enum AnnotationFormat : int
    {
        /** The data is a string with no inherrent format. */
        PLAIN_STRING,
        /** The data is a string of JSON name, value pairs. */
        JSON,
        /** The data is formatted as OSC (open sound control) data. */
        OSC,
    }
}