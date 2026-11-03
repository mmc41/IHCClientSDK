using System;

namespace Ihc
{
    /// <summary>
    /// The unwrap every domain-to-wire resource-value mapping performs on the union member its
    /// <see cref="ResourceValue.ValueKind"/> selects.
    ///
    /// Shared because there are two such mappings - <see cref="ResourceValueEnvelopeMapper.ToWire"/>
    /// over the resourceinteraction namespace and <see cref="OpenApiResourceValueMapper.ToWire"/> over the
    /// openapi one - written independently against generated types the vendor's WSDL shapes alike.
    /// They are each other's second opinion on the mapping itself, which is worth keeping; they are
    /// not two opinions on what a missing payload means, and the copy of this guard that used to be
    /// absent from one of them was how an OpenAPI write of a malformed value came to surface an opaque
    /// "Nullable object must have a value" instead of naming the field that was left unset.
    /// </summary>
    internal static class ResourceValuePayload
    {
        /// <summary>
        /// Unwraps a value-typed payload that must be present for the kind being written. A
        /// well-formed <see cref="ResourceValue"/> always carries the payload matching its
        /// <see cref="ResourceValue.ValueKind"/>; a caller that sets the kind but leaves the payload
        /// null would otherwise trip an unconditional <see cref="Nullable{T}"/> cast.
        /// </summary>
        internal static T Required<T>(T? payload, ResourceValue.ValueKind kind, string field) where T : struct
        {
            if (!payload.HasValue)
                throw new ArgumentException($"ResourceValue of kind {kind} is missing its required {field} payload.");

            return payload.Value;
        }

        /// <summary>
        /// The same refusal for a payload held by reference — ENUM and PhoneNumber, the two kinds
        /// whose payload is one. A null there is as malformed as a null <see cref="Nullable{T}"/>: it
        /// is not an empty value the controller can be asked to store, and both wires declare the
        /// element carrying it as required.
        /// </summary>
        /// <remarks>
        /// An OVERLOAD rather than a second name, so no call site has to decide which guard its payload
        /// takes — the two differ in the parameter type the compiler already knows. A second name is how
        /// PhoneNumber came to be guarded by neither: a reference-typed payload read as one of the
        /// <see cref="Nullable{T}"/> ones is a mistake a reader makes and the compiler does not.
        /// </remarks>
        internal static T Required<T>(T? payload, ResourceValue.ValueKind kind, string field) where T : class
        {
            if (payload is null)
                throw new ArgumentException($"ResourceValue of kind {kind} is missing its required {field} payload.");

            return payload;
        }

        /// <summary>
        /// Refuses a <see cref="TimeSpan"/> the TIME wire element cannot hold. That element carries whole
        /// hours, minutes and seconds and nothing else, so a span of a day or more, a negative span, or one
        /// with a sub-second remainder has no representation there at all.
        /// </summary>
        /// <remarks>
        /// Both mappers write the components straight out of the span, which for such a value is not a lossy
        /// encoding but a different one: 25 hours writes as one, and a negative span writes as its own
        /// magnitude's components. The loss the mappers' own doc-comments call intentional is the DATE
        /// time-of-day and the TIME sub-day granularity of a value that fits - not a value that does not.
        /// One implementation, called from both, so the two stay each other's second opinion on the mapping
        /// without becoming two opinions on what is writable.
        /// </remarks>
        internal static TimeSpan Representable(TimeSpan time, ResourceValue.ValueKind kind, string field)
        {
            if (time < TimeSpan.Zero || time >= TimeSpan.FromHours(24) || time.Ticks % TimeSpan.TicksPerSecond != 0)
                throw new ArgumentException($"ResourceValue of kind {kind} carries a {field} of {time}, which the wire cannot represent: it holds whole seconds from 00:00:00 up to but not including 24:00:00.");

            return time;
        }

        /// <summary>
        /// The range an INT or DOUBLE value arrived with, or none for 0 to 0: that is the form a write of a value
        /// without a range sends, and read as a range it would admit only 0.
        /// </summary>
        internal static (T? Minimum, T? Maximum) RangeFromWire<T>(T minimum, T maximum) where T : struct, System.Numerics.INumber<T> =>
            T.IsZero(minimum) && T.IsZero(maximum) ? (null, null) : (minimum, maximum);

        /// <summary>
        /// Refuses an INT or DOUBLE value outside the range it carries, as ServiceView refuses a value outside the
        /// range it was given; a bound the value does not carry checks nothing.
        /// </summary>
        internal static T InCarriedRange<T>(T value, T? minimum, T? maximum, ResourceValue.ValueKind kind, string field)
            where T : struct, System.Numerics.INumber<T>
        {
            if ((minimum is { } min && value < min) || (maximum is { } max && value > max))
                throw new ArgumentException($"ResourceValue of kind {kind} carries a {field} of {value}, outside the range {minimum}-{maximum} it carries.");

            return value;
        }

        /// <summary>An INT payload to write, refused when absent or outside the range it carries.</summary>
        internal static int WritableInt(ResourceValue.UnionValue value) =>
            InCarriedRange(Required(value.IntValue, value.ValueKind, nameof(value.IntValue)),
                value.IntMinimum, value.IntMaximum, value.ValueKind, nameof(value.IntValue));

        /// <summary>A DOUBLE payload to write, refused when absent or outside the range it carries.</summary>
        internal static double WritableDouble(ResourceValue.UnionValue value) =>
            InCarriedRange(Required(value.DoubleValue, value.ValueKind, nameof(value.DoubleValue)),
                value.DoubleMinimum, value.DoubleMaximum, value.ValueKind, nameof(value.DoubleValue));

        /// <summary>A WEEKDAY payload to write, refused when absent or outside 0-6.</summary>
        internal static int WritableWeekday(ResourceValue.UnionValue value) =>
            Within(Required(value.WeekdayValue, value.ValueKind, nameof(value.WeekdayValue)), 0, 6, value.ValueKind, nameof(value.WeekdayValue));

        /// <summary>A scene dimmer's percentage to write, refused when absent or outside 0-100.</summary>
        internal static int WritableDimmerPercentage(ResourceValue.UnionValue value) =>
            Within(Required(value.DimmerPercentage, value.ValueKind, nameof(value.DimmerPercentage)), 0, 100, value.ValueKind, nameof(value.DimmerPercentage));

        /// <summary>A scene dimmer's ramp to write, refused when absent or longer than the vendor editors accept.</summary>
        internal static int WritableDimmerRampTime(ResourceValue.UnionValue value) =>
            RampTime(Required(value.DimmerRampTime, value.ValueKind, nameof(value.DimmerRampTime)), value.ValueKind, nameof(value.DimmerRampTime));

        /// <summary>The longest dimmer ramp the vendor editors accept: 60 minutes.</summary>
        internal const int MaxRampTimeMilliseconds = 3_600_000;

        /// <summary>
        /// Refuses a value outside the range the vendor editors keep it in: a weekday in 0-6 and a scene dimmer
        /// percentage in 0-100. These are the rules that need nothing but the value; a range read from the
        /// controller, or an ENUM's members, are not known to the stateless mappers.
        /// </summary>
        internal static int Within(int value, int min, int max, ResourceValue.ValueKind kind, string field)
        {
            if (value < min || value > max)
                throw new ArgumentException($"ResourceValue of kind {kind} carries a {field} of {value}, outside {min}-{max}, which the vendor editors refuse.");

            return value;
        }

        /// <summary>Refuses a scene dimmer ramp longer than the vendor editors accept.</summary>
        internal static int RampTime(int ramp, ResourceValue.ValueKind kind, string field)
        {
            if (ramp > MaxRampTimeMilliseconds)
                throw new ArgumentException($"ResourceValue of kind {kind} carries a {field} of {ramp} ms, over the {MaxRampTimeMilliseconds} ms the vendor editors accept.");

            return ramp;
        }

        /// <summary>
        /// Refuses resource id 0, the <c>.vis</c> null reference: IHC Visual never allocates it, so a write to it
        /// addresses no resource.
        /// </summary>
        internal static void Addressed(ResourceValue value)
        {
            if (value.ResourceID == 0)
                throw new ArgumentException($"ResourceValue has {nameof(ResourceValue.ResourceID)} 0, the null reference no resource is allocated.");
        }
    }
}
