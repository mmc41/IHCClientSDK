using System.Threading.Tasks;
using System;
using System.Diagnostics;

namespace Ihc.App
{
    /// <summary>
    /// High level application service for retrieving a broad set of non-editable information from the IHC controller.
    /// This applications service is intended as a tech-agnostic backend for a GUI or console IHC admin application.
    /// Provides access to system status, versions, time settings, and controller information.
    /// Will auto-authenticate with provided settings unless already authenticated.
    /// </summary>
    public class InformationAppService : AppServiceBase, IDisposable, IAsyncDisposable
    {
        public readonly IAuthenticationService authService;
        private readonly IConfigurationService configService;
        private readonly ITimeManagerService timeService;
        private readonly IControllerService controllerService;
        private readonly ISmsModemService smsModemService;
        private readonly bool ownedServices;

        private IhcSettings settings;

        /// <summary>
        /// Create an InformationService instance with IhcSettings only.
        /// This constructor will internally create needed API services.
        /// </summary>
        /// <param name="settings">IHC configuration settings</param>
        public InformationAppService(IhcSettings settings)
        {
            this.settings = settings;
            this.authService = new AuthenticationService(settings);
            this.configService = new ConfigurationService(authService);
            this.timeService = new TimeManagerService(authService);
            this.controllerService = new ControllerService(authService);
            this.smsModemService = new SmsModemService(authService);
            this.ownedServices = true;
        }

        /// <summary>
        /// Create an InformationService instance with IhcSettings and all needed API services.
        /// Use this constructor when you already have service instances.
        /// </summary>
        /// <param name="settings">IHC configuration settings</param>
        /// <param name="authService">Auth manager service instance</param>
        /// <param name="configService">Configuration service instance</param>
        /// <param name="timeService">Time manager service instance</param>
        /// <param name="controllerService">Controller service instance</param>
        /// <param name="smsModemService">SMS modem service instance</param>
        public InformationAppService(IhcSettings settings, IAuthenticationService authService, IConfigurationService configService, ITimeManagerService timeService, IControllerService controllerService, ISmsModemService smsModemService)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.timeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
            this.controllerService = controllerService ?? throw new ArgumentNullException(nameof(controllerService));
            this.smsModemService = smsModemService ?? throw new ArgumentNullException(nameof(smsModemService));
            this.ownedServices = false;
        }

        /// <summary>
        /// Authenticate with the IHC controller if needed.
        /// </summary>
        private async Task EnsureAuthenticated()
        {
            if (!await authService.IsAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext))
            {
                await authService.Authenticate().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            }   
        }

        /// <summary>
        /// Get information model filled with relevant data from IHC system APIs.
        /// Retrieves uptime, time, versions, status, SD card info, SMS modem info, and time settings.
        /// Note: Some properties may be null if the API does not provide them or if the controller does not support them.
        /// </summary>
        /// <returns>InformationModel containing all available controller information</returns>
        public async Task<InformationModel> GetInformationModel()
        {
            using (var activity = StartActivity(nameof(GetInformationModel)))
            {
                try
                {                                    
                    await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    // Execute API calls sequentially - systemInfo provides uptime, time, and version data
                    var systemInfo = await configService.GetSystemInfo().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var controllerStatus = await controllerService.GetControllerState().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var sdCard = await controllerService.GetSDCardInfo().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var smsModemInfo = await smsModemService.GetSmsModemInfo().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    var retv = new InformationModel
                    {
                        Uptime = systemInfo?.Uptime != null ? TimeSpan.FromMilliseconds(systemInfo.Uptime) : default,
                        ControllerTime = systemInfo?.Realtimeclock ?? default,
                        SoftwareVersion = systemInfo?.Version,
                        ControllerStatus = controllerStatus,
                        SdCard = sdCard,
                        SmsModemVersion = systemInfo?.SmsModemSoftwareVersion,
                        SerialNumber = systemInfo?.SerialNumber,
                        ProductionDate = systemInfo?.ProductionDate,
                        HardwareVersion = systemInfo?.HWRevision,
                        IoVersion = systemInfo?.DatalineVersion,
                        RfVersion = systemInfo?.RFModuleSoftwareVersion,
                        RfSerialNumber = systemInfo?.RFModuleSerialNumber,
                        SoftwareDate = systemInfo?.SWDate ?? default,
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

        /// <summary>
        /// Dispose of owned services if they were created by this instance.
        /// </summary>
        public void Dispose()
        {
            if (ownedServices & authService!=null)
            {
                authService.Dispose();
            }
        }

        /// <summary>
        /// Async dispose of owned services if they were created by this instance.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (ownedServices && authService!=null)
            {
                await authService.DisposeAsync();
            }
        }
    }
}