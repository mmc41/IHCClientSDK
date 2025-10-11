using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Resourceinteraction;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC ResourceInteractionService without any of the soap distractions.
    *
    * Status: 100% API coverage but not fully tested or documented.
    */
    public interface IResourceInteractionService : IIHCService
    {
        /**
        * Disable initial value notifications for specified resource IDs.
        */
        public Task<bool> DisableInitialValueNotifactions(int[] resourceIds);

        /**
        * Disable runtime value notifications for specified resource IDs.
        */
        public Task<bool> DisableRuntimeValueNotifactions(int[] resourceIds);

        /**
        * Enable initial value notifications for specified resource IDs and return current values.
        */
        public Task<ResourceValue[]> EnableInitialValueNotifications(int[] resourceIds);

        /**
        * Enable runtime value notifications for specified resource IDs. Must be called before WaitForResourceValueChanges.
        */
        public Task<ResourceValue[]> EnableRuntimeValueNotifications(int[] resourceIds);

        /**
        * Get all dataline input resource definitions.
        */
        public Task<DatalineResource[]> GetAllDatalineInputs();

        /**
        * Get all dataline output resource definitions.
        */
        public Task<DatalineResource[]> GetAllDatalineOutputs();

        /**
        * Get all enumerator definitions from the IHC project.
        */
        public Task<EnumDefinition[]> GetEnumeratorDefinitions();

        /**
        * Get all extra dataline input resource definitions.
        */
        public Task<DatalineResource[]> GetExtraDatalineInputs();

        /**
        * Get all extra dataline output resource definitions.
        */
        public Task<DatalineResource[]> GetExtraDatalineOutputs();

        /**
        * Get initial value for a single resource ID.
        */
        public Task<ResourceValue> GetInitialValue(int? initialValue);

        /**
        * Get initial values for multiple resource IDs.
        */
        public Task<ResourceValue[]> GetInitialValues(int[] initialValues);

        /**
        * Get logged historical data for a resource ID.
        */
        public Task<LoggedData[]> GetLoggedData(int loggedData1);

        /**
        * Get the type string of a resource. Refer to TypeStrings constants for valid return values.
        */
        public Task<string> GetResourceType(int resourceID);

        /**
        * Get current runtime value of an input/output resource.
        */
        public Task<ResourceValue> GetRuntimeValue(int resourceID);

        /**
        * Get current runtime values for multiple resource IDs.
        */
        public Task<ResourceValue[]> GetRuntimeValues(int[] resourceIDs);

        /**
        * Set value for a single resource.
        */
        public Task<bool> SetResourceValue(ResourceValue v);

        /**
        * Set values for multiple resources.
        */
        public Task<bool> SetResourceValues(ResourceValue[] values);

        /**
        * Get scene resource IDs and positions for a scene group.
        */
        public Task<SceneResourceIdAndLocation[]> GetSceneGroupResourceIdAndPositions(int sceneGroupResourceIdAndPositions);

        /**
        * Get scene positions for a scene value resource.
        */
        public Task<SceneResourceIdAndLocation> GetScenePositionsForSceneValueResource(int scenePositionsForSceneValueResource);

        /// <summary>
        /// Long-poll for resource value changes. Resources must be enabled first using EnableRuntimeValueNotifications.
        /// Returns immediately with initial values on first call, then respects timeout. Timeout should be less than 20 seconds.
        /// TIP: Consider using GetResourceValueChanges instead.
        /// </summary>
        /// <param name="timeout_seconds">Timeout in seconds (default: 15)</param>
        public Task<ResourceValue[]> WaitForResourceValueChanges(int timeout_seconds = 15);

        /// <summary>
        /// Returns an async stream of value changes for specified resources.
        /// Automatically handles EnableRuntimeValueNotifications + WaitForResourceValueChanges loop.
        /// Timeout should be lower than system timeout (less than 20 seconds recommended).
        /// </summary>
        /// <param name="resourceIds">Array of resource IDs to monitor</param>
        /// <param name="cancellationToken">Cancellation token to stop monitoring</param>
        /// <param name="timeout_between_waits_in_seconds">Timeout between waits in seconds (default: 15)</param>
        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(int[] resourceIds, CancellationToken cancellationToken = default, int timeout_between_waits_in_seconds = 15);
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC ResourceInteractionService without exposing any of the soap distractions.
    /// </summary>
    public class ResourceInteractionService : ServiceBase, IResourceInteractionService
    {
        private readonly IAuthenticationService authService;

        /**
         * This internal class implements the raw IHC soap service interface and provides the basis
         * for the higher level public service methods below it.
         */
        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Resourceinteraction.ResourceInteractionService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, bool logSensitiveData, bool asyncContinueOnCapturedContext) : base(logger, cookieHandler, endpoint, "ResourceInteractionService", logSensitiveData, asyncContinueOnCapturedContext) { }

            public Task<outputMessageName7> disableInitialValueNotifactionsAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("disableInitialValueNotifactions", request);
            }

            public Task<outputMessageName5> disableRuntimeValueNotifactionsAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("disableRuntimeValueNotifactions", request);
            }

            public Task<outputMessageName6> enableInitialValueNotificationsAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("enableInitialValueNotifications", request);
            }

            public Task<outputMessageName4> enableRuntimeValueNotificationsAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("enableRuntimeValueNotifications", request);
            }

            public Task<outputMessageName12> getAllDatalineInputsAsync(inputMessageName12 request)
            {
                return soapPost<outputMessageName12, inputMessageName12>("getAllDatalineInputs", request);
            }

            public Task<outputMessageName13> getAllDatalineOutputsAsync(inputMessageName13 request)
            {
                return soapPost<outputMessageName13, inputMessageName13>("getAllDatalineOutputs", request);
            }

            public Task<outputMessageName9> getEnumeratorDefinitionsAsync(inputMessageName9 request)
            {
                return soapPost<outputMessageName9, inputMessageName9>("getEnumeratorDefinitions", request);
            }

            public Task<outputMessageName10> getExtraDatalineInputsAsync(inputMessageName10 request)
            {
                return soapPost<outputMessageName10, inputMessageName10>("getExtraDatalineInputs", request);
            }

            public Task<outputMessageName11> getExtraDatalineOutputsAsync(inputMessageName11 request)
            {
                return soapPost<outputMessageName11, inputMessageName11>("getExtraDatalineOutputs", request);
            }

            public Task<outputMessageName15> getInitialValueAsync(inputMessageName15 request)
            {
                return soapPost<outputMessageName15, inputMessageName15>("getInitialValue", request);
            }

            public Task<outputMessageName17> getInitialValuesAsync(inputMessageName17 request)
            {
                return soapPost<outputMessageName17, inputMessageName17>("getInitialValues", request);
            }

            public Task<outputMessageName20> getLoggedDataAsync(inputMessageName20 request)
            {
                return soapPost<outputMessageName20, inputMessageName20>("getLoggedData", request);
            }

            public Task<outputMessageName19> getResourceTypeAsync(inputMessageName19 request)
            {
                return soapPost<outputMessageName19, inputMessageName19>("getResourceType", request);
            }

            public Task<outputMessageName14> getRuntimeValueAsync(inputMessageName14 request)
            {
                return soapPost<outputMessageName14, inputMessageName14>("getRuntimeValue", request);
            }

            public Task<outputMessageName16> getRuntimeValuesAsync(inputMessageName16 request)
            {
                return soapPost<outputMessageName16, inputMessageName16>("getRuntimeValues", request);
            }

            public Task<outputMessageName1> getSceneGroupResourceIdAndPositionsAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("getSceneGroupResourceIdAndPositions", request);
            }

            public Task<outputMessageName2> getScenePositionsForSceneValueResourceAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("getScenePositionsForSceneValueResource", request);
            }

            public Task<outputMessageName18> setResourceValueAsync(inputMessageName18 request)
            {
                return soapPost<outputMessageName18, inputMessageName18>("setResourceValue", request);
            }

            public Task<outputMessageName3> setResourceValuesAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("setResourceValues", request);
            }

            public Task<outputMessageName8> waitForResourceValueChangesAsync(inputMessageName8 request)
            {
                return soapPost<outputMessageName8, inputMessageName8>("waitForResourceValueChanges", request);
            }
        }

        private readonly SoapImpl impl;

        private ResourceValue mapResourceValueEnvelope(WSResourceValueEnvelope v)
        {
            if (v == null)
                return null;

            var value = new ResourceValue.UnionValue() { };

            if (v.value is WSBooleanValue)
            {
                value.BoolValue = (v.value as WSBooleanValue).value;
                value.ValueKind = ResourceValue.ValueKind.BOOL;
            }

            if (v.value is WSDateValue)
            {
                value.DateValue = mapDate(v.value as WSDateValue);
                value.ValueKind = ResourceValue.ValueKind.DATE;
            }

            if (v.value is WSIntegerValue)
            {
                value.IntValue = (v.value as WSIntegerValue).integer;
                // TODO: What about min, max values ?
                value.ValueKind = ResourceValue.ValueKind.INT;
            }

            if (v.value is WSFloatingPointValue)
            {
                value.DoubleValue = (v.value as WSFloatingPointValue).floatingPointValue;
                // TODO: What about min, max values?
                value.ValueKind = ResourceValue.ValueKind.DOUBLE;
            }

            if (v.value is WSEnumValue)
            {
                value.EnumValue = mapEnumValue(v.value as WSEnumValue);
                value.ValueKind = ResourceValue.ValueKind.ENUM;
            }

            if (v.value is WSTimeValue)
            {
                value.TimeValue = mapTime(v.value as WSTimeValue);
                value.ValueKind = ResourceValue.ValueKind.TIME;
            }

            if (v.value is WSTimerValue)
            {
                value.TimerValue = mapTimer(v.value as WSTimerValue);
                value.ValueKind = ResourceValue.ValueKind.TIMER;
            }

            if (v.value is WSWeekdayValue)
            {
                value.WeekdayValue = mapWeekday(v.value as WSWeekdayValue);
                value.ValueKind = ResourceValue.ValueKind.WEEKDAY;
            }

            return new ResourceValue() { ResourceID = v.resourceID, IsValueRuntime = v.isValueRuntime, TypeString = v.typeString, Value = value };
        }

        private WSResourceValueEnvelope mapResourceValueEnvelope(ResourceValue v)
        {
            if (v == null)
                return null;

            WSResourceValue val;

            switch (v.Value.ValueKind)
            {
                case ResourceValue.ValueKind.BOOL: val = new WSBooleanValue() { value = (bool)v.Value.BoolValue }; break;
                case ResourceValue.ValueKind.DATE: val = mapDate((DateTimeOffset)v.Value.DateValue); break;
                case ResourceValue.ValueKind.INT: val = new WSIntegerValue() { integer = (int)v.Value.IntValue }; break;
                case ResourceValue.ValueKind.DOUBLE: val = new WSFloatingPointValue() { floatingPointValue = (double)v.Value.DoubleValue }; break;
                case ResourceValue.ValueKind.ENUM: val = mapEnumValue(v.Value.EnumValue); break;
                case ResourceValue.ValueKind.TIME: val = mapTime((TimeSpan)v.Value.TimeValue); break;
                case ResourceValue.ValueKind.TIMER: val = mapTimer((long)v.Value.TimerValue); break;
                case ResourceValue.ValueKind.WEEKDAY: val = mapWeekday((int)v.Value.WeekdayValue); break;
                default: throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "Support for value kind " + v.Value.ValueKind + " not (yet) implemented.");
            }

            return new WSResourceValueEnvelope()
            {
                resourceID = v.ResourceID,
                isValueRuntime = v.IsValueRuntime,
                typeString = v.TypeString,
                value = val
            };
        }

        private DatalineResource mapDatalineResource(WSDatalineResource r)
        {
            if (r == null)
                return null;

            return new DatalineResource() { ResourceID = r.resourceID, DatalineNumber = r.datalineNumber };
        }

        private EnumDefinition mapMapEnumeratorDefinitions(WSEnumDefinition e)
        {
            if (e == null)
                return null;

            return new EnumDefinition()
            {
                EnumeratorDefinitionID = e.enumeratorDefinitionID,
                Values = e.enumeratorValues?.Select((v) => mapEnumValue(v)).ToArray() ?? Array.Empty<EnumValue>()
            };
        }

        private EnumValue mapEnumValue(WSEnumValue v)
        {
            if (v == null)
                return null;

            return new EnumValue() { DefinitionTypeID = v.definitionTypeID, EnumValueID = v.enumValueID, EnumName = v.enumName };
        }

        private WSEnumValue mapEnumValue(EnumValue v)
        {
            if (v == null)
                return null;

            return new WSEnumValue() { definitionTypeID = v.DefinitionTypeID, enumValueID = v.EnumValueID, enumName = v.EnumName };
        }

        private DateTimeOffset mapDate(WSDateValue v)
        {
            if (v == null)
                return DateTimeOffset.MinValue;

            return new DateTimeOffset(v.year, v.month, v.day, 0, 0, 0, DateHelper.GetWSTimeOffset());
        }

        private WSDateValue mapDate(DateTimeOffset v)
        {
            return new WSDateValue() { year = (short)v.Year, month = (sbyte)v.Month, day = (sbyte)v.Day };
        }

        private WSTimeValue mapTime(TimeSpan v)
        {
            return new WSTimeValue() { hours = v.Hours, minutes = v.Minutes, seconds = v.Seconds };
        }

        private TimeSpan mapTime(WSTimeValue v)
        {
            if (v == null)
                return TimeSpan.Zero;

            return new TimeSpan(v.hours, v.minutes, v.seconds);
        }

        private long mapTimer(WSTimerValue v)
        {
            if (v == null)
                return 0;

            return v.milliseconds;
        }

        private WSTimerValue mapTimer(long v)
        {
            return new WSTimerValue() { milliseconds = v };
        }

        private int mapWeekday(WSWeekdayValue v)
        {
            if (v == null)
                return 0;

            return v.weekdayNumber;
        }

        private WSWeekdayValue mapWeekday(int v)
        {
            return new WSWeekdayValue() { weekdayNumber = v };
        }
        
        public SceneResourceIdAndLocation mapSceneResourceIdAndLocation(Ihc.Soap.Resourceinteraction.WSSceneResourceIdAndLocationURLs arg) {
            if (arg == null)
                return null;

            return new SceneResourceIdAndLocation() {
                SceneResourceId = arg.sceneResourceId,
                ScenePositionSeenFromProduct = arg.scenePositionSeenFromProduct,
                ScenePositionSeenFromFunctionBlock = arg.scenePositionSeenFromFunctionBlock
            };
        }

        /**
        * Create an ResourceInteractionService instance for access to the IHC API related to resources.
        * <param name="authService">AuthenticationService instance</param>
        * <param name="logSensitiveData">If true, log sensitive data. If false (default), redact sensitive values in logs.</param>
        * <param name="asyncContinueOnCapturedContext">If true, continue on captured context after await. If false (default), use ConfigureAwait(false) for better library performance.</param>
        */
        public ResourceInteractionService(IAuthenticationService authService, bool logSensitiveData = false, bool asyncContinueOnCapturedContext = false)
            : base(authService.Logger, logSensitiveData, asyncContinueOnCapturedContext)
        {
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint, logSensitiveData, asyncContinueOnCapturedContext);
        }

        public async Task<bool> DisableInitialValueNotifactions(int[] resourceIds)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(resourceIds), resourceIds));

            var result = await this.impl.disableInitialValueNotifactionsAsync(new inputMessageName7() { disableInitialValueNotifactions1 = resourceIds }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = result.disableInitialValueNotifactions2.HasValue ? result.disableInitialValueNotifactions2.Value : false;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<bool> DisableRuntimeValueNotifactions(int[] resourceIds)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(resourceIds), resourceIds));

            var result = await this.impl.disableRuntimeValueNotifactionsAsync(new inputMessageName5() { disableRuntimeValueNotifactions1 = resourceIds }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = result.disableRuntimeValueNotifactions2.HasValue ? result.disableRuntimeValueNotifactions2.Value : false;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<ResourceValue[]> EnableInitialValueNotifications(int[] resourceIds)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(resourceIds), resourceIds));

            var resp = await impl.enableInitialValueNotificationsAsync(new inputMessageName6() { enableInitialValueNotifications1 = resourceIds }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.enableInitialValueNotifications2.Where((v) => v != null).Select((v) => mapResourceValueEnvelope(v)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<ResourceValue[]> EnableRuntimeValueNotifications(int[] resourceIds)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(resourceIds), resourceIds));

            var resp = await impl.enableRuntimeValueNotificationsAsync(new inputMessageName4() { enableRuntimeValueNotifications1 = resourceIds }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.enableRuntimeValueNotifications2.Where((v) => v != null).Select((v) => mapResourceValueEnvelope(v)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<DatalineResource[]> GetAllDatalineInputs()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getAllDatalineInputsAsync(new inputMessageName12()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getAllDatalineInputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<DatalineResource[]> GetExtraDatalineInputs()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getExtraDatalineInputsAsync(new inputMessageName10()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getExtraDatalineInputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<DatalineResource[]> GetAllDatalineOutputs()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getAllDatalineOutputsAsync(new inputMessageName13()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getAllDatalineOutputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<EnumDefinition[]> GetEnumeratorDefinitions()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getEnumeratorDefinitionsAsync(new inputMessageName9() { }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getEnumeratorDefinitions1.Where((v) => v != null).Select((e) => mapMapEnumeratorDefinitions(e)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<DatalineResource[]> GetExtraDatalineOutputs()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getExtraDatalineOutputsAsync(new inputMessageName11()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getExtraDatalineOutputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<ResourceValue> GetInitialValue(int? initialValue)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(initialValue), initialValue));

            var resp = await impl.getInitialValueAsync(new inputMessageName15() { getInitialValue1 = initialValue }).ConfigureAwait(asyncContinueOnCapturedContext);
            var result = mapResourceValueEnvelope(resp.getInitialValue2);
            if (result == null)
            {
                throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "IHC controller returned null resource value for resource ID " + initialValue);
            }

            activity?.SetReturnValue(result);
            return result;
        }

        public async Task<ResourceValue[]> GetInitialValues(int[] initialValues)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(initialValues), initialValues));

            var resp = await impl.getInitialValuesAsync(new inputMessageName17() { getInitialValues1 = initialValues }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getInitialValues2.Where((v) => v != null).Select((v) => mapResourceValueEnvelope(v)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<bool> SetResourceValue(ResourceValue v)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(v), v));

            var input = new inputMessageName18() { setResourceValue1 = mapResourceValueEnvelope(v) };
            var resp = await impl.setResourceValueAsync(input).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.setResourceValue2.HasValue ? resp.setResourceValue2.Value : false;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<bool> SetResourceValues(ResourceValue[] values)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(values), values));

            var input = new inputMessageName3() { setResourceValues1 = values.Select(v => mapResourceValueEnvelope(v)).ToArray() };
            var resp = await impl.setResourceValuesAsync(input).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.setResourceValues2.HasValue ? resp.setResourceValues2.Value : false;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<LoggedData[]> GetLoggedData(int loggedData1)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(loggedData1), loggedData1));

            var resp = await impl.getLoggedDataAsync(new inputMessageName20() { getLoggedData1 = loggedData1 }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getLoggedData2.Where((v) => v != null).Select((l) => new LoggedData() { Value = l.value, Id = l.id, Timestamp = DateTimeOffset.FromUnixTimeSeconds(l.timestamp) }).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<string> GetResourceType(int resourceID)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(resourceID), resourceID));

            var resp = await impl.getResourceTypeAsync(new inputMessageName19() { getResourceType1 = resourceID }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getResourceType2;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<ResourceValue> GetRuntimeValue(int resourceID)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(resourceID), resourceID));

            var resp = await impl.getRuntimeValueAsync(new inputMessageName14() { getRuntimeValue1 = resourceID }).ConfigureAwait(asyncContinueOnCapturedContext);
            var result = mapResourceValueEnvelope(resp.getRuntimeValue2);
            if (result == null)
            {
                throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "IHC controller returned null runtime value for resource ID " + resourceID);
            }

            activity?.SetReturnValue(result);
            return result;
        }

        public async Task<ResourceValue[]> GetRuntimeValues(int[] resourceIDs)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(resourceIDs), resourceIDs));

            var resp = await impl.getRuntimeValuesAsync(new inputMessageName16() { getRuntimeValues1 = resourceIDs }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getRuntimeValues2.Where((v) => v != null).Select((v) => mapResourceValueEnvelope(v)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<SceneResourceIdAndLocation[]> GetSceneGroupResourceIdAndPositions(int sceneGroupResourceIdAndPositions)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(sceneGroupResourceIdAndPositions), sceneGroupResourceIdAndPositions));

            var resp = await impl.getSceneGroupResourceIdAndPositionsAsync(new inputMessageName1(sceneGroupResourceIdAndPositions) {}).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getSceneGroupResourceIdAndPositions2.Where((v) => v != null).Select((v) => mapSceneResourceIdAndLocation(v)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<SceneResourceIdAndLocation> GetScenePositionsForSceneValueResource(int scenePositionsForSceneValueResource)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(scenePositionsForSceneValueResource), scenePositionsForSceneValueResource));

            var resp = await impl.getScenePositionsForSceneValueResourceAsync(new inputMessageName2(scenePositionsForSceneValueResource) {}).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapSceneResourceIdAndLocation(resp.getScenePositionsForSceneValueResource2);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<ResourceValue[]> WaitForResourceValueChanges(int timeout_seconds = 15)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(timeout_seconds), timeout_seconds));

            var resp = await impl.waitForResourceValueChangesAsync(new inputMessageName8() { waitForResourceValueChanges1 = timeout_seconds }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.waitForResourceValueChanges2.Where((v) => v != null).Select((v) => mapResourceValueEnvelope(v)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(int[] resourceIds, CancellationToken cancellationToken = default, int timeout_between_waits_in_seconds = 15)
        {
            return ServiceHelpers.GetResourceValueChanges(
                resourceIds,
                EnableRuntimeValueNotifications,
                WaitForResourceValueChanges,
                DisableRuntimeValueNotifactions,
                this.logger,
                asyncContinueOnCapturedContext,
                cancellationToken,
                timeout_between_waits_in_seconds);
        }
    }
}