using System.Diagnostics;

namespace IhcLab {
    public static class Telemetry
    {
        public const string ActivitySourceName = "ihc_lab";
        public static ActivitySource ActivitySource { get; } = new ActivitySource(ActivitySourceName);        
    }
}
