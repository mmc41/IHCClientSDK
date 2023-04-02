using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Controller;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC ControllerService without any of the soap distractions.
    *
    * TODO: Add remaining operations.
    */
    public interface IControllerService
    {
        public Task<string> GetState();
        public Task<bool> IsIHCProjectAvailableAsync();
        public Task<bool> IsSDCardReadyAsync();
        public Task<SDInfo> getSDCardInfoAsync();
    }

    /**
    * A highlevel implementation of a client to the IHC ControllerService without exposing any of the soap distractions.
    *
    * TODO: Add remaining operations.
    */
    public class ControllerService : IControllerService
    {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Controller.ControllerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "ControllerService") { }

            public Task<outputMessageName12> enterProjectChangeModeAsync(inputMessageName12 request)
            {
                return soapPost<outputMessageName12, inputMessageName12>("enterProjectChangeMode", request);
            }

            public Task<outputMessageName13> exitProjectChangeModeAsync(inputMessageName13 request)
            {
                return soapPost<outputMessageName13, inputMessageName13>("exitProjectChangeMode", request);
            }

            public Task<outputMessageName2> getBackupAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("getBackup", request);
            }

            public Task<outputMessageName3> getIHCProjectAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("getIHCProject", request);
            }

            public Task<outputMessageName18> getIHCProjectNumberOfSegmentsAsync(inputMessageName18 request)
            {
                return soapPost<outputMessageName18, inputMessageName18>("getIHCProjectNumberOfSegments", request);
            }

            public Task<outputMessageName15> getIHCProjectSegmentAsync(inputMessageName15 request)
            {
                return soapPost<outputMessageName15, inputMessageName15>("getIHCProjectSegment", request);
            }

            public Task<outputMessageName17> getIHCProjectSegmentationSizeAsync(inputMessageName17 request)
            {
                return soapPost<outputMessageName17, inputMessageName17>("getIHCProjectSegmentationSize", request);
            }

            public Task<outputMessageName8> getProjectInfoAsync(inputMessageName8 request)
            {
                return soapPost<outputMessageName8, inputMessageName8>("getProjectInfo", request);
            }

            public Task<outputMessageName11> getS0MeterValueAsync(inputMessageName11 request)
            {
                return soapPost<outputMessageName11, inputMessageName11>("getS0MeterValue", request);
            }

            public Task<outputMessageName5> getSdCardInfoAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("getSdCardInfo", request);
            }

            public Task<outputMessageName1> getStateAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("getState", request);
            }

            public Task<outputMessageName14> isIHCProjectAvailableAsync(inputMessageName14 request)
            {
                return soapPost<outputMessageName14, inputMessageName14>("isIHCProjectAvailable", request);
            }

            public Task<outputMessageName9> isSDCardReadyAsync(inputMessageName9 request)
            {
                return soapPost<outputMessageName9, inputMessageName9>("isSDCardReady", request);
            }

            public Task<outputMessageName10> resetS0ValuesAsync(inputMessageName10 request)
            {
                return soapPost<outputMessageName10, inputMessageName10>("resetS0Values", request);
            }

            public Task<outputMessageName7> restoreAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("restore", request);
            }

            public Task<outputMessageName6> setS0ConsumptionAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("setS0Consumption", request);
            }

            public Task<outputMessageName20> setS0FiscalYearStartAsync(inputMessageName20 request)
            {
                return soapPost<outputMessageName20, inputMessageName20>("setS0FiscalYearStart", request);
            }

            public Task<outputMessageName4> storeIHCProjectAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("storeIHCProject", request);
            }

            public Task<outputMessageName16> storeIHCProjectSegmentAsync(inputMessageName16 request)
            {
                return soapPost<outputMessageName16, inputMessageName16>("storeIHCProjectSegment", request);
            }

            public Task<outputMessageName19> waitForControllerStateChangeAsync(inputMessageName19 request)
            {
                return soapPost<outputMessageName19, inputMessageName19>("waitForControllerStateChange", request);
            }
        }

        private readonly SoapImpl impl;

        /**
        * Create an ControllerService instance for access to the IHC API related to the controller itself.
        * <param name="authService">AuthenticationService instance</param>
        */
        public ControllerService(IAuthenticationService authService)
        {
            this.logger = authService.Logger;
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint);
        }

        // TODO: Implement remaining high level service.

        private SDInfo mapSDCardData(WSSdCardData e)
        {
            return new SDInfo()
            {
                Size = e.size,
                Free = e.free
            };
        }

        public async Task<bool> IsIHCProjectAvailableAsync()
        {
            var result = await impl.isIHCProjectAvailableAsync(new inputMessageName14() { });
            return result.isIHCProjectAvailable1.HasValue ? result.isIHCProjectAvailable1.Value : false;
        }

        public async Task<bool> IsSDCardReadyAsync()
        {
            var result = await impl.isSDCardReadyAsync(new inputMessageName9() { });
            return result.isSDCardReady1.HasValue ? result.isSDCardReady1.Value : false;
        }

        public async Task<SDInfo> getSDCardInfoAsync()
        {
          var result = await impl.getSdCardInfoAsync(new inputMessageName5() { });
          return result.getSdCardInfo1!=null ? mapSDCardData(result.getSdCardInfo1) : null;
        }

        public async Task<string> GetState()
        {
            var result = await impl.getStateAsync(new inputMessageName1() { });
            return result.getState1.state;
        }
    }
}