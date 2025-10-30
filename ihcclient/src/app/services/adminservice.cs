using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text;
using System.Reflection;


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
        private MutableAdminModel _originalSnapshot;
        private readonly bool ownedServices;

        private SimpleSecret secretMaker;

        private volatile bool rebootRequiredFlag = false;

        /// <summary>
        /// Create an AdminService instance with IhcSettings only.
        /// This constructor will internally create needed API services.
        /// </summary>
        /// <param name="settings">IHC configuration settings</param>
        /// <param name="fileEnryption">Should confidential fields be encrypted/decrypted in stored files</param>
        public AdminService(IhcSettings settings, bool fileEnryption)
            : this(settings, fileEnryption, new AuthenticationService(settings),
                  new UserManagerService(new AuthenticationService(settings)),
                  new ConfigurationService(new AuthenticationService(settings)))
        {
        }

        /// <summary>
        /// Create an AdminService instance with IhcSettings and all needed API services.
        /// Use this constructor when you already have service instances.
        /// </summary>
        /// <param name="settings">IHC configuration settings</param>
        /// <param name="fileEnryption">Should confidential fields be encrypted/decrypted in stored files</param>
        /// <param name="authService">Auth manager service instance</param>
        /// <param name="userService">User manager service instance</param>
        /// <param name="configService">Configuration service instance</param>
        public AdminService(IhcSettings settings, bool fileEnryption, IAuthenticationService authService, IUserManagerService userService, IConfigurationService configService)
            : base(settings)
        {
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.ownedServices = false;
            this.rebootRequiredFlag = false;
            this.secretMaker = new SimpleSecret(enable: fileEnryption);
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
        public async Task<MutableAdminModel> GetModel()
        {
            using (var activity = StartActivity(nameof(GetModel)))
            {
                try
                {
                    await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    var model = await DoGetModel().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    // Create a deep copy for the snapshot to ensure changes to returned model don't affect snapshot
                    _originalSnapshot = new MutableAdminModel
                    {
                        Users = new HashSet<IhcUser>(model.Users ?? Enumerable.Empty<IhcUser>()),
                        EmailControl = model.EmailControl,
                        SmtpSettings = model.SmtpSettings,
                        DnsServers = model.DnsServers,
                        NetworkSettings = model.NetworkSettings,
                        WebAccess = model.WebAccess,
                        WLanSettings = model.WLanSettings
                    };

                    activity?.SetReturnValue(model.ToString(settings.LogSensitiveData));
                    return model;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        private async Task<MutableAdminModel> DoGetModel()
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

            return new MutableAdminModel
            {
                Users = new HashSet<IhcUser>(await usersTask),
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
        public async Task Store(MutableAdminModel model)
        {
            using (var activity = StartActivity(nameof(Store)))
            {
                try
                {
                    activity?.SetParameters((nameof(model), model.ToString(settings.LogSensitiveData)));

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
        /// Save the given AdminModel as JSON to a stream. Does not change the controller.
        /// Encrypts sensitive properties marked with [SensitiveData] attribute if encryption is enabled.
        /// </summary>
        /// <param name="adminModel">The admin model to save</param>
        /// <param name="stream">The stream to write JSON to (caller is responsible for disposing)</param>
        public async Task SaveAsJson(MutableAdminModel adminModel, Stream stream)
        {
            using (var activity = StartActivity(nameof(SaveAsJson)))
            {
                try
                {
                    activity?.SetParameters((nameof(adminModel), adminModel.ToString(settings.LogSensitiveData)));

                    var modelCopy = (MutableAdminModel)CopyUtil.DeepCopyAndApply(adminModel, (PropertyInfo prop, object value) =>
                    {
                        // If property has SensitiveDataAttribute, encrypt the value
                        if (prop != null && prop.GetCustomAttribute<SensitiveDataAttribute>() != null)
                        {
                            // Handle null values
                            if (value == null)
                                return null;

                            // Convert value to string unless already a string
                            var stringValue = value as string ?? value.ToString();

                            // Encrypt and return
                            return secretMaker.EncryptString(stringValue);
                        }

                        // Otherwise just return value as-is
                        return value;
                    });

                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Converters = { new JsonStringEnumConverter() }
                    };

                    await JsonSerializer.SerializeAsync(stream, modelCopy, jsonOptions);
                    await stream.FlushAsync();
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Save the given AdminModel as a JSON file at the specified path. Does not change the controller.
        /// Encrypts sensitive properties marked with [SensitiveData] attribute if encryption is enabled.
        /// </summary>
        /// <param name="adminModel">The admin model to save</param>
        /// <param name="path">File path where JSON will be written</param>
        public async Task SaveAsJson(MutableAdminModel adminModel, string path)
        {
            using (var activity = StartActivity(nameof(SaveAsJson)))
            {
                try
                {
                    activity?.SetParameters((nameof(adminModel), adminModel.ToString(settings.LogSensitiveData)), (nameof(path), path));

                    using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                    await SaveAsJson(adminModel, fileStream);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Load the AdminModel from a JSON stream. Does not change the controller.
        /// Must be seperately applied via Store() to change controller.
        /// Decrypts sensitive properties marked with [SensitiveData] attribute if encryption is enabled.
        /// </summary>
        /// <param name="stream">The stream to read JSON from (caller is responsible for disposing)</param>
        public async Task<MutableAdminModel> LoadFromJson(Stream stream)
        {
            using (var activity = StartActivity(nameof(LoadFromJson)))
            {
                try
                {
                    activity?.SetParameters((nameof(stream), $"Steam with length={stream.Length}"));
                                        
                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Converters = { new JsonStringEnumConverter() }
                    };

                    var adminModel = await JsonSerializer.DeserializeAsync<MutableAdminModel>(stream, jsonOptions);

                    if (adminModel == null)
                        throw new InvalidOperationException("Failed to deserialize AdminModel from JSON stream");

                    // Decrypt sensitive properties using deep copy with transformation
                    var decryptedModel = (MutableAdminModel)CopyUtil.DeepCopyAndApply(adminModel, (PropertyInfo prop, object value) =>
                    {
                        // If property has SensitiveDataAttribute, decrypt the value
                        if (prop != null && prop.GetCustomAttribute<SensitiveDataAttribute>() != null)
                        {
                            // Handle null values
                            if (value == null)
                                return null;

                            // Value should be a string (encrypted data)
                            var stringValue = value as string;
                            if (stringValue == null)
                                return value; // If not a string, return as-is

                            // Decrypt and return
                            return secretMaker.DecryptString(stringValue);
                        }

                        // Otherwise just return value as-is
                        return value;
                    });

                    activity?.SetReturnValue(decryptedModel.ToString(settings.LogSensitiveData));
                    return decryptedModel;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Load the AdminModel from a JSON file at the current path. Does not change the controller.
        /// Must be seperately applied via Store() to change controller.
        /// Decrypts sensitive properties marked with [SensitiveData] attribute if encryption is enabled.
        /// </summary>
        /// <param name="path">File path to read JSON from</param>
        public async Task<MutableAdminModel> LoadFromJson(string path)
        {
            using (var activity = StartActivity(nameof(LoadFromJson)))
            {
                try
                {
                    activity?.SetParameters((nameof(path), path));

                    using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var result = await LoadFromJson(fileStream);

                    activity?.SetReturnValue(result.ToString(settings.LogSensitiveData));
                    return result;
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
        private List<AdminChange> DetectChanges(MutableAdminModel original, MutableAdminModel current)
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