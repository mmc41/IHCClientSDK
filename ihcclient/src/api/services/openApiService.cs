using System.Threading.Tasks;
using System;
using System.Globalization;
using System.Linq;
using Ihc.Soap.Openapi;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Net.Http;

namespace Ihc {
    /// <summary>
    /// The controller's open interface for third-party products, which the vendor calls the IHC API: mostly operations
    /// the other services also offer, behind a login of its own. IHC Administrator's access control opens it per route
    /// (Danish UI: "Åben for tredjepart produkter"). The vendor gives no support for it, so prefer the other services.
    /// No vendor client uses it; its operations are documented as their counterparts in the other services behave.
    /// </summary>
    public interface IOpenAPIService : ICookieHandlerService, IIHCApiService
    {
        /// <summary>
        /// Logs in with the user name and password from the settings, through this service's own login operation. Call
        /// it before this service's other operations.
        /// </summary>
        /// <remarks>A refused login throws <see cref="ErrorWithCodeException"/> with
        /// <see cref="Errors.LOGIN_UNKNOWN_ERROR"/>, as the reply says only whether it succeeded.</remarks>
        public Task Authenticate();

        /// <summary>
        /// Logs in as <see cref="Authenticate()"/> does, with the given user name and password in place of the
        /// settings'.
        /// </summary>
        /// <param name="userName">User name of the controller account; case-sensitive.</param>
        /// <param name="password">The account's password; case-sensitive, and sent as clear text in the request body,
        /// which only TLS (an <c>https</c> endpoint) protects.</param>
        /// <remarks>A refused login throws <see cref="ErrorWithCodeException"/> with
        /// <see cref="Errors.LOGIN_UNKNOWN_ERROR"/>, as the reply says only whether it succeeded.</remarks>
        public Task Authenticate(string userName, string password);

        /// <summary>
        /// Reads the controller's firmware version as major, minor and build numbers.
        /// </summary>
        /// <returns>The version, or <c>null</c> when the reply carries none.</returns>
        public Task<FWVersion?> GetFWVersion();

        /// <summary>
        /// Gets the OpenAPI version number.
        /// </summary>
        /// <returns>The controller's integer answer as a decimal string; <c>"0"</c> when the reply carries no
        /// value.</returns>
        public Task<string> GetAPIVersion();

        /// <summary>
        /// Reads the controller's uptime: milliseconds since it last started, as in
        /// <see cref="IConfigurationService.GetSystemInfo"/>; <c>TimeSpan.Zero</c> when the reply carries no value.
        /// </summary>
        public Task<TimeSpan> GetUptime();

        /// <summary>
        /// Reads the controller's local time, the clock <see cref="ITimeManagerService.GetCurrentLocalTime"/> reads.
        /// </summary>
        /// <returns>The controller's local time at a fixed +01:00 offset, so the instant is right only while the
        /// controller runs at UTC+1: the OpenAPI has no UTC clock to take the controller's current offset from, as
        /// <see cref="ITimeManagerService.GetCurrentLocalTime"/> does. <c>DateTimeOffset.MinValue</c> when the reply
        /// carries no readable date.</returns>
        public Task<DateTimeOffset> GetTime();

        /// <summary>
        /// Asks whether the controller holds a project, as <see cref="IControllerService.IsIHCProjectAvailable"/>
        /// does.
        /// </summary>
        /// <returns>The controller's answer; <c>false</c> also when the reply carries no value.</returns>
        public Task<bool> IsIHCProjectAvailable();

        /// <summary>
        /// Lists the resource ids of the dataline inputs, as
        /// <see cref="IResourceInteractionService.GetAllDatalineInputs"/> does; empty when the reply carries no list.
        /// The dataline number the controller sends with each id is dropped.
        /// </summary>
        public Task<IReadOnlyList<int>> GetDatalineInputIDs();

        /// <summary>
        /// Lists the resource ids of the dataline outputs, as
        /// <see cref="IResourceInteractionService.GetAllDatalineOutputs"/> does; empty when the reply carries no list.
        /// The dataline number the controller sends with each id is dropped.
        /// </summary>
        public Task<IReadOnlyList<int>> GetDatalineOutputIDs();

        /// <summary>
        /// Restarts the controller, which ends the session; log in again once it is back. As with
        /// <see cref="IConfigurationService.DelayedReboot"/>, the controller replies first and then restarts.
        /// </summary>
        public Task DoReboot();

        /// <summary>
        /// Pings the controller to check that it answers.
        /// </summary>
        public Task Ping();

        /// <summary>
        /// Reads the given resources' current (runtime) values, as
        /// <see cref="IResourceInteractionService.GetRuntimeValues"/> does. The reply carries no resource ids, so the
        /// SDK pairs values with the requested ids by position.
        /// </summary>
        /// <param name="resourceIds">Ids of the resources to read.</param>
        /// <returns>One value per requested id, in request order, with <see cref="ResourceValue.ResourceID"/> set from
        /// the request; empty for a request of no ids, also when the controller answers it in its empty-list form, one
        /// nil item.</returns>
        /// <exception cref="InvalidOperationException">The reply holds a different number of values than ids were
        /// requested, or a value is missing.</exception>
        public Task<IReadOnlyList<ResourceValue>> GetValues(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Writes current (runtime) values to their resources in one call, as
        /// <see cref="IResourceInteractionService.SetResourceValues"/> does; this call has no initial-value form, so
        /// the SDK ignores <see cref="ResourceValue.IsValueRuntime"/>.
        /// </summary>
        /// <param name="values">Values to write, each addressed by its <see cref="ResourceValue.ResourceID"/>.</param>
        /// <returns><c>true</c> when the controller accepted the values; what a <c>false</c> answer means is not
        /// known, as for <see cref="IResourceInteractionService.SetResourceValues"/>. <c>false</c> also when the reply
        /// carries no value.</returns>
        /// <exception cref="ArgumentException">A value cannot be written, refused alike by
        /// <see cref="IResourceInteractionService.SetResourceValues"/>; nothing is sent.</exception>
        public Task<bool> SetValues(IReadOnlyList<ResourceValue> values);

        /// <summary>
        /// Subscribes to value changes of the given resources, which <see cref="WaitForEvents"/> then reports; the
        /// same pattern as the value notifications of <see cref="IResourceInteractionService"/>.
        /// </summary>
        /// <param name="resourceIds">Ids of the resources to follow.</param>
        public Task EnableSubscription(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Ends the subscription to value changes of the given resources.
        /// </summary>
        /// <param name="resourceIds">Ids of the resources to stop following.</param>
        public Task DisableSubscription(IReadOnlyList<int> resourceIds);

        /// <summary>
        /// Waits for value changes of the subscribed resources and returns them with two controller status fields. The
        /// controller holds the call until a change arrives or <paramref name="holdSeconds"/> runs out, as
        /// <see cref="IResourceInteractionService.WaitForResourceValueChanges"/> does.
        /// </summary>
        /// <param name="holdSeconds">Longest time, in seconds, the controller holds the call.</param>
        /// <returns>Never <c>null</c>: when the reply carries no package the SDK returns an empty one whose
        /// <see cref="EventPackage.ControllerExecutionRunning"/> is <c>false</c>.</returns>
        public Task<EventPackage> WaitForEvents(int holdSeconds);

        /// <summary>
        /// Streams value changes of the given resources: subscribes once, calls <see cref="WaitForEvents"/> in a loop
        /// until cancelled, and unsubscribes when the enumeration ends. Failed waits are retried with a growing pause
        /// until too many fail in a row, and the last error is then thrown.
        /// </summary>
        /// <param name="resourceIds">Ids of the resources to follow.</param>
        /// <param name="cancellationToken">Stops the stream; a wait already in flight is not interrupted.</param>
        /// <param name="holdSeconds">Longest time, in seconds, the controller holds each
        /// <see cref="WaitForEvents"/> call.</param>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled during the
        /// short rest between waits or the pause after a failed one: the enumeration then throws. Cancelled while a
        /// wait is held, the stream yields that wait's changes and ends quietly. Either way the subscription is
        /// ended.</exception>
        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(IReadOnlyList<int> resourceIds, CancellationToken cancellationToken = default, int holdSeconds = 15);

        /// <summary>
        /// Reads information on the project stored on the controller: the record
        /// <see cref="IControllerService.GetProjectInfo"/> returns.
        /// </summary>
        /// <returns>The project information, or <c>null</c> when the reply carries none.</returns>
        public Task<ProjectInfo?> GetProjectInfo();

        /// <summary>
        /// Reads how many segments the stored project downloads in through <see cref="GetIHCProjectSegment"/>, as
        /// <see cref="IControllerService.GetIHCProjectNumberOfSegments"/> does; 0 when the reply carries no value.
        /// </summary>
        public Task<int> GetIHCProjectNumberOfSegments();

        /// <summary>
        /// Reads the project segment size in bytes of gzip-compressed data, as
        /// <see cref="IControllerService.GetIHCProjectSegmentationSize"/> does; 0 when the reply carries no value.
        /// </summary>
        public Task<int> GetIHCProjectSegmentationSize();

        /// <summary>
        /// Downloads one segment of the stored project, a slice of the controller's own gzip stream of it, as
        /// <see cref="IControllerService.GetIHCProjectSegment"/> does, but returns the slice raw: joined in index
        /// order, the segments decompress as a whole.
        /// </summary>
        /// <param name="index">Segment index, 0-based, below <see cref="GetIHCProjectNumberOfSegments"/>.</param>
        /// <param name="projectMajorRevision">The <see cref="ProjectInfo.ProjectMajorRevision"/> from
        /// <see cref="GetProjectInfo"/>.</param>
        /// <param name="projectMinorRevision">The <see cref="ProjectInfo.ProjectMinorRevision"/> from
        /// <see cref="GetProjectInfo"/>.</param>
        /// <returns>The segment; its data is empty when the reply carries no file.</returns>
        public Task<ProjectSegment> GetIHCProjectSegment(int index, int projectMajorRevision, int projectMinorRevision);

        /// <summary>
        /// Reads information on the scene project stored on the controller: the record
        /// <see cref="IModuleService.GetSceneProjectInfo"/> returns. This service can only download scene projects.
        /// </summary>
        /// <returns>The scene project information, or <c>null</c> when the reply carries none.</returns>
        public Task<SceneProjectInfo?> GetSceneProjectInfo();

        /// <summary>
        /// Reads the scene project segment size in bytes, as
        /// <see cref="IModuleService.GetSceneProjectSegmentationSize"/> does; 0 when the reply carries no value.
        /// </summary>
        public Task<int> GetSceneProjectSegmentationSize();

        /// <summary>
        /// Downloads one segment of the stored scene project's .icz file, as
        /// <see cref="IModuleService.GetSceneProjectSegment"/> does but without a project name. Ask for segments 0, 1,
        /// 2 and so on, joining their data in order; their count is the scene project's
        /// <see cref="SceneProjectInfo.Size"/> divided by the segment size, rounded up, and a segment past the last
        /// comes back empty.
        /// </summary>
        /// <param name="index">Zero-based segment index.</param>
        /// <returns>The segment; its data is empty when the reply carries no file.</returns>
        public Task<SceneProjectSegment> GetSceneProjectSegment(int index);
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC OpenAPIService without exposing any of the soap distractions.
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
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings, HttpClient? transport)
                : base(cookieHandler, settings, SoapServiceName, transport) { }

            public async Task<outputMessageName13> authenticateAsync(inputMessageName13 request)
            {
                string? cookie = null;

                // Withholds the cookie as AuthenticationService's login does - by the same reasoning, not by a
                // recording of this service: a login opens a new session, and a stored cookie may name one a
                // controller restart has ended. Awaited for the same reason as that login: a continuation
                // reading Result would wrap every fault and turn a cancellation into one.
                var result = await soapPost<outputMessageName13, inputMessageName13>("authenticate", request, resp =>
                {
                    // Use side-effect to capture cookie sice our post call only captures xml response.
                    cookie = SetCookieHeader.FirstOrNull(resp);
                }, sendSessionCookie: false).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

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

            // A controller answers an empty list as one nil item, so a nil item is skipped rather than read.
            var events = eventPackage.resourceValueEvents is null ? null : WireList.MapPresent(eventPackage.resourceValueEvents, e =>
            {
                var resourceValue = OpenApiResourceValueMapper.ToDomain(e.m_value);
                if (resourceValue != null)
                {
                    resourceValue = resourceValue with { ResourceID = e.m_resourceID };
                }
                return resourceValue;
            }, Activity.Current, nameof(WaitForEvents)).ToArray();

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
            this.impl = new SoapImpl(cookieHandler, settings, transport: null);
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
            this.impl = new SoapImpl(cookieHandler, settings, transport: null);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal OpenAPIService(IAuthenticationService authService, Ihc.Soap.Openapi.OpenAPIService impl)
            : base(SettingsOf(authService))
        {
            this.cookieHandler = authService.GetCookieHandler();
            this.impl = impl;
        }

        /// <summary>
        /// Test seam: substitute the HTTP transport (used by unit tests only). The fake-SOAP seam above replaces
        /// the whole exchange and with it the request headers; this one replaces only the socket, so the request
        /// is built exactly as in production - which is what a test of the login's headers needs.
        /// </summary>
        /// <remarks>
        /// Standalone, like the settings constructor: the service owns its login and mints its own cookie
        /// handler. The transport is REQUIRED for the reason it is on <see cref="AuthenticationService"/>'s
        /// seam - taking one is then a truthful claim that the caller supplied it, which the controller-reach
        /// guard reads off the signature.
        /// </remarks>
        internal OpenAPIService(IhcSettings settings, HttpClient transport)
            : base(settings)
        {
            ArgumentNullException.ThrowIfNull(transport);
            this.cookieHandler = new CookieHandler(settings.LogSensitiveData);
            this.impl = new SoapImpl(cookieHandler, settings, transport);
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

        // A controller answers an empty list as one nil item, so a nil item is skipped rather than read, as the
        // resource service's dataline lists, which these mirror, already skip it.
        private static IReadOnlyList<int> datalineIds(WSDatalineResource[]? resources, Activity? activity, string field)
        {
            if (resources == null)
                return Array.Empty<int>();
            return WireList.MapPresent(resources, resource => resource, activity, field).Select(resource => resource.resourceID).ToList();
        }

        public async Task<IReadOnlyList<int>> GetDatalineInputIDs()
        {
            using (var activity = StartActivity(nameof(GetDatalineInputIDs)))
            {
                try
                {
                    var result = await impl.getDatalineInputIDsAsync(new inputMessageName3()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    IReadOnlyList<int> retv = datalineIds(result.getDatalineInputIDs1, activity, nameof(GetDatalineInputIDs));

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
                    IReadOnlyList<int> retv = datalineIds(result.getDatalineOutputIDs1, activity, nameof(GetDatalineOutputIDs));

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
                    // A controller answers an empty list as one nil item. Only for a request of no resources is
                    // that the empty answer; with a resource asked for, it is that resource's empty entry.
                    if (resourceIds.Count == 0 && answered is [null])
                        answered = [];
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

        public async Task<EventPackage> WaitForEvents(int holdSeconds)
        {
            using (var activity = StartActivity(nameof(WaitForEvents)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(holdSeconds), holdSeconds));

                    var result = await impl.waitForEventsAsync(new inputMessageName5() { waitForEvents1 = holdSeconds }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
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
        /// </summary>
        /// <param name="resourceIds">Array of resource IDs to monitor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="holdSeconds">Longest time, in seconds, the controller holds each wait</param>
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
                EnableSubscription,
                async (hold) => (await WaitForEvents(hold).ConfigureAwait(settings.AsyncContinueOnCapturedContext)).ResourceValueEvents,
                DisableSubscription,
                settings.AsyncContinueOnCapturedContext,
                cancellationToken,
                holdSeconds);

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

        public Task<ProjectSegment> GetIHCProjectSegment(int index, int projectMajorRevision, int projectMinorRevision) =>
            TracedSegmentDownload(nameof(GetIHCProjectSegment), index, projectMajorRevision, projectMinorRevision, async () =>
                (await impl.getIHCProjectSegmentAsync(new inputMessageName17()
                {
                    getIHCProjectSegment1 = index,
                    getIHCProjectSegment2 = projectMajorRevision,
                    getIHCProjectSegment3 = projectMinorRevision
                }).ConfigureAwait(settings.AsyncContinueOnCapturedContext)).getIHCProjectSegment4?.data);

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