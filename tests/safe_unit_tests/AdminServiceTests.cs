using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ihc;
using Ihc.App;
using FakeItEasy;

namespace Ihc.Tests
{
    /// <summary>
    /// Unit tests for AdminService that verify change tracking and API integration
    /// using FakeItEasy mocked services (no actual controller connection).
    /// </summary>
    [TestFixture]
    public class AdminServiceTests
    {
        #pragma warning disable NUnit1032 // Fakes from FakeItEasy don't need disposal
        private IAuthenticationService fakeAuthService;
        #pragma warning restore NUnit1032
        private IUserManagerService fakeUserService;
        private IConfigurationService fakeConfigService;
        private IhcSettings settings;

        [SetUp]
        public void Setup()
        {
            // Set up encryption passphrase for tests
            Environment.SetEnvironmentVariable("IHC_ENCRYPT_PASSPHRASE", "test-passphrase-for-unit-tests");

            // Create fake services
            fakeAuthService = A.Fake<IAuthenticationService>();
            fakeUserService = A.Fake<IUserManagerService>();
            fakeConfigService = A.Fake<IConfigurationService>();

            // Configure fake auth service to always report as authenticated
            A.CallTo(() => fakeAuthService.IsAuthenticated()).Returns(Task.FromResult(true));

            // Create test settings
            settings = new IhcSettings
            {
                Endpoint = "http://test",
                UserName = "testuser",
                Password = "testpass",
                Application = "administrator",
                LogSensitiveData = false,
                AsyncContinueOnCapturedContext = false
            };
        }

        [Test]
        public void Constructor_WithSettings_CreatesInstance()
        {
            // Act
            var service = new AdminService(settings, fileEnryption: true);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithServicesAndSettings_CreatesInstance()
        {
            // Act
            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullSettings_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new AdminService(null, fileEnryption: true));
        }

        [Test]
        public void Constructor_WithNullServices_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AdminService(settings, fileEnryption: true, null, fakeUserService, fakeConfigService));
            Assert.Throws<ArgumentNullException>(() => new AdminService(settings, fileEnryption: true, fakeAuthService, null, fakeConfigService));
            Assert.Throws<ArgumentNullException>(() => new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, null));
        }

        [Test]
        public async Task GetAdminModel_ReturnsModelWithData()
        {
            // Arrange
            IReadOnlySet<IhcUser> testUsers = new HashSet<IhcUser>
            {
                new IhcUser { Username = "user1", Email = "user1@test.com", Group = IhcUserGroup.Administrators },
                new IhcUser { Username = "user2", Email = "user2@test.com", Group = IhcUserGroup.Users }
            };

            var testEmailControl = new EmailControlSettings
            {
                ServerIpAddress = "mail.test.com",
                ServerPortNumber = 110,
                EmailAddress = "test@test.com"
            };

            var testSmtp = new SMTPSettings
            {
                Hostname = "smtp.test.com",
                Hostport = 465,
                Username = "smtp_user"
            };

            var testDns = new DNSServers
            {
                PrimaryDNS = "8.8.8.8",
                SecondaryDNS = "8.8.4.4"
            };

            var testNetwork = new NetworkSettings
            {
                IpAddress = "192.168.1.100",
                Netmask = "255.255.255.0",
                Gateway = "192.168.1.1",
                HttpPort = 80,
                HttpsPort = 443
            };

            var testWebAccess = new WebAccessControl
            {
                AdministratorInternal = true,
                OpenapiInternal = true
            };

            var testWLan = new WLanSettings
            {
                Enabled = false,
                Ssid = "TestNetwork"
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<IReadOnlySet<IhcUser>>(testUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(testEmailControl));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(testSmtp));
            A.CallTo(() => fakeConfigService.GetDNSServers()).Returns(Task.FromResult(testDns));
            A.CallTo(() => fakeConfigService.GetNetworkSettings()).Returns(Task.FromResult(testNetwork));
            A.CallTo(() => fakeConfigService.GetWebAccessControl()).Returns(Task.FromResult(testWebAccess));
            A.CallTo(() => fakeConfigService.GetWLanSettings()).Returns(Task.FromResult(testWLan));

            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);

            // Act
            var model = await service.GetModel();

            // Assert
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Users, Is.Not.Null);
            Assert.That(model.Users.Count, Is.EqualTo(2));
            Assert.That(model.EmailControl, Is.EqualTo(testEmailControl));
            Assert.That(model.SmtpSettings, Is.EqualTo(testSmtp));
            Assert.That(model.DnsServers, Is.EqualTo(testDns));
            Assert.That(model.NetworkSettings, Is.EqualTo(testNetwork));
            Assert.That(model.WebAccess, Is.EqualTo(testWebAccess));
            Assert.That(model.WLanSettings, Is.EqualTo(testWLan));

            // Verify API calls
            A.CallTo(() => fakeUserService.GetUsers(true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.GetDNSServers()).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.GetNetworkSettings()).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.GetWebAccessControl()).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.GetWLanSettings()).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_NoChanges_NoApiCalls()
        {
            // Arrange
            IReadOnlySet<IhcUser> testUsers = new HashSet<IhcUser>
            {
                new IhcUser { Username = "user1", Email = "user1@test.com", Group = IhcUserGroup.Administrators }
            };

            var testEmailControl = new EmailControlSettings { ServerIpAddress = "mail.test.com" };
            var testSmtp = new SMTPSettings { Hostname = "smtp.test.com" };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<IReadOnlySet<IhcUser>>(testUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(testEmailControl));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(testSmtp));

            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - save same model without changes
            await service.Store(model);

            // Assert - no update calls should be made
            A.CallTo(() => fakeUserService.AddUser(A<IhcUser>._)).MustNotHaveHappened();
            A.CallTo(() => fakeUserService.UpdateUser(A<IhcUser>._)).MustNotHaveHappened();
            A.CallTo(() => fakeUserService.RemoveUser(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => fakeConfigService.SetEmailControlSettings(A<EmailControlSettings>._)).MustNotHaveHappened();
            A.CallTo(() => fakeConfigService.SetSMTPSettings(A<SMTPSettings>._)).MustNotHaveHappened();
        }

        [Test]
        public async Task SaveAdminModel_UserAdded_CallsAddUser()
        {
            // Arrange
            IReadOnlySet<IhcUser> initialUsers = new HashSet<IhcUser>
            {
                new IhcUser { Username = "user1", Email = "user1@test.com", Group = IhcUserGroup.Users }
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<IReadOnlySet<IhcUser>>(initialUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings()));
            A.CallTo(() => fakeConfigService.GetDNSServers()).Returns(Task.FromResult(new DNSServers()));
            A.CallTo(() => fakeConfigService.GetNetworkSettings()).Returns(Task.FromResult(new NetworkSettings()));
            A.CallTo(() => fakeConfigService.GetWebAccessControl()).Returns(Task.FromResult(new WebAccessControl()));
            A.CallTo(() => fakeConfigService.GetWLanSettings()).Returns(Task.FromResult(new WLanSettings()));

            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - add a new user
            var newUser = new IhcUser { Username = "user2", Email = "user2@test.com", Group = IhcUserGroup.Users };
            model.Users.Add(newUser);
            await service.Store(model);

            // Assert
            A.CallTo(() => fakeUserService.AddUser(A<IhcUser>.That.Matches(u => u.Username == "user2")))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_UserDeleted_CallsRemoveUser()
        {
            // Arrange
            IReadOnlySet<IhcUser> initialUsers = new HashSet<IhcUser>
            {
                new IhcUser { Username = "user1", Email = "user1@test.com", Group = IhcUserGroup.Users },
                new IhcUser { Username = "user2", Email = "user2@test.com", Group = IhcUserGroup.Users }
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<IReadOnlySet<IhcUser>>(initialUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings()));
            A.CallTo(() => fakeConfigService.GetDNSServers()).Returns(Task.FromResult(new DNSServers()));
            A.CallTo(() => fakeConfigService.GetNetworkSettings()).Returns(Task.FromResult(new NetworkSettings()));
            A.CallTo(() => fakeConfigService.GetWebAccessControl()).Returns(Task.FromResult(new WebAccessControl()));
            A.CallTo(() => fakeConfigService.GetWLanSettings()).Returns(Task.FromResult(new WLanSettings()));

            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - remove a user
            var userToRemove = model.Users.First(u => u.Username == "user2");
            model.Users.Remove(userToRemove);
            await service.Store(model);

            // Assert
            A.CallTo(() => fakeUserService.RemoveUser("user2")).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_UserUpdated_CallsUpdateUser()
        {
            // Arrange
            IReadOnlySet<IhcUser> initialUsers = new HashSet<IhcUser>
            {
                new IhcUser
                {
                    Username = "user1",
                    Email = "old@test.com",
                    Firstname = "Old",
                    Lastname = "Last",
                    Phone = "123",
                    Group = IhcUserGroup.Users,
                    Password = "pass",
                    Project = "proj",
                    CreatedDate = DateTimeOffset.Now,
                    LoginDate = DateTimeOffset.Now
                }
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<IReadOnlySet<IhcUser>>(initialUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings()));
            A.CallTo(() => fakeConfigService.GetDNSServers()).Returns(Task.FromResult(new DNSServers()));
            A.CallTo(() => fakeConfigService.GetNetworkSettings()).Returns(Task.FromResult(new NetworkSettings()));
            A.CallTo(() => fakeConfigService.GetWebAccessControl()).Returns(Task.FromResult(new WebAccessControl()));
            A.CallTo(() => fakeConfigService.GetWLanSettings()).Returns(Task.FromResult(new WLanSettings()));

            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - update user with new properties (using HashSet operations)
            var oldUser = model.Users.First(u => u.Username == "user1");
            model.Users.Remove(oldUser);
            var updatedUser = oldUser with { Email = "new@test.com", Firstname = "New" };
            model.Users.Add(updatedUser);
            await service.Store(model);

            // Assert - verify UpdateUser was called with user containing updated values
            A.CallTo(() => fakeUserService.UpdateUser(A<IhcUser>.That.Matches(u =>
                u.Username == "user1" && u.Email == "new@test.com" && u.Firstname == "New")))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_EmailControlChanged_CallsSetEmailControlSettings()
        {
            // Arrange
            var initialEmailControl = new EmailControlSettings
            {
                ServerIpAddress = "old.mail.com",
                ServerPortNumber = 110,
                Pop3Username = "user",
                Pop3Password = "pass",
                EmailAddress = "test@test.com",
                PollInterval = 60,
                RemoveEmailsAfterUsage = false,
                Ssl = false
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<IReadOnlySet<IhcUser>>(new HashSet<IhcUser>()));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(initialEmailControl));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings()));

            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - change email control settings (using record 'with' expression)
            model.EmailControl = model.EmailControl with { ServerIpAddress = "new.mail.com", ServerPortNumber = 995 };
            await service.Store(model);

            // Assert - verify SetEmailControlSettings was called (don't check exact match due to record equality)
            A.CallTo(() => fakeConfigService.SetEmailControlSettings(A<EmailControlSettings>._))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_SmtpSettingsChanged_CallsSetSMTPSettings()
        {
            // Arrange
            var initialSmtp = new SMTPSettings
            {
                Hostname = "old.smtp.com",
                Hostport = 465,
                Username = "user",
                Password = "pass",
                Ssl = true,
                SendLowBatteryNotification = false,
                SendLowBatteryNotificationRecipient = ""
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<IReadOnlySet<IhcUser>>(new HashSet<IhcUser>()));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(initialSmtp));

            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - change SMTP settings (using record 'with' expression)
            model.SmtpSettings = model.SmtpSettings with { Hostname = "new.smtp.com", Hostport = 587 };
            await service.Store(model);

            // Assert - verify SetSMTPSettings was called
            A.CallTo(() => fakeConfigService.SetSMTPSettings(A<SMTPSettings>._))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_WithoutGetAdminModel_LoadsSnapshotAutomatically()
        {
            // Arrange
            IReadOnlySet<IhcUser> testUsers = new HashSet<IhcUser>
            {
                new IhcUser { Username = "user1", Email = "user1@test.com", Group = IhcUserGroup.Users }
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<IReadOnlySet<IhcUser>>(testUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings()));
            A.CallTo(() => fakeConfigService.GetDNSServers()).Returns(Task.FromResult(new DNSServers()));
            A.CallTo(() => fakeConfigService.GetNetworkSettings()).Returns(Task.FromResult(new NetworkSettings()));
            A.CallTo(() => fakeConfigService.GetWebAccessControl()).Returns(Task.FromResult(new WebAccessControl()));
            A.CallTo(() => fakeConfigService.GetWLanSettings()).Returns(Task.FromResult(new WLanSettings()));

            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);

            // Act - call SaveAdminModel without calling GetAdminModel first
            var newModel = new MutableAdminModel
            {
                Users = new HashSet<IhcUser> { new IhcUser { Username = "user2", Email = "user2@test.com", Group = IhcUserGroup.Users } },
                EmailControl = new EmailControlSettings(),
                SmtpSettings = new SMTPSettings()
            };
            await service.Store(newModel);

            // Assert - should have loaded snapshot automatically
            A.CallTo(() => fakeUserService.GetUsers(true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeUserService.AddUser(A<IhcUser>.That.Matches(u => u.Username == "user2")))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeUserService.RemoveUser("user1")).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_MultipleChanges_AppliesAllChanges()
        {
            // Arrange
            IReadOnlySet<IhcUser> initialUsers = new HashSet<IhcUser>
            {
                new IhcUser
                {
                    Username = "user1",
                    Email = "user1@test.com",
                    Group = IhcUserGroup.Users,
                    Password = "pass",
                    Firstname = "First",
                    Lastname = "Last",
                    Phone = "123",
                    Project = "proj",
                    CreatedDate = DateTimeOffset.Now,
                    LoginDate = DateTimeOffset.Now
                }
            };
            var initialEmailControl = new EmailControlSettings
            {
                ServerIpAddress = "old.mail.com",
                ServerPortNumber = 110,
                Pop3Username = "",
                Pop3Password = "",
                EmailAddress = "",
                PollInterval = 60,
                RemoveEmailsAfterUsage = false,
                Ssl = false
            };
            var initialSmtp = new SMTPSettings
            {
                Hostname = "old.smtp.com",
                Hostport = 465,
                Username = "",
                Password = "",
                Ssl = false,
                SendLowBatteryNotification = false,
                SendLowBatteryNotificationRecipient = ""
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<IReadOnlySet<IhcUser>>(initialUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(initialEmailControl));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(initialSmtp));

            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - make multiple changes
            model.Users.Add(new IhcUser
            {
                Username = "user2",
                Email = "user2@test.com",
                Group = IhcUserGroup.Users,
                Password = "pass2",
                Firstname = "First2",
                Lastname = "Last2",
                Phone = "456",
                Project = "proj",
                CreatedDate = DateTimeOffset.Now,
                LoginDate = DateTimeOffset.Now
            });
            model.EmailControl = model.EmailControl with { ServerIpAddress = "new.mail.com" };
            model.SmtpSettings = model.SmtpSettings with { Hostname = "new.smtp.com" };
            await service.Store(model);

            // Assert - all changes should be applied
            A.CallTo(() => fakeUserService.AddUser(A<IhcUser>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.SetEmailControlSettings(A<EmailControlSettings>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.SetSMTPSettings(A<SMTPSettings>._)).MustHaveHappenedOnceExactly();
        }

        #region JSON Stream Tests

        /// <summary>
        /// Helper method to create a test AdminModel with all properties populated.
        /// </summary>
        private MutableAdminModel CreateTestAdminModel()
        {
            return new MutableAdminModel
            {
                Users = new HashSet<IhcUser>
                {
                    new IhcUser
                    {
                        Username = "admin",
                        Password = "admin123",
                        Email = "admin@test.com",
                        Firstname = "Admin",
                        Lastname = "User",
                        Phone = "1234567890",
                        Group = IhcUserGroup.Administrators,
                        Project = "TestProject",
                        CreatedDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                        LoginDate = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero)
                    },
                    new IhcUser
                    {
                        Username = "user1",
                        Password = "user123",
                        Email = "user@test.com",
                        Firstname = "Regular",
                        Lastname = "User",
                        Phone = "9876543210",
                        Group = IhcUserGroup.Users,
                        Project = "TestProject",
                        CreatedDate = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
                        LoginDate = new DateTimeOffset(2024, 1, 16, 14, 20, 0, TimeSpan.Zero)
                    }
                },
                EmailControl = new EmailControlSettings
                {
                    ServerIpAddress = "mail.test.com",
                    ServerPortNumber = 110,
                    Pop3Username = "testuser",
                    Pop3Password = "pop3secret",
                    EmailAddress = "control@test.com",
                    PollInterval = 5,
                    RemoveEmailsAfterUsage = true,
                    Ssl = false
                },
                SmtpSettings = new SMTPSettings
                {
                    Hostname = "smtp.test.com",
                    Hostport = 587,
                    Username = "smtpuser",
                    Password = "smtpSecret",
                    Ssl = true,
                    SendLowBatteryNotification = false,
                    SendLowBatteryNotificationRecipient = ""
                },
                WLanSettings = new WLanSettings
                {
                    Enabled = true,
                    Ssid = "TestWiFi",
                    Key = "wifiSecret123",
                    SecurityType = "WPA2",
                    EncryptionType = "AES",
                    IpAddress = "192.168.2.1",
                    Netmask = "255.255.255.0",
                    Gateway = "192.168.2.254"
                },
                DnsServers = new DNSServers
                {
                    PrimaryDNS = "8.8.8.8",
                    SecondaryDNS = "8.8.4.4"
                },
                NetworkSettings = new NetworkSettings
                {
                    IpAddress = "192.168.1.100",
                    Netmask = "255.255.255.0",
                    Gateway = "192.168.1.1",
                    HttpPort = 80,
                    HttpsPort = 443
                },
                WebAccess = new WebAccessControl
                {
                    UsbLoginRequired = false,
                    AdministratorUsb = true,
                    AdministratorInternal = true,
                    AdministratorExternal = false
                }
            };
        }

        [Test]
        public async Task SaveAndLoadJson_WithEncryptionEnabled_RoundTripSucceeds()
        {
            // Arrange
            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var originalModel = CreateTestAdminModel();
            var stream = new System.IO.MemoryStream();

            // Act - Save and then load
            await service.SaveAsJson(originalModel, stream);
            stream.Position = 0;
            var loadedModel = await service.LoadFromJson(stream);

            // Assert - All properties should match
            Assert.That(loadedModel, Is.Not.Null);
            Assert.That(loadedModel.Users, Is.Not.Null);
            Assert.That(loadedModel.Users.Count, Is.EqualTo(originalModel.Users.Count));

            // Verify users
            var originalAdmin = originalModel.Users.First(u => u.Username == "admin");
            var loadedAdmin = loadedModel.Users.First(u => u.Username == "admin");
            Assert.That(loadedAdmin.Password, Is.EqualTo(originalAdmin.Password));
            Assert.That(loadedAdmin.Email, Is.EqualTo(originalAdmin.Email));

            // Verify sensitive fields preserved
            Assert.That(loadedModel.EmailControl.Pop3Password, Is.EqualTo(originalModel.EmailControl.Pop3Password));
            Assert.That(loadedModel.SmtpSettings.Password, Is.EqualTo(originalModel.SmtpSettings.Password));
            Assert.That(loadedModel.WLanSettings.Key, Is.EqualTo(originalModel.WLanSettings.Key));

            // Verify non-sensitive fields
            Assert.That(loadedModel.DnsServers.PrimaryDNS, Is.EqualTo(originalModel.DnsServers.PrimaryDNS));
            Assert.That(loadedModel.NetworkSettings.IpAddress, Is.EqualTo(originalModel.NetworkSettings.IpAddress));
        }

        [Test]
        public async Task SaveAndLoadJson_WithEncryptionDisabled_RoundTripSucceeds()
        {
            // Arrange
            var service = new AdminService(settings, fileEnryption: false, fakeAuthService, fakeUserService, fakeConfigService);
            var originalModel = CreateTestAdminModel();
            var stream = new System.IO.MemoryStream();

            // Act - Save and then load
            await service.SaveAsJson(originalModel, stream);
            stream.Position = 0;
            var loadedModel = await service.LoadFromJson(stream);

            // Assert - All properties should match
            Assert.That(loadedModel, Is.Not.Null);
            Assert.That(loadedModel.Users.Count, Is.EqualTo(originalModel.Users.Count));
            Assert.That(loadedModel.EmailControl.Pop3Password, Is.EqualTo(originalModel.EmailControl.Pop3Password));
            Assert.That(loadedModel.SmtpSettings.Password, Is.EqualTo(originalModel.SmtpSettings.Password));
            Assert.That(loadedModel.WLanSettings.Key, Is.EqualTo(originalModel.WLanSettings.Key));
        }

        [Test]
        public async Task SaveAsJson_WithEncryptionEnabled_EncryptsSensitiveFieldsOnly()
        {
            // Arrange - Verify environment variable is set
            var envVar = Environment.GetEnvironmentVariable("IHC_ENCRYPT_PASSPHRASE");
            Assert.That(envVar, Is.Not.Null, "IHC_ENCRYPT_PASSPHRASE environment variable should be set");

            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = CreateTestAdminModel();
            var stream = new System.IO.MemoryStream();

            // Act - Save to stream
            await service.SaveAsJson(model, stream);

            // Read the JSON string
            stream.Position = 0;
            var jsonString = System.Text.Encoding.UTF8.GetString(stream.ToArray());

            // Assert - Sensitive fields should be encrypted (NOT appear in plain text)
            Assert.That(jsonString, Does.Not.Contain("admin123"), "User password should be encrypted");
            Assert.That(jsonString, Does.Not.Contain("user123"), "User password should be encrypted");
            Assert.That(jsonString, Does.Not.Contain("pop3secret"), "POP3 password should be encrypted");
            Assert.That(jsonString, Does.Not.Contain("smtpSecret"), "SMTP password should be encrypted");
            Assert.That(jsonString, Does.Not.Contain("wifiSecret123"), "WiFi key should be encrypted");

            // Assert - Non-sensitive fields should appear in plain text
            Assert.That(jsonString, Does.Contain("admin@test.com"), "Email should not be encrypted");
            Assert.That(jsonString, Does.Contain("smtp.test.com"), "SMTP hostname should not be encrypted");
            Assert.That(jsonString, Does.Contain("8.8.8.8"), "DNS should not be encrypted");
            Assert.That(jsonString, Does.Contain("192.168.1.100"), "IP address should not be encrypted");
            Assert.That(jsonString, Does.Contain("TestWiFi"), "SSID should not be encrypted");
        }

        [Test]
        public async Task SaveAsJson_WithEncryptionDisabled_DoesNotEncryptFields()
        {
            // Arrange
            var service = new AdminService(settings, fileEnryption: false, fakeAuthService, fakeUserService, fakeConfigService);
            var model = CreateTestAdminModel();
            var stream = new System.IO.MemoryStream();

            // Act - Save to stream
            await service.SaveAsJson(model, stream);

            // Read the JSON string
            stream.Position = 0;
            var jsonString = System.Text.Encoding.UTF8.GetString(stream.ToArray());

            // Assert - Sensitive fields should appear in plain text (not encrypted)
            Assert.That(jsonString, Does.Contain("admin123"), "User password should not be encrypted when encryption disabled");
            Assert.That(jsonString, Does.Contain("user123"), "User password should not be encrypted when encryption disabled");
            Assert.That(jsonString, Does.Contain("pop3secret"), "POP3 password should not be encrypted when encryption disabled");
            Assert.That(jsonString, Does.Contain("smtpSecret"), "SMTP password should not be encrypted when encryption disabled");
            Assert.That(jsonString, Does.Contain("wifiSecret123"), "WiFi key should not be encrypted when encryption disabled");
        }

        [Test]
        public async Task SaveAsJson_DoesNotModifyOriginalModel()
        {
            // Arrange
            var service = new AdminService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = CreateTestAdminModel();
            var stream = new System.IO.MemoryStream();

            // Capture original sensitive values
            var originalUserPassword = model.Users.First(u => u.Username == "admin").Password;
            var originalPop3Password = model.EmailControl.Pop3Password;
            var originalSmtpPassword = model.SmtpSettings.Password;
            var originalWifiKey = model.WLanSettings.Key;

            // Act - Save to stream (which encrypts internally)
            await service.SaveAsJson(model, stream);

            // Assert - Original model should remain unchanged
            var adminUser = model.Users.First(u => u.Username == "admin");
            Assert.That(adminUser.Password, Is.EqualTo(originalUserPassword), "Original user password should not be modified");
            Assert.That(model.EmailControl.Pop3Password, Is.EqualTo(originalPop3Password), "Original POP3 password should not be modified");
            Assert.That(model.SmtpSettings.Password, Is.EqualTo(originalSmtpPassword), "Original SMTP password should not be modified");
            Assert.That(model.WLanSettings.Key, Is.EqualTo(originalWifiKey), "Original WiFi key should not be modified");
        }

        #endregion
    }
}
