namespace Ihc
{
    /// <summary>
    /// Configuration settings for IHC client.
    /// </summary>
    public class IhcSettings
    {
        public string Endpoint { get; init; }
        public string UserName { get; init; }
        public string Password { get; init; }
        public string Application { get; init; }
        public bool LogSensitiveData { get; init; }
        public bool AsyncContinueOnCapturedContext { get; init; }

        public override string ToString()
        {
            return $"IhcSettings: Endpoint={Endpoint}, UserName={UserName}, Password={(string.IsNullOrEmpty(Password) ? "<not set>" : "<set>")}, Application={Application}, LogSensitiveData={LogSensitiveData}, AasyncContinueOnCapturedContext={AsyncContinueOnCapturedContext}";
        }
    }
}