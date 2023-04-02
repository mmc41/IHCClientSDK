using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;

namespace Ihc {
    public record SystemInfo { 

        public long Uptime { get; init; }
        
        public DateTimeOffset Realtimeclock { get; init; }
        
        public string SerialNumber { get; init; }
        
        public string ProductionDate { get; init; }
        
        public string Brand { get; init; }
        
        public string Version { get; init; }
        
        public string HWRevision { get; init; }
        
        public DateTimeOffset SWDate { get; init; }
        
        public string DatalineVersion { get; init; }
        
        public string RFModuleSoftwareVersion { get; init; }
        
        public string RFModuleSerialNumber { get; init; }
        
        public bool ApplicationIsWithoutViewer { get; init; }
    }

    public record UserLogFileText {
        public string FileName { get; init; }
        public string Content { get; init; }
    }
}