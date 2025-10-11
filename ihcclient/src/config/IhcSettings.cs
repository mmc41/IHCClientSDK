namespace Ihc
{
    /// <summary>
    /// Configuration settings for IHC client.
    /// </summary>
    public class IhcSettings
    {
        /// <summary>
        /// The IHC endpoint URL, e.g. "http://192.100.1.10" or "http://usb" (required value).
        /// </summary>
        public string Endpoint { get; init; }

        /// <summary>
        /// The IHC user name. Can be set here or supplied at call time to Authenticate (optional value).
        /// </summary>
        public string UserName { get; init; }

        /// <summary>
        /// The IHC user password. Can be set here or supplied at call time to Authenticate (optional value).
        /// </summary>
        public string Password { get; init; }

        /// <summary>
        /// The IHC application name. Known valid names are "treeview", "openapi", "administrator" (required value).
        /// </summary>
        public string Application { get; init; }

        /// <summary>
        /// Controls if passwords are logged in clear text or not (default false).
        /// </summary>
        public bool LogSensitiveData { get; init; }

        /// <summary>
        /// Controls current async context should be used (false by default)
        /// </summary>
        public bool AsyncContinueOnCapturedContext { get; init; }

        public override string ToString()
        {
            return $"IhcSettings: Endpoint={Endpoint}, UserName={UserName}, Password={(string.IsNullOrEmpty(Password) ? "<not set>" : "<set>")}, Application={Application}, LogSensitiveData={LogSensitiveData}, AasyncContinueOnCapturedContext={AsyncContinueOnCapturedContext}";
        }
    }
}