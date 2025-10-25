using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.App
{
    /// <summary>
    /// Service for managing administrator-related data including users, email, SMTP and network settings.
    /// Provides change tracking and efficient updates to the IHC controller. Will auto-authenticate
    /// with provided settings unless already authenticated.
    /// </summary>
    public class AdminService : ServiceBase, IDisposable, IAsyncDisposable
    {
        public readonly IAuthenticationService authService;
        private readonly IUserManagerService userService;
        private readonly IConfigurationService configService;
        private AdminModel _originalSnapshot;
        private readonly bool ownedServices;

        private volatile bool rebootRequiredFlag = false;

        /// <summary>
        /// Create an AdminService instance with IhcSettings only.
        /// This constructor will internally create needed API services.
        /// </summary>
        /// <param name="settings">IHC configuration settings</param>
        public AdminService(IhcSettings settings)
            : base(settings)
        {
            this.authService = new AuthenticationService(settings);
            this.userService = new UserManagerService(authService);
            this.configService = new ConfigurationService(authService);
            this.ownedServices = true;
            this.rebootRequiredFlag = false;
        }

        /// <summary>
        /// Create an AdminService instance with IhcSettings and all needed API services.
        /// Use this constructor when you already have service instances.
        /// </summary>
        /// <param name="settings">IHC configuration settings</param>
        /// <param name="authService">Auth manager service instance</param>
        /// <param name="userService">User manager service instance</param>
        /// <param name="configService">Configuration service instance</param>
        public AdminService(IhcSettings settings, IAuthenticationService authService, IUserManagerService userService, IConfigurationService configService)
            : base(settings)
        {
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.ownedServices = false;
            this.rebootRequiredFlag = false;
        }

        /// <summary>
        /// Indicates whether a reboot of the IHC controller is required after applying network-related changes.
        /// This flag is set to true when DNS servers, network settings, or WLAN settings are changed.
        /// </summary>
        public bool RebootRequired { get; private set; }

        /// <summary>
        /// Reboot the IHC controller if a reboot is required after applying changes.
        /// </summary>
        /// <returns></returns>
        public async Task RebootIfRequired()
        {
            if (rebootRequiredFlag)
            {
                await configService.DelayedReboot(1);
                rebootRequiredFlag = false;
            }
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
        /// Get administrator model filled with data from IHC controller APIs.
        /// Retrieves users, email control settings, and SMTP settings.
        /// Stores an internal snapshot for change detection.
        /// </summary>
        /// <returns>AdminModel containing all administrator data</returns>
        public async Task<AdminModel> GetModel()
        {
            using (var activity = StartActivity(nameof(GetModel)))
            {
                try
                {
                    await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    var model = await DoGetModel().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    // Create a deep copy for the snapshot to ensure changes to returned model don't affect snapshot
                    _originalSnapshot = new AdminModel
                    {
                        Users = new HashSet<IhcUser>(model.Users ?? Enumerable.Empty<IhcUser>()),
                        EmailControl = model.EmailControl,
                        SmtpSettings = model.SmtpSettings,
                        DnsServers = model.DnsServers,
                        NetworkSettings = model.NetworkSettings,
                        WebAccess = model.WebAccess,
                        WLanSettings = model.WLanSettings
                    };

                    activity?.SetReturnValue($"AdminModel(Users={model.Users?.Count ?? 0})");
                    return model;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        private async Task<AdminModel> DoGetModel()
        {
            var usersTask = userService.GetUsers(includePassword: true);
            var emailControlTask = configService.GetEmailControlSettings();
            var smtpTask = configService.GetSMTPSettings();
            var dnsTask = configService.GetDNSServers();
            var networkTask = configService.GetNetworkSettings();
            var webAccessTask = configService.GetWebAccessControl();
            var wlanTask = configService.GetWLanSettings();

            await Task.WhenAll(usersTask, emailControlTask, smtpTask, dnsTask, networkTask, webAccessTask, wlanTask)
                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            return new AdminModel
            {
                Users = await usersTask,
                EmailControl = await emailControlTask,
                SmtpSettings = await smtpTask,
                DnsServers = await dnsTask,
                NetworkSettings = await networkTask,
                WebAccess = await webAccessTask,
                WLanSettings = await wlanTask
            };
        }

        /// <summary>
        /// Save administrator model to the IHC controller.
        /// If no snapshot exists, loads current state from controller first.
        /// Detects changes between the original snapshot and the provided model,
        /// then applies only the changes to the controller via API calls.
        /// Updates the internal snapshot after successful save.
        /// </summary>
        /// <param name="model">Modified administrator model to save</param>
        public async Task Store(AdminModel model)
        {
            using (var activity = StartActivity(nameof(Store)))
            {
                try
                {
                    activity?.SetParameters((nameof(model), $"AdminModel(Users={model?.Users?.Count ?? 0})"));

                    await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    // Load snapshot from controller if not present
                    if (_originalSnapshot == null)
                    {
                        _originalSnapshot = await DoGetModel().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    }

                    var changes = DetectChanges(_originalSnapshot, model);
                    if (changes.Count > 0)
                    {
                        await ApplyChanges(changes).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    }

                    _originalSnapshot = model;

                    // Update RebootRequired property based on flag set during ApplyChanges
                    RebootRequired = rebootRequiredFlag;

                    activity?.SetTag("changes.count", changes.Count);
                    activity?.SetTag("reboot.required", RebootRequired);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Detect changes between original and current admin models.
        /// Uses Username-based comparison for user set operations,
        /// then deep equality for detecting actual property changes.
        /// </summary>
        private List<AdminChange> DetectChanges(AdminModel original, AdminModel current)
        {
            var changes = new List<AdminChange>();

            // User changes - Username-based shallow comparison for set operations
            var originalUsers = original.Users ?? new HashSet<IhcUser>();
            var currentUsers = current.Users ?? new HashSet<IhcUser>();

            // Build Username-based dictionaries for efficient lookup
            var originalByUsername = originalUsers.ToDictionary(u => u.Username);
            var currentByUsername = currentUsers.ToDictionary(u => u.Username);

            // Added users (Username in current but not in original)
            foreach (var username in currentByUsername.Keys.Except(originalByUsername.Keys))
            {
                changes.Add(new AdminChange
                {
                    ChangeType = AdminChangeType.UserAdded,
                    Payload = currentByUsername[username]
                });
            }

            // Deleted users (Username in original but not in current)
            foreach (var username in originalByUsername.Keys.Except(currentByUsername.Keys))
            {
                changes.Add(new AdminChange
                {
                    ChangeType = AdminChangeType.UserDeleted,
                    Payload = originalByUsername[username]
                });
            }

            // Updated users (Username in both, but properties differ)
            // Default IhcUser.Equals() does deep comparison of all properties
            foreach (var username in originalByUsername.Keys.Intersect(currentByUsername.Keys))
            {
                var origUser = originalByUsername[username];
                var currUser = currentByUsername[username];
                if (!origUser.Equals(currUser))
                {
                    changes.Add(new AdminChange
                    {
                        ChangeType = AdminChangeType.UserUpdated,
                        Payload = currUser
                    });
                }
            }

            // EmailControl changes - default record Equals() does deep comparison
            if (!Equals(original.EmailControl, current.EmailControl))
            {
                changes.Add(new AdminChange
                {
                    ChangeType = AdminChangeType.EmailControlChanged,
                    Payload = current.EmailControl
                });
            }

            // SMTP changes - default record Equals() does deep comparison
            if (!Equals(original.SmtpSettings, current.SmtpSettings))
            {
                changes.Add(new AdminChange
                {
                    ChangeType = AdminChangeType.SmtpSettingsChanged,
                    Payload = current.SmtpSettings
                });
            }

            // DNS server changes - default record Equals() does deep comparison
            if (!Equals(original.DnsServers, current.DnsServers))
            {
                changes.Add(new AdminChange
                {
                    ChangeType = AdminChangeType.DnsServersChanged,
                    Payload = current.DnsServers
                });
            }

            // Network settings changes - default record Equals() does deep comparison
            if (!Equals(original.NetworkSettings, current.NetworkSettings))
            {
                changes.Add(new AdminChange
                {
                    ChangeType = AdminChangeType.NetworkSettingsChanged,
                    Payload = current.NetworkSettings
                });
            }

            // Web access control changes - default record Equals() does deep comparison
            if (!Equals(original.WebAccess, current.WebAccess))
            {
                changes.Add(new AdminChange
                {
                    ChangeType = AdminChangeType.WebAccessChanged,
                    Payload = current.WebAccess
                });
            }

            // WLAN settings changes - default record Equals() does deep comparison
            if (!Equals(original.WLanSettings, current.WLanSettings))
            {
                changes.Add(new AdminChange
                {
                    ChangeType = AdminChangeType.WLanSettingsChanged,
                    Payload = current.WLanSettings
                });
            }

            return changes;
        }

        /// <summary>
        /// Apply detected changes to the IHC controller via API services.
        /// Sets RebootRequired flag if network-related changes are made.
        /// </summary>
        private async Task ApplyChanges(List<AdminChange> changes)
        {
            foreach (var change in changes)
            {
                switch (change.ChangeType)
                {
                    case AdminChangeType.UserAdded:
                        await userService.AddUser((IhcUser)change.Payload)
                            .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        break;

                    case AdminChangeType.UserUpdated:
                        await userService.UpdateUser((IhcUser)change.Payload)
                            .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        break;

                    case AdminChangeType.UserDeleted:
                        await userService.RemoveUser(((IhcUser)change.Payload).Username)
                            .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        break;

                    case AdminChangeType.EmailControlChanged:
                        await configService.SetEmailControlSettings((EmailControlSettings)change.Payload)
                            .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        break;

                    case AdminChangeType.SmtpSettingsChanged:
                        await configService.SetSMTPSettings((SMTPSettings)change.Payload)
                            .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        break;

                    case AdminChangeType.DnsServersChanged:
                        await configService.SetDNSServers((DNSServers)change.Payload)
                            .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        rebootRequiredFlag = true;
                        break;

                    case AdminChangeType.NetworkSettingsChanged:
                        await configService.SetNetworkSettings((NetworkSettings)change.Payload)
                            .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        rebootRequiredFlag = true;
                        break;

                    case AdminChangeType.WebAccessChanged:
                        await configService.SetWebAccessControl((WebAccessControl)change.Payload)
                            .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        break;

                    case AdminChangeType.WLanSettingsChanged:
                        await configService.SetWLanSettings((WLanSettings)change.Payload)
                            .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        rebootRequiredFlag = true;
                        break;
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
                (userService as IDisposable)?.Dispose();
                (configService as IDisposable)?.Dispose();
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
                if (userService is IAsyncDisposable userAsync)
                    await userAsync.DisposeAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                else
                    (userService as IDisposable)?.Dispose();

                if (configService is IAsyncDisposable configAsync)
                    await configAsync.DisposeAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                else
                    (configService as IDisposable)?.Dispose();

                if (authService is IAsyncDisposable authAsync)
                    await authAsync.DisposeAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                else
                    (authService as IDisposable)?.Dispose();
            }
        }
    }
}