using System.Threading;
using System.Threading.Tasks;
using System;
using System.Globalization;
using System.Linq;
using Ihc.Soap.Resourceinteraction;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Ihc {
    /// <summary>
    /// Reads and writes the current (runtime) and initial values of the resources in the controller's project, each
    /// addressed by its project element id, and reports value changes through subscriptions and a long poll.
    /// </summary>
    public interface IResourceInteractionService : IIHCApiService
    {
        /// <summary>
        /// Ends the subscription to initial-value changes of the given resources; it does not end a held poll.
        /// </summary>
        /// <param name="resourceIds">Ids of resources subscribed with
        /// <see cref="EnableInitialValueNotifications"/>.</param>
        /// <returns>The controller's answer; <c>false</c> also when the reply carries no value.</returns>
        /// <remarks>The wire operation keeps the vendor's spelling, <c>disableInitialValueNotifactions</c>.</remarks>
        public Task<bool> DisableInitialValueNotifications(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Ends the subscription to current-value changes of the given resources; it does not end a held poll.
        /// </summary>
        /// <param name="resourceIds">Ids of resources subscribed with
        /// <see cref="EnableRuntimeValueNotifications"/>.</param>
        /// <returns>The controller's answer; <c>true</c> does not confirm that the ids exist in its project.
        /// <c>false</c> also when the reply carries no value.</returns>
        /// <remarks>The wire operation keeps the vendor's spelling, <c>disableRuntimeValueNotifactions</c>.</remarks>
        public Task<bool> DisableRuntimeValueNotifications(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Subscribes to initial-value changes of the given resources. The values come through
        /// <see cref="WaitForResourceValueChanges"/>: the subscription ends a held poll, or the next poll returns at
        /// once, with each newly subscribed resource's initial value (<see cref="ResourceValue.IsValueRuntime"/>
        /// <c>false</c>).
        /// </summary>
        /// <param name="resourceIds">Ids of the resources to subscribe.</param>
        /// <returns>An empty list: the controller's reply carries no values.</returns>
        public Task<IReadOnlyList<ResourceValue>> EnableInitialValueNotifications(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Subscribes to current-value changes of the given resources, which <see cref="WaitForResourceValueChanges"/>
        /// then reports. The subscription ends a held poll, or the next poll returns at once, with each newly
        /// subscribed resource's current value; an id the controller's project lacks is accepted but gets no value.
        /// </summary>
        /// <param name="resourceIds">Ids of the resources to subscribe.</param>
        /// <returns>An empty list: the controller's reply carries no values.</returns>
        public Task<IReadOnlyList<ResourceValue>> EnableRuntimeValueNotifications(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Lists the controller's dataline inputs, each as its dataline number and resource id. Subscribe these ids to
        /// follow the inputs' activity.
        /// </summary>
        public Task<IReadOnlyList<DatalineResource>> GetAllDatalineInputs();

        /// <summary>
        /// Lists the controller's dataline outputs, each as its dataline number and resource id.
        /// </summary>
        public Task<IReadOnlyList<DatalineResource>> GetAllDatalineOutputs();

        /// <summary>
        /// Lists the enumeration definitions of the controller's project with their values, which name the enum values
        /// that resources carry. The controller lists a built-in dimmer load-mode definition (<c>Auto</c>, <c>RC</c>,
        /// <c>RL</c>) first.
        /// </summary>
        public Task<IReadOnlyList<EnumDefinition>> GetEnumeratorDefinitions();

        /// <summary>
        /// Lists the controller's "extra" dataline inputs, each as its dataline number and resource id: probably inputs
        /// wired to an input module but not used in the programming.
        /// </summary>
        public Task<IReadOnlyList<DatalineResource>> GetExtraDatalineInputs();

        /// <summary>
        /// Lists the controller's "extra" dataline outputs, each as its dataline number and resource id.
        /// </summary>
        public Task<IReadOnlyList<DatalineResource>> GetExtraDatalineOutputs();

        /// <summary>
        /// Reads a resource's initial value, which the controller loads into it at restart unless the resource keeps
        /// its value through a power cut.
        /// </summary>
        /// <param name="resourceId">Id of the resource to read.</param>
        /// <exception cref="ErrorWithCodeException">The reply carries no value.</exception>
        public Task<ResourceValue> GetInitialValue(int resourceId);

        /// <summary>
        /// Reads the initial values of several resources (see <see cref="GetInitialValue"/>).
        /// </summary>
        /// <param name="resourceIds">Ids of the resources to read.</param>
        /// <returns>The values; an entry the controller leaves empty is dropped, so match results by
        /// <see cref="ResourceValue.ResourceID"/>.</returns>
        public Task<IReadOnlyList<ResourceValue>> GetInitialValues(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Reads the samples the controller has logged to its SD card for a resource, whose logging is set up in IHC
        /// Visual through a product with logging. The list is empty when nothing is logged.
        /// </summary>
        /// <param name="resourceId">Id of the logged resource.</param>
        /// <remarks>The controller stamps each sample in milliseconds since 1970, read as UTC; that the epoch is UTC is
        /// not verified against a controller.</remarks>
        /// <exception cref="InvalidOperationException">A sample's stamp is outside the range a date can represent; the
        /// message names the value.</exception>
        public Task<IReadOnlyList<LoggedData>> GetLoggedData(int resourceId);

        /// <summary>
        /// Gets a resource's type name, probably its element type in the .vis project file as in
        /// <see cref="ResourceValue.TypeString"/>, such as <c>resource_output</c>.
        /// </summary>
        /// <param name="resourceId">Id of the resource.</param>
        /// <returns>The type name, or <c>null</c> when the reply carries none.</returns>
        // Null when the controller reports no type for the resource: the wire element is declared
        // nillable and the generated layer is oblivious, so a non-null return would be unfounded.
        public Task<string?> GetResourceType(int resourceId);

        /// <summary>
        /// Reads a resource's current (runtime) value, the one the program and the outputs act on now.
        /// </summary>
        /// <param name="resourceId">Id of the resource to read.</param>
        /// <exception cref="ErrorWithCodeException">The reply carries no value.</exception>
        public Task<ResourceValue> GetRuntimeValue(int resourceId);

        /// <summary>
        /// Reads the current (runtime) values of several resources; each value's
        /// <see cref="ResourceValue.TypeString"/> names the resource's project element type, such as
        /// <c>resource_output</c>.
        /// </summary>
        /// <param name="resourceIds">Ids of the resources to read.</param>
        /// <returns>The values; an entry the controller leaves empty is dropped, so match results by
        /// <see cref="ResourceValue.ResourceID"/>.</returns>
        public Task<IReadOnlyList<ResourceValue>> GetRuntimeValues(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Writes one resource value, as <see cref="SetResourceValues"/> does for a batch.
        /// </summary>
        /// <param name="v">The value to write: <see cref="ResourceValue.IsValueRuntime"/> <c>true</c> sets the
        /// current value, <c>false</c> the initial value.</param>
        /// <returns>The controller's answer; <c>false</c> also when the reply carries no value.</returns>
        /// <exception cref="ArgumentException">The value cannot be written, as for
        /// <see cref="SetResourceValues"/>; nothing is sent.</exception>
        public Task<bool> SetResourceValue(ResourceValue v);

        /// <summary>
        /// Writes a batch of resource values. The long poll reports a subscribed resource only when the write changed
        /// its value, and the program can overwrite a written current value at its next event.
        /// </summary>
        /// <param name="values">The values to write: <see cref="ResourceValue.IsValueRuntime"/> <c>true</c> sets the
        /// current value, <c>false</c> the initial value loaded at restart. <see cref="ResourceValue.TypeString"/> may
        /// be empty.</param>
        /// <returns><c>true</c> when the controller accepted the batch, also for values it already held; what a
        /// <c>false</c> answer means is not known. <c>false</c> also when the reply carries no value.</returns>
        /// <exception cref="ArgumentException">A value cannot be written: it lacks the payload its
        /// <see cref="ResourceValue.ValueKind"/> selects, is of kind <c>NONE</c>, holds a time the wire cannot carry,
        /// or is one the vendor editors refuse -- resource id 0, a weekday outside 0-6, a scene dimmer percentage
        /// outside 0-100, a ramp over 3,600,000 ms, or an INT or DOUBLE outside the range it carries. Nothing is
        /// sent.</exception>
        public Task<bool> SetResourceValues(IReadOnlyList<ResourceValue> values);

        /// <summary>
        /// Lists a scene group's members, each with the member's resource id and two location texts, seen from the
        /// product and from the function block. The list is empty for a group without members.
        /// </summary>
        /// <param name="sceneGroupResourceId">Id of the scene group, such as a function block's scene
        /// output (<c>resource_scene</c>).</param>
        public Task<IReadOnlyList<SceneResourceIdAndLocation>> GetSceneGroupResourceIdAndPositions(int sceneGroupResourceId);

        /// <summary>
        /// Reads the two location texts of one scene membership, seen from the product and from the function block.
        /// </summary>
        /// <param name="sceneMemberResourceId">Id of a scene member element, such as a
        /// <c>scene_dimmer</c>; not the scene's or the output's id.</param>
        /// <returns>The texts, or <c>null</c> when the reply carries none.</returns>
        public Task<SceneResourceIdAndLocation?> GetScenePositionsForSceneValueResource(int sceneMemberResourceId);

        /// <summary>
        /// Long-polls for changes of the subscribed resources: the controller holds the call until one changes or
        /// <paramref name="holdSeconds"/> runs out. A new subscription ends a held poll, or makes the next one
        /// return at once, with the newly subscribed resources' values. <see cref="GetResourceValueChanges"/> runs
        /// this loop for you.
        /// </summary>
        /// <param name="holdSeconds">Longest time, in seconds, the controller holds the call (default: 15).</param>
        /// <returns>The changes since the last poll, current values with <see cref="ResourceValue.IsValueRuntime"/>
        /// <c>true</c> and initial values with <c>false</c>, each with an empty <see cref="ResourceValue.TypeString"/>;
        /// an empty list when the hold ran out.</returns>
        public Task<IReadOnlyList<ResourceValue>> WaitForResourceValueChanges(int holdSeconds = 15);

        /// <summary>
        /// Streams value changes: subscribes <paramref name="resourceIds"/> with
        /// <see cref="EnableRuntimeValueNotifications"/>, long-polls with <see cref="WaitForResourceValueChanges"/> and
        /// ends the subscription when the enumeration ends. It starts with each newly subscribed resource's current
        /// value and passes on every change the poll reports, also of other subscribed resources; poll failures are
        /// retried with a growing pause, and repeated ones end the stream with the error.
        /// </summary>
        /// <param name="resourceIds">Ids of the resources to monitor.</param>
        /// <param name="cancellationToken">Stops the stream; it is checked between polls, so a held poll runs to its
        /// end first.</param>
        /// <param name="holdSeconds">Longest time, in seconds, the controller holds each poll
        /// (default: 15).</param>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled during the
        /// short rest between polls or the pause after a failed one: the enumeration then throws. Cancelled while a
        /// poll is held, the stream yields that poll's changes and ends quietly. Either way the subscription is
        /// ended.</exception>
        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(IReadOnlyList<int> resourceIds, CancellationToken cancellationToken = default, int holdSeconds = 15);
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC ResourceInteractionService without exposing any of the soap distractions.
    /// </summary>
    public class ResourceInteractionService : ServiceBase, IResourceInteractionService
    {
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
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal ResourceInteractionService(IAuthenticationService authService, Ihc.Soap.Resourceinteraction.ResourceInteractionService impl)
            : base(SettingsOf(authService))
        {
            this.impl = impl;
        }

        public async Task<bool> DisableInitialValueNotifications(IReadOnlyList<int> resourceIds)
        {
            using (var activity = StartActivity(nameof(DisableInitialValueNotifications)))
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

        public async Task<bool> DisableRuntimeValueNotifications(IReadOnlyList<int> resourceIds)
        {
            using (var activity = StartActivity(nameof(DisableRuntimeValueNotifications)))
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
                    var retv = WireList.MapPresent(resp.enableInitialValueNotifications2, ResourceValueEnvelopeMapper.ToDomain, activity, nameof(EnableInitialValueNotifications));

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
                    var retv = WireList.MapPresent(resp.enableRuntimeValueNotifications2, ResourceValueEnvelopeMapper.ToDomain, activity, nameof(EnableRuntimeValueNotifications));

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
                    var retv = WireList.MapPresent(resp.getAllDatalineInputs1, mapDatalineResource, activity, nameof(GetAllDatalineInputs));

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
                    var retv = WireList.MapPresent(resp.getExtraDatalineInputs1, mapDatalineResource, activity, nameof(GetExtraDatalineInputs));

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
                    var retv = WireList.MapPresent(resp.getAllDatalineOutputs1, mapDatalineResource, activity, nameof(GetAllDatalineOutputs));

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
                    var retv = WireList.MapPresent(resp.getEnumeratorDefinitions1, mapMapEnumeratorDefinitions, activity, nameof(GetEnumeratorDefinitions));

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
                    var retv = WireList.MapPresent(resp.getExtraDatalineOutputs1, mapDatalineResource, activity, nameof(GetExtraDatalineOutputs));

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

        public async Task<ResourceValue> GetInitialValue(int resourceId)
        {
            using (var activity = StartActivity(nameof(GetInitialValue)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceId), resourceId));

                    var resp = await impl.getInitialValueAsync(new inputMessageName15() { getInitialValue1 = resourceId }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var result = ResourceValueEnvelopeMapper.ToDomain(resp.getInitialValue2);
                    if (result == null)
                    {
                        throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "IHC controller returned null resource value for resource ID " + resourceId);
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

        public async Task<IReadOnlyList<ResourceValue>> GetInitialValues(IReadOnlyList<int> resourceIds)
        {
            using (var activity = StartActivity(nameof(GetInitialValues)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceIds), resourceIds));

                    var resp = await impl.getInitialValuesAsync(new inputMessageName17() { getInitialValues1 = resourceIds.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = WireList.MapPresent(resp.getInitialValues2, ResourceValueEnvelopeMapper.ToDomain, activity, nameof(GetInitialValues));

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

        // A logged sample's stamp is milliseconds since 1970: ServiceView reads it with Calendar.setTimeInMillis, and
        // the vendor's export shows 2015 samples, which as seconds would be past any date. Read as UTC, which no
        // capture has confirmed.
        private static DateTimeOffset loggedTimestamp(long millisecondsSince1970)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(millisecondsSince1970);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new InvalidOperationException(
                    $"The controller logged a sample at {millisecondsSince1970.ToString(CultureInfo.InvariantCulture)} milliseconds since 1970, which no date can represent.", ex);
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
                    var retv = WireList.MapPresent(resp.getLoggedData2, (l) => new LoggedData() { Value = l.value, Id = l.id, Timestamp = loggedTimestamp(l.timestamp) }, activity, nameof(GetLoggedData));

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

        public async Task<string?> GetResourceType(int resourceId)
        {
            using (var activity = StartActivity(nameof(GetResourceType)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceId), resourceId));

                    var resp = await impl.getResourceTypeAsync(new inputMessageName19() { getResourceType1 = resourceId }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
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

        public async Task<ResourceValue> GetRuntimeValue(int resourceId)
        {
            using (var activity = StartActivity(nameof(GetRuntimeValue)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceId), resourceId));

                    var resp = await impl.getRuntimeValueAsync(new inputMessageName14() { getRuntimeValue1 = resourceId }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var result = ResourceValueEnvelopeMapper.ToDomain(resp.getRuntimeValue2);
                    if (result == null)
                    {
                        throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "IHC controller returned null runtime value for resource ID " + resourceId);
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

        public async Task<IReadOnlyList<ResourceValue>> GetRuntimeValues(IReadOnlyList<int> resourceIds)
        {
            using (var activity = StartActivity(nameof(GetRuntimeValues)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceIds), resourceIds));

                    var resp = await impl.getRuntimeValuesAsync(new inputMessageName16() { getRuntimeValues1 = resourceIds.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = WireList.MapPresent(resp.getRuntimeValues2, ResourceValueEnvelopeMapper.ToDomain, activity, nameof(GetRuntimeValues));

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

        public async Task<IReadOnlyList<SceneResourceIdAndLocation>> GetSceneGroupResourceIdAndPositions(int sceneGroupResourceId)
        {
            using (var activity = StartActivity(nameof(GetSceneGroupResourceIdAndPositions)))
            {
                try
                {
                    activity?.SetParameters((nameof(sceneGroupResourceId), sceneGroupResourceId));

                    var resp = await impl.getSceneGroupResourceIdAndPositionsAsync(new inputMessageName1(sceneGroupResourceId) {}).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = WireList.MapPresent(resp.getSceneGroupResourceIdAndPositions2, mapSceneResourceIdAndLocation, activity, nameof(GetSceneGroupResourceIdAndPositions));

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

        public async Task<SceneResourceIdAndLocation?> GetScenePositionsForSceneValueResource(int sceneMemberResourceId)
        {
            using (var activity = StartActivity(nameof(GetScenePositionsForSceneValueResource)))
            {
                try
                {
                    activity?.SetParameters((nameof(sceneMemberResourceId), sceneMemberResourceId));

                    var resp = await impl.getScenePositionsForSceneValueResourceAsync(new inputMessageName2(sceneMemberResourceId) {}).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
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

        public async Task<IReadOnlyList<ResourceValue>> WaitForResourceValueChanges(int holdSeconds = 15)
        {
            using (var activity = StartActivity(nameof(WaitForResourceValueChanges)))
            {
                try
                {
                    activity?.SetParameters((nameof(holdSeconds), holdSeconds));

                    var resp = await impl.waitForResourceValueChangesAsync(new inputMessageName8() { waitForResourceValueChanges1 = holdSeconds }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = WireList.MapPresent(resp.waitForResourceValueChanges2, ResourceValueEnvelopeMapper.ToDomain, activity, nameof(WaitForResourceValueChanges));

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
            int holdSeconds = 15)
        {
            // Deliberately an async iterator, so the activity spans the iteration rather than the call that
            // creates it. Returning the stream from a plain method would stop and export the activity before the
            // polling loop had run at all, and every error the loop reports would land on a dead span.
            using var activity = StartActivity(nameof(GetResourceValueChanges));

            activity?.SetParameters(
                (nameof(resourceIds), resourceIds),
                (nameof(cancellationToken), cancellationToken),
                (nameof(holdSeconds), holdSeconds));

            IAsyncEnumerable<ResourceValue> changes = ServiceHelpers.GetResourceValueChanges(
                activity,
                resourceIds,
                EnableRuntimeValueNotifications,
                WaitForResourceValueChanges,
                DisableRuntimeValueNotifications,
                settings.AsyncContinueOnCapturedContext,
                cancellationToken,
                holdSeconds);

            await foreach (var change in changes.ConfigureAwait(settings.AsyncContinueOnCapturedContext))
            {
                yield return change;
            }
        }
    }
}