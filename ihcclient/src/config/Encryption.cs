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
    }
}