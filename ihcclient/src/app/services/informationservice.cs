using System.Threading.Tasks;
using System;
using System.Diagnostics;

namespace Ihc.App
{
    /// <summary>
    /// Service for retrieving non-editable information from the IHC controller.
    /// Provides access to network, DNS, time, and access control settings.
    /// Will auto-authenticate with provided settings unless already authenticated.
    /// </summary>
    public class InformationService : ServiceBase, IDisposable, IAsyncDisposable
    {
        public readonly IAuthenticationService authService;
        private readonly IConfigurationService configService;
        private readonly ITimeManagerService timeService;
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
        public InformationService(IhcSettings settings, IAuthenticationService authService, IConfigurationService configService, ITimeManagerService timeService)
            : base(settings)
        {
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.timeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
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
        /// Retrieves network settings, DNS servers, time settings, and access control configuration.
        /// </summary>
        /// <returns>InformationModel containing all controller information</returns>
        public async Task<InformationModel> GetInformationModel()
        {
            using (var activity = StartActivity(nameof(GetInformationModel)))
            {
                try
                {
                    await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    var networkTask = configService.GetNetworkSettings();
                    var dnsTask = configService.GetDNSServers();
                    var timeTask = timeService.GetSettings();
                    var accessControlTask = configService.GetWebAccessControl();

                    await Task.WhenAll(networkTask, dnsTask, timeTask, accessControlTask).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    var retv = new InformationModel
                    {
                        Network = await networkTask,
                        Dns = await dnsTask,
                        Time = await timeTask,
                        AccessControl = await accessControlTask
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