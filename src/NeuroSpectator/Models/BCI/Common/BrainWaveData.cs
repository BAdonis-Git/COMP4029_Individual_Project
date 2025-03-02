using System;

namespace NeuroSpectator.Models.BCI.Common
{
    /// <summary>
    /// Represents brain wave data from a BCI device
    /// </summary>
    public class BrainWaveData
    {
        /// <summary>
        /// Gets the timestamp when the data was captured
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the type of brain wave this data represents
        /// </summary>
        public BrainWaveTypes WaveType { get; }

        /// <summary>
        /// Gets the brain wave values for each channel
        /// </summary>
        public double[] ChannelValues { get; }

        /// <summary>
        /// Gets the number of channels in this data
        /// </summary>
        public int ChannelCount => ChannelValues?.Length ?? 0;

        /// <summary>
        /// Gets the average value across all channels
        /// </summary>
        public double AverageValue => CalculateAverage();

        /// <summary>
        /// Creates a new instance of BrainWaveData
        /// </summary>
        public BrainWaveData(BrainWaveTypes waveType, double[] channelValues, DateTimeOffset timestamp)
        {
            WaveType = waveType;
            ChannelValues = channelValues ?? Array.Empty<double>();
            Timestamp = timestamp;
        }

        /// <summary>
        /// Creates a new instance of BrainWaveData for a single value (e.g. derived metrics)
        /// </summary>
        public BrainWaveData(BrainWaveTypes waveType, double value, DateTimeOffset timestamp)
            : this(waveType, new[] { value }, timestamp)
        {
        }

        /// <summary>
        /// Gets the value for a specific channel
        /// </summary>
        public double GetChannel(int channelIndex)
        {
            if (channelIndex >= 0 && channelIndex < ChannelCount)
            {
                return ChannelValues[channelIndex];
            }
            return 0.0;
        }

        private double CalculateAverage()
        {
            if (ChannelCount == 0) return 0.0;

            double sum = 0.0;
            foreach (var value in ChannelValues)
            {
                sum += value;
            }
            return sum / ChannelCount;
        }

        /// <summary>
        /// Creates a brain wave data packet for Alpha waves
        /// </summary>
        public static BrainWaveData CreateAlpha(double[] values, DateTimeOffset timestamp) =>
            new BrainWaveData(BrainWaveTypes.Alpha, values, timestamp);

        /// <summary>
        /// Creates a brain wave data packet for Beta waves
        /// </summary>
        public static BrainWaveData CreateBeta(double[] values, DateTimeOffset timestamp) =>
            new BrainWaveData(BrainWaveTypes.Beta, values, timestamp);

        /// <summary>
        /// Creates a brain wave data packet for Delta waves
        /// </summary>
        public static BrainWaveData CreateDelta(double[] values, DateTimeOffset timestamp) =>
            new BrainWaveData(BrainWaveTypes.Delta, values, timestamp);

        /// <summary>
        /// Creates a brain wave data packet for Theta waves
        /// </summary>
        public static BrainWaveData CreateTheta(double[] values, DateTimeOffset timestamp) =>
            new BrainWaveData(BrainWaveTypes.Theta, values, timestamp);

        /// <summary>
        /// Creates a brain wave data packet for Gamma waves
        /// </summary>
        public static BrainWaveData CreateGamma(double[] values, DateTimeOffset timestamp) =>
            new BrainWaveData(BrainWaveTypes.Gamma, values, timestamp);

        /// <summary>
        /// Creates a brain wave data packet for raw EEG data
        /// </summary>
        public static BrainWaveData CreateRaw(double[] values, DateTimeOffset timestamp) =>
            new BrainWaveData(BrainWaveTypes.Raw, values, timestamp);
    }
}