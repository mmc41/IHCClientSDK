using Ihc;
using FakeItEasy;

namespace IhcLab {
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

            // Setup faked return values for authentication service:
            A.CallTo(() => service.Authenticate()).Returns(new IhcUser
            {
                Username = settings.UserName,
                Password = settings.Password,
                Firstname = "Mock",
                Lastname = settings.Application,
                Group = IhcUserGroup.Administrators
            });

            A.CallTo(() => service.Authenticate(A<string>._, A<string>._, A<string>._)).ReturnsLazily((string u, string p, string app) => new IhcUser
            {
                Username = u,
                Password = p,
                Firstname = "Mock",
                Lastname = app,
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
        {
            var service = A.Fake<IResourceInteractionService>();
            return service;
        }

        public static IConfigurationService SetupConfigurationService(IhcSettings settings)
        {
            var service = A.Fake<IConfigurationService>();
            return service;
        }

        public static IOpenAPIService SetupOpenAPIService(IhcSettings settings)
        {
            var service = A.Fake<IOpenAPIService>();
            return service;
        }

        public static INotificationManagerService SetupNotificationManagerService(IhcSettings settings)
        {
            var service = A.Fake<INotificationManagerService>();
            return service;
        }

        public static IMessageControlLogService SetupMessageControlLogService(IhcSettings settings)
        {
            var service = A.Fake<IMessageControlLogService>();
            return service;
        }

        public static IModuleService SetupModuleService(IhcSettings settings)
        {
            var service = A.Fake<IModuleService>();
            return service;
        }

        public static ITimeManagerService SetupTimeManagerService(IhcSettings settings)
        {
            var service = A.Fake<ITimeManagerService>();
            return service;
        }

        public static IUserManagerService SetupUserManagerService(IhcSettings settings)
        {
            var service = A.Fake<IUserManagerService>();
            return service;
        }

        public static IAirlinkManagementService SetupAirlinkManagementService(IhcSettings settings)
        {
            var service = A.Fake<IAirlinkManagementService>();
            return service;
        }
    }
}