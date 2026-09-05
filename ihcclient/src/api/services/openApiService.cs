using System.Threading.Tasks;
using System;
using System.Globalization;
using System.Linq;
using Ihc.Soap.Openapi;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC OpenAPIService for v3.0+ controllers without any of the soap distractions. It represents a subset of other services.
    /// The IHC service provided here does not appear to be fully functional, complete and perhaps not with same quality as other services. Probably best to use other services instead.
    /// </summary>
    public interface IOpenAPIService : ICookieHandlerService, IIHCApiService
    {
        /// <summary>
        /// Login to IHC controller with user/password in predefined configuration settings. This method must be called prior to most other calls on other services.
        /// </summary>
        public Task Authenticate();

        /// <summary>
        /// Authenticate with the OpenAPI service overriding predefined configuration settings for username and password.
        /// </summary>
        /// <param name="userName">Username for authentication</param>
        /// <param name="password">Password for authentication</param>
        public Task Authenticate(string userName, string password);

        /// <summary>
        /// Get firmware version information.
        /// </summary>
        public Task<FWVersion?> GetFWVersion();

        /// <summary>
        /// Get OpenAPI version number.
        /// </summary>
        public Task<string> GetAPIVersion();

        /// <summary>
        /// Get controller uptime.
        /// </summary>
        public Task<TimeSpan> GetUptime();

        /// <summary>
        /// Get current controller time.
        /// </summary>
        public Task<DateTimeOffset> GetTime();

        /// <summary>
        /// Check if an IHC project is available.
        /// </summary>
        public Task<bool> IsIHCProjectAvailable();

        /// <summary>
        /// Get resource IDs for all dataline inputs.
        /// </summary>
        public Task<IReadOnlyList<int>> GetDatalineInputIDs();

        /// <summary>
        /// Get resource IDs for all dataline outputs.
        /// </summary>
        public Task<IReadOnlyList<int>> GetDatalineOutputIDs();

        /// <summary>
        /// Reboot the controller immediately.
        /// </summary>
        public Task DoReboot();

        /// <summary>
        /// Ping the controller to verify connectivity.
        /// </summary>
        public Task Ping();

        /// <summary>
        /// Get current values for specified resource IDs.
        /// </summary>
        /// <param name="resourceIds">Collection of resource IDs to get values for</param>
        public Task<IReadOnlyList<ResourceValue>> GetValues(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Set values for multiple resources.
        /// </summary>
        /// <param name="values">Collection of resource values to set</param>
        public Task<bool> SetValues(IReadOnlyList<ResourceValue> values);

        /// <summary>
        /// Enable event subscription for specified resource IDs.
        /// </summary>
        /// <param name="resourceIds">Collection of resource IDs to subscribe to</param>
        public Task EnableSubscription(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Disable event subscription for specified resource IDs.
        /// </summary>
        /// <param name="resourceIds">Collection of resource IDs to unsubscribe from</param>
        public Task DisableSubscription(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Wait for resource value change events from subscribed resources.
        /// </summary>
        /// <param name="timeout">Timeout in seconds</param>
        public Task<EventPackage> WaitForEvents(int timeout);

        /// <summary>
        /// Get async stream of resource value changes for subscribed resources.
        /// </summary>
        /// <param name="resourceIds">Collection of resource IDs to monitor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="timeout_between_waits_in_seconds">Timeout between waits in seconds</param>
        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(IReadOnlyList<int> resourceIds, CancellationToken cancellationToken = default, int timeout_between_waits_in_seconds = 15);

        /// <summary>
        /// Get project information.
        /// </summary>
        public Task<ProjectInfo?> GetProjectInfo();

        /// <summary>
        /// Get number of project segments.
        /// </summary>
        public Task<int> GetIHCProjectNumberOfSegments();

        /// <summary>
        /// Get project segmentation size in bytes.
        /// </summary>
        public Task<int> GetIHCProjectSegmentationSize();

        /// <summary>
        /// Get a specific project segment by index and version.
        /// </summary>
        /// <param name="index">Segment index</param>
        /// <param name="majorVersion">Major version number</param>
        /// <param name="minorVersion">Minor version number</param>
        public Task<ProjectSegment> GetIHCProjectSegment(int index, int majorVersion, int minorVersion);

        /// <summary>
        /// Get scene project information.
        /// </summary>
        public Task<SceneProjectInfo?> GetSceneProjectInfo();

        /// <summary>
        /// Get scene project segmentation size.
        /// </summary>
        public Task<int> GetSceneProjectSegmentationSize();

        /// <summary>
        /// Get a specific scene project segment by index.
        /// </summary>
        /// <param name="index">Segment index</param>
        public Task<SceneProjectSegment> GetSceneProjectSegment(int index);
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC OpenAPIService without exposing any of the soap distractions.
    /// Nb. Supported by v3.0+ controllers only.
    /// </summary>
    public class OpenAPIService : ServiceBase, IOpenAPIService
    {
        private readonly ICookieHandler cookieHandler;

        public ICookieHandler GetCookieHandler()
        {
            return cookieHandler;
        }

        /// <summary>The SOAP service name this wrapper posts to, shared with the endpoint the login
        /// diagnostics name so the two cannot drift apart.</summary>
        private const string SoapServiceName = "OpenAPIService";

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Openapi.OpenAPIService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, SoapServiceName) { }

            public Task<outputMessageName13> authenticateAsync(inputMessageName13 request)
            {
                string? cookie = null;

                var result = soapPost<outputMessageName13, inputMessageName13>("authenticate", request, resp =>
                {
                    // Use side-effect to capture cookie sice our post call only captures xml response.
                    cookie = SetCookieHeader.FirstOrNull(resp);
                });

                return result.ContinueWith<outputMessageName13>((r) =>
                {
                    var result = r.Result;
                    // Add cookie only on success.
                    if (result.authenticate3.HasValue && result.authenticate3.Value)
                    {
                        cookieHandler.SetCookie(cookie);
                    }
                    else
                    {
                        cookieHandler.SetCookie(null);
                    }
                    return result;
                });
            }

            public Task<outputMessageName2> disableSubscriptionAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("disableSubscription", request);
            }

            public Task<outputMessageName15> doRebootAsync(inputMessageName15 request)
            {
                return soapPost<outputMessageName15, inputMessageName15>("doReboot", request);
            }

            public Task<outputMessageName1> enableSubscriptionAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("enableSubscription", request);
            }

            public Task<outputMessageName12> getAPIVersionAsync(inputMessageName12 request)
            {
                return soapPost<outputMessageName12, inputMessageName12>("getAPIVersion", request);
            }

            public Task<outputMessageName3> getDatalineInputIDsAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("getDatalineInputIDs", request);
            }

            public Task<outputMessageName4> getDatalineOutputIDsAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("getDatalineOutputIDs", request);
            }

            public Task<outputMessageName11> getFWVersionAsync(inputMessageName11 request)
            {
                return soapPost<outputMessageName11, inputMessageName11>("getFWVersion", request);
            }

            public Task<outputMessageName19> getIHCProjectNumberOfSegmentsAsync(inputMessageName19 request)
            {
                return soapPost<outputMessageName19, inputMessageName19>("getIHCProjectNumberOfSegments", request);
            }

            public Task<outputMessageName17> getIHCProjectSegmentAsync(inputMessageName17 request)
            {
                return soapPost<outputMessageName17, inputMessageName17>("getIHCProjectSegment", request);
            }

            public Task<outputMessageName18> getIHCProjectSegmentationSizeAsync(inputMessageName18 request)
            {
                return soapPost<outputMessageName18, inputMessageName18>("getIHCProjectSegmentationSize", request);
            }

            public Task<outputMessageName14> getProjectInfoAsync(inputMessageName14 request)
            {
                return soapPost<outputMessageName14, inputMessageName14>("getProjectInfo", request);
            }

            public Task<outputMessageName20> getSceneProjectInfoAsync(inputMessageName20 request)
            {
                return soapPost<outputMessageName20, inputMessageName20>("getSceneProjectInfo", request);
            }

            public Task<outputMessageName22> getSceneProjectSegmentAsync(inputMessageName22 request)
            {
                return soapPost<outputMessageName22, inputMessageName22>("getSceneProjectSegment", request);
            }

            public Task<outputMessageName21> getSceneProjectSegmentationSizeAsync(inputMessageName21 request)
            {
                return soapPost<outputMessageName21, inputMessageName21>("getSceneProjectSegmentationSize", request);
            }

            public Task<outputMessageName8> getTimeAsync(inputMessageName8 request)
            {
                return soapPost<outputMessageName8, inputMessageName8>("getTime", request);
            }

            public Task<outputMessageName9> getUptimeAsync(inputMessageName9 request)
            {
                return soapPost<outputMessageName9, inputMessageName9>("getUptime", request);
            }

            public Task<outputMessageName6> getValuesAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("getValues", request);
            }

            public Task<outputMessageName16> isIHCProjectAvailableAsync(inputMessageName16 request)
            {
                return soapPost<outputMessageName16, inputMessageName16>("isIHCProjectAvailable", request);
            }

            public Task<outputMessageName10> pingAsync(inputMessageName10 request)
            {
                return soapPost<outputMessageName10, inputMessageName10>("ping", request);
            }

            public Task<outputMessageName7> setValuesAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("setValues", request);
            }

            public Task<outputMessageName5> waitForEventsAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("waitForEvents", request);
            }
        }

        private readonly Ihc.Soap.Openapi.OpenAPIService impl;

        // Helper methods for converting between OpenAPI SOAP types and high-level models

        private static FWVersion? mapFWVersion(Ihc.Soap.Openapi.WSVersionInfo? version)
        {
            return version != null ? new FWVersion()
            {
                MajorVersion = version.majorVersion,
                MinorVersion = version.minorVersion,
                BuildVersion = version.buildVersion
            } : null;
        }

        private static Ihc.Soap.Openapi.WSResourceValueEvent mapToWSResourceValueEvent(ResourceValue v)
        {
            return new Ihc.Soap.Openapi.WSResourceValueEvent()
            {
                m_resourceID = v.ResourceID,
                m_value = OpenApiResourceValueMapper.ToWire(v)
            };
        }

        private static EventPackage mapEventPackage(Ihc.Soap.Openapi.WSEventPackage? eventPackage)
        {
            if (eventPackage == null)
            {
                // A FABRICATED package: "not running, no events, no subscriptions" is a well-formed answer a
                // caller acts on, and none of it was read from the controller. The shape stays - WaitForEvents
                // polls this and a null would only move the problem - and the warning is what tells the two
                // apart afterwards.
                Activity.Current.AddWarning(
                    "The controller answered no event package; substituting an empty, not-running one.",
                    ("type", "AbsentEventPackage"));

                return new EventPackage()
                {
                    ResourceValueEvents = Array.Empty<ResourceValue>(),
                    ControllerExecutionRunning = false,
                    SubscriptionAmount = 0
                };
            }

            var events = eventPackage.resourceValueEvents?.Select(e =>
            {
                var resourceValue = OpenApiResourceValueMapper.ToDomain(e.m_value);
                if (resourceValue != null)
                {
                    resourceValue = resourceValue with { ResourceID = e.m_resourceID };
                }
                return resourceValue;
            }).OfType<ResourceValue>().ToArray();

            return new EventPackage()
            {
                ResourceValueEvents = events ?? Array.Empty<ResourceValue>(),
                ControllerExecutionRunning = eventPackage.controllerExecutionRunning,
                SubscriptionAmount = eventPackage.subscriptionAmount
            };
        }

        private static ProjectInfo? mapProjectInfo(Ihc.Soap.Openapi.WSProjectInfo? info)
        {
            return info != null ? new ProjectInfo()
            {
                VisualMinorVersion = info.visualMinorVersion,
                VisualMajorVersion = info.visualMajorVersion,
                ProjectMajorRevision = info.projectMajorRevision,
                ProjectMinorRevision = info.projectMinorRevision,
                Lastmodified = DateHelper.OrAbsentSentinel(info.lastmodified?.ToDateTimeOffset(), nameof(ProjectInfo.Lastmodified)),
                ProjectNumber = info.projectNumber,
                CustomerName = info.customerName,
                InstallerName = info.installerName
            } : null;
        }

        private static SceneProjectInfo? mapSceneProjectInfo(Ihc.Soap.Openapi.WSSceneProjectInfo? info)
        {
            return info != null ? new SceneProjectInfo()
            {
                Name = info.name,
                Size = info.size,
                Filepath = info.filepath,
                Remote = info.remote,
                Version = info.version,
                Created = DateHelper.OrAbsentSentinel(info.created?.ToDateTimeOffset(), nameof(SceneProjectInfo.Created)),
                LastModified = DateHelper.OrAbsentSentinel(info.lastmodified?.ToDateTimeOffset(), nameof(SceneProjectInfo.LastModified)),
                Description = info.description,
                Crc = info.crc
            } : null;
        }

        /// <summary>
        /// Create an OpenAPIService instance for access to the IHC API related to the open api.
        /// </summary>
        /// <param name="settings">IHC settings configuration</param>
        public OpenAPIService(IhcSettings settings)
            : base(settings)
        {
            this.cookieHandler = new CookieHandler(settings.LogSensitiveData);
            this.impl = new SoapImpl(cookieHandler, settings);
        }
        
        /// <summary>
        /// Create an OpenAPIService instance for access to the IHC API related to the open api, where 
        /// authentication is handled through AuthenticationService instead of this API.
        /// Warning: This is not the intended use case of this service but can be helpful in testing scenarios.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public OpenAPIService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.cookieHandler = authService.GetCookieHandler();
            this.impl = new SoapImpl(cookieHandler, settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal OpenAPIService(IAuthenticationService authService, Ihc.Soap.Openapi.OpenAPIService impl)
            : base(SettingsOf(authService))
        {
            this.cookieHandler = authService.GetCookieHandler();
            this.impl = impl;
        }

        public async Task Authenticate()
        {
            using (var activity = StartActivity(nameof(Authenticate)))
            {
                try
                {
                    await Authenticate(settings.UserName, settings.Password).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }   

        public async Task Authenticate(string userName, string password)
        {
            using (var activity = StartActivity(nameof(Authenticate)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(userName), userName),
                        (nameof(password), settings.LogSensitiveData ? password : UserConstants.REDACTED_PASSWORD)
                    );

                    var resp = await impl.authenticateAsync(new inputMessageName13() { authenticate1 = userName, authenticate2 = password }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    if (resp.authenticate3.HasValue && resp.authenticate3.Value)
                    {
                        return;
                    }
                    else
                    {
                        throw new ErrorWithCodeException(Errors.LOGIN_UNKNOWN_ERROR,
                            "Ihc server login failed for " + ServiceBaseImpl.UrlOf(settings, SoapServiceName));
                    }
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task Ping()
        {
            using (var activity = StartActivity(nameof(Ping)))
            {
                try
                {
                    await impl.pingAsync(new inputMessageName10()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    return;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<FWVersion?> GetFWVersion()
        {
            using (var activity = StartActivity(nameof(GetFWVersion)))
            {
                try
                {
                    var result = await impl.getFWVersionAsync(new inputMessageName11()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapFWVersion(result.getFWVersion1);

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

        public async Task<string> GetAPIVersion()
        {
            using (var activity = StartActivity(nameof(GetAPIVersion)))
            {
                try
                {
                    var result = await impl.getAPIVersionAsync(new inputMessageName12()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.getAPIVersion1.HasValue ? result.getAPIVersion1.Value.ToString(CultureInfo.InvariantCulture) : "0";

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

        public async Task<TimeSpan> GetUptime()
        {
            using (var activity = StartActivity(nameof(GetUptime)))
            {
                try
                {
                    var result = await impl.getUptimeAsync(new inputMessageName9()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = TimeSpan.FromMilliseconds(result.getUptime1.HasValue ? result.getUptime1.Value : 0);

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

        public async Task<DateTimeOffset> GetTime()
        {
            using (var activity = StartActivity(nameof(GetTime)))
            {
                try
                {
                    var result = await impl.getTimeAsync(new inputMessageName8()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = DateHelper.OrAbsentSentinel(result.getTime1?.ToDateTimeOffset(), nameof(GetTime));

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

        public async Task<bool> IsIHCProjectAvailable()
        {
            using (var activity = StartActivity(nameof(IsIHCProjectAvailable)))
            {
                try
                {
                    var result = await impl.isIHCProjectAvailableAsync(new inputMessageName16()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.isIHCProjectAvailable1.HasValue ? result.isIHCProjectAvailable1.Value : false;

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

        public async Task<IReadOnlyList<int>> GetDatalineInputIDs()
        {
            using (var activity = StartActivity(nameof(GetDatalineInputIDs)))
            {
                try
                {
                    var result = await impl.getDatalineInputIDsAsync(new inputMessageName3()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    IReadOnlyList<int> retv = result.getDatalineInputIDs1 != null ? result.getDatalineInputIDs1.Select(r => r.resourceID).ToList() : Array.Empty<int>();

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

        public async Task<IReadOnlyList<int>> GetDatalineOutputIDs()
        {
            using (var activity = StartActivity(nameof(GetDatalineOutputIDs)))
            {
                try
                {
                    var result = await impl.getDatalineOutputIDsAsync(new inputMessageName4()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    IReadOnlyList<int> retv = result.getDatalineOutputIDs1 != null ? result.getDatalineOutputIDs1.Select(r => r.resourceID).ToList() : Array.Empty<int>();

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

        public async Task DoReboot()
        {
            using (var activity = StartActivity(nameof(DoReboot)))
            {
                try
                {
                    // TODO raise activity event
                    await impl.doRebootAsync(new inputMessageName15()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<ResourceValue>> GetValues(IReadOnlyList<int> resourceIds)
        {
            ArgumentNullException.ThrowIfNull(resourceIds);

            using (var activity = StartActivity(nameof(GetValues)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(resourceIds), resourceIds));

                    var result = await impl.getValuesAsync(new inputMessageName6() { getValues1 = resourceIds.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    // A value on THIS wire carries no resource id of its own - unlike WSResourceValueEnvelope
                    // and WSResourceValueEvent, which do, and which is why the mappers for those may filter
                    // freely. Here a value is bound to its resource by POSITION in this array alone, so the
                    // position is also where the id a caller gets back comes from: without it every value in
                    // the list would answer ResourceID 0 and a caller holding more than one could not tell
                    // them apart. That makes the pairing load-bearing rather than incidental, so both ways it
                    // can break are refused - a count the request does not match, and an entry the wire left
                    // empty, either of which would slide values onto the wrong resource silently.
                    // The array itself is nullable here too, so "no values" arrives either as an omitted
                    // element or as an empty list; the two say the same thing and take the same count check.
                    // Letting the omitted one through as a successful empty list would tell a caller that
                    // asked for a resource that the read worked and there was nothing there.
                    WSResourceValue[] answered = result.getValues2 ?? [];
                    if (answered.Length != resourceIds.Count)
                    {
                        throw new InvalidOperationException(
                            $"The controller answered {answered.Length} values for {resourceIds.Count} " +
                            "requested resources; values are paired with the requested resources by position, " +
                            "so the response cannot be used.");
                    }

                    IReadOnlyList<ResourceValue> retv = answered.Select((v, i) =>
                    {
                        ResourceValue value = OpenApiResourceValueMapper.ToDomain(v)
                            ?? throw new InvalidOperationException(
                                $"The controller returned an empty value at position {i} of {answered.Length} " +
                                $"(resource {resourceIds[i]}); values are paired with the requested resources by " +
                                "position, so the response cannot be used.");
                        return value with { ResourceID = resourceIds[i] };
                    }).ToList();

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

        public async Task<bool> SetValues(IReadOnlyList<ResourceValue> values)
        {
            using (var activity = StartActivity(nameof(SetValues)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(values), values));

                    var wsEvents = values.Select(v => mapToWSResourceValueEvent(v)).ToArray();
                    var result = await impl.setValuesAsync(new inputMessageName7() { setValues1 = wsEvents }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.setValues2.HasValue ? result.setValues2.Value : false;

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

        public async Task EnableSubscription(IReadOnlyList<int> resourceIds)
        {
            using (var activity = StartActivity(nameof(EnableSubscription)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(resourceIds), resourceIds));

                    await impl.enableSubscriptionAsync(new inputMessageName1() { enableSubscription1 = resourceIds.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task DisableSubscription(IReadOnlyList<int> resourceIds)
        {
            using (var activity = StartActivity(nameof(DisableSubscription)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(resourceIds), resourceIds));

                    await impl.disableSubscriptionAsync(new inputMessageName2() { disableSubscription1 = resourceIds.ToArray() }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<EventPackage> WaitForEvents(int timeout)
        {
            using (var activity = StartActivity(nameof(WaitForEvents)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(timeout), timeout));

                    var result = await impl.waitForEventsAsync(new inputMessageName5() { waitForEvents1 = timeout }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapEventPackage(result.waitForEvents2);

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

        /// <summary>
        /// Returns an async stream of changes in specified resources.
        /// Corresponds to EnableSubscription + WaitForEvents in a loop.
        /// Nb. Internal timeout should be lower that system timeout or the call will fail after a couple of calls.
        /// Limit seems to be maybe around 20s.
        /// </summary>
        /// <param name="resourceIds">Array of resource IDs to monitor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="timeout_between_waits_in_seconds">Timeout between waits in seconds</param>
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
                EnableSubscription,
                async (timeout) => (await WaitForEvents(timeout).ConfigureAwait(settings.AsyncContinueOnCapturedContext)).ResourceValueEvents,
                DisableSubscription,
                settings.AsyncContinueOnCapturedContext,
                cancellationToken,
                timeout_between_waits_in_seconds);

            await foreach (var change in changes.ConfigureAwait(settings.AsyncContinueOnCapturedContext))
            {
                yield return change;
            }
        }

        public async Task<ProjectInfo?> GetProjectInfo()
        {
            using (var activity = StartActivity(nameof(GetProjectInfo)))
            {
                try
                {
                    var result = await impl.getProjectInfoAsync(new inputMessageName14()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapProjectInfo(result.getProjectInfo1);

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

        public async Task<int> GetIHCProjectNumberOfSegments()
        {
            using (var activity = StartActivity(nameof(GetIHCProjectNumberOfSegments)))
            {
                try
                {
                    var result = await impl.getIHCProjectNumberOfSegmentsAsync(new inputMessageName19()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    if (!result.getIHCProjectNumberOfSegments1.HasValue)
                    {
                        // A caller sizing a segmented download off this reads "no segments" and downloads
                        // nothing - the one substitution here that silently produces an empty result rather
                        // than a wrong number.
                        activity.AddWarning(
                            "The controller answered no project segment count; substituting 0.",
                            ("type", "AbsentWireValue"),
                            ("field", nameof(GetIHCProjectNumberOfSegments)));
                    }
                    var retv = result.getIHCProjectNumberOfSegments1.HasValue ? result.getIHCProjectNumberOfSegments1.Value : 0;

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

        public async Task<int> GetIHCProjectSegmentationSize()
        {
            using (var activity = StartActivity(nameof(GetIHCProjectSegmentationSize)))
            {
                try
                {
                    var result = await impl.getIHCProjectSegmentationSizeAsync(new inputMessageName18()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.getIHCProjectSegmentationSize1.HasValue ? result.getIHCProjectSegmentationSize1.Value : 0;

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

        public async Task<ProjectSegment> GetIHCProjectSegment(int index, int majorVersion, int minorVersion)
        {
            using (var activity = StartActivity(nameof(GetIHCProjectSegment)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(index), index),
                        (nameof(majorVersion), majorVersion),
                        (nameof(minorVersion), minorVersion));

                    var result = await impl.getIHCProjectSegmentAsync(new inputMessageName17()
                    {
                        getIHCProjectSegment1 = index,
                        getIHCProjectSegment2 = majorVersion,
                        getIHCProjectSegment3 = minorVersion
                    }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    var retv = new ProjectSegment
                    {
                        Data = result.getIHCProjectSegment4?.data ?? Array.Empty<byte>()
                    };

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

        public async Task<SceneProjectInfo?> GetSceneProjectInfo()
        {
            using (var activity = StartActivity(nameof(GetSceneProjectInfo)))
            {
                try
                {
                    var result = await impl.getSceneProjectInfoAsync(new inputMessageName20()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapSceneProjectInfo(result.getSceneProjectInfo1);

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

        public async Task<int> GetSceneProjectSegmentationSize()
        {
            using (var activity = StartActivity(nameof(GetSceneProjectSegmentationSize)))
            {
                try
                {
                    var result = await impl.getSceneProjectSegmentationSizeAsync(new inputMessageName21()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.getSceneProjectSegmentationSize1.HasValue ? result.getSceneProjectSegmentationSize1.Value : 0;

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

        public async Task<SceneProjectSegment> GetSceneProjectSegment(int index)
        {
            using (var activity = StartActivity(nameof(GetSceneProjectSegment)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(index), index));

                    var result = await impl.getSceneProjectSegmentAsync(new inputMessageName22()
                    {
                        getSceneProjectSegment1 = index
                    }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    var retv = new SceneProjectSegment
                    {
                        Data = result.getSceneProjectSegment2?.data ?? Array.Empty<byte>()
                    };

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
    }
}