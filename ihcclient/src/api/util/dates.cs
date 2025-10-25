using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text;

namespace Ihc {
    internal class DateHelper {
        // Configurable timezone offset for IHC controller
        // Default is UTC+1 (Central European Time) which matches most IHC installations
        public static readonly TimeSpan TimeOffset = TimeSpan.FromHours(1); // TODO: Make configurable

        /// <summary>
        /// Get timespan used for converting WS dates to DateTimeOffset.
        /// Default is UTC+1 (Central European Time).
        /// </summary>
        /// <returns>TimeSpan offset for WS date conversion.</returns>
        internal static TimeSpan GetWSTimeOffset() {
            return TimeOffset;
        }

        /// <summary>
        /// Get time kind used for converting WS dates to DateTimeOffset.
        /// </summary>
        /// <returns>DateTimeKind for WS date conversion.</returns>
        internal static DateTimeKind GetWSDateTimeKind()
        {
            return DateTimeKind.Utc;
        }

        /// <summary>
        /// Safely try to create a DateTimeOffset from individual components.
        /// Returns MinValue if invalid date.
        /// </summary>
        /// <param name="year">Year component.</param>
        /// <param name="month">Month component (1-12).</param>
        /// <param name="day">Day component (1-31).</param>
        /// <param name="hours">Hours component (0-23).</param>
        /// <param name="minutes">Minutes component (0-59).</param>
        /// <param name="seconds">Seconds component (0-59).</param>
        /// <param name="offset">Time zone offset.</param>
        /// <returns>DateTimeOffset or MinValue if invalid.</returns>
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