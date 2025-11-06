using System.Diagnostics;

namespace IhcLab {
    public static class Telemetry
    {
        public const string AppServiceName = "IhcLab";
        public const string AppServiceNamespace = "Ihc";
        public const string ActivitySourceName = "IhcLab";
        public static ActivitySource ActivitySource { get; } = new ActivitySource(name: ActivitySourceName, version: VersionInfo.GetAppVersionStr());        
    }
}
