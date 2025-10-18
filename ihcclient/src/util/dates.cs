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
        internal static DateTimeKind GetWSDateTimeKind()
        {
            return DateTimeKind.Utc;
        }

        /**
        * Safely try to create a DateTimeOffset from individual components.
        * Return MinValue if invalud date.
        */
        internal static DateTimeOffset CreateDateTimeOffset(int year, int month, int day, int hours, int minutes, int seconds, TimeSpan offset) {
            try {
                // First validate that the date components are in valid ranges
                if (year < 1 || year > 9999) return DateTimeOffset.MinValue;
                if (month < 1 || month > 12) return DateTimeOffset.MinValue;
                if (day < 1 || day > 31) return DateTimeOffset.MinValue;

                // Clear time if out of bounds.
                if (hours < 0 || hours > 23) hours = 0;
                if (minutes < 0 || minutes > 59) minutes = 0;
                if (seconds < 0 || seconds > 59) seconds = 0;

                // Try to create the DateTimeOffset
                return new DateTimeOffset(year, month, day, hours, minutes, seconds, offset);
            }
            catch {
                return DateTimeOffset.MinValue;
            }
        }
    }

}