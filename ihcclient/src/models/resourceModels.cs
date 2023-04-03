using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;

namespace Ihc {
    /**
    * Valid TypeString values.
    */
    public class TypeStrings {
        public static readonly string DatalineInput = "dataline_input";
        public static readonly string DatalineOutput = "dataline_output";
    };

     /**
     * High level model of a IHC WS Dataline resource without soap distractions.
     */
    public record DatalineResource {
        public int DatalineNumber { get; set; }
        public int ResourceID { get; set; }
    }

     /**
     * High level model of a IHC WS resource value without soap distractions.
     * 
     * TODO: Support missing types: PhoneNumber, SceneDimmer, SceneRelay, WSSceneShutter
     */
    public record ResourceValue {
        public enum ValueKind { 
            BOOL, DATE, INT, DOUBLE, ENUM, 
            TIME, TIMER, WEEKDAY 
        };

        public struct UnionValue {
            public ValueKind ValueKind { get; set; }
  
            public bool? BoolValue { get; set; }

            public DateTimeOffset? DateTime { get; set; }

            public int? IntValue { get; set; }
            
            public double? DoubleValue  { get; set; }
            
            public DateTimeOffset? DateValue { get; set; }

            public TimeSpan? TimeValue { get; set; }

            public long? TimerValue { get; set; }

            public int? WeekdayValue { get; set; }

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
                    buf.AppendFormat(", TimerValue={0}", WeekdayValue.Value);
                } 
                buf.Append("}");

                return buf.ToString(); 
            }
        };

        /**
        * See TypeStrings constants for valid values.
        */
        public string TypeString  { get; set; } 

        public UnionValue Value { get; set; }
        
        public int ResourceID  { get; set; }
        
        public bool IsValueRuntime  { get; set; }

        /**
         * Approximately the time this value was created/changed (in most cases
         * just approximted by the the time this object was created).
         *
         * Nb. Not used by IHC - For internal diagnostics only.
         */
        public DateTimeOffset ValueTime { get; init; }

        public ResourceValue(DateTimeOffset valueTime = default(DateTimeOffset)) {
            this.ValueTime = (valueTime == default(DateTimeOffset) ? DateTimeOffset.Now : valueTime);
        }

        /**
        * Helper that creates an boolean input value.
        */
        public static ResourceValue CreateBooleanRuntimeInput(int resourceId, bool value) {
            return new ResourceValue() {
              TypeString = TypeStrings.DatalineInput,
              ResourceID = resourceId,
              IsValueRuntime = true,
              Value = new ResourceValue.UnionValue() { BoolValue = value }
            };
        }

        /**
        * Helper that creates an boolean output value.
        */
        public static ResourceValue CreateBooleanRuntimeOutput(int resourceId, bool value) {
            return new ResourceValue() {
              TypeString = TypeStrings.DatalineOutput,
              ResourceID = resourceId,
              IsValueRuntime = true,
              Value = new ResourceValue.UnionValue() { BoolValue = value }
            };
        }

        /**
        * Helper that crete a boolean resource value with value is opposite of source.
        */
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
    }

     /**
     * High level model of a IHC WS Logged data without soap distractions.
     */
    public record LoggedData {
        public string Value  { get; set; }
        public int Id  { get; init; }
        public DateTimeOffset Timestamp  { get; init; }
    }

     /**
     * High level model of a IHC WS Enum definition without soap distractions.
     */
    public record EnumDefinition {
         public int EnumeratorDefinitionID  { get; init; }
         public EnumValue[] Values { get; init; }
    }

    /**
     * High level model of a IHC WS Enum value without soap distractions.
     */
    public record EnumValue {
        public int DefinitionTypeID  { get; init; }
        public int EnumValueID  { get; init; }
        public string EnumName  { get; init; }
    }

    public record SceneResourceIdAndLocation
    {
        public string ScenePositionSeenFromFunctionBlock  { get; init; }
        
        public int SceneResourceId  { get; init; }
        
        public string ScenePositionSeenFromProduct  { get; init; }
    }
}