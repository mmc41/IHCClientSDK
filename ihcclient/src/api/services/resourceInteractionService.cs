using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Resourceinteraction;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC ResourceInteractionService without any of the soap distractions.
    /// Status: 100% API coverage but not fully tested or documented.
    /// </summary>
    public interface IResourceInteractionService : IIHCApiService
    {
        /// <summary>
        /// Disable initial value notifications for specified resource IDs.
        /// </summary>
        /// <param name="resourceIds">Collection of resource IDs to disable</param>
        public Task<bool> DisableInitialValueNotifactions(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Disable runtime value notifications for specified resource IDs.
        /// </summary>
        /// <param name="resourceIds">Collection of resource IDs to disable</param>
        public Task<bool> DisableRuntimeValueNotifactions(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Enable initial value notifications for specified resource IDs and return current values.
        /// </summary>
        /// <param name="resourceIds">Collection of resource IDs to enable</param>
        public Task<IReadOnlyList<ResourceValue>> EnableInitialValueNotifications(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Enable runtime value notifications for specified resource IDs. Must be called before WaitForResourceValueChanges.
        /// </summary>
        /// <param name="resourceIds">Collection of resource IDs to enable</param>
        public Task<IReadOnlyList<ResourceValue>> EnableRuntimeValueNotifications(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Get all dataline input resource definitions.
        /// </summary>
        public Task<IReadOnlyList<DatalineResource>> GetAllDatalineInputs();

        /// <summary>
        /// Get all dataline output resource definitions.
        /// </summary>
        public Task<IReadOnlyList<DatalineResource>> GetAllDatalineOutputs();

        /// <summary>
        /// Get all enumerator definitions from the IHC project.
        /// </summary>
        public Task<IReadOnlyList<EnumDefinition>> GetEnumeratorDefinitions();

        /// <summary>
        /// Get all extra dataline input resource definitions.
        /// </summary>
        public Task<IReadOnlyList<DatalineResource>> GetExtraDatalineInputs();

        /// <summary>
        /// Get all extra dataline output resource definitions.
        /// </summary>
        public Task<IReadOnlyList<DatalineResource>> GetExtraDatalineOutputs();

        /// <summary>
        /// Get initial value for a single resource ID.
        /// </summary>
        /// <param name="initialValue">Resource ID to get initial value for</param>
        public Task<ResourceValue> GetInitialValue(int initialValue);

        /// <summary>
        /// Get initial values for multiple resource IDs.
        /// </summary>
        /// <param name="initialValues">Collection of resource IDs to get initial values for</param>
        public Task<IReadOnlyList<ResourceValue>> GetInitialValues(IReadOnlyList<int> initialValues);

        /// <summary>
        /// Get logged historical data for a resource ID.
        /// </summary>
        /// <param name="resourceId">Resource ID to get logged data for</param>
        public Task<IReadOnlyList<LoggedData>> GetLoggedData(int resourceId);

        /// <summary>
        /// Get the type string of a resource. Refer to TypeStrings constants for valid return values.
        /// </summary>
        /// <param name="resourceID">Resource ID to get type for</param>
        // Null when the controller reports no type for the resource: the wire element is declared
        // nillable and the generated layer is oblivious, so a non-null return would be unfounded.
        public Task<string?> GetResourceType(int resourceID);

        /// <summary>
        /// Get current runtime value of an input/output resource.
        /// </summary>
        /// <param name="resourceID">Resource ID to get runtime value for</param>
        public Task<ResourceValue> GetRuntimeValue(int resourceID);

        /// <summary>
        /// Get current runtime values for multiple resource IDs.
        /// </summary>
        /// <param name="resourceIDs">Collection of resource IDs to get runtime values for</param>
        public Task<IReadOnlyList<ResourceValue>> GetRuntimeValues(IReadOnlyList<int> resourceIDs);

        /// <summary>
        /// Set value for a single resource.
        /// </summary>
        /// <param name="v">Resource value to set</param>
        public Task<bool> SetResourceValue(ResourceValue v);

        /// <summary>
        /// Set values for multiple resources.
        /// </summary>
        /// <param name="values">Collection of resource values to set</param>
        public Task<bool> SetResourceValues(IReadOnlyList<ResourceValue> values);

        /// <summary>
        /// Get scene resource IDs and positions for a scene group.
        /// </summary>
        /// <param name="sceneGroupResourceIdAndPositions">Scene group resource ID</param>
        public Task<IReadOnlyList<SceneResourceIdAndLocation>> GetSceneGroupResourceIdAndPositions(int sceneGroupResourceIdAndPositions);

        /// <summary>
        /// Get scene positions for a scene value resource.
        /// </summary>
        /// <param name="scenePositionsForSceneValueResource">Scene value resource ID</param>
        public Task<SceneResourceIdAndLocation?> GetScenePositionsForSceneValueResource(int scenePositionsForSceneValueResource);

        /// <summary>
        /// Long-poll for resource value changes. Resources must be enabled first using EnableRuntimeValueNotifications.
        /// Returns immediately with initial values on first call, then respects timeout. Timeout should be less than 20 seconds.
        /// TIP: Consider using GetResourceValueChanges instead.
        /// </summary>
        /// <param name="timeout_seconds">Timeout in seconds (default: 15)</param>
        public Task<IReadOnlyList<ResourceValue>> WaitForResourceValueChanges(int timeout_seconds = 15);

        /// <summary>
        /// Returns an async stream of value changes for specified resources.
        /// Automatically handles EnableRuntimeValueNotifications + WaitForResourceValueChanges loop.
        /// Timeout should be lower than system timeout (less than 20 seconds recommended).
        /// </summary>
        /// <param name="resourceIds">Collection of resource IDs to monitor</param>
        /// <param name="cancellationToken">Cancellation token to stop monitoring</param>
        /// <param name="timeout_between_waits_in_seconds">Timeout between waits in seconds (default: 15)</param>
        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(IReadOnlyList<int> resourceIds, CancellationToken cancellationToken = default, int timeout_between_waits_in_seconds = 15);
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC ResourceInteractionService without exposing any of the soap distractions.
    /// </summary>
    public class ResourceInteractionService : ServiceBase, IResourceInteractionService
    {
        private readonly IAuthenticationService authService;

        /// <summary>
        /// This internal class implements the raw IHC soap service interface and provides the basis
        /// for the higher level public service methods below it.
        /// </summary>
        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Resourceinteraction.ResourceInteractionService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "ResourceInteractionService") { }

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

        private readonly Ihc.Soap.Resourceinteraction.ResourceInteractionService impl;

        private static DatalineResource? mapDatalineResource(WSDatalineResource? r)
        {
            if (r == null)
                return null;

            return new DatalineResource() { ResourceID = r.resourceID, DatalineNumber = r.datalineNumber };
        }

        private static EnumDefinition? mapMapEnumeratorDefinitions(WSEnumDefinition? e)
        {
            if (e == null)
                return null;

            return new EnumDefinition()
            {
                EnumeratorDefinitionID = e.enumeratorDefinitionID,
                // OfType drops any enumerator value the wire left empty; an absent list becomes an empty one.
                Values = e.enumeratorValues?.Select((v) => ResourceValueEnvelopeMapper.MapEnumValue(v)).OfType<EnumValue>().ToArray() ?? Array.Empty<EnumValue>()
            };
        }

        private static SceneResourceIdAndLocation? mapSceneResourceIdAndLocation(Ihc.Soap.Resourceinteraction.WSSceneResourceIdAndLocationURLs? arg) {
            if (arg == null)
                return null;

            return new SceneResourceIdAndLocation() {
                SceneResourceId = arg.sceneResourceId,
                ScenePositionSeenFromProduct = arg.scenePositionSeenFromProduct,
                ScenePositionSeenFromFunctionBlock = arg.scenePositionSeenFromFunctionBlock
            };
        }

        /// <summary>
        /// Create an ResourceInteractionService instance for access to the IHC API related to resources.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public ResourceInteractionService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.authService = authService;
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal ResourceInteractionService(IAuthenticationService authService, Ihc.Soap.Resourceinteraction.ResourceInteractionService impl)
            : base(SettingsOf(authService))
        {
            this.authService = authService;
            this.impl = impl;
        }

        public async Task<bool> DisableInitialValueNotifactions(IReadOnlyList<int> resourceIds)
        {
            using (var activity = StartActivity(nameof(DisableInitialValueNotifactions)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceIds), resourceIds));

                    var result = await this.impl.disableInitialValueNotifactionsAsync(new inputMessageName7() { disableInitialValueNotifactions1 = resourceIds.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.disableInitialValueNotifactions2.HasValue ? result.disableInitialValueNotifactions2.Value : false;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> DisableRuntimeValueNotifactions(IReadOnlyList<int> resourceIds)
        {
            using (var activity = StartActivity(nameof(DisableRuntimeValueNotifactions)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceIds), resourceIds));

                    var result = await this.impl.disableRuntimeValueNotifactionsAsync(new inputMessageName5() { disableRuntimeValueNotifactions1 = resourceIds.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.disableRuntimeValueNotifactions2.HasValue ? result.disableRuntimeValueNotifactions2.Value : false;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<ResourceValue>> EnableInitialValueNotifications(IReadOnlyList<int> resourceIds)
        {
            using (var activity = StartActivity(nameof(EnableInitialValueNotifications)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceIds), resourceIds));

                    var resp = await impl.enableInitialValueNotificationsAsync(new inputMessageName6() { enableInitialValueNotifications1 = resourceIds.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.enableInitialValueNotifications2.Where((v) => v != null).Select((v) => ResourceValueEnvelopeMapper.ToDomain(v)).OfType<ResourceValue>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<ResourceValue>> EnableRuntimeValueNotifications(IReadOnlyList<int> resourceIds)
        {
            using (var activity = StartActivity(nameof(EnableRuntimeValueNotifications)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceIds), resourceIds));

                    var resp = await impl.enableRuntimeValueNotificationsAsync(new inputMessageName4() { enableRuntimeValueNotifications1 = resourceIds.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.enableRuntimeValueNotifications2.Where((v) => v != null).Select((v) => ResourceValueEnvelopeMapper.ToDomain(v)).OfType<ResourceValue>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<DatalineResource>> GetAllDatalineInputs()
        {
            using (var activity = StartActivity(nameof(GetAllDatalineInputs)))
            {
                try
                {
                    var resp = await impl.getAllDatalineInputsAsync(new inputMessageName12()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getAllDatalineInputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).OfType<DatalineResource>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<DatalineResource>> GetExtraDatalineInputs()
        {
            using (var activity = StartActivity(nameof(GetExtraDatalineInputs)))
            {
                try
                {
                    var resp = await impl.getExtraDatalineInputsAsync(new inputMessageName10()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getExtraDatalineInputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).OfType<DatalineResource>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<DatalineResource>> GetAllDatalineOutputs()
        {
            using (var activity = StartActivity(nameof(GetAllDatalineOutputs)))
            {
                try
                {
                    var resp = await impl.getAllDatalineOutputsAsync(new inputMessageName13()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getAllDatalineOutputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).OfType<DatalineResource>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<EnumDefinition>> GetEnumeratorDefinitions()
        {
            using (var activity = StartActivity(nameof(GetEnumeratorDefinitions)))
            {
                try
                {
                    var resp = await impl.getEnumeratorDefinitionsAsync(new inputMessageName9() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getEnumeratorDefinitions1.Where((v) => v != null).Select((e) => mapMapEnumeratorDefinitions(e)).OfType<EnumDefinition>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<DatalineResource>> GetExtraDatalineOutputs()
        {
            using (var activity = StartActivity(nameof(GetExtraDatalineOutputs)))
            {
                try
                {
                    var resp = await impl.getExtraDatalineOutputsAsync(new inputMessageName11()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getExtraDatalineOutputs1.Where((v) => v != null).Select((i) => mapDatalineResource(i)).OfType<DatalineResource>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<ResourceValue> GetInitialValue(int initialValue)
        {
            using (var activity = StartActivity(nameof(GetInitialValue)))
            {
                try
                {
                    activity?.SetParameters((nameof(initialValue), initialValue));

                    var resp = await impl.getInitialValueAsync(new inputMessageName15() { getInitialValue1 = initialValue }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var result = ResourceValueEnvelopeMapper.ToDomain(resp.getInitialValue2);
                    if (result == null)
                    {
                        throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "IHC controller returned null resource value for resource ID " + initialValue);
                    }

                    activity?.SetReturnValue(result);
                    return result;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<ResourceValue>> GetInitialValues(IReadOnlyList<int> initialValues)
        {
            using (var activity = StartActivity(nameof(GetInitialValues)))
            {
                try
                {
                    activity?.SetParameters((nameof(initialValues), initialValues));

                    var resp = await impl.getInitialValuesAsync(new inputMessageName17() { getInitialValues1 = initialValues.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getInitialValues2.Where((v) => v != null).Select((v) => ResourceValueEnvelopeMapper.ToDomain(v)).OfType<ResourceValue>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> SetResourceValue(ResourceValue v)
        {
            using (var activity = StartActivity(nameof(SetResourceValue)))
            {
                try
                {
                    activity?.SetParameters((nameof(v), v));

                    var input = new inputMessageName18() { setResourceValue1 = ResourceValueEnvelopeMapper.ToWire(v) };
                    var resp = await impl.setResourceValueAsync(input).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.setResourceValue2.HasValue ? resp.setResourceValue2.Value : false;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> SetResourceValues(IReadOnlyList<ResourceValue> values)
        {
            using (var activity = StartActivity(nameof(SetResourceValues)))
            {
                try
                {
                    activity?.SetParameters((nameof(values), values));

                    var input = new inputMessageName3() { setResourceValues1 = values.Select(v => ResourceValueEnvelopeMapper.ToWire(v)).ToArray() };
                    var resp = await impl.setResourceValuesAsync(input).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.setResourceValues2.HasValue ? resp.setResourceValues2.Value : false;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<LoggedData>> GetLoggedData(int resourceId)
        {
            using (var activity = StartActivity(nameof(GetLoggedData)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceId), resourceId));

                    var resp = await impl.getLoggedDataAsync(new inputMessageName20() { getLoggedData1 = resourceId }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getLoggedData2.Where((v) => v != null).Select((l) => new LoggedData() { Value = l.value, Id = l.id, Timestamp = DateTimeOffset.FromUnixTimeSeconds(l.timestamp) }).ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<string?> GetResourceType(int resourceID)
        {
            using (var activity = StartActivity(nameof(GetResourceType)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceID), resourceID));

                    var resp = await impl.getResourceTypeAsync(new inputMessageName19() { getResourceType1 = resourceID }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getResourceType2;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<ResourceValue> GetRuntimeValue(int resourceID)
        {
            using (var activity = StartActivity(nameof(GetRuntimeValue)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceID), resourceID));

                    var resp = await impl.getRuntimeValueAsync(new inputMessageName14() { getRuntimeValue1 = resourceID }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var result = ResourceValueEnvelopeMapper.ToDomain(resp.getRuntimeValue2);
                    if (result == null)
                    {
                        throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "IHC controller returned null runtime value for resource ID " + resourceID);
                    }

                    activity?.SetReturnValue(result);
                    return result;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<ResourceValue>> GetRuntimeValues(IReadOnlyList<int> resourceIDs)
        {
            using (var activity = StartActivity(nameof(GetRuntimeValues)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceIDs), resourceIDs));

                    var resp = await impl.getRuntimeValuesAsync(new inputMessageName16() { getRuntimeValues1 = resourceIDs.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getRuntimeValues2.Where((v) => v != null).Select((v) => ResourceValueEnvelopeMapper.ToDomain(v)).OfType<ResourceValue>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<SceneResourceIdAndLocation>> GetSceneGroupResourceIdAndPositions(int sceneGroupResourceIdAndPositions)
        {
            using (var activity = StartActivity(nameof(GetSceneGroupResourceIdAndPositions)))
            {
                try
                {
                    activity?.SetParameters((nameof(sceneGroupResourceIdAndPositions), sceneGroupResourceIdAndPositions));

                    var resp = await impl.getSceneGroupResourceIdAndPositionsAsync(new inputMessageName1(sceneGroupResourceIdAndPositions) {}).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getSceneGroupResourceIdAndPositions2.Where((v) => v != null).Select((v) => mapSceneResourceIdAndLocation(v)).OfType<SceneResourceIdAndLocation>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<SceneResourceIdAndLocation?> GetScenePositionsForSceneValueResource(int scenePositionsForSceneValueResource)
        {
            using (var activity = StartActivity(nameof(GetScenePositionsForSceneValueResource)))
            {
                try
                {
                    activity?.SetParameters((nameof(scenePositionsForSceneValueResource), scenePositionsForSceneValueResource));

                    var resp = await impl.getScenePositionsForSceneValueResourceAsync(new inputMessageName2(scenePositionsForSceneValueResource) {}).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapSceneResourceIdAndLocation(resp.getScenePositionsForSceneValueResource2);

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<ResourceValue>> WaitForResourceValueChanges(int timeout_seconds = 15)
        {
            using (var activity = StartActivity(nameof(WaitForResourceValueChanges)))
            {
                try
                {
                    activity?.SetParameters((nameof(timeout_seconds), timeout_seconds));

                    var resp = await impl.waitForResourceValueChangesAsync(new inputMessageName8() { waitForResourceValueChanges1 = timeout_seconds }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.waitForResourceValueChanges2.Where((v) => v != null).Select((v) => ResourceValueEnvelopeMapper.ToDomain(v)).OfType<ResourceValue>().ToList();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async IAsyncEnumerable<ResourceValue> GetResourceValueChanges(
            IReadOnlyList<int> resourceIds,
            [EnumeratorCancellation] CancellationToken cancellationToken = default,
            int timeout_between_waits_in_seconds = 15)
        {
            // Deliberately an async iterator, so the activity spans the iteration rather than the call that
            // creates it. Returning the stream from a plain method would stop and export the activity before the
            // polling loop had run at all, and every error the loop reports would land on a dead span.
            using var activity = StartActivity(nameof(GetResourceValueChanges));

            activity?.SetParameters(
                (nameof(resourceIds), resourceIds),
                (nameof(cancellationToken), cancellationToken),
                (nameof(timeout_between_waits_in_seconds), timeout_between_waits_in_seconds));

            IAsyncEnumerable<ResourceValue> changes = ServiceHelpers.GetResourceValueChanges(
                activity,
                resourceIds,
                EnableRuntimeValueNotifications,
                WaitForResourceValueChanges,
                DisableRuntimeValueNotifactions,
                settings.AsyncContinueOnCapturedContext,
                cancellationToken,
                timeout_between_waits_in_seconds);

            await foreach (var change in changes.ConfigureAwait(settings.AsyncContinueOnCapturedContext))
            {
                yield return change;
            }
        }
    }
}