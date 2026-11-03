using System.Threading.Tasks;
using System;
using System.Globalization;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;

namespace Ihc {
    /// <summary>
    /// Element type names of the dataline resources, as <see cref="ResourceValue.TypeString"/> carries them; other
    /// resources have other names, such as <c>resource_input</c> or <c>resource_integer</c>.
    /// </summary>
    public class TypeStrings {
        /// <summary>
        /// Type string of a wired product's dataline input, whose value is on/off (BOOL).
        /// </summary>
        public static readonly string DatalineInput = "dataline_input";

        /// <summary>
        /// Type string of a wired product's dataline output, whose value is on/off (BOOL).
        /// </summary>
        public static readonly string DatalineOutput = "dataline_output";
    };

     /// <summary>
     /// One input or output of the controller's wired dataline, by number, with the resource that carries its value.
     /// </summary>
    public record DatalineResource {
        /// <summary>
        /// The dataline number, 1-128 per direction. A controller terminal carries 16 inputs or 8 outputs, so input 17
        /// is terminal 2, port 1.
        /// </summary>
        public int DatalineNumber { get; init; }

        /// <summary>
        /// Id of the resource that carries this dataline's on/off value.
        /// </summary>
        public int ResourceID { get; init; }

        public override string ToString()
        {
          return $"DatalineResource(DatalineNumber={DatalineNumber}, ResourceID={ResourceID})";
        }
    }

     /// <summary>
     /// One resource's value as the controller reports or accepts it: the resource id, whether it is the current or
     /// the initial value, and the typed value itself.
     /// </summary>
    public record ResourceValue {
        /// <summary>
        /// Enumeration of supported value kinds for resource values.
        /// </summary>
        public enum ValueKind {
            /// <summary>On/off value of inputs, outputs, flags, and dataline inputs and outputs.</summary>
            BOOL,
            /// <summary>Calendar date: year, month and day (<c>resource_date</c>).</summary>
            DATE,
            /// <summary>Whole number: integers, counters and levels in percent.</summary>
            INT,
            /// <summary>Decimal number: temperatures, humidity levels, and power and energy readings.</summary>
            DOUBLE,
            /// <summary>One value of an enumeration: an enum resource, or a dimmer's load mode.</summary>
            ENUM,
            /// <summary>Time of day to the second (<c>resource_time</c>).</summary>
            TIME,
            /// <summary>Duration in milliseconds: timers and stored timer times.</summary>
            TIMER,
            /// <summary>Day of the week (<c>resource_weekday</c>).</summary>
            WEEKDAY,
            /// <summary>Phone number as text, such as an SMS modem's number.</summary>
            PhoneNumber,
            /// <summary>A dimmer's scene member: the level it takes when the scene is recalled, with ramp and delay
            /// times.</summary>
            SceneDimmer,
            /// <summary>A relay's scene member: the on/off state it takes when the scene is recalled, with a delay
            /// time.</summary>
            SceneRelay,
            /// <summary>A shutter's scene member: up or down when the scene is recalled, with a delay time.</summary>
            SceneShutter,
            /// <summary>No readable value: the resource carries none, as a function block's scene output
            /// (<c>resource_scene</c>) arrives without a value, or the wire value is of a type the SDK does not
            /// recognize. Read-only — not writable to the controller.</summary>
            NONE
        };

        /// <summary>
        /// Union structure holding the actual value based on ValueKind.
        /// Only the field matching ValueKind should be populated.
        /// </summary>
        public struct UnionValue {
            /// <summary>
            /// Indicates which type of value this union contains.
            /// </summary>
            public ValueKind ValueKind { get; set; }

            /// <summary>
            /// On (<c>true</c>) or off (<c>false</c>) (when ValueKind is BOOL).
            /// </summary>
            public bool? BoolValue { get; set; }

            /// <summary>
            /// Whole number, 32-bit (when ValueKind is INT); a level in percent runs 0-100. Its range, when the
            /// controller sent one, is <see cref="IntMinimum"/> to <see cref="IntMaximum"/>.
            /// </summary>
            public int? IntValue { get; set; }

            /// <summary>
            /// Lowest value the controller allows for an INT value, as it sent it with the value; <c>null</c> when it
            /// sent none. A write sends the bounds the value carries, as IHC Visual writes a value back with its range,
            /// and 0 for an absent one; a range of 0 to 0 is that wire form and reads as none. A write refuses an
            /// <see cref="IntValue"/> outside the bounds it carries, as ServiceView does.
            /// </summary>
            public int? IntMinimum { get; set; }

            /// <summary>Highest value the controller allows for an INT value; see <see cref="IntMinimum"/>.</summary>
            public int? IntMaximum { get; set; }

            /// <summary>
            /// Decimal number (when ValueKind is DOUBLE), such as a temperature in degrees Celsius or a relative
            /// humidity in percent. Its range, when the controller sent one, is <see cref="DoubleMinimum"/> to
            /// <see cref="DoubleMaximum"/>.
            /// </summary>
            public double? DoubleValue  { get; set; }

            /// <summary>
            /// Lowest value the controller allows for a DOUBLE value, as it sent it; <c>null</c> when it sent none. As
            /// for <see cref="IntMinimum"/>, a write sends the carried bounds, 0 for an absent one, and 0 to 0 reads as
            /// none; a write refuses a <see cref="DoubleValue"/> outside the bounds it carries.
            /// </summary>
            public double? DoubleMinimum { get; set; }

            /// <summary>Highest value the controller allows for a DOUBLE value; see <see cref="DoubleMinimum"/>.</summary>
            public double? DoubleMaximum { get; set; }

            /// <summary>
            /// Calendar date (when ValueKind is DATE). Only year, month and day travel: the SDK reads them as midnight
            /// at a fixed +01:00 offset and writes the value's own year, month and day unconverted, so use the date
            /// parts, not the instant.
            /// </summary>
            public DateTimeOffset? DateValue { get; set; }

            /// <summary>
            /// Time of day in whole seconds, 00:00:00 to 23:59:59 (when ValueKind is TIME); a write throws
            /// <see cref="ArgumentException"/> for any other span.
            /// </summary>
            public TimeSpan? TimeValue { get; set; }

            /// <summary>
            /// Timer value in milliseconds (when ValueKind is TIMER).
            /// </summary>
            public long? TimerValue { get; set; }

            /// <summary>
            /// Day of the week, 0 = Monday to 6 = Sunday (when ValueKind is WEEKDAY); a write refuses any other.
            /// </summary>
            public int? WeekdayValue { get; set; }

            /// <summary>
            /// Enum value (when ValueKind is ENUM).
            /// </summary>
            public EnumValue? EnumValue { get; set; }

            /// <summary>
            /// Phone number as text (when ValueKind is PhoneNumber). The SMS modem's editor in the vendor clients
            /// takes 3-20 characters; the RS-485 modem's checks nothing, and the SDK checks neither.
            /// </summary>
            public string? PhoneNumberValue { get; set; }

            /// <summary>
            /// Light level in percent, 0-100, that the dimmer takes when the scene is recalled (when ValueKind is
            /// SceneDimmer); a write refuses any other.
            /// </summary>
            public int? DimmerPercentage { get; set; }

            /// <summary>
            /// Dimmer delay time in milliseconds (when ValueKind is SceneDimmer).
            /// </summary>
            public int? DimmerDelayTime { get; set; }

            /// <summary>
            /// Dimmer ramp time in milliseconds (when ValueKind is SceneDimmer), at most 3,600,000 (60 minutes); a
            /// write refuses a longer one.
            /// </summary>
            public int? DimmerRampTime { get; set; }

            /// <summary>
            /// Relay delay time in milliseconds (when ValueKind is SceneRelay).
            /// </summary>
            public int? RelayDelayTime { get; set; }

            /// <summary>
            /// Relay state the scene recalls, <c>true</c> = on (when ValueKind is SceneRelay).
            /// </summary>
            public bool? RelayValue { get; set; }

            /// <summary>
            /// Shutter position the scene recalls: <c>true</c> = up, <c>false</c> = down (when ValueKind is
            /// SceneShutter).
            /// </summary>
            public bool? ShutterPositionIsUp { get; set; }

            /// <summary>
            /// Shutter delay time in milliseconds (when ValueKind is SceneShutter).
            /// </summary>
            public int? ShutterDelayTime { get; set; }

            public override String ToString() {
                StringBuilder buf = new StringBuilder();

                buf.Append("UnionValue {");
                buf.AppendFormat(CultureInfo.InvariantCulture, "ValueKind={0}", ValueKind);

                // Raw print out of any values, regardless of kind
                // so even invalid combinations show up:

                AppendIfPresent(buf, nameof(BoolValue), BoolValue);
                AppendIfPresent(buf, nameof(IntValue), IntValue);
                AppendIfPresent(buf, nameof(IntMinimum), IntMinimum);
                AppendIfPresent(buf, nameof(IntMaximum), IntMaximum);
                AppendIfPresent(buf, nameof(DoubleValue), DoubleValue);
                AppendIfPresent(buf, nameof(DoubleMinimum), DoubleMinimum);
                AppendIfPresent(buf, nameof(DoubleMaximum), DoubleMaximum);
                AppendIfPresent(buf, nameof(DateValue), DateValue);
                AppendIfPresent(buf, nameof(TimeValue), TimeValue);
                AppendIfPresent(buf, nameof(TimerValue), TimerValue);
                AppendIfPresent(buf, nameof(WeekdayValue), WeekdayValue);
                AppendIfPresent(buf, nameof(PhoneNumberValue), PhoneNumberValue);
                AppendIfPresent(buf, nameof(DimmerPercentage), DimmerPercentage);
                AppendIfPresent(buf, nameof(DimmerDelayTime), DimmerDelayTime);
                AppendIfPresent(buf, nameof(DimmerRampTime), DimmerRampTime);
                AppendIfPresent(buf, nameof(RelayDelayTime), RelayDelayTime);
                AppendIfPresent(buf, nameof(RelayValue), RelayValue);
                AppendIfPresent(buf, nameof(ShutterPositionIsUp), ShutterPositionIsUp);
                AppendIfPresent(buf, nameof(ShutterDelayTime), ShutterDelayTime);
                buf.Append('}');

                return buf.ToString();
            }

            // Appends ", name=value" for any present value. Nullable value types box to null when empty, so this
            // one helper covers both Nullable<T> fields and reference-typed fields (e.g. PhoneNumberValue).
            private static void AppendIfPresent(StringBuilder buf, string name, object? value) {
                if (value != null) {
                    buf.AppendFormat(CultureInfo.InvariantCulture, ", {0}={1}", name, value);
                }
            }
        };

        /// <summary>
        /// The resource's element type name in the .vis project file, such as <c>resource_input</c> or
        /// <c>dataline_output</c>; <see cref="TypeStrings"/> holds the dataline ones. A controller names it in
        /// <see cref="IResourceInteractionService.GetRuntimeValues"/> replies but leaves it empty in
        /// <see cref="IResourceInteractionService.WaitForResourceValueChanges"/> replies, and accepts a write with it
        /// empty.
        /// </summary>
        public string? TypeString  { get; init; }

        /// <summary>
        /// The union value containing the actual data.
        /// </summary>
        public UnionValue Value { get; init; }

        /// <summary>
        /// The resource's id: the decimal of the hexadecimal id the .vis project file gives the element, so
        /// <c>_0x74a15e</c> is 7643486.
        /// </summary>
        public int ResourceID  { get; init; }

        /// <summary>
        /// <c>true</c> for the current (runtime) value the program and the hardware act on now; <c>false</c> for the
        /// initial value, which the controller loads into the resource when it starts unless the resource keeps its
        /// value through a power cut. <see cref="IOpenAPIService"/> ignores it on a write and leaves it <c>false</c>
        /// on a read.
        /// </summary>
        public bool IsValueRuntime  { get; init; }

        /// <summary>
        /// When the SDK created this object, by the local clock, unless its creator passed a time: the controller sends
        /// no time with a value. Not sent to the controller - for diagnostics only.
        /// </summary>
        public DateTimeOffset ValueTime { get; init; }

        public ResourceValue(DateTimeOffset valueTime = default(DateTimeOffset)) {
            this.ValueTime = (valueTime == default(DateTimeOffset) ? DateTimeOffset.Now : valueTime);
        }

        /// <summary>
        /// Helper that creates a boolean input value.
        /// </summary>
        /// <param name="resourceId">Id of the input resource.</param>
        /// <param name="value">Boolean value to set.</param>
        /// <returns>A new ResourceValue configured as a boolean input.</returns>
        public static ResourceValue CreateBooleanRuntimeInput(int resourceId, bool value) {
            return new ResourceValue() {
              TypeString = TypeStrings.DatalineInput,
              ResourceID = resourceId,
              IsValueRuntime = true,
              Value = new ResourceValue.UnionValue() { BoolValue = value, ValueKind = ResourceValue.ValueKind.BOOL }
            };
        }

        /// <summary>
        /// Helper that creates a boolean output value.
        /// </summary>
        /// <param name="resourceId">Id of the output resource.</param>
        /// <param name="value">Boolean value to set.</param>
        /// <returns>A new ResourceValue configured as a boolean output.</returns>
        public static ResourceValue CreateBooleanRuntimeOutput(int resourceId, bool value) {
            return new ResourceValue() {
              TypeString = TypeStrings.DatalineOutput,
              ResourceID = resourceId,
              IsValueRuntime = true,
              Value = new ResourceValue.UnionValue() { BoolValue = value, ValueKind = ResourceValue.ValueKind.BOOL }
            };
        }

        /// <summary>
        /// Helper that creates a boolean resource value with the value opposite of the source.
        /// </summary>
        /// <param name="src">Source resource value to toggle.</param>
        /// <returns>A new ResourceValue with the boolean value toggled.</returns>
        /// <exception cref="ArgumentException">Thrown if source is not a boolean type.</exception>
        public static ResourceValue ToggleBool(ResourceValue src) {
            ArgumentNullException.ThrowIfNull(src);
            if (src.Value.ValueKind != ValueKind.BOOL)
                throw new ArgumentException("Source resource should be of boolean type");

            return new ResourceValue() {
              TypeString = src.TypeString,
              ResourceID = src.ResourceID,
              IsValueRuntime = src.IsValueRuntime,
              Value = new ResourceValue.UnionValue() { BoolValue = !src.Value.BoolValue }
            };
        }

        public override string ToString()
        {
          return $"ResourceValue(TypeString={TypeString}, Value={Value}, ResourceID={ResourceID}, IsValueRuntime={IsValueRuntime}, ValueTime={ValueTime})";
        }
    }

     /// <summary>
     /// One sample from a resource's data log, which the controller keeps on its SD card.
     /// </summary>
    public record LoggedData {
        /// <summary>
        /// The logged value as text, as the controller formats it: <c>true</c> or <c>false</c> for an on/off
        /// resource.
        /// </summary>
        public string? Value  { get; init; }

        /// <summary>
        /// Running number of the sample in the log, counting from 0.
        /// </summary>
        public int Id  { get; init; }

        /// <summary>
        /// When the sample was logged, converted by the SDK from the wire value as milliseconds since 1970 UTC.
        /// </summary>
        /// <remarks>
        /// The unit is the one ServiceView reads the value in; that the epoch is UTC rather than the controller's local
        /// time is not verified against a controller.
        /// </remarks>
        public DateTimeOffset Timestamp  { get; init; }

        public override string ToString()
        {
          return $"LoggedData(Value={Value}, Id={Id}, Timestamp={Timestamp})";
        }
    }

     /// <summary>
     /// An enumeration type the controller knows: one of the project's enum definitions, or a built-in one such as a
     /// dimmer's load mode with the values Auto, RC and RL.
     /// </summary>
    public record EnumDefinition {
         /// <summary>
         /// The definition's id: the decimal of its hexadecimal id in the .vis project file, or the controller's own id
         /// for a built-in one (-2147483647, i.e. 0x80000001, for the dimmer load mode).
         /// </summary>
         public int EnumeratorDefinitionID  { get; init; }

         /// <summary>
         /// Array of possible enum values for this definition.
         /// </summary>
         public EnumValue[] Values { get; init; } = Array.Empty<EnumValue>();

         public override string ToString()
         {
           return $"EnumDefinition(EnumeratorDefinitionID={EnumeratorDefinitionID}, Values=EnumValue[{Values?.Length ?? 0}])";
         }
    }

    /// <summary>
    /// One value of an enumeration, identified by its definition's id and its own id, never by its position in the
    /// definition's list.
    /// </summary>
    public record EnumValue {
        /// <summary>
        /// Id of the enumeration definition this value belongs to, equal to that definition's
        /// <see cref="EnumDefinition.EnumeratorDefinitionID"/>.
        /// </summary>
        public int DefinitionTypeID  { get; init; }

        /// <summary>
        /// The value's id: the decimal of its hexadecimal id in the .vis project file, or the controller's own id for
        /// a built-in value.
        /// </summary>
        public int EnumValueID  { get; init; }

        /// <summary>
        /// The value's display name: the project's, or the controller's own for a built-in value. A controller fills
        /// it in the values it reports.
        /// </summary>
        public string? EnumName  { get; init; }

        public override string ToString()
        {
          return $"EnumValue(DefinitionTypeID={DefinitionTypeID}, EnumValueID={EnumValueID}, EnumName={EnumName})";
        }
    }

    /// <summary>
    /// One scene membership, linking a function block's scene output to one product output: the member's resource id,
    /// and display texts for each side.
    /// </summary>
    public record SceneResourceIdAndLocation
    {
        /// <summary>
        /// Display text for the membership on the function block's side, suited as the label of the block's scene
        /// link.
        /// </summary>
        public string? ScenePositionSeenFromFunctionBlock  { get; init; }

        /// <summary>
        /// The member resource's id, not the scene's: the one that carries the member's scene value (SceneDimmer,
        /// SceneRelay or SceneShutter). In the answer of
        /// <see cref="IResourceInteractionService.GetScenePositionsForSceneValueResource"/> it is probably the id asked
        /// for.
        /// </summary>
        public int SceneResourceId  { get; init; }

        /// <summary>
        /// Display text for the membership on the product's side, suited as the label of the product's scene member.
        /// </summary>
        public string? ScenePositionSeenFromProduct  { get; init; }

        public override string ToString()
        {
          return $"SceneResourceIdAndLocation(ScenePositionSeenFromFunctionBlock={ScenePositionSeenFromFunctionBlock}, SceneResourceId={SceneResourceId}, ScenePositionSeenFromProduct={ScenePositionSeenFromProduct})";
        }
    }
}