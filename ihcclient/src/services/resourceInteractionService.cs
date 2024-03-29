using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Resourceinteraction;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC ResourceInteractionService without any of the soap distractions.
    *
    * Status: 100% API coverage but not fully tested or documented.
    */
    public interface IResourceInteractionService
    {
        public Task<bool?> DisableInitialValueNotifactions(int[] resourceIds);
        
        public Task<bool?> DisableRuntimeValueNotifactions(int[] resourceIds);
        
        public Task<ResourceValue[]> EnableInitialValueNotifications(int[] resourceIds);
        
        /*
        * Enable notification for specified IHC resource ids. Must be called before waitForResourceValueChanges.
        */
        public Task<ResourceValue[]> EnableRuntimeValueNotifications(int[] resourceIds);
        
        public Task<DatalineResource[]> GetAllDatalineInputs();
        
        /**
        * Get all output definitions.
        */
        public Task<DatalineResource[]> GetAllDatalineOutputs();
        
        public Task<EnumDefinition[]> GetEnumeratorDefinitions();
        
        /**
        * Get all extra (??) input definitions.
        */
        public Task<DatalineResource[]> GetExtraDatalineInputs();
        
        /**
        * Get all extra (??) output definitions.
        */
        public Task<DatalineResource[]> GetExtraDatalineOutputs();
        
        public Task<ResourceValue> GetInitialValue(int? initialValue);
        
        public Task<ResourceValue[]> GetInitialValues(int[] initialValues);
        
        public Task<LoggedData[]> GetLoggedData(int loggedData1);
        
        /**
        * Return type of resource.
        * Refer to TypeStrings constants for valid return values.
        */
        public Task<string> GetResourceType(int resourceID);

        /*
        * Get current state of input/output resource
        */
        public Task<ResourceValue> GetRuntimeValue(int resourceID);
        
        
        public Task<ResourceValue[]> GetRuntimeValues(int[] resourceIDs);
        
        public Task<bool?> SetResourceValue(ResourceValue v);
        
        public Task<bool?> setResourceValues(ResourceValue[] values);

        public Task<SceneResourceIdAndLocation[]> GetSceneGroupResourceIdAndPositions(int sceneGroupResourceIdAndPositions);

        public Task<SceneResourceIdAndLocation> GetScenePositionsForSceneValueResource(int scenePositionsForSceneValueResource);
        
        /**
         * Can be used for long pooling of resource changes for resources previously enabled wait for using enableRuntimeValueNotifications.
         * Will return immidiately with first all initial values, after that it repects timeout value in seconds.
         * Nb. Timeout should be lower that system timeout or the call will fail after a couple of calls. Limit seems to be around 20s
         * TIP: Consider using GetResourceValueChanges instead of this method.
         */
        public Task<ResourceValue[]> WaitForResourceValueChanges(int timeout_seconds = 15);

        /**
         * Returns an async stream of changes in specified resources.
         * Corresponds to enableRuntimeValueNotifications + WaitForResourceValueChanges in a loop
         *
         * Nb. Internal timeout should be lower that system timeout or the call will fail after a couple of calls. 
         * Limit seems to be maybe around 20s ?
         */
        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(int[] resourceIds, CancellationToken cancellationToken = default, int timeout_between_waits_in_seconds = 15);
    }

    /**
    * A highlevel implementation of a client to the IHC ResourceInteractionService without exposing any of the soap distractions.
    */
    public class ResourceInteractionService : IResourceInteractionService
    {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;

        /**
         * This internal class implements the raw IHC soap service interface and provides the basis
         * for the higher level public service methods below it.
         */
        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Resourceinteraction.ResourceInteractionService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "ResourceInteractionService") { }

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
                value.TimerValue = mapWeekday(v.value as WSWeekdayValue);
                value.ValueKind = ResourceValue.ValueKind.WEEKDAY;
            }

            return new ResourceValue() { ResourceID = v.resourceID, IsValueRuntime = v.isValueRuntime, TypeString = v.typeString, Value = value };
        }

        private WSResourceValueEnvelope mapResourceValueEnvelope(ResourceValue v)
        {
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
            return new DatalineResource() { ResourceID = r.resourceID, DatalineNumber = r.datalineNumber };
        }

        private EnumDefinition mapMapEnumeratorDefinitions(WSEnumDefinition e)
        {
            return new EnumDefinition()
            {
                EnumeratorDefinitionID = e.enumeratorDefinitionID,
                Values = e.enumeratorValues.Select((v) => mapEnumValue(v)).ToArray()
            };
        }

        private EnumValue mapEnumValue(WSEnumValue v)
        {
            return new EnumValue() { DefinitionTypeID = v.definitionTypeID, EnumValueID = v.enumValueID, EnumName = v.enumName };
        }

        private WSEnumValue mapEnumValue(EnumValue v)
        {
            return new WSEnumValue() { definitionTypeID = v.DefinitionTypeID, enumValueID = v.EnumValueID, enumName = v.EnumName };
        }

        private DateTimeOffset mapDate(WSDateValue v)
        {
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
            return new TimeSpan(v.hours, v.minutes, v.seconds);
        }

        private long mapTimer(WSTimerValue v)
        {
            return v.milliseconds;
        }

        private WSTimerValue mapTimer(long v)
        {
            return new WSTimerValue() { milliseconds = v };
        }

        private int mapWeekday(WSWeekdayValue v)
        {
            return v.weekdayNumber;
        }

        private WSWeekdayValue mapWeekday(int v)
        {
            return new WSWeekdayValue() { weekdayNumber = v };
        }
        
        public SceneResourceIdAndLocation mapSceneResourceIdAndLocation(Ihc.Soap.Resourceinteraction.WSSceneResourceIdAndLocationURLs arg) {
            return new SceneResourceIdAndLocation() {
                SceneResourceId = arg.sceneResourceId,
                ScenePositionSeenFromProduct = arg.scenePositionSeenFromProduct,
                ScenePositionSeenFromFunctionBlock = arg.scenePositionSeenFromFunctionBlock
            };
        }

        /**
        * Create an ResourceInteractionService instance for access to the IHC API related to resources.
        * <param name="authService">AuthenticationService instance</param>
        */
        public ResourceInteractionService(IAuthenticationService authService)
        {
            this.logger = authService.Logger;
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint);
        }

        public async Task<System.Nullable<bool>> DisableInitialValueNotifactions(int[] resourceIds)
        {
            var result = await this.impl.disableInitialValueNotifactionsAsync(new inputMessageName7() { disableInitialValueNotifactions1 = resourceIds });
            return result.disableInitialValueNotifactions2;
        }

        public async Task<System.Nullable<bool>> DisableRuntimeValueNotifactions(int[] resourceIds)
        {
            var result = await this.impl.disableRuntimeValueNotifactionsAsync(new inputMessageName5() { disableRuntimeValueNotifactions1 = resourceIds });
            return result.disableRuntimeValueNotifactions2;
        }

        public async Task<ResourceValue[]> EnableInitialValueNotifications(int[] resourceIds)
        {
            var resp = await impl.enableInitialValueNotificationsAsync(new inputMessageName6() { enableInitialValueNotifications1 = resourceIds });
            return resp.enableInitialValueNotifications2.Where((v) => v != null).Select((v) => mapResourceValueEnvelope(v)).ToArray();
        }

        public async Task<ResourceValue[]> EnableRuntimeValueNotifications(int[] resourceIds)
        {
            var resp = await impl.enableRuntimeValueNotificationsAsync(new inputMessageName4() { enableRuntimeValueNotifications1 = resourceIds });
            return resp.enableRuntimeValueNotifications2.Where((v) => v != null).Select((v) => mapResourceValueEnvelope(v)).ToArray();
        }

        public async Task<DatalineResource[]> GetAllDatalineInputs()
        {
            var resp = await impl.getAllDatalineInputsAsync(new inputMessageName12());
            return resp.getAllDatalineInputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).ToArray();
        }

        public async Task<DatalineResource[]> GetExtraDatalineInputs()
        {
            var resp = await impl.getExtraDatalineInputsAsync(new inputMessageName10());
            return resp.getExtraDatalineInputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).ToArray();
        }

        public async Task<DatalineResource[]> GetAllDatalineOutputs()
        {
            var resp = await impl.getAllDatalineOutputsAsync(new inputMessageName13());
            return resp.getAllDatalineOutputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).ToArray();
        }

        public async Task<EnumDefinition[]> GetEnumeratorDefinitions()
        {
            var resp = await impl.getEnumeratorDefinitionsAsync(new inputMessageName9() { });
            return resp.getEnumeratorDefinitions1.Where((v) => v != null).Select((e) => mapMapEnumeratorDefinitions(e)).ToArray();
        }

        public async Task<DatalineResource[]> GetExtraDatalineOutputs()
        {
            var resp = await impl.getExtraDatalineOutputsAsync(new inputMessageName11());
            return resp.getExtraDatalineOutputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).ToArray();
        }

        public async Task<ResourceValue> GetInitialValue(int? initialValue)
        {
            var resp = await impl.getInitialValueAsync(new inputMessageName15() { getInitialValue1 = initialValue });
            return mapResourceValueEnvelope(resp.getInitialValue2);
        }

        public async Task<ResourceValue[]> GetInitialValues(int[] initialValues)
        {
            var resp = await impl.getInitialValuesAsync(new inputMessageName17() { getInitialValues1 = initialValues });
            return resp.getInitialValues2.Where((v) => v != null).Select((v) => mapResourceValueEnvelope(v)).ToArray();
        }

        public async Task<bool?> SetResourceValue(ResourceValue v)
        {
            var input = new inputMessageName18() { setResourceValue1 = mapResourceValueEnvelope(v) };
            var resp = await impl.setResourceValueAsync(input);
            return resp.setResourceValue2.HasValue;
        }

        public async Task<bool?> setResourceValues(ResourceValue[] values)
        {
            var input = new inputMessageName3() { setResourceValues1 = values.Select((v) => mapResourceValueEnvelope(v)).ToArray() };
            var resp = await impl.setResourceValuesAsync(input);
            return resp.setResourceValues2;
        }

        public async Task<LoggedData[]> GetLoggedData(int loggedData1)
        {
            var resp = await impl.getLoggedDataAsync(new inputMessageName20() { getLoggedData1 = loggedData1 });
            return resp.getLoggedData2.Where((v) => v != null).Select((l) => new LoggedData() { Value = l.value, Id = l.id, Timestamp = DateTimeOffset.FromUnixTimeSeconds(l.timestamp) }).ToArray();
        }

        public async Task<string> GetResourceType(int resourceID)
        {
            var resp = await impl.getResourceTypeAsync(new inputMessageName19() { getResourceType1 = resourceID });
            return resp.getResourceType2;
        }

        public async Task<ResourceValue> GetRuntimeValue(int resourceID)
        {
            var resp = await impl.getRuntimeValueAsync(new inputMessageName14() { getRuntimeValue1 = resourceID });
            return mapResourceValueEnvelope(resp.getRuntimeValue2);
        }

        public async Task<ResourceValue[]> GetRuntimeValues(int[] resourceIDs)
        {
            var resp = await impl.getRuntimeValuesAsync(new inputMessageName16() { getRuntimeValues1 = resourceIDs });
            return resp.getRuntimeValues2.Where((v) => v != null).Select((v) => mapResourceValueEnvelope(v)).ToArray();
        }

        public async Task<SceneResourceIdAndLocation[]> GetSceneGroupResourceIdAndPositions(int sceneGroupResourceIdAndPositions)
        {
            var resp = await impl.getSceneGroupResourceIdAndPositionsAsync(new inputMessageName1(sceneGroupResourceIdAndPositions) {});
            return resp.getSceneGroupResourceIdAndPositions2.Where((v) => v != null).Select((v) => mapSceneResourceIdAndLocation(v)).ToArray();
        }

        public async Task<SceneResourceIdAndLocation> GetScenePositionsForSceneValueResource(int scenePositionsForSceneValueResource)
        {
            var resp = await impl.getScenePositionsForSceneValueResourceAsync(new inputMessageName2(scenePositionsForSceneValueResource) {});
            return mapSceneResourceIdAndLocation(resp.getScenePositionsForSceneValueResource2);
        }

        public async Task<ResourceValue[]> WaitForResourceValueChanges(int timeout_seconds = 15)
        {
            var resp = await impl.waitForResourceValueChangesAsync(new inputMessageName8() { waitForResourceValueChanges1 = timeout_seconds });
            return resp.waitForResourceValueChanges2.Where((v) => v != null).Select((v) => mapResourceValueEnvelope(v)).ToArray();
        }

        public async System.Collections.Generic.IAsyncEnumerable<ResourceValue> GetResourceValueChanges(int[] resourceIds, [EnumeratorCancellation] CancellationToken cancellationToken = default, int timeout_between_waits_in_seconds = 15)
        {
            try
            {
                await EnableRuntimeValueNotifications(resourceIds);
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "enableRuntimeValueNotifications error");
                throw;
            }

            try
            {
                int sequentialErrorCount = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(25); // Give the server a short rest between calls.
                    ResourceValue[] changes;
                    try
                    {
                        changes = await WaitForResourceValueChanges(timeout_between_waits_in_seconds);
                        sequentialErrorCount = 0;
                    }
                    catch (Exception e)
                    {
                        this.logger.LogWarning(e, "waitForResourceValueChanges failed #" + sequentialErrorCount);
                        changes = new ResourceValue[] { };
                        if (++sequentialErrorCount > 10)
                        {
                            this.logger.LogError(e, "waitForResourceValueChanges repeated failure");
                            throw; // Fail hard if exception repeats.
                        } else {
                             // Allow server to recover.
                             Thread.Sleep(sequentialErrorCount*sequentialErrorCount*100);
                        }
                    }

                    foreach (var change in changes)
                    {
                        yield return change;
                    }
                }
            }
            finally
            {
                try
                {
                    Thread.Sleep(25); // Give the server a short rest between calls.
                    await DisableRuntimeValueNotifactions(resourceIds);
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "disableRuntimeValueNotifactions error");
                    throw;
                }
            }
        }
    }
}