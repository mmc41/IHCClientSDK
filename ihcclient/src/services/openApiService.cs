using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
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
    public interface IOpenAPIService : ICookieHandlerService, IIHCService
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
        public Task<FWVersion> GetFWVersion();

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
        public Task<DateTime> GetTime();

        /// <summary>
        /// Check if an IHC project is available.
        /// </summary>
        public Task<bool> IsIHCProjectAvailable();

        /// <summary>
        /// Get resource IDs for all dataline inputs.
        /// </summary>
        public Task<int[]> GetDatalineInputIDs();

        /// <summary>
        /// Get resource IDs for all dataline outputs.
        /// </summary>
        public Task<int[]> GetDatalineOutputIDs();

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
        /// <param name="resourceIds">Array of resource IDs to get values for</param>
        public Task<ResourceValue[]> GetValues(int[] resourceIds);

        /// <summary>
        /// Set values for multiple resources.
        /// </summary>
        /// <param name="values">Array of resource values to set</param>
        public Task<bool> SetValues(ResourceValue[] values);

        /// <summary>
        /// Enable event subscription for specified resource IDs.
        /// </summary>
        /// <param name="resourceIds">Array of resource IDs to subscribe to</param>
        public Task EnableSubscription(int[] resourceIds);

        /// <summary>
        /// Disable event subscription for specified resource IDs.
        /// </summary>
        /// <param name="resourceIds">Array of resource IDs to unsubscribe from</param>
        public Task DisableSubscription(int[] resourceIds);

        /// <summary>
        /// Wait for resource value change events from subscribed resources.
        /// </summary>
        /// <param name="timeout">Timeout in seconds</param>
        public Task<EventPackage> WaitForEvents(int? timeout);

        /// <summary>
        /// Get async stream of resource value changes for subscribed resources.
        /// </summary>
        /// <param name="resourceIds">Array of resource IDs to monitor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="timeout_between_waits_in_seconds">Timeout between waits in seconds</param>
        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(int[] resourceIds, CancellationToken cancellationToken = default, int timeout_between_waits_in_seconds = 15);

        /// <summary>
        /// Get project information.
        /// </summary>
        public Task<ProjectInfo> GetProjectInfo();

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
        public Task<byte[]> GetIHCProjectSegment(int index, int majorVersion, int minorVersion);

        /// <summary>
        /// Get scene project information.
        /// </summary>
        public Task<SceneProjectInfo> GetSceneProjectInfo();

        /// <summary>
        /// Get scene project segmentation size.
        /// </summary>
        public Task<int> GetSceneProjectSegmentationSize();

        /// <summary>
        /// Get a specific scene project segment by index.
        /// </summary>
        /// <param name="index">Segment index</param>
        public Task<byte[]> GetSceneProjectSegment(int index);

        /// <summary>
        /// The IHC endpoint URL.
        /// </summary>
        public string Endpoint { get; }
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC OpenAPIService without exposing any of the soap distractions.
    /// Nb. Supported by v3.0+ controllers only.
    /// </summary>
    public class OpenAPIService : ServiceBase, IOpenAPIService
    {
        private readonly string endpoint;
        private readonly ICookieHandler cookieHandler;

        public ICookieHandler GetCookieHandler()
        {
            return cookieHandler;
        }

        public string Endpoint { 
          get {
            return endpoint;
          } 
        }

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Openapi.OpenAPIService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, IhcSettings settings) : base(logger, cookieHandler, settings, "OpenAPIService") { }

            public Task<outputMessageName13> authenticateAsync(inputMessageName13 request)
            {
                string cookie = null;

                var result = soapPost<outputMessageName13, inputMessageName13>("authenticate", request, resp =>
                {
                    // Use side-effect to capture cookie sice our post call only captures xml response.
                    cookie = resp.Headers.GetValues("Set-Cookie").FirstOrDefault();
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

        private readonly SoapImpl impl;

        // Helper methods for converting between OpenAPI SOAP types and high-level models

        private FWVersion mapFWVersion(Ihc.Soap.Openapi.WSVersionInfo version)
        {
            return version != null ? new FWVersion()
            {
                MajorVersion = version.majorVersion,
                MinorVersion = version.minorVersion,
                BuildVersion = version.buildVersion
            } : null;
        }

        private ResourceValue mapResourceValue(Ihc.Soap.Openapi.WSResourceValue v)
        {
            if (v == null)
                return null;

            var value = new ResourceValue.UnionValue() { };

            if (v is Ihc.Soap.Openapi.WSBooleanValue boolVal)
            {
                value.BoolValue = boolVal.value;
                value.ValueKind = ResourceValue.ValueKind.BOOL;
            }
            else if (v is Ihc.Soap.Openapi.WSIntegerValue intVal)
            {
                value.IntValue = intVal.integer;
                value.ValueKind = ResourceValue.ValueKind.INT;
            }
            else if (v is Ihc.Soap.Openapi.WSFloatingPointValue floatVal)
            {
                value.DoubleValue = floatVal.floatingPointValue;
                value.ValueKind = ResourceValue.ValueKind.DOUBLE;
            }
            else if (v is Ihc.Soap.Openapi.WSEnumValue enumVal)
            {
                value.EnumValue = new EnumValue()
                {
                    DefinitionTypeID = enumVal.definitionTypeID,
                    EnumValueID = enumVal.enumValueID,
                    EnumName = enumVal.enumName
                };
                value.ValueKind = ResourceValue.ValueKind.ENUM;
            }
            else if (v is Ihc.Soap.Openapi.WSDateValue dateVal)
            {
                value.DateValue = new DateTimeOffset(dateVal.year, dateVal.month, dateVal.day, 0, 0, 0, DateHelper.GetWSTimeOffset());
                value.ValueKind = ResourceValue.ValueKind.DATE;
            }
            else if (v is Ihc.Soap.Openapi.WSTimeValue timeVal)
            {
                value.TimeValue = new TimeSpan(timeVal.hours, timeVal.minutes, timeVal.seconds);
                value.ValueKind = ResourceValue.ValueKind.TIME;
            }
            else if (v is Ihc.Soap.Openapi.WSTimerValue timerVal)
            {
                value.TimerValue = timerVal.milliseconds;
                value.ValueKind = ResourceValue.ValueKind.TIMER;
            }
            else if (v is Ihc.Soap.Openapi.WSWeekdayValue weekdayVal)
            {
                value.WeekdayValue = weekdayVal.weekdayNumber;
                value.ValueKind = ResourceValue.ValueKind.WEEKDAY;
            }

            return new ResourceValue() { Value = value };
        }

        private Ihc.Soap.Openapi.WSResourceValue mapToWSResourceValue(ResourceValue v)
        {
            switch (v.Value.ValueKind)
            {
                case ResourceValue.ValueKind.BOOL:
                    return new Ihc.Soap.Openapi.WSBooleanValue() { value = (bool)v.Value.BoolValue };
                case ResourceValue.ValueKind.INT:
                    return new Ihc.Soap.Openapi.WSIntegerValue() { integer = (int)v.Value.IntValue };
                case ResourceValue.ValueKind.DOUBLE:
                    return new Ihc.Soap.Openapi.WSFloatingPointValue() { floatingPointValue = (double)v.Value.DoubleValue };
                case ResourceValue.ValueKind.ENUM:
                    return new Ihc.Soap.Openapi.WSEnumValue()
                    {
                        definitionTypeID = v.Value.EnumValue.DefinitionTypeID,
                        enumValueID = v.Value.EnumValue.EnumValueID,
                        enumName = v.Value.EnumValue.EnumName
                    };
                case ResourceValue.ValueKind.DATE:
                    var date = (DateTimeOffset)v.Value.DateValue;
                    return new Ihc.Soap.Openapi.WSDateValue()
                    {
                        year = (short)date.Year,
                        month = (sbyte)date.Month,
                        day = (sbyte)date.Day
                    };
                case ResourceValue.ValueKind.TIME:
                    var time = (TimeSpan)v.Value.TimeValue;
                    return new Ihc.Soap.Openapi.WSTimeValue()
                    {
                        hours = time.Hours,
                        minutes = time.Minutes,
                        seconds = time.Seconds
                    };
                case ResourceValue.ValueKind.TIMER:
                    return new Ihc.Soap.Openapi.WSTimerValue() { milliseconds = (long)v.Value.TimerValue };
                case ResourceValue.ValueKind.WEEKDAY:
                    return new Ihc.Soap.Openapi.WSWeekdayValue() { weekdayNumber = (int)v.Value.WeekdayValue };
                default:
                    throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "Support for value kind " + v.Value.ValueKind + " not (yet) implemented.");
            }
        }

        private Ihc.Soap.Openapi.WSResourceValueEvent mapToWSResourceValueEvent(ResourceValue v)
        {
            return new Ihc.Soap.Openapi.WSResourceValueEvent()
            {
                m_resourceID = v.ResourceID,
                m_value = mapToWSResourceValue(v)
            };
        }

        private EventPackage mapEventPackage(Ihc.Soap.Openapi.WSEventPackage eventPackage)
        {
            if (eventPackage == null)
            {
                return new EventPackage()
                {
                    ResourceValueEvents = Array.Empty<ResourceValue>(),
                    ControllerExecutionRunning = false,
                    SubscriptionAmount = 0
                };
            }

            var events = eventPackage.resourceValueEvents?.Select(e =>
            {
                var resourceValue = mapResourceValue(e.m_value);
                if (resourceValue != null)
                {
                    resourceValue.ResourceID = e.m_resourceID;
                }
                return resourceValue;
            }).Where(rv => rv != null).ToArray();

            return new EventPackage()
            {
                ResourceValueEvents = events,
                ControllerExecutionRunning = eventPackage.controllerExecutionRunning,
                SubscriptionAmount = eventPackage.subscriptionAmount
            };
        }

        private ProjectInfo mapProjectInfo(Ihc.Soap.Openapi.WSProjectInfo info)
        {
            return info != null ? new ProjectInfo()
            {
                VisualMinorVersion = info.visualMinorVersion,
                VisualMajorVersion = info.visualMajorVersion,
                ProjectMajorRevision = info.projectMajorRevision,
                ProjectMinorRevision = info.projectMinorRevision,
                Lastmodified = info.lastmodified?.ToDateTimeOffset(),
                ProjectNumber = info.projectNumber,
                CustomerName = info.customerName,
                InstallerName = info.installerName
            } : null;
        }

        private SceneProjectInfo mapSceneProjectInfo(Ihc.Soap.Openapi.WSSceneProjectInfo info)
        {
            return info != null ? new SceneProjectInfo()
            {
                Name = info.name,
                Size = info.size,
                Filepath = info.filepath,
                Remote = info.remote,
                Version = info.version,
                Created = info.created?.ToDateTimeOffset().DateTime,
                LastModified = info.lastmodified?.ToDateTimeOffset().DateTime,
                Description = info.description,
                Crc = info.crc
            } : null;
        }

        /// <summary>
        /// Create an OpenAPIService instance for access to the IHC API related to the open api.
        /// </summary>
        /// <param name="logger">A logger instance. Alternatively, use NullLogger&lt;YourClass&gt;.Instance</param>
        /// <param name="settings">IHC settings configuration</param>
        public OpenAPIService(ILogger logger, IhcSettings settings)
            : base(logger, settings)
        {
            this.endpoint = settings.Endpoint;
            this.cookieHandler = new CookieHandler(logger, settings.LogSensitiveData);
            this.impl = new SoapImpl(logger, cookieHandler, settings);
        }

        public async Task Authenticate()
        {
            await Authenticate(settings.UserName, settings.Password).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }   

        public async Task Authenticate(string userName, string password)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(
                (nameof(userName), userName),
                (nameof(password), settings.AsyncContinueOnCapturedContext ? password : "***REDACTED***")
            );                

            logger.LogInformation("IHC OpenAPI Authenticate called");
            var resp = await impl.authenticateAsync(new inputMessageName13() { authenticate1 = userName, authenticate2 = password }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            if (resp.authenticate3.HasValue && resp.authenticate3.Value)
            {
                logger.LogInformation("IHC OpenAPI authentication successful");
                return;
            }
            else
            {
                logger.LogError("IHC OpenAPI authentication failed for endpoint {Url}", impl.Url);
                throw new ErrorWithCodeException(Errors.LOGIN_UNKNOWN_ERROR, "Ihc server login failed for " + impl.Url);
            }
        }

        public async Task Ping()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            await impl.pingAsync(new inputMessageName10()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            return;
        }

        public async Task<FWVersion> GetFWVersion()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getFWVersionAsync(new inputMessageName11()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = mapFWVersion(result.getFWVersion1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<string> GetAPIVersion()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getAPIVersionAsync(new inputMessageName12()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.getAPIVersion1.HasValue ? result.getAPIVersion1.Value.ToString() : "0";

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<TimeSpan> GetUptime()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getUptimeAsync(new inputMessageName9()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = TimeSpan.FromMilliseconds(result.getUptime1.HasValue ? result.getUptime1.Value : 0);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<DateTime> GetTime()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getTimeAsync(new inputMessageName8()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.getTime1 != null ? result.getTime1.ToDateTimeOffset().DateTime : DateTime.MinValue;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<bool> IsIHCProjectAvailable()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.isIHCProjectAvailableAsync(new inputMessageName16()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.isIHCProjectAvailable1.HasValue ? result.isIHCProjectAvailable1.Value : false;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<int[]> GetDatalineInputIDs()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getDatalineInputIDsAsync(new inputMessageName3()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.getDatalineInputIDs1 != null ? result.getDatalineInputIDs1.Select(r => r.resourceID).ToArray() : Array.Empty<int>();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<int[]> GetDatalineOutputIDs()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getDatalineOutputIDsAsync(new inputMessageName4()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.getDatalineOutputIDs1 != null ? result.getDatalineOutputIDs1.Select(r => r.resourceID).ToArray() : Array.Empty<int>();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task DoReboot()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            logger.LogWarning("IHC OpenAPI DoReboot called - controller will reboot");
            await impl.doRebootAsync(new inputMessageName15()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        public async Task<ResourceValue[]> GetValues(int[] resourceIds)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(
                (nameof(resourceIds), resourceIds));

            var result = await impl.getValuesAsync(new inputMessageName6() { getValues1 = resourceIds }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.getValues2 != null ? result.getValues2.Select(v => mapResourceValue(v)).ToArray() : Array.Empty<ResourceValue>();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<bool> SetValues(ResourceValue[] values)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(
                (nameof(values), values));

            var wsEvents = values.Select(v => mapToWSResourceValueEvent(v)).ToArray();
            var result = await impl.setValuesAsync(new inputMessageName7() { setValues1 = wsEvents }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.setValues2.HasValue ? result.setValues2.Value : false;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task EnableSubscription(int[] resourceIds)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(
                (nameof(resourceIds), resourceIds));

            await impl.enableSubscriptionAsync(new inputMessageName1() { enableSubscription1 = resourceIds }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        public async Task DisableSubscription(int[] resourceIds)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(
                (nameof(resourceIds), resourceIds));

            await impl.disableSubscriptionAsync(new inputMessageName2() { disableSubscription1 = resourceIds }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        public async Task<EventPackage> WaitForEvents(int? timeout)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(
                (nameof(timeout), timeout));

            var result = await impl.waitForEventsAsync(new inputMessageName5() { waitForEvents1 = timeout }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = mapEventPackage(result.waitForEvents2);

            activity?.SetReturnValue(retv);
            return retv;
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
        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(int[] resourceIds, CancellationToken cancellationToken = default, int timeout_between_waits_in_seconds = 15)
        {
            return ServiceHelpers.GetResourceValueChanges(
                resourceIds,
                EnableSubscription,
                async (timeout) => (await WaitForEvents(timeout).ConfigureAwait(settings.AsyncContinueOnCapturedContext)).ResourceValueEvents,
                DisableSubscription,
                logger,
                settings.AsyncContinueOnCapturedContext,
                cancellationToken,
                timeout_between_waits_in_seconds);
        }

        public async Task<ProjectInfo> GetProjectInfo()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getProjectInfoAsync(new inputMessageName14()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = mapProjectInfo(result.getProjectInfo1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<int> GetIHCProjectNumberOfSegments()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getIHCProjectNumberOfSegmentsAsync(new inputMessageName19()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.getIHCProjectNumberOfSegments1.HasValue ? result.getIHCProjectNumberOfSegments1.Value : 0;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<int> GetIHCProjectSegmentationSize()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getIHCProjectSegmentationSizeAsync(new inputMessageName18()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.getIHCProjectSegmentationSize1.HasValue ? result.getIHCProjectSegmentationSize1.Value : 0;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<byte[]> GetIHCProjectSegment(int index, int majorVersion, int minorVersion)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
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
            var retv = result.getIHCProjectSegment4?.data != null ? result.getIHCProjectSegment4.data : Array.Empty<byte>();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<SceneProjectInfo> GetSceneProjectInfo()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getSceneProjectInfoAsync(new inputMessageName20()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = mapSceneProjectInfo(result.getSceneProjectInfo1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<int> GetSceneProjectSegmentationSize()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getSceneProjectSegmentationSizeAsync(new inputMessageName21()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.getSceneProjectSegmentationSize1.HasValue ? result.getSceneProjectSegmentationSize1.Value : 0;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<byte[]> GetSceneProjectSegment(int index)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(
                (nameof(index), index));

            var result = await impl.getSceneProjectSegmentAsync(new inputMessageName22()
            {
                getSceneProjectSegment1 = index
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var retv = result.getSceneProjectSegment2?.data != null ? result.getSceneProjectSegment2.data : Array.Empty<byte>();

            activity?.SetReturnValue(retv);
            return retv;
        }
    }
}