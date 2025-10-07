using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text;

namespace Ihc {
    internal class DateHelper {
        // Configurable timezone offset for IHC controller
        // Default is UTC+1 (Central European Time) which matches most IHC installations
        private static TimeSpan _wsTimeOffset = TimeSpan.FromHours(1);

        /**
        * Set the timezone offset for converting WS dates to DateTimeOffset.
        * This should match the timezone where your IHC controller is located.
        *
        * @param offset Timezone offset (e.g., TimeSpan.FromHours(1) for UTC+1)
        *
        * Note: This is a global setting that affects all date/time conversions.
        * Call this once during application initialization before using any IHC services.
        */
        public static void SetWSTimeOffset(TimeSpan offset) {
            _wsTimeOffset = offset;
        }

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