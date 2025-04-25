using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Storage;
using MK.IO.Models;
using NeuroSpectator.Models.Stream;

namespace NeuroSpectator.Services.Streaming
{
    /// <summary>
    /// Provides configuration and settings for MK.IO integration
    /// </summary>
    public class MKIOConfig
    {
        // Core MK.IO settings
        private readonly string subscriptionName;
        private readonly string apiToken;
        private readonly string storageName;
        private readonly string licenseKey;

        // Stream naming conventions
        private const string StreamPrefix = "ns";
        private const string LiveEventPrefix = "lv";
        private const string AssetPrefix = "asset";
        private const string OutputPrefix = "output";
        private const string LocatorPrefix = "locator";

        // Transform names
        public const string CopyAllBitrateTransformName = "CopyAllBitrateTransform";
        public const string H264MultiBitrateTransformName = "H264MultiBitrateTransform";
        public const string VODTranscriptionTransformName = "VODTranscriptionTransform";
        public const string ThumbnailsTransformName = "ThumbnailsTransform";

        // Preset quality settings
        private readonly Dictionary<StreamQualityLevel, StreamQualitySettings> qualityPresets;

        /// <summary>
        /// Gets the MK.IO subscription name
        /// </summary>
        public string SubscriptionName => subscriptionName;

        /// <summary>
        /// Gets the MK.IO API token (secure)
        /// </summary>
        public string ApiToken => apiToken;

        /// <summary>
        /// Gets the Azure Storage account name
        /// </summary>
        public string StorageName => storageName;

        /// <summary>
        /// Gets the MK.IO license key
        /// </summary>
        public string LicenseKey => licenseKey;

        /// <summary>
        /// Gets a value indicating whether the configuration is valid
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(subscriptionName)
                            && !string.IsNullOrEmpty(apiToken)
                            && !string.IsNullOrEmpty(storageName)
                            && !string.IsNullOrEmpty(licenseKey);

        /// <summary>
        /// Gets the default video player URL template
        /// </summary>
        public string DefaultPlayerUrlTemplate => "https://bitmovin.com/demos/stream-test?format={0}&manifest={1}";

        /// <summary>
        /// Creates a new instance of MKIOConfig
        /// </summary>
        public MKIOConfig(IConfiguration configuration)
        {
            // Required settings - try to load from configuration
            subscriptionName = configuration["MKIOSubscriptionName"];
            Debug.WriteLine($"Loaded MKIOSubscriptionName: {(string.IsNullOrEmpty(subscriptionName) ? "NOT FOUND" : "FOUND")}");
            apiToken = configuration["MKIOToken"];
            Debug.WriteLine($"Loaded MKIOToken: {(string.IsNullOrEmpty(apiToken) ? "NOT FOUND" : "FOUND (token hidden)")}");
            storageName = configuration["StorageName"];
            Debug.WriteLine($"Loaded StorageName: {(string.IsNullOrEmpty(storageName) ? "NOT FOUND" : "FOUND")}");
            licenseKey = configuration["MKIOPlayerLicenseKey"];
            Debug.WriteLine($"Loaded MKIOPlayerLicenseKey: {(string.IsNullOrEmpty(licenseKey) ? "NOT FOUND" : "FOUND (key hidden)")}");

            // Initialize quality presets
            qualityPresets = new Dictionary<StreamQualityLevel, StreamQualitySettings>
            {
                // Low quality preset (mobile-friendly)
                { StreamQualityLevel.Low, new StreamQualitySettings
                    {
                        Width = 852,
                        Height = 480,
                        FrameRate = 30,
                        Bitrate = 1_500_000, // 1.5 Mbps
                        Preset = "veryfast",
                        Profile = "main"
                    }
                },
                
                // Medium quality preset (default)
                { StreamQualityLevel.Medium, new StreamQualitySettings
                    {
                        Width = 1280,
                        Height = 720,
                        FrameRate = 30,
                        Bitrate = 3_000_000, // 3 Mbps
                        Preset = "fast",
                        Profile = "main"
                    }
                },
                
                // High quality preset
                { StreamQualityLevel.High, new StreamQualitySettings
                    {
                        Width = 1920,
                        Height = 1080,
                        FrameRate = 60,
                        Bitrate = 6_000_000, // 6 Mbps
                        Preset = "medium",
                        Profile = "high"
                    }
                }
            };
        }

        /// <summary>
        /// Gets the quality settings for a specified quality level
        /// </summary>
        public StreamQualitySettings GetQualitySettings(StreamQualityLevel qualityLevel)
        {
            if (qualityPresets.TryGetValue(qualityLevel, out var settings))
            {
                return settings;
            }

            // Default to medium quality
            return qualityPresets[StreamQualityLevel.Medium];
        }

        /// <summary>
        /// Generates a unique live event name
        /// </summary>
        public string GenerateLiveEventName(string streamerId = null)
        {
            // Maximum allowed length for MK.IO live event names
            const int maxLength = 32;

            // Start with short prefixes
            string prefix = "ns-lv";

            // Generate a hash of the streamerId + timestamp for uniqueness
            string uniqueInput = $"{streamerId ?? "anon"}-{DateTime.UtcNow.Ticks}";
            string hash = CreateShortHash(uniqueInput);

            // Combine with hyphen separator
            string name = $"{prefix}-{hash}";

            // CHANGE: Replace underscores with hyphens to avoid validation errors
            name = name.Replace('_', '-');

            // Safety check (should never exceed max length with this approach)
            if (name.Length > maxLength)
                name = name.Substring(0, maxLength);

            return name;
        }

        private string CreateShortHash(string input)
        {
            // Create a short hash (using SHA256 and converting to Base64)
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha.ComputeHash(bytes);

                // Take first 12 bytes and convert to Base64 for shorter string
                byte[] shortenedHash = new byte[12];
                Array.Copy(hashBytes, shortenedHash, 12);

                // Convert to Base64 and make URL-safe
                return Convert.ToBase64String(shortenedHash)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .Replace("=", "");
            }
        }

        /// <summary>
        /// Generates a unique asset name
        /// </summary>
        public string GenerateAssetName(string prefix = null, string streamerId = null)
        {
            string uniqueId = GenerateUniqueId();
            string assetPrefix = string.IsNullOrEmpty(prefix) ? AssetPrefix : prefix;

            if (string.IsNullOrEmpty(streamerId))
            {
                return $"{StreamPrefix}-{assetPrefix}-{uniqueId}";
            }

            return $"{StreamPrefix}-{assetPrefix}-{streamerId}-{uniqueId}";
        }

        /// <summary>
        /// Generates a unique output asset name
        /// </summary>
        public string GenerateOutputAssetName(string streamerId = null)
        {
            return GenerateAssetName(OutputPrefix, streamerId);
        }

        /// <summary>
        /// Generates a unique locator name
        /// </summary>
        public string GenerateLocatorName(string streamerId = null)
        {
            string uniqueId = GenerateUniqueId();

            if (string.IsNullOrEmpty(streamerId))
            {
                return $"{StreamPrefix}-{LocatorPrefix}-{uniqueId}";
            }

            return $"{StreamPrefix}-{LocatorPrefix}-{streamerId}-{uniqueId}";
        }

        /// <summary>
        /// Generates a unique job name
        /// </summary>
        public string GenerateJobName(string type, string streamerId = null)
        {
            string uniqueId = GenerateUniqueId();

            if (string.IsNullOrEmpty(streamerId))
            {
                return $"{StreamPrefix}-job-{type}-{uniqueId}";
            }

            return $"{StreamPrefix}-job-{type}-{streamerId}-{uniqueId}";
        }

        /// <summary>
        /// Generates a unique ID string (used as a component in resource names)
        /// </summary>
        private string GenerateUniqueId()
        {
            // Generate an ID based on timestamp and random component
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>
        /// Gets the encoder preset for a given quality level
        /// </summary>
        public EncoderNamedPreset GetEncoderPreset(StreamQualityLevel qualityLevel)
        {
            return qualityLevel switch
            {
                StreamQualityLevel.Low => EncoderNamedPreset.H264SingleBitrate720p,
                StreamQualityLevel.Medium => EncoderNamedPreset.H264MultipleBitrate720p,
                StreamQualityLevel.High => EncoderNamedPreset.H264MultipleBitrate1080p,
                _ => EncoderNamedPreset.H264MultipleBitrate720p
            };
        }

        /// <summary>
        /// Gets the converter preset for VOD conversion
        /// </summary>
        public ConverterNamedPreset GetConverterPreset()
        {
            // Use the CopyAllBitrateInterleaved preset for VOD conversion
            return ConverterNamedPreset.CopyAllBitrateInterleaved;
        }
    }

    /// <summary>
    /// Defines quality levels for streaming
    /// </summary>
    public enum StreamQualityLevel
    {
        Low,
        Medium,
        High
    }
}