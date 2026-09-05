using System.Threading.Tasks;
using System;
using System.Diagnostics;
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

                // The sentinel, not a cleared component: zeroing the time and keeping the date turns a
                // record that could not be read into a midnight instant no caller can tell from a real one.
                if (hours < 0 || hours > 23) return DateTimeOffset.MinValue;
                if (minutes < 0 || minutes > 59) return DateTimeOffset.MinValue;
                if (seconds < 0 || seconds > 59) return DateTimeOffset.MinValue;

                // Try to create the DateTimeOffset
                return new DateTimeOffset(year, month, day, hours, minutes, seconds, offset);
            }
            catch {
                return DateTimeOffset.MinValue;
            }
        }

        /// <summary>
        /// The value when the controller sent a readable one; otherwise the SDK's documented "no reading"
        /// sentinel, with a span warning recording that a substitution happened.
        /// </summary>
        /// <remarks>
        /// <para>The SENTINEL IS THE CONTRACT and does not change here: every mapping site that reads a wire date
        /// already answered <see cref="DateTimeOffset.MinValue"/> for an absent one, and
        /// <c>TimeManagerServiceMappingTests</c> pins two of them. What was missing is any record that the answer
        /// was substituted rather than read, so a caller comparing against the sentinel could not tell an absent
        /// element from a controller whose clock genuinely reads that instant.</para>
        /// <para>TWO PRODUCERS, TWO WARNINGS. The sentinel arrives here by two different routes and both are
        /// substitutions: the response carried no element at all (<c>AbsentWireDate</c>), or it carried one whose
        /// components <see cref="CreateDateTimeOffset"/> could not read and already answered with the sentinel
        /// (<c>UnreadableWireDate</c>). Warning on the null alone would have made the silence at the second route
        /// read as "the controller sent this instant", which is the very confusion the first warning exists to
        /// remove. The two are separate tag values because the repair differs: one is a missing element, the
        /// other is a malformed one.</para>
        /// <para>It takes an ALREADY-CONVERTED <see cref="Nullable{T}"/> deliberately: <c>WSDate</c> is a
        /// different generated type per SOAP namespace, so no helper can take one and serve every site. Each
        /// site converts, then passes the result here.</para>
        /// <para><see cref="Activity.Current"/> rather than a passed activity, because most of the sites are
        /// static <c>mapX</c> helpers with no activity in scope — the SDK span is current there because the
        /// mapping runs inside the service's own <c>StartActivity</c> block. A host with tracing off has no
        /// current activity and the warning is a no-op, which is why the returned value carries the contract and
        /// the warning only carries the diagnosis.</para>
        /// </remarks>
        /// <param name="value">The converted wire date, or null when the response carried none.</param>
        /// <param name="field">The domain field being filled, for the warning's <c>field</c> tag.</param>
        /// <returns>The value, or <see cref="DateTimeOffset.MinValue"/>.</returns>
        internal static DateTimeOffset OrAbsentSentinel(DateTimeOffset? value, string field)
        {
            if (value is { } read && read != DateTimeOffset.MinValue)
            {
                return read;
            }

            Activity.Current.AddWarning(
                value.HasValue
                    ? $"The controller response carried a date for {field} that could not be read; substituting DateTimeOffset.MinValue."
                    : $"The controller response carried no date for {field}; substituting DateTimeOffset.MinValue.",
                ("type", value.HasValue ? "UnreadableWireDate" : "AbsentWireDate"),
                ("field", field));

            return DateTimeOffset.MinValue;
        }
    }

}