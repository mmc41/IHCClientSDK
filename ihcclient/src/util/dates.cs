using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text;

namespace Ihc {
    internal class DateHelper {
        // Configurable timezone offset for IHC controller
        // Default is UTC+1 (Central European Time) which matches most IHC installations
        private static readonly TimeSpan _wsTimeOffset = TimeSpan.FromHours(1);

        /**
        * Get timespan used for converting WS dates to DateTimeOffset.
        * Default is UTC+1 (Central European Time).
        */
        public static TimeSpan GetWSTimeOffset() {
            return _wsTimeOffset;
        }

        /**
        * Get time kind used for converting WS dates to DateTimeOffset.
        */
        public static DateTimeKind GetWSDateTimeKind() {
            return DateTimeKind.Utc;
        }
    }

}