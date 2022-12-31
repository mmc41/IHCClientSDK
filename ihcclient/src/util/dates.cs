using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text;

namespace Ihc {
    internal class DateHelper {
        /**
        * Get timespan used for converting WS dates to DateTimeOffset.
        */
        public static TimeSpan GetWSTimeOffset() {
            return TimeSpan.FromHours(1);
        }

        /**
        * Get time kind used for converting WS dates to DateTimeOffset.
        */
        public static DateTimeKind GetWSDateTimeKind() {
            return DateTimeKind.Utc;
        }
    }
    
}