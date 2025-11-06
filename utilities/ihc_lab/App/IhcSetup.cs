using System;
using System.Linq;
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

        // Setup faked return values for authentication service:
        A.CallTo(() => service.Authenticate()).Returns(new IhcUser
        {
            Username = settings.UserName,
            Password = settings.Password,
            Firstname = "Mock",
            Lastname = settings.Application.ToString(),
            Group = IhcUserGroup.Administrators
        });

        A.CallTo(() => service.Authenticate(A<string>._, A<string>._, A<Application>._)).ReturnsLazily((string u, string p, Application app) => new IhcUser
        {
            Username = u,
            Password = p,
            Firstname = "Mock",
            Lastname = app.ToString(),
            Group = IhcUserGroup.Administrators
        });

        A.CallTo(() => service.Ping()).Returns(true);
        A.CallTo(() => service.Disconnect()).Returns(true);

        return service;
    }

    public static IControllerService SetupControllerService(IhcSettings settings)
    {
        var service = A.Fake<IControllerService>();

        A.CallTo(() => service.IsIHCProjectAvailable()).Returns(true);

        A.CallTo(() => service.GetProjectInfo()).Returns(new ProjectInfo()
        {
            CustomerName = "ihcclient",
            InstallerName = "ihcclient",
        });

        A.CallTo(() => service.GetProject()).Returns(new Ihc.ProjectFile("project-mock.vis",
            """
                    <?xml version="1.0" encoding="ISO-8859-1"?>
                    <utcs_project version_major="4" version_minor="0" id1="1" id2="2" last_unique_id="3">
                        <modified year="2025" month="10" day="4" hour="17" minute="6"/>
                        <customer_info name="ihcclient"/>
                        <installer_info name="ihcclient" country="Danmark"/>
                        <project_info programmer="ihcclient" number="42" description="empty installation"/>
                    </utcs_project>   
                """
        ));

        A.CallTo(() => service.GetBackup()).Returns(new Ihc.BackupFile("backup-mock.dat",
            new byte[] { 0x42, 0x25 }
        ));

        A.CallTo(() => service.StoreProject(A<ProjectFile>._)).ReturnsLazily((ProjectFile prj) =>
        {
            return prj?.Data.StartsWith("<?xml") == true;
        });

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
    