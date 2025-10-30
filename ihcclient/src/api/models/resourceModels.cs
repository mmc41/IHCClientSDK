using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;

namespace Ihc {
    /// <summary>
    /// Valid TypeString values for IHC resource types.
    /// </summary>
    public class TypeStrings {
        /// <summary>
        /// Type string for dataline input resources.
        /// </summary>
        public static readonly string DatalineInput = "dataline_input";

        /// <summary>
        /// Type string for dataline output resources.
        /// </summary>
        public static readonly string DatalineOutput = "dataline_output";
    };

     /// <summary>
     /// High level model of an IHC WS Dataline resource without soap distractions.
     /// </summary>
    public record DatalineResource {
        /// <summary>
        /// The dataline number in the IHC system.
        /// </summary>
        public int DatalineNumber { get; init; }

        /// <summary>
        /// Unique resource identifier for this dataline.
        /// </summary>
        public int ResourceID { get; init; }

        public override string ToString()
        {
          return $"DatalineResource(DatalineNumber={DatalineNumber}, ResourceID={ResourceID})";
        }
    }

     /// <summary>
     /// High level model of an IHC WS resource value without soap distractions.
     /// TODO: Support missing types: PhoneNumber, SceneDimmer, SceneRelay, WSSceneShutter
     /// </summary>
    public record ResourceValue {
        /// <summary>
        /// Enumeration of supported value kinds for resource values.
        /// </summary>
        public enum ValueKind {
            /// <summary>Boolean value type.</summary>
            BOOL,
            /// <summary>Date value type.</summary>
            DATE,
            /// <summary>Integer value type.</summary>
            INT,
            /// <summary>Double precision floating point value type.</summary>
            DOUBLE,
            /// <summary>Enumeration value type.</summary>
            ENUM,
            /// <summary>Time value type.</summary>
            TIME,
            /// <summary>Timer value type.</summary>
            TIMER,
            /// <summary>Weekday value type.</summary>
            WEEKDAY
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
            /// Boolean value (when ValueKind is BOOL).
            /// </summary>
            public bool? BoolValue { get; set; }

            /// <summary>
            /// DateTime value (when ValueKind is DATE).
            /// </summary>
            public DateTimeOffset? DateTime { get; set; }

            /// <summary>
            /// Integer value (when ValueKind is INT).
            /// </summary>
            public int? IntValue { get; set; }

            /// <summary>
            /// Double precision value (when ValueKind is DOUBLE).
            /// </summary>
            public double? DoubleValue  { get; set; }

            /// <summary>
            /// Date value (when ValueKind is DATE).
            /// </summary>
            public DateTimeOffset? DateValue { get; set; }

            /// <summary>
            /// Time span value (when ValueKind is TIME).
            /// </summary>
            public TimeSpan? TimeValue { get; set; }

            /// <summary>
            /// Timer value in milliseconds (when ValueKind is TIMER).
            /// </summary>
            public long? TimerValue { get; set; }

            /// <summary>
            /// Weekday value as integer (when ValueKind is WEEKDAY).
            /// </summary>
            public int? WeekdayValue { get; set; }

            /// <summary>
            /// Enum value (when ValueKind is ENUM).
            /// </summary>
            public EnumValue EnumValue { get; set; }

            public override String ToString() {
                StringBuilder buf = new StringBuilder();

                buf.Append("UnionValue {");
                buf.AppendFormat("ValueKind={0}", ValueKind);

                // Raw print out of any values, regardless of kind
                // so even invalid combinations show up:

                if (BoolValue.HasValue) {
                    buf.AppendFormat(", BoolValue={0}", BoolValue.Value);
                }
                if (IntValue.HasValue) {
                    buf.AppendFormat(", IntValue={0}", IntValue.Value);
                }
                if (DoubleValue.HasValue) {
                    buf.AppendFormat(", DoubleValue={0}", DoubleValue.Value);
                }
                if (DateValue.HasValue) {
                    buf.AppendFormat(", DateValue={0}", DateValue.Value);
                } 
                if (TimeValue.HasValue) {
                    buf.AppendFormat(", TimeValue={0}", TimeValue.Value);
                } 
                if (TimerValue.HasValue) {
                    buf.AppendFormat(", TimerValue={0}", TimerValue.Value);
                }
                if (WeekdayValue.HasValue) {
                    buf.AppendFormat(", WeekdayValue={0}", WeekdayValue.Value);
                } 
                buf.Append("}");

                return buf.ToString(); 
            }
        };

        /// <summary>
        /// Type string identifying the resource type.
        /// See TypeStrings constants for valid values.
        /// </summary>
        public string TypeString  { get; init; }

        /// <summary>
        /// The union value containing the actual data.
        /// </summary>
        public UnionValue Value { get; init; }

        /// <summary>
        /// Unique resource identifier in the IHC system.
        /// </summary>
        public int ResourceID  { get; init; }

        /// <summary>
        /// Indicates whether this is a runtime value (vs. configuration value).
        /// </summary>
        public bool IsValueRuntime  { get; init; }

        /// <summary>
        /// Approximately the time this value was created/changed (in most cases just approximated by the time this object was created).
        /// Note: Not used by IHC - For internal diagnostics only.
        /// </summary>
        public DateTimeOffset ValueTime { get; init; }

        public ResourceValue(DateTimeOffset valueTime = default(DateTimeOffset)) {
            this.ValueTime = (valueTime == default(DateTimeOffset) ? DateTimeOffset.Now : valueTime);
        }

        /// <summary>
        /// Helper that creates a boolean input value.
        /// </summary>
        /// <param name="resourceId">Resource ID for the input.</param>
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
        /// <param name="resourceId">Resource ID for the output.</param>
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
        public static ResourceValue ToogleBool(ResourceValue src) {
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
     /// High level model of IHC WS logged data without soap distractions.
     /// </summary>
    public record LoggedData {
        /// <summary>
        /// The logged value as a string.
        /// </summary>
        public string Value  { get; init; }

        /// <summary>
        /// Unique identifier for this log entry.
        /// </summary>
        public int Id  { get; init; }

        /// <summary>
        /// Timestamp when this data was logged.
        /// </summary>
        public DateTimeOffset Timestamp  { get; init; }

        public override string ToString()
        {
          return $"LoggedData(Value={Value}, Id={Id}, Timestamp={Timestamp})";
        }
    }

     /// <summary>
     /// High level model of an IHC WS Enum definition without soap distractions.
     /// </summary>
    public record EnumDefinition {
         /// <summary>
         /// Unique identifier for this enumerator definition.
         /// </summary>
         public int EnumeratorDefinitionID  { get; init; }

         /// <summary>
         /// Array of possible enum values for this definition.
         /// </summary>
         public EnumValue[] Values { get; init; }

         public override string ToString()
         {
           return $"EnumDefinition(EnumeratorDefinitionID={EnumeratorDefinitionID}, Values=EnumValue[{Values?.Length ?? 0}])";
         }
    }

    /// <summary>
    /// High level model of an IHC WS Enum value without soap distractions.
    /// </summary>
    public record EnumValue {
        /// <summary>
        /// Type identifier for the enum definition this value belongs to.
        /// </summary>
        public int DefinitionTypeID  { get; init; }

        /// <summary>
        /// Unique identifier for this enum value.
        /// </summary>
        public int EnumValueID  { get; init; }

        /// <summary>
        /// Human-readable name of this enum value.
        /// </summary>
        public string EnumName  { get; init; }

        public override string ToString()
        {
          return $"EnumValue(DefinitionTypeID={DefinitionTypeID}, EnumValueID={EnumValueID}, EnumName={EnumName})";
        }
    }

    /// <summary>
    /// High level model of a scene resource ID and its location without soap distractions.
    /// </summary>
    public record SceneResourceIdAndLocation
    {
        /// <summary>
        /// Scene position as seen from the function block perspective.
        /// </summary>
        public string ScenePositionSeenFromFunctionBlock  { get; init; }

        /// <summary>
        /// Unique resource ID for the scene.
        /// </summary>
        public int SceneResourceId  { get; init; }

        /// <summary>
        /// Scene position as seen from the product perspective.
        /// </summary>
        public string ScenePositionSeenFromProduct  { get; init; }

        public override string ToString()
        {
          return $"SceneResourceIdAndLocation(ScenePositionSeenFromFunctionBlock={ScenePositionSeenFromFunctionBlock}, SceneResourceId={SceneResourceId}, ScenePositionSeenFromProduct={ScenePositionSeenFromProduct})";
        }
    }
}