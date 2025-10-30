using Microsoft.Extensions.Configuration;

namespace Ihc
{
    /// <summary>
    /// Configuration settings for encryption of sensitive data.
    /// </summary>
    public record EncryptionConfiguration
    {
        /// <summary>
        /// Controls data in the ihcsettings.json file is encrypted or not.
        /// </summary>
        public bool IsEncrypted { get; set; }

        /// <summary>
        /// Reads encryption configuration from a IConfiguration
        /// </summary>
        public static EncryptionConfiguration GetFromConfiguration(IConfigurationRoot config)
        {
            return config.GetSection("encryption").Get<EncryptionConfiguration>() 
                    ?? new EncryptionConfiguration { IsEncrypted = false }; 
        }
    }
}