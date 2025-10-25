using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.App
{
    /// <summary>
    /// Service for managing administrator-related data including users, email control, and SMTP settings.
    /// Provides change tracking and efficient updates to the IHC controller. Will auto-authenticate 
    /// with provided settings unless already authenticated.
    /// </summary>
    public class AdminService : IDisposable, IAsyncDisposable
    {
        public readonly IAuthenticationService authService;
        public readonly IhcSettings settings;
        private readonly IUserManagerService userService;
        private readonly IConfigurationService configService;
        private AdminModel _originalSnapshot;
        private readonly bool ownedServices;

        /// <summary>
        /// Create an AdminService instance with IhcSettings only.
        /// This constructor will internally create needed API services.
        /// </summary>
        /// <param name="settings">IHC configuration settings</param>
        public AdminService(IhcSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            this.authService = new AuthenticationService(settings);
            this.userService = new UserManagerService(authService);
            this.configService = new ConfigurationService(authService);
            this.ownedServices = true;
        }

        /// <summary>
        /// Create an AdminService instance with IhcSettings and all needed API services.
        /// Use this constructor when you already have service instances.
        /// </summary>
        /// <param name="settings">IHC configuration settings</param>
        /// <param name="userService">User manager service instance</param>
        /// <param name="configService">Configuration service instance</param>
        public AdminService(IhcSettings settings, IUserManagerService userService, IConfigurationService configService)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
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
        /// Get administrator model filled with data from IHC controller APIs.
        /// Retrieves users, email control settings, and SMTP settings.
        /// Stores an internal snapshot for change detection.
        /// </summary>
        /// <returns>AdminModel containing all administrator data</returns>
        public async Task<AdminModel> GetAdminModel()
        {
            await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            var model = await GetAdminModelInternal().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            // Create a deep copy for the snapshot to ensure changes to returned model don't affect snapshot
            _originalSnapshot = new AdminModel
            {
                Users = new HashSet<IhcUser>(model.Users ?? Enumerable.Empty<IhcUser>()),
                EmailControl = model.EmailControl,
                SmtpSettings = model.SmtpSettings
            };

            return model;
        }

        /// <summary>
        /// Internal method to fetch admin model from controller without updating snapshot.
        /// Used by both GetAdminModel() and SaveAdminModel() when snapshot is needed.
        /// </summary>
        private async Task<AdminModel> GetAdminModelInternal()
        {
            var usersTask = userService.GetUsers(includePassword: true);
            var emailControlTask = configService.GetEmailControlSettings();
            var smtpTask = configService.GetSMTPSettings();

            await Task.WhenAll(usersTask, emailControlTask, smtpTask).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            return new AdminModel
            {
                Users = await usersTask,
                EmailControl = await emailControlTask,
                SmtpSettings = await smtpTask
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
        public async Task SaveAdminModel(AdminModel model)
        {
            await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        
            // Load snapshot from controller if not present
            if (_originalSnapshot == null)
            {
                _originalSnapshot = await GetAdminModelInternal().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            }

            var changes = DetectChanges(_originalSnapshot, model);
            if (changes.Count > 0)
            {
                await ApplyChanges(changes).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            }

            _originalSnapshot = model;
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

            return changes;
        }

        /// <summary>
        /// Apply detected changes to the IHC controller via API services.
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