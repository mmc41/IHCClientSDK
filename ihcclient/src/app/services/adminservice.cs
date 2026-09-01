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
    /// High-level application service for managing administrator-related data including users, email, SMTP, DNS, network, web access, and WLAN settings.
    /// This applications service is intended as a tech-agnostic backend for a GUI or console IHC admin application.
    /// Provides change tracking and efficient updates to the IHC controller by detecting and applying only modified settings.
    /// Supports JSON serialization/deserialization with optional encryption of sensitive data.
    /// Will auto-authenticate with provided settings unless already authenticated.
    /// </summary>
    /// <remarks>
    /// This service maintains an internal snapshot of the last retrieved model for efficient change detection.
    /// When saving via Store(), only the detected changes are sent to the controller, minimizing API calls.
    /// Some configuration changes (DNS, network, WLAN) require a controller reboot to take effect.
    /// </remarks>
    public class AdminAppService : AuthenticatedAppServiceBase
    {
        private readonly IUserManagerService userService;
        private readonly IConfigurationService configService;
        private MutableAdminModel? _originalSnapshot;
        private readonly object _snapshotLock = new object();

        private SimpleSecret secretMaker;

        /// <summary>
        /// Summary of changes made to controller.
        /// </summary>
        public record ChangeInformation
        {
            /// <summary>
            /// Number of changes performed.
            /// </summary>
            public int ChangeCount { get; init; }

            /// <summary>
            /// Reboot needed after changes.
            /// </summary>
            public bool RebootRequired { get;  init; }
        }

        /// <summary>
        /// Create an AdminService instance with IhcSettings only.
        /// This constructor will internally create needed API services.
        /// </summary>
        /// <param name="settings">IHC configuration settings</param>
        /// <param name="fileEnryption">Should confidential fields be encrypted/decrypted in stored files</param>
        public AdminAppService(IhcSettings settings, bool fileEnryption)
            : base(settings, new AuthenticationService(settings), ownsAuthService: true)
        {
            this.userService = new UserManagerService(this.authService);
            this.configService = new ConfigurationService(this.authService);
            this.secretMaker = new SimpleSecret(enable: fileEnryption);
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
        public AdminAppService(IhcSettings settings, bool fileEnryption, IAuthenticationService authService, IUserManagerService userService, IConfigurationService configService)
            : base(settings, authService, ownsAuthService: false)
        {
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
            this.secretMaker = new SimpleSecret(enable: fileEnryption);
        }

        /// <summary>
        /// Reboot the IHC controller with a 1-second delay.
        /// </summary>
        /// <remarks>
        /// This method is typically called after making configuration changes that require a reboot (DNS, network, WLAN settings).
        /// The controller will restart after approximately 1 second.
        /// </remarks>
        public async Task Restart()
        {
                await configService.DelayedReboot(1).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
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
        /// Retrieves users, email control settings, SMTP settings, DNS servers, network settings, web access control, and WLAN settings.
        /// Stores an internal snapshot for change detection used by Store() method.
        /// </summary>
        /// <returns>MutableAdminModel containing all administrator data from the controller</returns>
        /// <remarks>
        /// This method performs sequential API calls to retrieve all configuration data.
        /// The returned model can be modified and saved back using Store() method.
        /// Validates all data annotations before returning.
        /// </remarks>
        public async Task<MutableAdminModel> GetModel()
        {
            return await RunTracedAsync(nameof(GetModel), async activity =>
            {
                // Make sure we are logged in.
                await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                // Read Model from controller.
                var model = await DoGetModel().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                // Create a deep copy for the snapshot to ensure changes to returned model don't affect snapshot
                lock (_snapshotLock)
                {
                    _originalSnapshot = model.Copy();
                }

                activity?.SetReturnValue(model.ToString(settings.LogSensitiveData));
                return model;
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Internal method to retrieve all administrator settings from the IHC controller.
        /// Performs sequential API calls to fetch users, email, SMTP, DNS, network, web access, and WLAN settings.
        /// </summary>
        /// <returns>MutableAdminModel populated with data from the controller</returns>
        /// <remarks>
        /// This method executes API calls sequentially to ensure predictable behavior and avoid overwhelming the controller.
        /// Called by both GetModel() for initial retrieval and Store() for loading snapshots when needed.
        /// </remarks>
        private async Task<MutableAdminModel> DoGetModel()
        {
            var users = await userService.GetUsers(includePassword: true).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var emailControl = await configService.GetEmailControlSettings().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var smtp = await configService.GetSMTPSettings().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var dns = await configService.GetDNSServers().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var network = await configService.GetNetworkSettings().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var webAccess = await configService.GetWebAccessControl().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            var wlan = await configService.GetWLanSettings().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            var model = new MutableAdminModel
            {
                ModelMetadata = ModelMetadata.Current(typeof(MutableAdminModel)),
                Users = new HashSet<IhcUser>(users),
                EmailControl = emailControl,
                SmtpSettings = smtp,
                DnsServers = dns,
                NetworkSettings = network,
                WebAccess = webAccess,
                WLanSettings = wlan
            };

            // Checks the model OBJECT, not the settings inside it. Validator.TryValidateObject does not recurse
            // into complex properties, and MutableAdminModel declares no annotations of its own, so in practice
            // this rejects only a null model: a controller that reports an over-long SMTP password or a missing
            // [Required] netmask still loads. That is deliberate — the constraints on the nested blocks are
            // enforced where they are APPLIED, by the leaf setters in ConfigurationService, and enforcing them
            // here as well would refuse real controller data that loads today.
            ValidationHelper.ValidateDataAnnotations(model, nameof(model));

            return model;
        }

        /// <summary>
        /// Save administrator model to the IHC controller.
        /// If no snapshot exists, loads current state from controller first.
        /// Detects changes between the original snapshot and the provided model,
        /// then applies only the changes to the controller via API calls.
        /// Updates the internal snapshot after successful save.
        /// </summary>
        /// <param name="model">Modified administrator model to save</param>
        /// <returns>ChangeInformation containing the number of changes applied and whether a reboot is required</returns>
        /// <remarks>
        /// This method validates the model using data annotations before applying changes.
        /// Changes to DNS servers, network settings, or WLAN settings will set the RebootRequired flag.
        /// If no changes are detected, no API calls are made to the controller.
        /// </remarks>
        public async Task<ChangeInformation> Store(MutableAdminModel model)
        {
            return await RunTracedAsync(nameof(Store), async activity =>
            {
                activity?.SetParameters((nameof(model), model.ToString(settings.LogSensitiveData)));

                // Make defensive copy to protect against concurrent mutation now and later
                var modelCopy = model.Copy();

                // Reaches only the top-level object, exactly as in DoGetModel above: a nested value that
                // breaks its own [StringLength] or [Required] passes here, and is refused — or not — by the
                // leaf setter that applies it.
                ValidationHelper.ValidateDataAnnotations(modelCopy, nameof(modelCopy));

                // Make sure we are logged in.
                await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                // Load snapshot from controller if not present
                MutableAdminModel? snapshot = null;
                lock (_snapshotLock)
                {
                    snapshot = _originalSnapshot;
                }

                if (snapshot == null)
                {
                    snapshot = await DoGetModel().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    lock (_snapshotLock)
                    {
                        _originalSnapshot = snapshot;
                    }
                }

                var changes = DetectChanges(snapshot, modelCopy);
                bool rebootRequiredFlag = false;
                if (changes.Count > 0)
                {
                    rebootRequiredFlag = await ApplyChanges(changes).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }

                lock (_snapshotLock)
                {
                    _originalSnapshot = modelCopy;
                }

                var retv = new ChangeInformation
                {
                    ChangeCount = changes.Count,
                    RebootRequired = rebootRequiredFlag
                };

                activity?.SetReturnValue(retv);

                return retv;
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Save the given AdminModel as JSON to a stream. Does not change the controller.
        /// Encrypts sensitive properties marked with [SensitiveData] attribute if encryption is enabled.
        /// </summary>
        /// <param name="adminModel">The admin model to save</param>
        /// <param name="stream">The stream to write JSON to (caller is responsible for disposing)</param>
        public async Task SaveAsJson(MutableAdminModel adminModel, Stream stream)
        {
            await RunTracedAsync(nameof(SaveAsJson), async activity =>
            {
                activity?.SetParameters((nameof(adminModel), adminModel.ToString(settings.LogSensitiveData)));

                var modelCopy = (MutableAdminModel)CopyUtil.DeepCopyAndApply(adminModel, (PropertyInfo? prop, object? value) =>
                {
                    // If property has SensitiveDataAttribute, encrypt the value
                    if (prop != null && prop.GetCustomAttribute<SensitiveDataAttribute>() != null)
                    {
                        // Handle null values
                        if (value == null)
                            return null;

                        // Convert value to string unless already a string
                        var stringValue = value as string ?? value.ToString()
                            ?? throw new InvalidOperationException($"A {value.GetType().Name} value marked sensitive has no string form to encrypt.");

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

                await JsonSerializer.SerializeAsync(stream, modelCopy, jsonOptions).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                await stream.FlushAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Save the given AdminModel as a JSON file at the specified path. Does not change the controller.
        /// Encrypts sensitive properties marked with [SensitiveData] attribute if encryption is enabled.
        /// </summary>
        /// <param name="adminModel">The admin model to save</param>
        /// <param name="path">File path where JSON will be written</param>
        public async Task SaveAsJson(MutableAdminModel adminModel, string path)
        {
            await RunTracedAsync(nameof(SaveAsJson), async activity =>
            {
                activity?.SetParameters((nameof(adminModel), adminModel.ToString(settings.LogSensitiveData)), (nameof(path), path));

                using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                await SaveAsJson(adminModel, fileStream).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Load the AdminModel from a JSON stream. Does not change the controller.
        /// Must be separately applied via Store() to change controller.
        /// Decrypts sensitive properties marked with [SensitiveData] attribute if encryption is enabled.
        /// </summary>
        /// <param name="stream">The stream to read JSON from (caller is responsible for disposing)</param>
        /// <returns>MutableAdminModel deserialized from JSON with sensitive data decrypted if encryption is enabled</returns>
        /// <exception cref="ArgumentException">Thrown when the stream does not deserialize to a model, or when its
        /// metadata is missing, names another type, or carries an incompatible major version</exception>
        /// <exception cref="System.Text.Json.JsonException">Thrown when JSON format is invalid</exception>
        public async Task<MutableAdminModel> LoadFromJson(Stream stream)
        {
            return await RunTracedAsync(nameof(LoadFromJson), async activity =>
            {
                activity?.SetParameters((nameof(stream), $"Steam with length={stream.Length}"));

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                var adminModel = await JsonSerializer.DeserializeAsync<MutableAdminModel>(stream, jsonOptions).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                if (adminModel == null)
                    throw new ArgumentException("Failed to deserialize AdminModel from JSON stream");

                // The metadata block is what the checks below read, and a file can simply not have one: it is
                // written from whatever the saved model carried, never stamped by SaveAsJson. Say so, rather
                // than dereferencing it and reporting a NullReferenceException for an ordinary bad input.
                if (adminModel.ModelMetadata == null)
                    throw new ArgumentException("Missing model metadata");

                string? serializedTypeName = adminModel.ModelMetadata.TypeFullName;
                string? expectedTypeName = typeof(MutableAdminModel).FullName;
                if (serializedTypeName!=expectedTypeName)
                    throw new ArgumentException($"Type incompatiblitly. Got {serializedTypeName} but expected {expectedTypeName}");

                Version? streamVersion = adminModel.ModelMetadata.Version;
                if (streamVersion == null)
                    throw new ArgumentException("Missing version metadata");

                System.Version currentVersion = typeof(MutableAdminModel).Assembly.GetName().Version!;
                if (streamVersion.Major!=currentVersion.Major)
                    throw new ArgumentException($"Version incompatiblitly. Got {streamVersion.Major} but expected {currentVersion.Major}");

                // Decrypt sensitive properties using deep copy with transformation
                var decryptedModel = (MutableAdminModel)CopyUtil.DeepCopyAndApply(adminModel, (PropertyInfo? prop, object? value) =>
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

                // Checks that the deserialized object exists, not the values inside it — see the note in
                // DoGetModel. A JSON file whose nested settings violate their annotations loads without
                // complaint; the type and version metadata checked above are what actually gate this path.
                ValidationHelper.ValidateDataAnnotations(decryptedModel, nameof(decryptedModel));

                activity?.SetReturnValue(decryptedModel.ToString(settings.LogSensitiveData));
                return decryptedModel;
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Load the AdminModel from a JSON file at the specified path. Does not change the controller.
        /// Must be separately applied via Store() to change controller.
        /// Decrypts sensitive properties marked with [SensitiveData] attribute if encryption is enabled.
        /// </summary>
        /// <param name="path">File path to read JSON from</param>
        /// <returns>MutableAdminModel deserialized from JSON file with sensitive data decrypted if encryption is enabled</returns>
        /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist</exception>
        /// <exception cref="InvalidOperationException">Thrown when deserialization fails or returns null</exception>
        /// <exception cref="System.Text.Json.JsonException">Thrown when JSON format is invalid</exception>
        public async Task<MutableAdminModel> LoadFromJson(string path)
        {
            return await RunTracedAsync(nameof(LoadFromJson), async activity =>
            {
                activity?.SetParameters((nameof(path), path));

                using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var result = await LoadFromJson(fileStream).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                activity?.SetReturnValue(result.ToString(settings.LogSensitiveData));
                return result;
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Detect changes between original and current admin models.
        /// Uses Username-based comparison for user set operations,
        /// then deep equality for detecting actual property changes.
        /// </summary>
        /// <param name="original">Original admin model from the controller snapshot</param>
        /// <param name="current">Current admin model with potential modifications</param>
        /// <returns>List of AdminChange objects representing detected differences</returns>
        /// <remarks>
        /// For users: Compares by Username to detect additions, deletions, and updates.
        /// For other settings: Uses default record equality for deep comparison.
        /// Network settings are intentionally ordered last as they may affect connectivity.
        /// </remarks>
        private List<AdminChange> DetectChanges(MutableAdminModel original, MutableAdminModel current)
        {
            return RunTraced(nameof(DetectChanges), activity =>
            {
                var changes = new List<AdminChange>();

                // User changes - Username-based shallow comparison for set operations
                var originalUsers = original.Users ?? new HashSet<IhcUser>();
                var currentUsers = current.Users ?? new HashSet<IhcUser>();

                // Build Username-based dictionaries for efficient lookup
                var originalByUsername = originalUsers.ToDictionary(u => u.Username!);
                var currentByUsername = currentUsers.ToDictionary(u => u.Username!);

                // Added users (Username in current but not in original)
                foreach (var username in currentByUsername.Keys.Except(originalByUsername.Keys))
                {
                    var change = new AdminChange
                    {
                        ChangeType = AdminChangeType.UserAdded,
                        Payload = currentByUsername[username]
                    };
                    changes.Add(change);
                    activity?.AddEvent(new ActivityEvent(change.ChangeType.ToString(), tags: new ActivityTagsCollection { { "payload", change.Payload } }));
                }

                // Deleted users (Username in original but not in current)
                foreach (var username in originalByUsername.Keys.Except(currentByUsername.Keys))
                {
                    var change = new AdminChange
                    {
                        ChangeType = AdminChangeType.UserDeleted,
                        Payload = originalByUsername[username]
                    };
                    changes.Add(change);
                    activity?.AddEvent(new ActivityEvent(change.ChangeType.ToString(), tags: new ActivityTagsCollection { { "payload", change.Payload } }));
                }

                // Updated users (Username in both, but admin-changeable properties differ)
                // EqualsAdminProperties() compares only fields that admins can change (excludes LoginDate, CreatedDate)
                foreach (var username in originalByUsername.Keys.Intersect(currentByUsername.Keys))
                {
                    var origUser = originalByUsername[username];
                    var currUser = currentByUsername[username];
                    if (!origUser.EqualsChangeableProperties(currUser))
                    {
                        var change = new AdminChange
                        {
                            ChangeType = AdminChangeType.UserUpdated,
                            Payload = currUser
                        };
                        changes.Add(change);
                        activity?.AddEvent(new ActivityEvent(change.ChangeType.ToString(), tags: new ActivityTagsCollection {
                         {
                            "orginal", origUser.ToString(settings.LogSensitiveData)
                         },
                         {
                            "payload", currUser.ToString(settings.LogSensitiveData)
                         }
                        }));
                    }
                }

                // EmailControl changes - default record Equals() does deep comparison
                if (!Equals(original.EmailControl, current.EmailControl))
                {
                    var change = new AdminChange
                    {
                        ChangeType = AdminChangeType.EmailControlChanged,
                        Payload = current.EmailControl
                    };
                    changes.Add(change);
                    activity?.AddEvent(new ActivityEvent(change.ChangeType.ToString(), tags: new ActivityTagsCollection {
                     {
                        "original", original.EmailControl
                     },
                     {
                        "payload", change.Payload
                     }
                    }));
                }

                // SMTP changes - default record Equals() does deep comparison
                if (!Equals(original.SmtpSettings, current.SmtpSettings))
                {
                    var change = new AdminChange
                    {
                        ChangeType = AdminChangeType.SmtpSettingsChanged,
                        Payload = current.SmtpSettings
                    };
                    changes.Add(change);
                    activity?.AddEvent(new ActivityEvent(change.ChangeType.ToString(), tags: new ActivityTagsCollection {
                     {
                        "original", original.SmtpSettings
                     },
                     {
                        "payload", change.Payload
                     }
                    }));
                }

                // Web access control changes - default record Equals() does deep comparison
                if (!Equals(original.WebAccess, current.WebAccess))
                {
                    var change = new AdminChange
                    {
                        ChangeType = AdminChangeType.WebAccessChanged,
                        Payload = current.WebAccess
                    };
                    changes.Add(change);
                    activity?.AddEvent(new ActivityEvent(change.ChangeType.ToString(), tags: new ActivityTagsCollection {
                     {
                        "original", original.WebAccess
                     },
                     {
                        "payload", change.Payload
                     }
                    }));
                }

                // DNS server changes - default record Equals() does deep comparison
                if (!Equals(original.DnsServers, current.DnsServers))
                {
                    var change = new AdminChange
                    {
                        ChangeType = AdminChangeType.DnsServersChanged,
                        Payload = current.DnsServers
                    };
                    changes.Add(change);
                    activity?.AddEvent(new ActivityEvent(change.ChangeType.ToString(), tags: new ActivityTagsCollection {
                     {
                        "original", original.DnsServers
                     },
                     {
                        "payload", change.Payload
                     }
                    }));
                }
                // WLAN settings changes - default record Equals() does deep comparison
                if (!Equals(original.WLanSettings, current.WLanSettings))
                {
                    var change = new AdminChange
                    {
                        ChangeType = AdminChangeType.WLanSettingsChanged,
                        Payload = current.WLanSettings
                    };
                    changes.Add(change);
                    activity?.AddEvent(new ActivityEvent(change.ChangeType.ToString(), tags: new ActivityTagsCollection {
                     {
                        "original", original.WLanSettings
                     },
                     {
                        "payload", change.Payload
                     }
                    }));
                }

                // Network settings changes - default record Equals() does deep comparison
                // This should be the last change as it might(?) affect the controller's ability to make other changes.
                if (!Equals(original.NetworkSettings, current.NetworkSettings))
                {
                    var change = new AdminChange
                    {
                        ChangeType = AdminChangeType.NetworkSettingsChanged,
                        Payload = current.NetworkSettings
                    };
                    changes.Add(change);
                    activity?.AddEvent(new ActivityEvent(change.ChangeType.ToString(), tags: new ActivityTagsCollection {
                     {
                        "original", original.NetworkSettings
                     },
                     {
                        "payload", change.Payload
                     }
                    }));
                }

                activity?.SetTag("ChangeCount", changes.Count);

                return changes;
            });
        }

        /// <summary>
        /// Apply detected changes to the IHC controller via API services.
        /// Sets RebootRequired flag if network-related changes are made.
        /// </summary>
        /// <param name="changes">List of changes to apply to the controller</param>
        /// <returns>True if a reboot is required for changes to take effect, false otherwise</returns>
        /// <remarks>
        /// Changes are applied sequentially in the order provided.
        /// DNS, network, and WLAN changes require a controller reboot and will set the return value to true.
        /// Each change type is routed to the appropriate service method (UserManagerService or ConfigurationService).
        /// </remarks>
        private async Task<bool> ApplyChanges(List<AdminChange> changes)
        {
            return await RunTracedAsync(nameof(ApplyChanges), async activity =>
            {
                bool rebootRequiredFlag = false;

                foreach (var change in changes)
                {
                    activity?.AddEvent(new ActivityEvent(change.ChangeType.ToString(), tags: new ActivityTagsCollection {
                        {
                            "payload", change.Payload
                        }
                    }));

                    switch (change.ChangeType)
                    {
                        case AdminChangeType.UserAdded:                  
                            await userService.AddUser((IhcUser)change.Payload!)
                                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            break;

                        case AdminChangeType.UserUpdated:
                            await userService.UpdateUser((IhcUser)change.Payload!)
                                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            break;

                        case AdminChangeType.UserDeleted:
                            await userService.RemoveUser(((IhcUser)change.Payload!).Username!)
                                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            break;

                        case AdminChangeType.EmailControlChanged:
                            await configService.SetEmailControlSettings((EmailControlSettings)change.Payload!)
                                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            break;

                        case AdminChangeType.SmtpSettingsChanged:
                            await configService.SetSMTPSettings((SMTPSettings)change.Payload!)
                                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            break;

                        case AdminChangeType.DnsServersChanged:
                            await configService.SetDNSServers((DNSServers)change.Payload!)
                                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            rebootRequiredFlag = true;
                            break;

                        case AdminChangeType.NetworkSettingsChanged:
                            await configService.SetNetworkSettings((NetworkSettings)change.Payload!)
                                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            rebootRequiredFlag = true;
                            break;

                        case AdminChangeType.WebAccessChanged:
                            await configService.SetWebAccessControl((WebAccessControl)change.Payload!)
                                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            break;

                        case AdminChangeType.WLanSettingsChanged:
                            await configService.SetWLanSettings((WLanSettings)change.Payload!)
                                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                            rebootRequiredFlag = true;
                            break;
                    }
                }

                return rebootRequiredFlag;
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

    }
}