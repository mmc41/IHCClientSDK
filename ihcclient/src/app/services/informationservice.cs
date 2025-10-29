using System.Threading.Tasks;
using System;
using System.Diagnostics;

namespace Ihc.App
{
    /// <summary>
    /// Service for retrieving non-editable information from the IHC controller.
    /// Provides access to system status, versions, time settings, and controller information.
    /// Will auto-authenticate with provided settings unless already authenticated.
    /// </summary>
    public class InformationService : ServiceBase, IDisposable, IAsyncDisposable
    {
        public readonly IAuthenticationService authService;
        private readonly IConfigurationService configService;
        private readonly ITimeManagerService timeService;
        private readonly IOpenAPIService openApiService;
        private readonly IControllerService controllerService;
        private readonly ISmsModelService smsModemService;
        private readonly bool ownedServices;

        /// <summary>
        /// Create an InformationService instance with IhcSettings only.
        /// This constructor will internally create needed API services.
        /// </summary>
        /// <param name="settings">IHC configuration settings</param>
        public InformationService(IhcSettings settings)
            : base(settings)
        {
            this.authService = new AuthenticationService(settings);
            this.configService = new ConfigurationService(authService);
            this.timeService = new TimeManagerService(authService);
            this.openApiService = new OpenAPIService(settings);
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
        /// <param name="openApiService">OpenAPI service instance</param>
        /// <param name="controllerService">Controller service instance</param>
        /// <param name="smsModemService">SMS modem service instance</param>
        public InformationService(IhcSettings settings, IAuthenticationService authService, IConfigurationService configService, ITimeManagerService timeService, IOpenAPIService openApiService, IControllerService controllerService, ISmsModelService smsModemService)
            : base(settings)
        {
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.timeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
            this.openApiService = openApiService ?? throw new ArgumentNullException(nameof(openApiService));
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

                    // Launch all API calls in parallel for efficiency
                    var uptimeTask = openApiService.GetUptime();
                    var controllerTimeTask = openApiService.GetTime();
                    var softwareVersionTask = openApiService.GetFWVersion();
                    var controllerStatusTask = controllerService.GetControllerState();
                    var sdCardTask = controllerService.GetSDCardInfo();
                    var smsModemInfoTask = smsModemService.GetSmsModemInfo();
                    var systemInfoTask = configService.GetSystemInfo();

                    await Task.WhenAll(
                        uptimeTask,
                        controllerTimeTask,
                        softwareVersionTask,
                        controllerStatusTask,
                        sdCardTask,
                        smsModemInfoTask,
                        systemInfoTask
                    ).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    var systemInfo = await systemInfoTask;

                    var retv = new InformationModel
                    {
                        Uptime = await uptimeTask,
                        ControllerTime =systemInfo?.Realtimeclock ?? default,
                        SoftwareVersion = await softwareVersionTask,
                        ControllerStatus = await controllerStatusTask,
                        SdCard = await sdCardTask,
                        SmsModemVersion = systemInfo?.SmsModemSoftwareVersion,
                        SerialNumber = systemInfo?.SerialNumber,
                        ProductionDate = systemInfo?.ProductionDate,
                        HardwareVersion = systemInfo?.HWRevision,
                        IoVersion = systemInfo?.DatalineVersion,
                        RfVersion = systemInfo?.RFModuleSoftwareVersion,
                        RfSerialNumber = systemInfo?.RFModuleSerialNumber,
                        SoftwareDate = systemInfo?.SWDate ?? default,
                    };

                    activity?.SetReturnValue("InformationModel");
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
            if (ownedServices)
            {
                (smsModemService as IDisposable)?.Dispose();
                (controllerService as IDisposable)?.Dispose();
                (openApiService as IDisposable)?.Dispose();
                (configService as IDisposable)?.Dispose();
                (timeService as IDisposable)?.Dispose();
                (authService as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// Async dispose of owned services if they were created by this instance.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (ownedServices)
            {
                if (smsModemService is IAsyncDisposable smsModemDisposable)
                    await smsModemDisposable.DisposeAsync();
                else
                    (smsModemService as IDisposable)?.Dispose();

                if (controllerService is IAsyncDisposable controllerDisposable)
                    await controllerDisposable.DisposeAsync();
                else
                    (controllerService as IDisposable)?.Dispose();

                if (openApiService is IAsyncDisposable openApiDisposable)
                    await openApiDisposable.DisposeAsync();
                else
                    (openApiService as IDisposable)?.Dispose();

                if (configService is IAsyncDisposable configDisposable)
                    await configDisposable.DisposeAsync();
                else
                    (configService as IDisposable)?.Dispose();

                if (timeService is IAsyncDisposable timeDisposable)
                    await timeDisposable.DisposeAsync();
                else
                    (timeService as IDisposable)?.Dispose();

                if (authService is IAsyncDisposable authDisposable)
                    await authDisposable.DisposeAsync();
                else
                    (authService as IDisposable)?.Dispose();
            }
        }
    }
}