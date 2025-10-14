using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text;

namespace Ihc {
    public class DateHelper {
        // Configurable timezone offset for IHC controller
        // Default is UTC+1 (Central European Time) which matches most IHC installations
        public static readonly TimeSpan TimeOffset = TimeSpan.FromHours(1);

        /**
        * Get timespan used for converting WS dates to DateTimeOffset.
        * Default is UTC+1 (Central European Time).
        */
        internal static TimeSpan GetWSTimeOffset() {
            return TimeOffset;
        }

        /**
        * Get time kind used for converting WS dates to DateTimeOffset.
        */
        internal static DateTimeKind GetWSDateTimeKind() {
            return DateTimeKind.Utc;
        }
    }

}