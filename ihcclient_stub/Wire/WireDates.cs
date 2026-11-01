namespace Ihc.Stubbing
{
    /// <summary>
    /// Writes an SDK date as the generated <c>WSDate</c> it arrives in. Each SOAP namespace generates its own copy of
    /// that type, so one mapper serves them all by member name.
    /// </summary>
    internal static class WireDates
    {
        /// <summary>
        /// The offset a controller writes its date components in, and the SDK reads them back with. Restated
        /// rather than shared, because the SDK's copy is internal to it; the typed round trips pin the two equal.
        /// </summary>
        internal static readonly TimeSpan Offset = TimeSpan.FromHours(1);

        /// <summary>
        /// <paramref name="value"/> as a <typeparamref name="TWsDate"/>, or null for
        /// <see cref="DateTimeOffset.MinValue"/>, which is how the SDK represents a date the controller left out.
        /// </summary>
        internal static TWsDate? ToWire<TWsDate>(DateTimeOffset value) where TWsDate : class, new()
        {
            if (value == DateTimeOffset.MinValue)
            {
                return null;
            }
            return (TWsDate)ShapeDefaults.SetMembers(new TWsDate(), Parts(value.ToOffset(Offset)));
        }

        /// <summary>The members a <c>WSDate</c> states <paramref name="date"/> in, as its own clock shows it.</summary>
        internal static (string Name, object Value)[] Parts(DateTimeOffset date) =>
        [
            ("year", date.Year), ("monthWithJanuaryAsOne", date.Month), ("day", date.Day),
            ("hours", date.Hour), ("minutes", date.Minute), ("seconds", date.Second),
        ];

        /// <summary>
        /// A generated <c>WSDate</c> of any namespace as the date it states, read at <see cref="Offset"/>, or
        /// <see cref="DateTimeOffset.MinValue"/> for none, the SDK's own sign for a date left out.
        /// </summary>
        internal static DateTimeOffset FromWire(object? wsDate)
        {
            if (wsDate is null)
            {
                return DateTimeOffset.MinValue;
            }
            Type type = wsDate.GetType();
            int Part(string name) => (int)(type.GetProperty(name)?.GetValue(wsDate)
                ?? throw new InvalidOperationException($"{type.FullName} has no member {name}"));
            return new DateTimeOffset(Part("year"), Part("monthWithJanuaryAsOne"), Part("day"),
                Part("hours"), Part("minutes"), Part("seconds"), Offset);
        }
    }
}
