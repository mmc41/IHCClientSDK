using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Ihc
{
    /// <summary>
    /// Special endpoint constants for IHC client configuration. Other endpoints are expected to be standard HTTP URLs.
    /// </summary>
    public static class SpecialEndpoints {
        /// <summary>
        /// Endpoint URL for USB connected IHC controller.
        /// </summary>
        public const string Usb = "http://usb";

        /// <summary>
        /// Reserved endpoint URL prefix for test/utilities that support mocked IHC 
        /// services for testing purposes.
        /// </summary>
        public const string MockedPrefix = "mock://";
    }

    /// <summary>
    /// Configuration settings for IHC client.
    /// </summary>
    public record IhcSettings
    {
        /// <summary>
        /// Creates a new instance of IhcSettings with blank/default values.
        /// </summary>
        public IhcSettings()
        {
            Endpoint = string.Empty;
            UserName = string.Empty;
            Password = string.Empty;
            Application = string.Empty;
            LogSensitiveData = false;
            AsyncContinueOnCapturedContext = false;
            AllowDangerousInternTestCalls = false;
        }

        /// <summary>
        /// The IHC endpoint URL, e.g. "http://192.100.1.10" or "http://usb" (required value).
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// The IHC user name. Can be set here or supplied at call time to Authenticate (optional value).
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// The IHC user password. Can be set here or supplied at call time to Authenticate (optional value).
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// The IHC application name. Known valid names are "treeview", "openapi", "administrator" (required value).
        /// </summary>
        public string Application { get; set; }

        /// <summary>
        /// Controls if passwords are logged in clear text or not (default false).
        /// </summary>
        public bool LogSensitiveData { get; set; }

        /// <summary>
        /// Controls current async context should be used (false by default)
        /// </summary>
        public bool AsyncContinueOnCapturedContext { get; set; }

        /// <summary>
        /// Controls if potentially dangerous internal IHC test operations may be falled (default false).
        /// </summary>
        public bool AllowDangerousInternTestCalls { get; set; }

        /// <summary>
        /// Validates if the IhcSettings contains the minimum required settings.
        /// </summary>
        /// <returns>True if valid</returns>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Endpoint)
                && !string.IsNullOrEmpty(UserName)
                && !string.IsNullOrEmpty(Password)
                && !string.IsNullOrEmpty(Application);
        }

        public override string ToString()
        {
            return $"IhcSettings: Endpoint={Endpoint}, UserName={UserName}, Password={(string.IsNullOrEmpty(Password) ? "<not set>" : "<set>")}, Application={Application}, LogSensitiveData={LogSensitiveData}, AllowDangerousInternTestCalls={AllowDangerousInternTestCalls}, AasyncContinueOnCapturedContext={AsyncContinueOnCapturedContext}";
        }

        /// <summary>
        /// Reads IHC client settings from a IConfiguration (usually built from ihcsettings.json file).
        /// Will decrypt sensitive data if encryption is enabled (using SimpleSecret).
        /// </summary>
        /// <param name="config">The configuration root</param>
        /// <returns>The IHC client settings.</returns>
        public static IhcSettings GetFromConfiguration(IConfigurationRoot config)
        {
            var settings = config.GetSection("ihcclient").Get<IhcSettings>();
            if (settings == null)
            {
                throw new InvalidOperationException("Could not read IHC client settings from configuration");
            }

            var encryption = config.GetSection("encryption").Get<EncryptionConfiguration>();
            bool encrypted = encryption != null && encryption.IsEncrypted;

            if (encrypted)
            {
                var secret = new SimpleSecret();
                settings.Password = secret.DecryptString(settings.Password);
            }

            return settings;
        }

        /// <summary>
        /// Reads IHC client settings from ihcsettings.json file.
        /// Will decrypt sensitive data if encryption is enabled (using SimpleSecret)
        /// </summary>
        /// <returns>The IHC client settings.</returns>
        public static IhcSettings GetFromFile()
        {
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
            IConfigurationRoot config = new ConfigurationBuilder()
                      .SetBasePath(basePath)
                      .AddJsonFile("ihcsettings.json")
                      .Build();
            return GetFromConfiguration(config);
        }
    }
}