using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Openapi;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC OpenAPIService for v3.0+ controllers without any of the soap distractions. It represents a subset of other services.
    *
    * The IHC service provided here does not appear to be fully functional, complete and perhaps not with same quality as other services. Probably best to use other services instead.
    */
    public interface IOpenAPIService : ICookieHandlerService
    {
        /**
        * Authenticate with the OpenAPI service using username and password.
        */
        public Task Authenticate(string userName, string password);

        /**
        * Get firmware version information.
        */
        public Task<FWVersion> GetFWVersion();

        /**
        * Get OpenAPI version number.
        */
        public Task<string> GetAPIVersion();

        /**
        * Get controller uptime.
        */
        public Task<TimeSpan> GetUptime();

        /**
        * Get current controller time.
        */
        public Task<DateTime> GetTime();

        /**
        * Check if an IHC project is available.
        */
        public Task<bool> IsIHCProjectAvailable();

        /**
        * Get resource IDs for all dataline inputs.
        */
        public Task<int[]> GetDatalineInputIDs();

        /**
        * Get resource IDs for all dataline outputs.
        */
        public Task<int[]> GetDatalineOutputIDs();

        /**
        * Reboot the controller immediately.
        */
        public Task DoReboot();

        /**
        * Ping the controller to verify connectivity.
        */
        public Task Ping();

        /**
        * Get current values for specified resource IDs.
        */
        public Task<ResourceValue[]> GetValues(int[] resourceIds);

        /**
        * Set values for multiple resources.
        */
        public Task<bool> SetValues(ResourceValue[] values);

        /**
        * Enable event subscription for specified resource IDs.
        */
        public Task EnableSubscription(int[] resourceIds);

        /**
        * Disable event subscription for specified resource IDs.
        */
        public Task DisableSubscription(int[] resourceIds);

        /**
        * Wait for resource value change events from subscribed resources.
        */
        public Task<EventPackage> WaitForEvents(int? timeout);

        /**
        * Get async stream of resource value changes for subscribed resources.
        */
        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(int[] resourceIds, CancellationToken cancellationToken = default, int timeout_between_waits_in_seconds = 15);

        /**
        * Get project information.
        */
        public Task<ProjectInfo> GetProjectInfo();

        /**
        * Get number of project segments.
        */
        public Task<int> GetIHCProjectNumberOfSegments();

        /**
        * Get project segmentation size in bytes.
        */
        public Task<int> GetIHCProjectSegmentationSize();

        /**
        * Get a specific project segment by index and version.
        */
        public Task<byte[]> GetIHCProjectSegment(int index, int majorVersion, int minorVersion);

        /**
        * Get scene project information.
        */
        public Task<SceneProjectInfo> GetSceneProjectInfo();

        /**
        * Get scene project segmentation size.
        */
        public Task<int> GetSceneProjectSegmentationSize();

        /**
        * Get a specific scene project segment by index.
        */
        public Task<byte[]> GetSceneProjectSegment(int index);

        /**
        * The IHC endpoint URL.
        */
        public string Endpoint { get; }
    }

    /**
    * A highlevel implementation of a client to the IHC OpenAPIService without exposing any of the soap distractions.
    *
    * Nb. Supported by v3.0+ controllers only.
    *
    */
    public class OpenAPIService : IOpenAPIService
    {
        private readonly ILogger logger;
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
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "OpenAPIService") { }

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
                resourceValue.ResourceID = e.m_resourceID;
                return resourceValue;
            }).ToArray() ?? Array.Empty<ResourceValue>();

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

        /**
        * Create an OpenAPIService instance for access to the IHC API related to the open api.
        *
        * <param name="logger">A logger instance. Alternatively, use NullLogger<YourClass>.Instance</param>
        * <param name="endpoint">IHC controller endpoint of form http://\<YOUR CONTROLLER IP ADDRESS\></param>
        */
        public OpenAPIService(ILogger logger, string endpoint)
        {
            this.logger = logger;
            this.endpoint = endpoint;
            this.cookieHandler = new CookieHandler(logger);
            this.impl = new SoapImpl(logger, cookieHandler, endpoint);
        }

        public async Task Authenticate(string userName, string password)
        {
            logger.LogInformation("IHC OpenAPI Authenticate called");
            var resp = await impl.authenticateAsync(new inputMessageName13() { authenticate1 = userName, authenticate2 = password });

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
            await impl.pingAsync(new inputMessageName10());
            return;
        }

        public async Task<FWVersion> GetFWVersion()
        {
            var result = await impl.getFWVersionAsync(new inputMessageName11());
            return mapFWVersion(result.getFWVersion1);
        }

        public async Task<string> GetAPIVersion()
        {
            var result = await impl.getAPIVersionAsync(new inputMessageName12());
            return result.getAPIVersion1.HasValue ? result.getAPIVersion1.Value.ToString() : "0";
        }

        public async Task<TimeSpan> GetUptime()
        {
            var result = await impl.getUptimeAsync(new inputMessageName9());
            return TimeSpan.FromMilliseconds(result.getUptime1.HasValue ? result.getUptime1.Value : 0);
        }

        public async Task<DateTime> GetTime()
        {
            var result = await impl.getTimeAsync(new inputMessageName8());
            return result.getTime1.ToDateTimeOffset().DateTime;
        }

        public async Task<bool> IsIHCProjectAvailable()
        {
            var result = await impl.isIHCProjectAvailableAsync(new inputMessageName16());
            return result.isIHCProjectAvailable1.HasValue ? result.isIHCProjectAvailable1.Value : false;
        }

        public async Task<int[]> GetDatalineInputIDs()
        {
            var result = await impl.getDatalineInputIDsAsync(new inputMessageName3());
            return result.getDatalineInputIDs1?.Select(r => r.resourceID).ToArray() ?? Array.Empty<int>();
        }

        public async Task<int[]> GetDatalineOutputIDs()
        {
            var result = await impl.getDatalineOutputIDsAsync(new inputMessageName4());
            return result.getDatalineOutputIDs1?.Select(r => r.resourceID).ToArray() ?? Array.Empty<int>();
        }

        public async Task DoReboot()
        {
            logger.LogWarning("IHC OpenAPI DoReboot called - controller will reboot");
            await impl.doRebootAsync(new inputMessageName15());
        }

        public async Task<ResourceValue[]> GetValues(int[] resourceIds)
        {
            var result = await impl.getValuesAsync(new inputMessageName6() { getValues1 = resourceIds });
            return result.getValues2?.Select(v => mapResourceValue(v)).ToArray() ?? Array.Empty<ResourceValue>();
        }

        public async Task<bool> SetValues(ResourceValue[] values)
        {
            var wsEvents = values.Select(v => mapToWSResourceValueEvent(v)).ToArray();
            var result = await impl.setValuesAsync(new inputMessageName7() { setValues1 = wsEvents });
            return result.setValues2.HasValue ? result.setValues2.Value : false;
        }

        public async Task EnableSubscription(int[] resourceIds)
        {
            await impl.enableSubscriptionAsync(new inputMessageName1() { enableSubscription1 = resourceIds });
        }

        public async Task DisableSubscription(int[] resourceIds)
        {
            await impl.disableSubscriptionAsync(new inputMessageName2() { disableSubscription1 = resourceIds });
        }

        public async Task<EventPackage> WaitForEvents(int? timeout)
        {
            var result = await impl.waitForEventsAsync(new inputMessageName5() { waitForEvents1 = timeout });
            return mapEventPackage(result.waitForEvents2);
        }

        /**
         * Returns an async stream of changes in specified resources.
         * Corresponds to EnableSubscription + WaitForEvents in a loop
         *
         * Nb. Internal timeout should be lower that system timeout or the call will fail after a couple of calls.
         * Limit seems to be maybe around 20s ?
         */
        public IAsyncEnumerable<ResourceValue> GetResourceValueChanges(int[] resourceIds, CancellationToken cancellationToken = default, int timeout_between_waits_in_seconds = 15)
        {
            return ServiceHelpers.GetResourceValueChanges(
                resourceIds,
                EnableSubscription,
                async (timeout) => (await WaitForEvents(timeout)).ResourceValueEvents,
                DisableSubscription,
                logger,
                cancellationToken,
                timeout_between_waits_in_seconds);
        }

        public async Task<ProjectInfo> GetProjectInfo()
        {
            var result = await impl.getProjectInfoAsync(new inputMessageName14());
            return mapProjectInfo(result.getProjectInfo1);
        }

        public async Task<int> GetIHCProjectNumberOfSegments()
        {
            var result = await impl.getIHCProjectNumberOfSegmentsAsync(new inputMessageName19());
            return result.getIHCProjectNumberOfSegments1.HasValue ? result.getIHCProjectNumberOfSegments1.Value : 0;
        }

        public async Task<int> GetIHCProjectSegmentationSize()
        {
            var result = await impl.getIHCProjectSegmentationSizeAsync(new inputMessageName18());
            return result.getIHCProjectSegmentationSize1.HasValue ? result.getIHCProjectSegmentationSize1.Value : 0;
        }

        public async Task<byte[]> GetIHCProjectSegment(int index, int majorVersion, int minorVersion)
        {
            var result = await impl.getIHCProjectSegmentAsync(new inputMessageName17()
            {
                getIHCProjectSegment1 = index,
                getIHCProjectSegment2 = majorVersion,
                getIHCProjectSegment3 = minorVersion
            });
            return result.getIHCProjectSegment4?.data ?? Array.Empty<byte>();
        }

        public async Task<SceneProjectInfo> GetSceneProjectInfo()
        {
            var result = await impl.getSceneProjectInfoAsync(new inputMessageName20());
            return mapSceneProjectInfo(result.getSceneProjectInfo1);
        }

        public async Task<int> GetSceneProjectSegmentationSize()
        {
            var result = await impl.getSceneProjectSegmentationSizeAsync(new inputMessageName21());
            return result.getSceneProjectSegmentationSize1.HasValue ? result.getSceneProjectSegmentationSize1.Value : 0;
        }

        public async Task<byte[]> GetSceneProjectSegment(int index)
        {
            var result = await impl.getSceneProjectSegmentAsync(new inputMessageName22()
            {
                getSceneProjectSegment1 = index
            });
            return result.getSceneProjectSegment2?.data ?? Array.Empty<byte>();
        }
    }
}