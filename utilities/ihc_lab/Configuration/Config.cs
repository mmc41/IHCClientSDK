using System;
using System.IO;
using System.Reflection;
using Ihc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace IhcLab
{
    /// <summary>
    /// Combinded logging and telemtry setting
    /// </summary>
    public class Configuration
    {
        public IConfigurationSection loggingConfig { get; init; }
        public TelemetryConfiguration telemetryConfig { get; init; }
        public IhcSettings ihcSettings { get; init; }

        public Configuration() {
            // Access configuration file that stores IHC and SDK setup informnation including username, password etc.
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
            IConfigurationRoot config = new ConfigurationBuilder()
                        .SetBasePath(basePath)
                        .AddJsonFile("ihcsettings.json")
                        .Build();

            IConfigurationSection? loggingConfig = config.GetSection("Logging");
            TelemetryConfiguration? telemetryConfig = config.GetSection("telemetry").Get<TelemetryConfiguration>();
            if (telemetryConfig == null || loggingConfig == null)
            {
                throw new InvalidDataException("Could not read Telemtry/logging settings from configuration");
            }

            IhcSettings? ihcSettings = config.GetSection("ihcclient").Get<IhcSettings>();
            if (ihcSettings == null)
            {
                throw new InvalidDataException("Could not read Telemtry/logging settings from configuration");
            }

            this.loggingConfig = loggingConfig;
            this.telemetryConfig = telemetryConfig;
            this.ihcSettings = ihcSettings;
        }
    }
}