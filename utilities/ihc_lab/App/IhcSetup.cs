using System;
using System.Linq;
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
    private static T CreateEmptyFake<T>() where T : class => A.Fake<T>();

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

        A.CallTo(() => service.GetProjectInfo()).Returns(Task.FromResult(new ProjectInfo()
        {
            CustomerName = "Mock Customer",
            InstallerName = "Mock Installer",
            ProjectNumber = "12345",
            VisualMajorVersion = 4,
            VisualMinorVersion = 0,
            ProjectMajorRevision = 1,
            ProjectMinorRevision = 2,
            Lastmodified = new DateTimeOffset(2025, 10, 4, 17, 6, 0, TimeSpan.Zero)
        }));

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

    public static IResourceInteractionService SetupResourceInteractionService(IhcSettings settings)
        => CreateEmptyFake<IResourceInteractionService>();

    public static IConfigurationService SetupConfigurationService(IhcSettings settings)
        => CreateEmptyFake<IConfigurationService>();

    public static IOpenAPIService SetupOpenAPIService(IhcSettings settings)
        => CreateEmptyFake<IOpenAPIService>();

    public static INotificationManagerService SetupNotificationManagerService(IhcSettings settings)
        => CreateEmptyFake<INotificationManagerService>();

    public static IMessageControlLogService SetupMessageControlLogService(IhcSettings settings)
        => CreateEmptyFake<IMessageControlLogService>();

    public static IModuleService SetupModuleService(IhcSettings settings)
        => CreateEmptyFake<IModuleService>();

    public static ITimeManagerService SetupTimeManagerService(IhcSettings settings)
        => CreateEmptyFake<ITimeManagerService>();

    public static IUserManagerService SetupUserManagerService(IhcSettings settings)
        => CreateEmptyFake<IUserManagerService>();

    public static IAirlinkManagementService SetupAirlinkManagementService(IhcSettings settings)
        => CreateEmptyFake<IAirlinkManagementService>();

    public static IInternalTestService SetupInternalTestService(IhcSettings settings)
        => CreateEmptyFake<IInternalTestService>();

    public static ISmsModemService SetupSmsModemService(IhcSettings settings)
        => CreateEmptyFake<ISmsModemService>();
}
    