using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ihc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FakeItEasy;

namespace IhcLab;

public class IhcSetup
{
    public IhcSettings IhcSettings { get; init; }
    public ILoggerFactory loggerFactory { get; internal set; }

    public IAuthenticationService AuthenticationService { get; set; }
    public IControllerService ControllerService { get; set; }
    public IResourceInteractionService ResourceInteractionService { get; set; }
    public IConfigurationService ConfigurationService { get; set; }
    public IOpenAPIService OpenAPIService { get; set; }
    public INotificationManagerService NotificationManagerService { get; set; }
    public IMessageControlLogService MessageControlLogService { get; set; }
    public IModuleService ModuleService { get; set; }
    public ITimeManagerService TimeManagerService { get; set; }
    public IUserManagerService UserManagerService { get; set; }
    public IAirlinkManagementService AirlinkManagementService { get; set; }

    public ISmsModemService SmsModemService { get; set; }
    public IInternalTestService InternalTestService { get; set; }

    public IIHCApiService[] AllIhcServices
    {
        get
        {
            return
            [
                AuthenticationService,
                ControllerService,
                ResourceInteractionService,
                ConfigurationService,
                OpenAPIService,
                NotificationManagerService,
                MessageControlLogService,
                ModuleService,
                TimeManagerService,
                UserManagerService,
                AirlinkManagementService,
                SmsModemService,
                InternalTestService
            ];
        }
    }

    // CS8618: Non-nullable field must contain a non-null value when exiting constructor.
    // Suppression is safe here because UpdateSetup() is called immediately in constructor
    // and will initialize all service fields. If UpdateSetup() throws, the object is not used.
#pragma warning disable CS8618
    public IhcSetup()
    {
        this.loggerFactory = Program.loggerFactory ?? new NullLoggerFactory();
        this.IhcSettings = Program.config?.ihcSettings ?? new IhcSettings();

        // This will initialize all service properties
        UpdateSetup();
    }
#pragma warning restore CS8618

    public void UpdateSetup()
    {
        if (IhcSettings.Endpoint == null)
            throw new Exception("IhcSettings.Endpoint is null in IhcDomain UpdateSetup");

        if (!IhcSettings.Endpoint.StartsWith(SpecialEndpoints.MockedPrefix))
        {
            // Real services by default:
            this.AuthenticationService = new AuthenticationService(IhcSettings);
            this.ControllerService = new ControllerService(AuthenticationService);
            this.ResourceInteractionService = new ResourceInteractionService(AuthenticationService);
            this.ConfigurationService = new ConfigurationService(AuthenticationService);
            this.OpenAPIService = new OpenAPIService(IhcSettings);
            this.NotificationManagerService = new NotificationManagerService(AuthenticationService);
            this.MessageControlLogService = new MessageControlLogService(AuthenticationService);
            this.ModuleService = new ModuleService(AuthenticationService);
            this.TimeManagerService = new TimeManagerService(AuthenticationService);
            this.UserManagerService = new UserManagerService(AuthenticationService);
            this.AirlinkManagementService = new AirlinkManagementService(AuthenticationService);
            this.SmsModemService = new SmsModemService(AuthenticationService);
            this.InternalTestService = new InternalTestService(AuthenticationService);
        }
        else
        {
            // All services can be faked. This may at first seem out of place,
            // but it allows for easy human explorative testing of the GUI aspects of
            // the app without being connected to a real IHC system. ALso allows for 
            // safe automated testing og the GUI without a real IHC system.

            this.AuthenticationService = IhcFakeSetup.SetupAuthenticationService(IhcSettings);
            this.ControllerService = IhcFakeSetup.SetupControllerService(IhcSettings);
            this.ResourceInteractionService = IhcFakeSetup.SetupResourceInteractionService(IhcSettings);
            this.ConfigurationService = IhcFakeSetup.SetupConfigurationService(IhcSettings);
            this.OpenAPIService = IhcFakeSetup.SetupOpenAPIService(IhcSettings);
            this.NotificationManagerService = IhcFakeSetup.SetupNotificationManagerService(IhcSettings);
            this.MessageControlLogService = IhcFakeSetup.SetupMessageControlLogService(IhcSettings);
            this.ModuleService = IhcFakeSetup.SetupModuleService(IhcSettings);
            this.TimeManagerService = IhcFakeSetup.SetupTimeManagerService(IhcSettings);
            this.UserManagerService = IhcFakeSetup.SetupUserManagerService(IhcSettings);
            this.AirlinkManagementService = IhcFakeSetup.SetupAirlinkManagementService(IhcSettings);
            this.SmsModemService = IhcFakeSetup.SetupSmsModemService(IhcSettings);
            this.InternalTestService = IhcFakeSetup.SetupInternalTestService(IhcSettings);
        }
    }

    public void Dispose()
    {
        AuthenticationService?.Dispose();
    }
}


/// <summary>
/// Create faked services with fixed behavior as a configurable alternative to real IHC servicews. This may at first seem out of place,
/// but it allows for easy human explorative testing of the GUI aspects of the app without being connected to a real IHC system. ALso
/// allows for safe automated testing og the GUI without a real IHC system.
/// </summary>
public class IhcFakeSetup
{
    public static IAuthenticationService SetupAuthenticationService(IhcSettings settings)
    {
        var service = A.Fake<IAuthenticationService>();

        A.CallTo(() => service.Authenticate()).Returns(Task.FromResult(new IhcUser
        {
            Username = settings.UserName,
            Password = settings.Password,
            Firstname = "Mock",
            Lastname = settings.Application.ToString(),
            Group = IhcUserGroup.Administrators
        }));

        A.CallTo(() => service.Authenticate(A<string>._, A<string>._, A<Application>._))
            .ReturnsLazily((string u, string p, Application app) => Task.FromResult(new IhcUser
        {
            Username = u,
            Password = p,
            Firstname = "Mock",
            Lastname = app.ToString(),
            Group = IhcUserGroup.Administrators
        }));

        A.CallTo(() => service.Ping()).Returns(Task.FromResult(true));
        A.CallTo(() => service.Disconnect()).Returns(Task.FromResult(true));
        A.CallTo(() => service.IsAuthenticated()).Returns(Task.FromResult(true));

        return service;
    }

    public static IControllerService SetupControllerService(IhcSettings settings)
    {
        var service = A.Fake<IControllerService>();

        A.CallTo(() => service.IsIHCProjectAvailable()).Returns(Task.FromResult(true));

        A.CallTo(() => service.GetControllerState()).Returns(Task.FromResult(ControllerState.Ready));

        A.CallTo(() => service.WaitForControllerStateChange(A<ControllerState>._, A<int>._))
            .ReturnsLazily((ControllerState waitState, int waitSec) => Task.FromResult(waitState));


        A.CallTo(() => service.IsSDCardReady()).Returns(Task.FromResult(true));

        A.CallTo(() => service.GetSDCardInfo()).Returns(Task.FromResult(new SDInfo()
        {
            Size = 8_000_000_000,
            Free = 4_500_000_000
        }));

        A.CallTo(() => service.GetProjectInfo()).Returns(Task.FromResult(MockProjectInfo()));

        A.CallTo(() => service.GetProject()).Returns(Task.FromResult(new Ihc.ProjectFile("project-mock.vis",
            """
            <?xml version="1.0" encoding="ISO-8859-1"?>
            <utcs_project version_major="4" version_minor="0" id1="1" id2="2" last_unique_id="3">
                <modified year="2025" month="10" day="4" hour="17" minute="6"/>
                <customer_info name="Mock Customer"/>
                <installer_info name="Mock Installer" country="Danmark"/>
                <project_info programmer="Mock Programmer" number="12345" description="Mock test project"/>
            </utcs_project>
            """
        )));

        A.CallTo(() => service.StoreProject(A<ProjectFile>._)).ReturnsLazily((ProjectFile prj) =>
        {
            return Task.FromResult(prj?.Data.StartsWith("<?xml") == true);
        });

        A.CallTo(() => service.EnterProjectChangeMode()).Returns(Task.FromResult(true));

        A.CallTo(() => service.ExitProjectChangeMode()).Returns(Task.FromResult(true));

        // ========== Project Segmentation (for large projects) ==========

        A.CallTo(() => service.GetIHCProjectSegmentationSize()).Returns(Task.FromResult(1_048_576)); // 1 MB segments

        A.CallTo(() => service.GetIHCProjectNumberOfSegments()).Returns(Task.FromResult(1)); // Single segment project

        A.CallTo(() => service.GetIHCProjectSegment(A<int>._, A<int>._, A<int>._))
            .ReturnsLazily((int index, int major, int minor) => Task.FromResult(new Ihc.ProjectFile(
                $"project-segment-{index}.vis",
                $"""
                <?xml version="1.0" encoding="ISO-8859-1"?>
                """
            )));

        A.CallTo(() => service.StoreIHCProjectSegment(A<ProjectFile>._, A<int>._, A<int>._))
            .ReturnsLazily((ProjectFile segment, int index, int major) =>
            {
                return Task.FromResult(segment?.Data.StartsWith("<?xml") == true);
            });

        A.CallTo(() => service.GetBackup()).Returns(Task.FromResult(new Ihc.BackupFile("backup-mock.dat",
            new byte[] { 0x42, 0x61, 0x63, 0x6B, 0x75, 0x70, 0x00, 0xFF } // "Backup" + null + 0xFF
        )));

        A.CallTo(() => service.Restore()).Returns(Task.FromResult(0)); // 0 = success

        A.CallTo(() => service.GetS0MeterValue()).Returns(Task.FromResult(1234.56f)); // kWh

        A.CallTo(() => service.ResetS0Values()).Returns(Task.CompletedTask);

        A.CallTo(() => service.SetS0Consumption(A<float>._, A<bool>._)).Returns(Task.CompletedTask);

        A.CallTo(() => service.SetS0FiscalYearStart(A<sbyte>._, A<sbyte>._)).Returns(Task.CompletedTask);

        return service;
    }

    public static IUserManagerService SetupUserManagerService(IhcSettings settings)
    {
        var service = A.Fake<IUserManagerService>();

        // Per-call user store: a fresh, independently seeded dictionary captured by the lambdas below as a
        // closure. This keeps each fake service instance's state isolated, so AddUser/RemoveUser/UpdateUser
        // mutations never leak across fake instances or across test cases (a static store would leak).
        // ConcurrentDictionary keeps the username-based lookups thread-safe.
        var mockUsers = new ConcurrentDictionary<string, IhcUser>(
            new Dictionary<string, IhcUser>
            {
                ["admin"] = new IhcUser
                {
                    Username = "admin",
                    Password = "admin123",
                    Email = "admin@mock.com",
                    Firstname = "Admin",
                    Lastname = "User",
                    Phone = "+4512345678",
                    Group = IhcUserGroup.Administrators,
                    Project = "Mock Project",
                    CreatedDate = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero),
                    LoginDate = new DateTimeOffset(2025, 11, 1, 9, 30, 0, TimeSpan.Zero)
                },
                ["testuser"] = new IhcUser
                {
                    Username = "testuser",
                    Password = "test123",
                    Email = "test@mock.com",
                    Firstname = "Test",
                    Lastname = "User",
                    Phone = "+4587654321",
                    Group = IhcUserGroup.Users,
                    Project = "Mock Project",
                    CreatedDate = new DateTimeOffset(2024, 6, 15, 14, 30, 0, TimeSpan.Zero),
                    LoginDate = new DateTimeOffset(2025, 11, 5, 11, 15, 0, TimeSpan.Zero)
                }
            });

        // GetUsers - returns users from state with optional password filtering
        A.CallTo(() => service.GetUsers(A<bool>._))
            .ReturnsLazily((bool includePassword) =>
            {
                var users = mockUsers.Values
                    .Select(u => includePassword ? u : u.RedactPasword())
                    .ToHashSet();
                return Task.FromResult<IReadOnlySet<IhcUser>>(users);
            });

        // AddUser - adds user to state
        A.CallTo(() => service.AddUser(A<IhcUser>._))
            .ReturnsLazily((IhcUser user) =>
            {
                // Validate using same validation as real service
                ValidationHelper.ValidateDataAnnotations(user, nameof(user));

                // Add user with current timestamp if dates are not set
                var userToAdd = user with
                {
                    CreatedDate = user.CreatedDate == default ? DateTimeOffset.Now : user.CreatedDate,
                    LoginDate = user.LoginDate == default ? DateTimeOffset.MinValue : user.LoginDate
                };

                // Simulate SOAP-level failure for duplicate user
                if (!mockUsers.TryAdd(userToAdd.Username, userToAdd))
                    throw new InvalidOperationException($"User '{user.Username}' already exists");

                return Task.CompletedTask;
            });

        // RemoveUser - removes user from state
        A.CallTo(() => service.RemoveUser(A<string>._))
            .ReturnsLazily((string username) =>
            {
                // Match real service validation - check for reserved "usb" user
                if (username == "usb")
                    throw new ArgumentException(message: "Can not delete reserved usb user", paramName: nameof(username));

                // Simulate SOAP-level failure for non-existent user
                if (!mockUsers.TryRemove(username, out _))
                    throw new InvalidOperationException($"User '{username}' not found");

                return Task.CompletedTask;
            });

        // UpdateUser - updates existing user in state
        A.CallTo(() => service.UpdateUser(A<IhcUser>._))
            .ReturnsLazily((IhcUser user) =>
            {
                // Validate using same validation as real service
                ValidationHelper.ValidateDataAnnotations(user, nameof(user));

                // Match real service validation - check for REDACTED_PASSWORD
                // After validation, user is guaranteed non-null, but keeping ?. for consistency with real service
                if (user?.Password == UserConstants.REDACTED_PASSWORD)
                    throw new ArgumentException($"Password of user should not be set to reserved value ${UserConstants.REDACTED_PASSWORD}. This is likely an error!");

                // Simulate SOAP-level failure for non-existent user
                if (!mockUsers.TryGetValue(user!.Username, out var existingUser))
                    throw new InvalidOperationException($"User '{user!.Username}' not found");

                // Update user while preserving CreatedDate, update LoginDate if not set
                var userToUpdate = user with
                {
                    CreatedDate = existingUser.CreatedDate, // Preserve original creation date
                    LoginDate = user.LoginDate == default ? existingUser.LoginDate : user.LoginDate
                };

                mockUsers[user!.Username] = userToUpdate;
                return Task.CompletedTask;
            });

        return service;
    }

    public static IResourceInteractionService SetupResourceInteractionService(IhcSettings settings)
    {
        var service = A.Fake<IResourceInteractionService>();

        // A small, realistic set of resource values reused across the read operations below so the Lab shows
        // representative data (not empty/default fakes) when a human explores the GUI or a smoke test runs.
        IReadOnlyList<ResourceValue> sampleValues = new List<ResourceValue>
        {
            new ResourceValue { ResourceID = 1001, TypeString = "dataline_input", IsValueRuntime = true,
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.BOOL, BoolValue = true } },
            new ResourceValue { ResourceID = 1002, TypeString = "dataline_output", IsValueRuntime = true,
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.INT, IntValue = 42 } },
            new ResourceValue { ResourceID = 1003, TypeString = "temperature", IsValueRuntime = true,
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.DOUBLE, DoubleValue = 21.5 } },
        };

        // The same resources as initial (not runtime) values, so the plural initial-value reads stay consistent
        // with the singular GetInitialValue mock (which reports IsValueRuntime = false).
        IReadOnlyList<ResourceValue> initialSampleValues = sampleValues
            .Select(v => new ResourceValue { ResourceID = v.ResourceID, TypeString = v.TypeString, IsValueRuntime = false, Value = v.Value })
            .ToList();

        // Accept resource value writes from the Lab's ResourceValue union editor (single + collection).
        A.CallTo(() => service.SetResourceValue(A<ResourceValue>._)).Returns(Task.FromResult(true));
        A.CallTo(() => service.SetResourceValues(A<IReadOnlyList<ResourceValue>>._)).Returns(Task.FromResult(true));

        // Runtime / initial value reads (single + collection). Echo the requested id so the result is distinct.
        A.CallTo(() => service.GetRuntimeValue(A<int>._)).ReturnsLazily((int id) => Task.FromResult(
            new ResourceValue { ResourceID = id, TypeString = "dataline_output", IsValueRuntime = true,
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.BOOL, BoolValue = true } }));
        A.CallTo(() => service.GetRuntimeValues(A<IReadOnlyList<int>>._)).Returns(Task.FromResult(sampleValues));
        A.CallTo(() => service.GetInitialValue(A<int>._)).ReturnsLazily((int id) => Task.FromResult(
            new ResourceValue { ResourceID = id, TypeString = "dataline_output", IsValueRuntime = false,
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.BOOL, BoolValue = false } }));
        A.CallTo(() => service.GetInitialValues(A<IReadOnlyList<int>>._)).Returns(Task.FromResult(initialSampleValues));

        // Notification enable/disable.
        A.CallTo(() => service.EnableInitialValueNotifications(A<IReadOnlyList<int>>._)).Returns(Task.FromResult(initialSampleValues));
        A.CallTo(() => service.EnableRuntimeValueNotifications(A<IReadOnlyList<int>>._)).Returns(Task.FromResult(sampleValues));
        A.CallTo(() => service.DisableInitialValueNotifactions(A<IReadOnlyList<int>>._)).Returns(Task.FromResult(true));
        A.CallTo(() => service.DisableRuntimeValueNotifactions(A<IReadOnlyList<int>>._)).Returns(Task.FromResult(true));

        // Dataline catalogues.
        A.CallTo(() => service.GetAllDatalineInputs()).Returns(Task.FromResult<IReadOnlyList<DatalineResource>>(new List<DatalineResource>
        {
            new DatalineResource { DatalineNumber = 1, ResourceID = 1001 },
            new DatalineResource { DatalineNumber = 2, ResourceID = 1004 },
        }));
        A.CallTo(() => service.GetAllDatalineOutputs()).Returns(Task.FromResult<IReadOnlyList<DatalineResource>>(new List<DatalineResource>
        {
            new DatalineResource { DatalineNumber = 1, ResourceID = 1002 },
            new DatalineResource { DatalineNumber = 2, ResourceID = 1005 },
        }));
        A.CallTo(() => service.GetExtraDatalineInputs()).Returns(Task.FromResult<IReadOnlyList<DatalineResource>>(new List<DatalineResource>()));
        A.CallTo(() => service.GetExtraDatalineOutputs()).Returns(Task.FromResult<IReadOnlyList<DatalineResource>>(new List<DatalineResource>()));

        // Resource type + enumerator definitions.
        A.CallTo(() => service.GetResourceType(A<int>._)).Returns(Task.FromResult("dataline_output"));
        A.CallTo(() => service.GetEnumeratorDefinitions()).Returns(Task.FromResult<IReadOnlyList<EnumDefinition>>(new List<EnumDefinition>
        {
            new EnumDefinition { EnumeratorDefinitionID = 10, Values = new[]
            {
                new EnumValue { DefinitionTypeID = 10, EnumValueID = 1, EnumName = "Off" },
                new EnumValue { DefinitionTypeID = 10, EnumValueID = 2, EnumName = "On" },
            }},
        }));

        // Logged data history for a resource.
        A.CallTo(() => service.GetLoggedData(A<int>._)).ReturnsLazily((int id) => Task.FromResult<IReadOnlyList<LoggedData>>(new List<LoggedData>
        {
            new LoggedData { Id = id, Value = "21.5", Timestamp = new DateTimeOffset(2025, 10, 4, 17, 0, 0, TimeSpan.Zero) },
            new LoggedData { Id = id, Value = "22.0", Timestamp = new DateTimeOffset(2025, 10, 4, 18, 0, 0, TimeSpan.Zero) },
        }));

        // Scene resource lookups.
        A.CallTo(() => service.GetSceneGroupResourceIdAndPositions(A<int>._)).Returns(Task.FromResult<IReadOnlyList<SceneResourceIdAndLocation>>(new List<SceneResourceIdAndLocation>
        {
            new SceneResourceIdAndLocation { SceneResourceId = 2001, ScenePositionSeenFromFunctionBlock = "FB1", ScenePositionSeenFromProduct = "Living room" },
        }));
        A.CallTo(() => service.GetScenePositionsForSceneValueResource(A<int>._)).ReturnsLazily((int id) => Task.FromResult(
            new SceneResourceIdAndLocation { SceneResourceId = id, ScenePositionSeenFromFunctionBlock = "FB1", ScenePositionSeenFromProduct = "Living room" }));

        // Blocking poll for value changes (non-streaming).
        A.CallTo(() => service.WaitForResourceValueChanges(A<int>._)).Returns(Task.FromResult(sampleValues));

        // Yield a live change-stream for desk demos (TC-3 of the demonstration test plan): without a
        // controller GetResourceValueChanges has nothing to poll, so emit a synthetic ResourceValue
        // roughly once per second until StopStream cancels. This lets the Start/Stop streaming GUI be
        // exercised in the mocked environment.
        A.CallTo(() => service.GetResourceValueChanges(A<IReadOnlyList<int>>._, A<CancellationToken>._, A<int>._))
            .ReturnsLazily((IReadOnlyList<int> resourceIds, CancellationToken ct, int timeout) =>
                DemoResourceValueChangeStream(resourceIds, ct));

        return service;
    }

    /// <summary>
    /// A demo change-stream for the mocked environment: yields a fresh <see cref="ResourceValue"/> about
    /// once per second - cycling through the requested resource IDs and incrementing the value so each
    /// arrival is visibly distinct - until the token is cancelled by StopStream. Mirrors the real
    /// long-poll's "append as values change" behaviour without a controller. <see cref="Task.Delay(TimeSpan,
    /// CancellationToken)"/> observes the token, so cancellation ends the stream promptly (StartStream
    /// treats the resulting OperationCanceledException as a normal end).
    /// </summary>
    private static async IAsyncEnumerable<ResourceValue> DemoResourceValueChangeStream(
        IReadOnlyList<int> resourceIds,
        [EnumeratorCancellation] CancellationToken ct)
    {
        int[] ids = resourceIds != null && resourceIds.Count > 0 ? resourceIds.ToArray() : new[] { 1 };
        int counter = 0;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            int id = ids[counter % ids.Length];
            yield return new ResourceValue
            {
                ResourceID = id,
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.INT, IntValue = counter }
            };
            counter++;
        }
    }

    public static IConfigurationService SetupConfigurationService(IhcSettings settings)
    {
        var service = A.Fake<IConfigurationService>();

        // Realistic system info so "Save Result to File…" (TC-1 step 4) writes a representative
        // SystemInfo(...) text instead of an empty-fake dummy with all-default fields.
        A.CallTo(() => service.GetSystemInfo()).Returns(Task.FromResult(new SystemInfo
        {
            Uptime = 86_400_000, // 1 day in ms
            Realtimeclock = new DateTimeOffset(2025, 10, 4, 17, 6, 0, TimeSpan.Zero),
            SerialNumber = "MOCK-0001",
            ProductionDate = "2024-01-15",
            Brand = "Mock Brand",
            Version = "4.0.0",
            HWRevision = "1",
            SWDate = new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero),
            DatalineVersion = "2.0",
            RFModuleSoftwareVersion = "1.5",
            RFModuleSerialNumber = "RF-0001",
            ApplicationIsWithoutViewer = true,
            SmsModemSoftwareVersion = "1.0",
            LedDimmerSoftwareVersion = "1.0"
        }));

        // User log.
        A.CallTo(() => service.GetUserLog(A<string>._)).Returns(Task.FromResult<IReadOnlyList<string>>(new List<string>
        {
            "2025-10-04 17:00 User admin logged in",
            "2025-10-04 17:05 Project stored",
        }));

        // Network / DNS / WLAN.
        A.CallTo(() => service.GetNetworkSettings()).Returns(Task.FromResult(new NetworkSettings
        {
            IpAddress = "192.168.1.10", Netmask = "255.255.255.0", Gateway = "192.168.1.1", HttpPort = 80, HttpsPort = 443
        }));
        A.CallTo(() => service.GetDNSServers()).Returns(Task.FromResult(new DNSServers { PrimaryDNS = "8.8.8.8", SecondaryDNS = "8.8.4.4" }));
        A.CallTo(() => service.GetWLanSettings()).Returns(Task.FromResult(new WLanSettings
        {
            Enabled = true, Ssid = "HomeNetwork", Key = "secret", SecurityType = "WPA2", EncryptionType = "AES",
            IpAddress = "192.168.1.11", Netmask = "255.255.255.0", Gateway = "192.168.1.1"
        }));
        A.CallTo(() => service.GetWLanInterface()).Returns(Task.FromResult(new WLanInterface { Connected = true, Name = "wlan0", Ssid = "HomeNetwork", Quality = "Good" }));
        A.CallTo(() => service.GetWLanScan()).Returns(Task.FromResult<IReadOnlyList<WLanCell>>(new List<WLanCell>
        {
            new WLanCell { Ssid = "HomeNetwork", HasEncryption = true, SecurityType = "WPA2", EncryptionType = "AES" },
            new WLanCell { Ssid = "GuestNetwork", HasEncryption = false, SecurityType = "", EncryptionType = "" },
        }));

        // SMTP / email.
        A.CallTo(() => service.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings
        {
            Hostname = "smtp.mock.com", Hostport = 587, Username = "alert@mock.com", Password = "secret",
            Ssl = true, SendLowBatteryNotification = true, SendLowBatteryNotificationRecipient = "admin@mock.com"
        }));
        A.CallTo(() => service.TestSendMessage(A<string>._, A<string>._, A<string>._)).Returns(Task.FromResult(true));
        A.CallTo(() => service.GetEmailControlEnabled()).Returns(Task.FromResult(true));
        A.CallTo(() => service.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings
        {
            ServerIpAddress = "pop.mock.com", ServerPortNumber = 995, Pop3Username = "ctrl",
            Pop3Password = "secret", EmailAddress = "control@mock.com", PollInterval = 5,
            RemoveEmailsAfterUsage = true, Ssl = true
        }));

        // Web access control.
        A.CallTo(() => service.GetWebAccessControl()).Returns(Task.FromResult(new WebAccessControl
        {
            UsbLoginRequired = false, AdministratorUsb = true, AdministratorInternal = true, AdministratorExternal = false,
            OpenapiInternal = true, OpenapiUsed = true
        }));

        // Void ops (ClearUserLog, DelayedReboot, SetNetworkSettings, SetDNSServers, SetWLanSettings,
        // SetSMTPSettings, TestSettingsNow, SetEmailControlEnabled, SetEmailControlSettings,
        // SetWebAccessControl, SetServerLanguage) return a completed Task by default via FakeItEasy.
        return service;
    }

    public static IOpenAPIService SetupOpenAPIService(IhcSettings settings)
    {
        var service = A.Fake<IOpenAPIService>();

        IReadOnlyList<ResourceValue> sampleValues = new List<ResourceValue>
        {
            new ResourceValue { ResourceID = 1002, TypeString = "dataline_output", IsValueRuntime = true,
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.BOOL, BoolValue = true } },
        };

        A.CallTo(() => service.GetFWVersion()).Returns(Task.FromResult(new FWVersion { MajorVersion = 4, MinorVersion = 0, BuildVersion = 1284 }));
        A.CallTo(() => service.GetAPIVersion()).Returns(Task.FromResult("4.0.0"));
        A.CallTo(() => service.GetUptime()).Returns(Task.FromResult(TimeSpan.FromHours(36)));
        A.CallTo(() => service.GetTime()).Returns(Task.FromResult(new DateTimeOffset(2025, 10, 4, 17, 6, 0, TimeSpan.Zero)));
        A.CallTo(() => service.IsIHCProjectAvailable()).Returns(Task.FromResult(true));
        A.CallTo(() => service.GetDatalineInputIDs()).Returns(Task.FromResult<IReadOnlyList<int>>(new List<int> { 1001, 1004 }));
        A.CallTo(() => service.GetDatalineOutputIDs()).Returns(Task.FromResult<IReadOnlyList<int>>(new List<int> { 1002, 1005 }));
        A.CallTo(() => service.GetValues(A<IReadOnlyList<int>>._)).Returns(Task.FromResult(sampleValues));
        A.CallTo(() => service.SetValues(A<IReadOnlyList<ResourceValue>>._)).Returns(Task.FromResult(true));
        A.CallTo(() => service.WaitForEvents(A<int>._)).Returns(Task.FromResult(new EventPackage
        {
            ResourceValueEvents = sampleValues.ToArray(),
            ControllerExecutionRunning = true,
            SubscriptionAmount = sampleValues.Count
        }));
        A.CallTo(() => service.GetProjectInfo()).Returns(Task.FromResult(MockProjectInfo()));
        A.CallTo(() => service.GetIHCProjectNumberOfSegments()).Returns(Task.FromResult(1));
        A.CallTo(() => service.GetIHCProjectSegmentationSize()).Returns(Task.FromResult(1_048_576));
        A.CallTo(() => service.GetIHCProjectSegment(A<int>._, A<int>._, A<int>._)).Returns(Task.FromResult(
            new ProjectSegment { Data = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>") }));
        A.CallTo(() => service.GetSceneProjectInfo()).Returns(Task.FromResult(MockSceneProjectInfo()));
        A.CallTo(() => service.GetSceneProjectSegmentationSize()).Returns(Task.FromResult(1_048_576));
        A.CallTo(() => service.GetSceneProjectSegment(A<int>._)).Returns(Task.FromResult(new SceneProjectSegment { Data = new byte[] { 0x01, 0x02, 0x03, 0x04 } }));

        // Live demo change-stream (mirrors ResourceInteractionService's stream for the Start/Stop GUI).
        A.CallTo(() => service.GetResourceValueChanges(A<IReadOnlyList<int>>._, A<CancellationToken>._, A<int>._))
            .ReturnsLazily((IReadOnlyList<int> resourceIds, CancellationToken ct, int timeout) =>
                DemoResourceValueChangeStream(resourceIds, ct));

        // Void ops (Authenticate x2, DoReboot, Ping, EnableSubscription, DisableSubscription) return a
        // completed Task by default via FakeItEasy.
        return service;
    }

    public static INotificationManagerService SetupNotificationManagerService(IhcSettings settings)
    {
        var service = A.Fake<INotificationManagerService>();

        A.CallTo(() => service.GetMessages()).Returns(Task.FromResult<IReadOnlyList<NotificationMessage>>(new List<NotificationMessage>
        {
            new NotificationMessage { Date = new DateTimeOffset(2025, 10, 4, 9, 0, 0, TimeSpan.Zero),
                NotificationType = "email", Recipient = "user@mock.com", Sender = "system@ihc.local",
                Subject = "System Alert", Body = "Temperature threshold exceeded.", Delivered = true },
            new NotificationMessage { Date = new DateTimeOffset(2025, 10, 4, 10, 30, 0, TimeSpan.Zero),
                NotificationType = "sms", Recipient = "+4512345678", Sender = "system@ihc.local",
                Subject = "Door opened", Body = "Front door was opened.", Delivered = false },
        }));

        // ClearMessages() is a void op - FakeItEasy returns a completed Task by default.
        return service;
    }

    public static IMessageControlLogService SetupMessageControlLogService(IhcSettings settings)
    {
        var service = A.Fake<IMessageControlLogService>();

        A.CallTo(() => service.GetEvents()).Returns(Task.FromResult<IReadOnlyList<LogEventEntry>>(new List<LogEventEntry>
        {
            new LogEventEntry { Date = new DateTimeOffset(2025, 10, 4, 8, 15, 0, TimeSpan.Zero),
                ControlType = "email", LogEntryType = 1, SenderAddress = "ctrl@mock.com",
                SenderAddressDescription = "Email control handler", TriggerString = "lights_on",
                AuthenticationTypeAsString = "Internal", ActionTypeAsString = "Activate" },
        }));

        // EmptyLog() is a void op - FakeItEasy returns a completed Task by default.
        return service;
    }

    public static IModuleService SetupModuleService(IhcSettings settings)
    {
        var service = A.Fake<IModuleService>();

        A.CallTo(() => service.GetSceneProjectInfo()).Returns(Task.FromResult(MockSceneProjectInfo()));
        A.CallTo(() => service.GetSceneProjectSegmentationSize()).Returns(Task.FromResult(1_048_576));
        A.CallTo(() => service.GetSceneProject(A<string>._)).ReturnsLazily((string name) =>
            Task.FromResult(new SceneProject(string.IsNullOrEmpty(name) ? "mock.icw" : name, new byte[] { 0x01, 0x02, 0x03, 0x04 })));
        A.CallTo(() => service.GetSceneProjectSegment(A<string>._, A<int>._)).ReturnsLazily((string name, int segment) =>
            Task.FromResult(new SceneProject(string.IsNullOrEmpty(name) ? "mock.icw" : name, new byte[] { 0x01, 0x02 })));

        // Accept a stored scene project picked via the Lab's file picker (StoreSceneProject /
        // StoreSceneProjectSegment take a SceneProject : BinaryFile). Report success when bytes were supplied.
        A.CallTo(() => service.StoreSceneProject(A<SceneProject>._)).Returns(Task.CompletedTask);
        A.CallTo(() => service.StoreSceneProjectSegment(A<SceneProject>._, A<bool>._, A<bool>._))
            .ReturnsLazily((SceneProject project, bool isFirst, bool isLast) => Task.FromResult(project?.Data != null));

        // ClearAll() is a void op - FakeItEasy returns a completed Task by default.
        return service;
    }

    public static ITimeManagerService SetupTimeManagerService(IhcSettings settings)
    {
        var service = A.Fake<ITimeManagerService>();

        A.CallTo(() => service.GetCurrentLocalTime()).Returns(Task.FromResult(new DateTimeOffset(2025, 10, 4, 19, 6, 0, TimeSpan.FromHours(2))));
        A.CallTo(() => service.GetUptime()).Returns(Task.FromResult(TimeSpan.FromHours(36)));
        A.CallTo(() => service.GetSettings()).Returns(Task.FromResult(new TimeManagerSettings
        {
            SynchroniseTimeAgainstServer = true, UseDST = true, GmtOffsetInHours = 1,
            ServerName = "time.nist.gov", SyncIntervalInHours = 24,
            TimeAndDateInUTC = new DateTimeOffset(2025, 10, 4, 17, 6, 0, TimeSpan.Zero),
            OnlineCalendarUpdateOnline = true, OnlineCalendarCountry = "DK", OnlineCalendarValidUntil = 2026
        }));
        A.CallTo(() => service.SetSettings(A<TimeManagerSettings>._)).Returns(Task.FromResult(true));
        A.CallTo(() => service.GetTimeFromServer()).Returns(Task.FromResult(new TimeServerConnectionResult
        {
            ConnectionWasSuccessful = true,
            DateFromServer = new DateTimeOffset(2025, 10, 4, 17, 6, 0, TimeSpan.Zero),
            ConnectionFailedDueToUnknownHost = false, ConnectionFailedDueToOtherErrors = false
        }));

        return service;
    }

    public static IAirlinkManagementService SetupAirlinkManagementService(IhcSettings settings)
    {
        var service = A.Fake<IAirlinkManagementService>();

        var device = new RFDevice { BatteryLevel = 85, SignalStrength = 18, DeviceType = 201, SerialNumber = 5678901234L, Version = 2, Detected = true };

        A.CallTo(() => service.EnterRFConfiguration()).Returns(Task.FromResult(true));
        A.CallTo(() => service.ExitRFConfiguration()).Returns(Task.FromResult(true));
        A.CallTo(() => service.EnterRFTest()).Returns(Task.FromResult(true));
        A.CallTo(() => service.ExitRFTest()).Returns(Task.FromResult(true));
        A.CallTo(() => service.TestRFActuatorWithSerialNumber(A<long>._)).Returns(Task.FromResult(true));
        A.CallTo(() => service.GetDevicesRunningOutOfBattery()).Returns(Task.FromResult<IReadOnlyList<int>>(new List<int> { 2001 }));
        A.CallTo(() => service.WaitForDeviceDetected(A<int>._)).Returns(Task.FromResult(device));
        A.CallTo(() => service.WaitForDeviceTestResult(A<int>._)).Returns(Task.FromResult(device));
        A.CallTo(() => service.GetDetectedDeviceList()).Returns(Task.FromResult<IReadOnlyList<RFDevice>>(new List<RFDevice> { device }));
        A.CallTo(() => service.GetBatteryLevel(A<int>._)).Returns(Task.FromResult(85));

        return service;
    }

    public static IInternalTestService SetupInternalTestService(IhcSettings settings)
    {
        var service = A.Fake<IInternalTestService>();

        A.CallTo(() => service.GetAirlinkVersion()).Returns(Task.FromResult("AL-1.2.3"));
        A.CallTo(() => service.GetIOBoardVersion()).Returns(Task.FromResult("IO-2.0.1"));
        A.CallTo(() => service.GetWiserBoardVersion()).Returns(Task.FromResult("WB-3.1.0"));
        A.CallTo(() => service.GetWiserBoardMACAddress()).Returns(Task.FromResult("00:1A:2B:3C:4D:5E"));
        A.CallTo(() => service.GetWiserBoardHWVersion()).Returns(Task.FromResult("HW-1"));
        A.CallTo(() => service.GetWiserBoardSerialNumber()).Returns(Task.FromResult("WSN-0001"));
        A.CallTo(() => service.GetTimeAndDate()).Returns(Task.FromResult(new DateTimeOffset(2025, 10, 4, 17, 6, 0, TimeSpan.Zero)));
        A.CallTo(() => service.BurnIO()).Returns(Task.FromResult(true));
        A.CallTo(() => service.TestSdCard()).Returns(Task.FromResult(true));
        A.CallTo(() => service.TestIOBoard()).Returns(Task.FromResult(true));
        A.CallTo(() => service.ReadUsbHost()).Returns(Task.FromResult(true));
        A.CallTo(() => service.SendRS485Data(A<string>._)).Returns(Task.FromResult(true));
        A.CallTo(() => service.ReadRS485Data(A<string>._)).Returns(Task.FromResult(true));

        // Void ops (SendAirlinkPacket, ProductionTestPassed, SetTimeAndDate, and all LED Turn* ops) return a
        // completed Task by default via FakeItEasy.
        return service;
    }

    public static ISmsModemService SetupSmsModemService(IhcSettings settings)
    {
        var service = A.Fake<ISmsModemService>();

        // Mock SetSmsModemSettings operation
        A.CallTo(() => service.SetSmsModemSettings(A<SmsModemSettings>._))
            .Returns(Task.CompletedTask);

        // Mock GetSmsModemSettings operation
        A.CallTo(() => service.GetSmsModemSettings())
            .Returns(Task.FromResult(new SmsModemSettings
            {
                PowerupMessage = "",
                PowerdownMessage = "",
                PowerdownNumber = "",
                RelaySMS = false,
                ForceStandAloneMode = false,
                SendLowBatteryNotification = false,
                SendLowBatteryNotificationLanguage = false,
                SendLEDDimmerErrorNotification = false
            }));

        A.CallTo(() => service.GetSmsModemStatus()).Returns(Task.FromResult(new SmsModemStatus
        {
            AntennaCoverage = "24", MobileOperator = "Telia", ModemStatus = "Ready", MobileNumber = "+4540123456"
        }));
        A.CallTo(() => service.GetSmsModemInfo()).Returns(Task.FromResult(new SmsModemInfo
        {
            FirmwareVersion = "1.10.45", GSMChipVersion = "SIM800H", HardwareRevision = "v2.1",
            ProductionDate = "2024-01-15", Detected = true, SerialNumber = "SN12345678", IMEINumber = "354856070135231"
        }));

        // ResetSmsModem() is a void op - FakeItEasy returns a completed Task by default.
        return service;
    }

    // Canonical mock fixtures shared by the services that expose the same data, so the literal is defined once
    // (the ControllerService and OpenAPIService project info; the OpenAPIService and ModuleService scene info).

    private static ProjectInfo MockProjectInfo() => new ProjectInfo
    {
        CustomerName = "Mock Customer", InstallerName = "Mock Installer", ProjectNumber = "12345",
        VisualMajorVersion = 4, VisualMinorVersion = 0, ProjectMajorRevision = 1, ProjectMinorRevision = 2,
        Lastmodified = new DateTimeOffset(2025, 10, 4, 17, 6, 0, TimeSpan.Zero)
    };

    private static SceneProjectInfo MockSceneProjectInfo() => new SceneProjectInfo
    {
        Name = "Mock Scene", Size = 4096, Filepath = "scenes/mock.icw", Remote = false, Version = "1.0",
        Created = new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero),
        LastModified = new DateTimeOffset(2025, 10, 4, 17, 6, 0, TimeSpan.Zero),
        Description = "Mock scene project", Crc = 0x1A2B3C4D
    };
}
    