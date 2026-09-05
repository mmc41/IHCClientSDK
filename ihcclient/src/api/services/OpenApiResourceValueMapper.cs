using System;
using System.Diagnostics;
using Ihc.Soap.Openapi;
using static Ihc.ResourceValuePayload;

namespace Ihc
{
    /// <summary>
    /// Pure translation between the high-level <see cref="ResourceValue"/> domain model and the generated
    /// OpenAPI <see cref="WSResourceValue"/> wire types. Extracted from <see cref="OpenAPIService"/> so the
    /// value mapping can be unit-tested in isolation, the way its twin
    /// <see cref="ResourceValueEnvelopeMapper"/> already was; it holds no state and performs no I/O.
    ///
    /// That twin is the same mapping written independently over the resourceinteraction namespace, which the
    /// vendor's WSDL shapes alike. They are each other's second opinion, and
    /// <c>OpenApiResourceValueMappingTests</c> holds the two together - a differential no single-sided
    /// fixture could give, since neither side shares code with the other.
    /// </summary>
    internal static class OpenApiResourceValueMapper
    {
        /// <summary>Wire-to-model for a single OpenAPI resource value.</summary>
        internal static ResourceValue? ToDomain(WSResourceValue? v)
        {
            if (v == null)
                return null;

            // Default to NONE so a wire value the mapping does not recognize is classified as "no
            // readable value" rather than silently mislabeled as the enum-default BOOL - which a caller
            // cannot tell from a genuine reading of a switch that is off. Confirmed live through the
            // twin ResourceValueEnvelopeMapper.ToDomain, which carries the same default: scene
            // resources (type 'resource_scene') return no readable value.
            var value = new ResourceValue.UnionValue() { ValueKind = ResourceValue.ValueKind.NONE };

            if (v is WSBooleanValue boolVal)
            {
                value.BoolValue = boolVal.value;
                value.ValueKind = ResourceValue.ValueKind.BOOL;
            }
            else if (v is WSIntegerValue intVal)
            {
                value.IntValue = intVal.integer;
                value.ValueKind = ResourceValue.ValueKind.INT;
            }
            else if (v is WSFloatingPointValue floatVal)
            {
                value.DoubleValue = floatVal.floatingPointValue;
                value.ValueKind = ResourceValue.ValueKind.DOUBLE;
            }
            else if (v is WSEnumValue enumVal)
            {
                value.EnumValue = new EnumValue()
                {
                    DefinitionTypeID = enumVal.definitionTypeID,
                    EnumValueID = enumVal.enumValueID,
                    EnumName = enumVal.enumName
                };
                value.ValueKind = ResourceValue.ValueKind.ENUM;
            }
            else if (v is WSDateValue dateVal)
            {
                value.DateValue = new DateTimeOffset(dateVal.year, dateVal.month, dateVal.day, 0, 0, 0, DateHelper.GetWSTimeOffset());
                value.ValueKind = ResourceValue.ValueKind.DATE;
            }
            else if (v is WSTimeValue timeVal)
            {
                value.TimeValue = new TimeSpan(timeVal.hours, timeVal.minutes, timeVal.seconds);
                value.ValueKind = ResourceValue.ValueKind.TIME;
            }
            else if (v is WSTimerValue timerVal)
            {
                value.TimerValue = timerVal.milliseconds;
                value.ValueKind = ResourceValue.ValueKind.TIMER;
            }
            else if (v is WSWeekdayValue weekdayVal)
            {
                value.WeekdayValue = weekdayVal.weekdayNumber;
                value.ValueKind = ResourceValue.ValueKind.WEEKDAY;
            }
            else if (v is WSPhoneNumberValue phoneVal)
            {
                value.PhoneNumberValue = phoneVal.number;
                value.ValueKind = ResourceValue.ValueKind.PhoneNumber;
            }
            else if (v is WSSceneDimmerValue dimmerVal)
            {
                value.DimmerPercentage = dimmerVal.dimmerPercentage;
                value.DimmerDelayTime = dimmerVal.delayTime;
                value.DimmerRampTime = dimmerVal.rampTime;
                value.ValueKind = ResourceValue.ValueKind.SceneDimmer;
            }
            else if (v is WSSceneRelayValue relayVal)
            {
                value.RelayDelayTime = relayVal.delayTime;
                value.RelayValue = relayVal.relayValue;
                value.ValueKind = ResourceValue.ValueKind.SceneRelay;
            }
            else if (v is WSSceneShutterSimpleValue shutterVal)
            {
                value.ShutterPositionIsUp = shutterVal.shutterPositionIsUp;
                value.ShutterDelayTime = shutterVal.delayTime;
                value.ValueKind = ResourceValue.ValueKind.SceneShutter;
            }

            if (value.ValueKind == ResourceValue.ValueKind.NONE)
            {
                // NONE is the deliberate classification for a value this mapping does not recognise, and it
                // stays. What it cannot say is WHICH type went unrecognised - this side carries no TypeString -
                // so a caller seeing NONE has no way to tell a scene from a wire kind the SDK has not learned.
                Activity.Current.AddWarning(
                    $"An OpenAPI resource value of wire type '{v.GetType().Name}' was not recognised; classified as NONE.",
                    ("type", "UnrecognizedWireValueKind"),
                    ("wireType", v.GetType().Name));
            }

            return new ResourceValue() { Value = value };
        }

        // Each case reads the union member its ValueKind selects, unwrapped through the shared guard so
        // a value whose kind is set but whose payload was left null is NAMED rather than tripping a
        // Nullable<T> cast into an opaque "Nullable object must have a value".
        // ResourceValueEnvelopeMapper.ToWire is the twin of this method over the resourceinteraction
        // namespace and refuses the same values identically; OpenApiResourceValueMappingTests holds the
        // two together.
        internal static WSResourceValue ToWire(ResourceValue v)
        {
            var kind = v.Value.ValueKind;

            switch (kind)
            {
                case ResourceValue.ValueKind.BOOL:
                    return new WSBooleanValue() { value = Required(v.Value.BoolValue, kind, nameof(v.Value.BoolValue)) };
                case ResourceValue.ValueKind.INT:
                    return new WSIntegerValue() { integer = Required(v.Value.IntValue, kind, nameof(v.Value.IntValue)) };
                case ResourceValue.ValueKind.DOUBLE:
                    return new WSFloatingPointValue() { floatingPointValue = Required(v.Value.DoubleValue, kind, nameof(v.Value.DoubleValue)) };
                case ResourceValue.ValueKind.ENUM:
                    var enumValue = Required(v.Value.EnumValue, kind, nameof(v.Value.EnumValue));
                    return new WSEnumValue()
                    {
                        definitionTypeID = enumValue.DefinitionTypeID,
                        enumValueID = enumValue.EnumValueID,
                        enumName = enumValue.EnumName
                    };
                case ResourceValue.ValueKind.DATE:
                    var date = Required(v.Value.DateValue, kind, nameof(v.Value.DateValue));
                    return new WSDateValue()
                    {
                        year = (short)date.Year,
                        month = (sbyte)date.Month,
                        day = (sbyte)date.Day
                    };
                case ResourceValue.ValueKind.TIME:
                    var time = Representable(Required(v.Value.TimeValue, kind, nameof(v.Value.TimeValue)), kind, nameof(v.Value.TimeValue));
                    return new WSTimeValue()
                    {
                        hours = time.Hours,
                        minutes = time.Minutes,
                        seconds = time.Seconds
                    };
                case ResourceValue.ValueKind.TIMER:
                    return new WSTimerValue() { milliseconds = Required(v.Value.TimerValue, kind, nameof(v.Value.TimerValue)) };
                case ResourceValue.ValueKind.WEEKDAY:
                    return new WSWeekdayValue() { weekdayNumber = Required(v.Value.WeekdayValue, kind, nameof(v.Value.WeekdayValue)) };
                case ResourceValue.ValueKind.PhoneNumber:
                    return new WSPhoneNumberValue() { number = Required(v.Value.PhoneNumberValue, kind, nameof(v.Value.PhoneNumberValue)) };
                case ResourceValue.ValueKind.SceneDimmer:
                    return new WSSceneDimmerValue() { dimmerPercentage = Required(v.Value.DimmerPercentage, kind, nameof(v.Value.DimmerPercentage)), delayTime = Required(v.Value.DimmerDelayTime, kind, nameof(v.Value.DimmerDelayTime)), rampTime = Required(v.Value.DimmerRampTime, kind, nameof(v.Value.DimmerRampTime)) };
                case ResourceValue.ValueKind.SceneRelay:
                    return new WSSceneRelayValue() { delayTime = Required(v.Value.RelayDelayTime, kind, nameof(v.Value.RelayDelayTime)), relayValue = Required(v.Value.RelayValue, kind, nameof(v.Value.RelayValue)) };
                case ResourceValue.ValueKind.SceneShutter:
                    return new WSSceneShutterSimpleValue() { shutterPositionIsUp = Required(v.Value.ShutterPositionIsUp, kind, nameof(v.Value.ShutterPositionIsUp)), delayTime = Required(v.Value.ShutterDelayTime, kind, nameof(v.Value.ShutterDelayTime)) };
                case ResourceValue.ValueKind.NONE:
                    throw new ArgumentException($"Cannot write a ResourceValue with ValueKind.NONE — it represents a resource with no writable value (e.g. a scene). ResourceID {v.ResourceID}.");
                default:
                    throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "Support for value kind " + kind + " not (yet) implemented.");
            }
        }
    }
}
