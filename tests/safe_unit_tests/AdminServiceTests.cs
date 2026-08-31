using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CsCheck;
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
                Application = Application.administrator,
                LogSensitiveData = false,
                AsyncContinueOnCapturedContext = false
            };
        }

        /// <summary>
        /// Arranges the seven reads <see cref="AdminAppService.GetModel"/> makes for its snapshot, so a test states
        /// only the values it is about and every other read still answers with a default instance. Adding a getter to
        /// that snapshot is one edit here rather than one per test.
        /// </summary>
        private void ArrangeSnapshot(
            IReadOnlySet<IhcUser>? users = null,
            EmailControlSettings? emailControl = null,
            SMTPSettings? smtp = null,
            DNSServers? dns = null,
            NetworkSettings? network = null,
            WebAccessControl? webAccess = null,
            WLanSettings? wlan = null)
        {
            A.CallTo(() => fakeUserService.GetUsers(true))
                .Returns(Task.FromResult(users ?? new HashSet<IhcUser>()));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings())
                .Returns(Task.FromResult(emailControl ?? new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings())
                .Returns(Task.FromResult(smtp ?? new SMTPSettings()));
            A.CallTo(() => fakeConfigService.GetDNSServers())
                .Returns(Task.FromResult(dns ?? new DNSServers()));
            A.CallTo(() => fakeConfigService.GetNetworkSettings())
                .Returns(Task.FromResult(network ?? new NetworkSettings()));
            A.CallTo(() => fakeConfigService.GetWebAccessControl())
                .Returns(Task.FromResult(webAccess ?? new WebAccessControl()));
            A.CallTo(() => fakeConfigService.GetWLanSettings())
                .Returns(Task.FromResult(wlan ?? new WLanSettings()));
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

            ArrangeSnapshot(testUsers, testEmailControl, testSmtp, testDns, testNetwork, testWebAccess, testWLan);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);

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

            ArrangeSnapshot(testUsers, testEmailControl, testSmtp);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - save same model without changes
            var changeInfo = await service.Store(model);

            // Assert - no changes detected
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(0));
            Assert.That(changeInfo.RebootRequired, Is.False);

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

            ArrangeSnapshot(initialUsers);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - add a new user
            var newUser = new IhcUser { Username = "user2", Email = "user2@test.com", Group = IhcUserGroup.Users };
            model.Users.Add(newUser);
            var changeInfo = await service.Store(model);

            // Assert - verify change information
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(1));
            Assert.That(changeInfo.RebootRequired, Is.False);

            // Assert - verify API call
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

            ArrangeSnapshot(initialUsers);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - remove a user
            var userToRemove = model.Users.First(u => u.Username == "user2");
            model.Users.Remove(userToRemove);
            var changeInfo = await service.Store(model);

            // Assert - verify change information
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(1));
            Assert.That(changeInfo.RebootRequired, Is.False);

            // Assert - verify API call
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

            ArrangeSnapshot(initialUsers);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - update user with new properties (using HashSet operations)
            var oldUser = model.Users.First(u => u.Username == "user1");
            model.Users.Remove(oldUser);
            var updatedUser = oldUser with { Email = "new@test.com", Firstname = "New" };
            model.Users.Add(updatedUser);
            var changeInfo = await service.Store(model);

            // Assert - verify change information
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(1));
            Assert.That(changeInfo.RebootRequired, Is.False);

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

            ArrangeSnapshot(emailControl: initialEmailControl);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - change email control settings (using record 'with' expression)
            model.EmailControl = model.EmailControl with { ServerIpAddress = "new.mail.com", ServerPortNumber = 995 };
            var changeInfo = await service.Store(model);

            // Assert - verify change information
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(1));
            Assert.That(changeInfo.RebootRequired, Is.False);

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

            ArrangeSnapshot(smtp: initialSmtp);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - change SMTP settings (using record 'with' expression)
            model.SmtpSettings = model.SmtpSettings with { Hostname = "new.smtp.com", Hostport = 587 };
            var changeInfo = await service.Store(model);

            // Assert - verify change information
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(1));
            Assert.That(changeInfo.RebootRequired, Is.False);

            // Assert - verify SetSMTPSettings was called
            A.CallTo(() => fakeConfigService.SetSMTPSettings(A<SMTPSettings>._))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_DnsServersChanged_CallsSetDNSServersAndRequiresReboot()
        {
            // Arrange
            var initialDns = new DNSServers
            {
                PrimaryDNS = "8.8.8.8",
                SecondaryDNS = "8.8.4.4"
            };

            ArrangeSnapshot(dns: initialDns);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - change DNS servers (using record 'with' expression)
            model.DnsServers = model.DnsServers with { PrimaryDNS = "1.1.1.1", SecondaryDNS = "1.0.0.1" };
            var changeInfo = await service.Store(model);

            // Assert - verify change information and reboot required
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(1));
            Assert.That(changeInfo.RebootRequired, Is.True, "DNS changes should require reboot");

            // Assert - verify SetDNSServers was called
            A.CallTo(() => fakeConfigService.SetDNSServers(A<DNSServers>._))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_NetworkSettingsChanged_CallsSetNetworkSettingsAndRequiresReboot()
        {
            // Arrange
            var initialNetwork = new NetworkSettings
            {
                IpAddress = "192.168.1.100",
                Netmask = "255.255.255.0",
                Gateway = "192.168.1.1",
                HttpPort = 80,
                HttpsPort = 443
            };

            ArrangeSnapshot(network: initialNetwork);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - change network settings (using record 'with' expression)
            model.NetworkSettings = model.NetworkSettings with { IpAddress = "192.168.1.200", Gateway = "192.168.1.254" };
            var changeInfo = await service.Store(model);

            // Assert - verify change information and reboot required
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(1));
            Assert.That(changeInfo.RebootRequired, Is.True, "Network settings changes should require reboot");

            // Assert - verify SetNetworkSettings was called
            A.CallTo(() => fakeConfigService.SetNetworkSettings(A<NetworkSettings>._))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_WLanSettingsChanged_CallsSetWLanSettingsAndRequiresReboot()
        {
            // Arrange
            var initialWLan = new WLanSettings
            {
                Enabled = false,
                Ssid = "OldNetwork",
                Key = "oldkey123",
                SecurityType = "WPA2",
                EncryptionType = "AES"
            };

            ArrangeSnapshot(wlan: initialWLan);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
            var model = await service.GetModel();

            // Act - change WLAN settings (using record 'with' expression)
            model.WLanSettings = model.WLanSettings with { Enabled = true, Ssid = "NewNetwork", Key = "newkey456" };
            var changeInfo = await service.Store(model);

            // Assert - verify change information and reboot required
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(1));
            Assert.That(changeInfo.RebootRequired, Is.True, "WLAN settings changes should require reboot");

            // Assert - verify SetWLanSettings was called
            A.CallTo(() => fakeConfigService.SetWLanSettings(A<WLanSettings>._))
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

            ArrangeSnapshot(testUsers);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);

            // Act - call SaveAdminModel without calling GetAdminModel first
            var newModel = new MutableAdminModel
            {
                Users = new HashSet<IhcUser> { new IhcUser { Username = "user2", Email = "user2@test.com", Group = IhcUserGroup.Users } },
                EmailControl = new EmailControlSettings(),
                SmtpSettings = new SMTPSettings(),
                DnsServers = new DNSServers(),
                NetworkSettings = new NetworkSettings(),
                WebAccess = new WebAccessControl(),
                WLanSettings = new WLanSettings()
            };
            var changeInfo = await service.Store(newModel);

            // Assert - verify change information (1 user added, 1 user removed)
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(2));
            Assert.That(changeInfo.RebootRequired, Is.False);

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

            ArrangeSnapshot(initialUsers, initialEmailControl, initialSmtp);

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
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
            var changeInfo = await service.Store(model);

            // Assert - verify change information (3 changes: user added, email control, smtp)
            Assert.That(changeInfo, Is.Not.Null);
            Assert.That(changeInfo.ChangeCount, Is.EqualTo(3));
            Assert.That(changeInfo.RebootRequired, Is.False);

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
                ModelMetadata = ModelMetadata.Current(typeof(MutableAdminModel)),
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

        /// <summary>
        /// The JSON round trip must return the model it was given — the WHOLE model, not the handful of fields a
        /// hand-written assertion happens to name. A subset assertion cannot fail for a field it does not mention,
        /// and the fields it would not mention are exactly the ones a serializer gets wrong: the ones carrying
        /// characters a JSON writer has to escape.
        ///
        /// <para>So the values here are chosen to be hostile rather than realistic — quotes, backslashes, braces,
        /// newlines and tabs, an embedded NUL, control characters, the Danish letters this SDK is full of, an
        /// astral-plane emoji (a surrogate PAIR — a lone surrogate is not valid text and is not the engine's
        /// problem), leading and trailing whitespace, and nulls — and every property of every block is filled,
        /// including all 29 web-access flags. Comparison is by the records' own value equality, so a property
        /// added to any of these types is covered the day it is added, without touching this test.</para>
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void SaveAndLoadJson_PreservesTheWholeModel(bool fileEncryption)
        {
            var service = new AdminAppService(
                settings, fileEncryption, fakeAuthService, fakeUserService, fakeConfigService);

            AdminModelGen.Sample(model =>
            {
                using var stream = new System.IO.MemoryStream();
                service.SaveAsJson(model, stream).GetAwaiter().GetResult();
                stream.Position = 0;
                MutableAdminModel loaded = service.LoadFromJson(stream).GetAwaiter().GetResult();

                if (Mismatch(model, loaded) is { } difference)
                {
                    throw new AssertionException($"encryption={fileEncryption}: {difference}");
                }
                return true;
            }, iter: 100, threads: 1);
        }

        /// <summary>
        /// A JSON file with no <c>ModelMetadata</c> block is refused the same way a file naming the wrong type or
        /// carrying no version is: with an <see cref="ArgumentException"/> saying what is wrong. The metadata is
        /// what this method gates on, so its absence is an ordinary rejection, not an internal failure — and a
        /// caller can legitimately produce such a file, because <c>SaveAsJson</c> writes whatever metadata the
        /// model carries rather than stamping its own.
        /// </summary>
        [Test]
        public async Task LoadFromJson_WhenTheSavedModelCarriedNoMetadata_IsRefusedWithAnArgumentException()
        {
            MutableAdminModel withoutMetadata = CreateTestAdminModel();
            withoutMetadata.ModelMetadata = null;
            var service = new AdminAppService(
                settings, fileEnryption: false, fakeAuthService, fakeUserService, fakeConfigService);

            using var stream = new System.IO.MemoryStream();
            await service.SaveAsJson(withoutMetadata, stream);
            stream.Position = 0;

            ArgumentException refusal = Assert.ThrowsAsync<ArgumentException>(
                async () => await service.LoadFromJson(stream));
            Assert.That(refusal.Message, Does.Contain("metadata"), "the refusal must name what is missing");
        }

        /// <summary>The same for a file that never had the property at all — a hand-written or foreign JSON
        /// document, which is the case that reaches this method from outside the SDK.</summary>
        [Test]
        public void LoadFromJson_WhenTheJsonHasNoMetadataProperty_IsRefusedWithAnArgumentException()
        {
            var service = new AdminAppService(
                settings, fileEnryption: false, fakeAuthService, fakeUserService, fakeConfigService);
            using var stream = new System.IO.MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("{ \"Users\": [] }"));

            ArgumentException refusal = Assert.ThrowsAsync<ArgumentException>(
                async () => await service.LoadFromJson(stream));
            Assert.That(refusal.Message, Does.Contain("metadata"));
        }

        // ----- what the three aggregate ValidateDataAnnotations calls actually reach -----
        //
        // All three (DoGetModel, Store, LoadFromJson) validate the top-level MutableAdminModel only:
        // Validator.TryValidateObject does not recurse into complex properties and the model declares no
        // annotations of its own. These tests pin that leniency at each of the three doors, so that if someone
        // later makes validation recurse, the change announces itself here instead of in the field — where it would
        // start refusing controller data and saved files that load today.

        /// <summary>Values a nested block's own annotations forbid: the SMTP password is capped at 20 characters
        /// and the netmask is [Required].</summary>
        private static (SMTPSettings Smtp, NetworkSettings Network) IllegalNestedValues() =>
            (new SMTPSettings { Hostname = "smtp.test.com", Password = new string('x', 40), Hostport = 465 },
             new NetworkSettings { IpAddress = "192.168.1.100", Netmask = null, Gateway = "192.168.1.1" });

        /// <summary>Whether a block passes its OWN annotations, checked directly. Every leniency test below asserts
        /// this is false for the values it uses: if a cap were widened or dropped, these tests would otherwise keep
        /// passing while pinning nothing at all.</summary>
        private static bool PassesOwnAnnotations(object block) =>
            System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                block,
                new System.ComponentModel.DataAnnotations.ValidationContext(block),
                new List<System.ComponentModel.DataAnnotations.ValidationResult>(),
                validateAllProperties: true);

        private static void AssertReallyIllegal(SMTPSettings smtp, NetworkSettings network) =>
            Assert.Multiple(() =>
            {
                Assert.That(PassesOwnAnnotations(smtp), Is.False,
                    "precondition: a 40-character password must break SMTPSettings' own [StringLength(20)]");
                Assert.That(PassesOwnAnnotations(network), Is.False,
                    "precondition: a null netmask must break NetworkSettings' own [Required]");
            });

        /// <summary>A controller reporting values its own annotations forbid is still readable — the check at the
        /// end of DoGetModel does not reach them.</summary>
        [Test]
        public async Task GetModel_AcceptsControllerValuesThatBreakNestedAnnotations()
        {
            (SMTPSettings smtp, NetworkSettings network) = IllegalNestedValues();
            AssertReallyIllegal(smtp, network);
            ArrangeSnapshot(smtp: smtp, network: network);
            var service = new AdminAppService(
                settings, fileEnryption: false, fakeAuthService, fakeUserService, fakeConfigService);

            MutableAdminModel model = await service.GetModel();

            Assert.Multiple(() =>
            {
                Assert.That(model.SmtpSettings.Password, Has.Length.EqualTo(40), "the over-long password is kept");
                Assert.That(model.NetworkSettings.Netmask, Is.Null, "the missing [Required] netmask is kept");
            });
        }

        /// <summary>Store's check has the same reach: it does not refuse a model whose nested values break their
        /// annotations, so what reaches the controller is whatever the leaf setters accept.</summary>
        [Test]
        public async Task Store_DoesNotRefuseAModelThatBreaksNestedAnnotations()
        {
            (SMTPSettings smtp, NetworkSettings network) = IllegalNestedValues();
            AssertReallyIllegal(smtp, network);
            ArrangeSnapshot(smtp: new SMTPSettings { Hostname = "smtp.test.com" });
            var service = new AdminAppService(
                settings, fileEnryption: false, fakeAuthService, fakeUserService, fakeConfigService);
            MutableAdminModel model = await service.GetModel();
            model.SmtpSettings = smtp;
            model.NetworkSettings = network;

            AdminAppService.ChangeInformation change = await service.Store(model);

            Assert.That(change.ChangeCount, Is.GreaterThan(0), "the offending values were passed on, not rejected");
            A.CallTo(() => fakeConfigService.SetSMTPSettings(smtp)).MustHaveHappened();
        }

        /// <summary>And so does the check after deserialization: a saved file whose nested settings break their
        /// annotations loads, values intact.</summary>
        [Test]
        public async Task LoadFromJson_AcceptsAFileWhoseNestedValuesBreakAnnotations()
        {
            (SMTPSettings smtp, NetworkSettings network) = IllegalNestedValues();
            AssertReallyIllegal(smtp, network);
            MutableAdminModel model = CreateTestAdminModel();
            model.SmtpSettings = smtp;
            model.NetworkSettings = network;
            var service = new AdminAppService(
                settings, fileEnryption: false, fakeAuthService, fakeUserService, fakeConfigService);

            using var stream = new System.IO.MemoryStream();
            await service.SaveAsJson(model, stream);
            stream.Position = 0;
            MutableAdminModel loaded = await service.LoadFromJson(stream);

            Assert.Multiple(() =>
            {
                Assert.That(loaded.SmtpSettings.Password, Has.Length.EqualTo(40));
                Assert.That(loaded.NetworkSettings.Netmask, Is.Null);
            });
        }

        /// <summary>
        /// The comparison above is only worth as much as its ability to notice a difference, and "compare the whole
        /// model" is exactly the kind of claim that quietly degrades. This perturbs each block in turn and requires
        /// the comparison to object to every one of them — and to stay silent for an untouched copy.
        /// </summary>
        [Test]
        public void WholeModelComparison_NoticesADifferenceInEveryBlock()
        {
            MutableAdminModel model = CreateTestAdminModel();

            Assert.Multiple(() =>
            {
                Assert.That(Mismatch(model, model.Copy()), Is.Null, "an untouched copy must compare equal");
                Assert.That(Mismatch(model, model.Copy() with { Users = new HashSet<IhcUser>() }),
                    Is.Not.Null, "a dropped user must be noticed");
                Assert.That(Mismatch(model, model.Copy() with
                {
                    EmailControl = model.EmailControl with { Pop3Password = "changed" },
                }), Is.Not.Null, "EmailControl");
                Assert.That(Mismatch(model, model.Copy() with
                {
                    SmtpSettings = model.SmtpSettings with { Hostport = model.SmtpSettings.Hostport + 1 },
                }), Is.Not.Null, "SmtpSettings");
                Assert.That(Mismatch(model, model.Copy() with
                {
                    DnsServers = model.DnsServers with { SecondaryDNS = "changed" },
                }), Is.Not.Null, "DnsServers");
                Assert.That(Mismatch(model, model.Copy() with
                {
                    NetworkSettings = model.NetworkSettings with { HttpsPort = 1 },
                }), Is.Not.Null, "NetworkSettings");
                Assert.That(Mismatch(model, model.Copy() with
                {
                    WebAccess = model.WebAccess with { OpenapiUsed = !model.WebAccess.OpenapiUsed },
                }), Is.Not.Null, "WebAccess — a single flag out of 29");
                Assert.That(Mismatch(model, model.Copy() with
                {
                    WLanSettings = model.WLanSettings with { Key = "changed" },
                }), Is.Not.Null, "WLanSettings");
                Assert.That(Mismatch(model, model.Copy() with { ModelMetadata = null }),
                    Is.Not.Null, "missing metadata");
            });
        }

        /// <summary>Describes the first structural difference between a saved and a reloaded model, or null when
        /// they agree.</summary>
        private static string? Mismatch(MutableAdminModel original, MutableAdminModel loaded)
        {
            if (loaded is null)
            {
                return "the reloaded model is null";
            }
            if (original.Users is null != loaded.Users is null)
            {
                return $"Users presence: original {(original.Users is null ? "null" : "set")}, "
                       + $"loaded {(loaded.Users is null ? "null" : "set")}";
            }
            if (original.Users is not null && !loaded.Users!.SetEquals(original.Users))
            {
                return $"Users differ: {original.Users.Count} saved, {loaded.Users!.Count} reloaded";
            }
            if (!Equals(original.EmailControl, loaded.EmailControl))
            {
                return "EmailControl differs";
            }
            if (!Equals(original.SmtpSettings, loaded.SmtpSettings))
            {
                return "SmtpSettings differs";
            }
            if (!Equals(original.DnsServers, loaded.DnsServers))
            {
                return "DnsServers differs";
            }
            if (!Equals(original.NetworkSettings, loaded.NetworkSettings))
            {
                return "NetworkSettings differs";
            }
            if (!Equals(original.WebAccess, loaded.WebAccess))
            {
                return "WebAccess differs";
            }
            if (!Equals(original.WLanSettings, loaded.WLanSettings))
            {
                return "WLanSettings differs";
            }
            // The metadata is stamped by the save, not carried from the caller's model, so it is checked for what
            // it should say rather than compared.
            if (loaded.ModelMetadata?.TypeFullName != typeof(MutableAdminModel).FullName)
            {
                return $"ModelMetadata names '{loaded.ModelMetadata?.TypeFullName}'";
            }
            return null;
        }

        // ----- generators -----

        /// <summary>The text a subset assertion hides: JSON metacharacters, whitespace edges, control characters
        /// including NUL, the Unicode line separators, Danish letters, and astral-plane characters. The control
        /// characters are COMPUTED rather than written as literals, so the test source itself stays readable and
        /// no editor or tool can quietly normalize them away.</summary>
        private static readonly string[] HostileFragments =
        {
            "\"", "\\", "{", "}", "[", "]", ":", ",", "//", "/*",
            "\n", "\r", "\t", "\b", "\f", " ", "  ",
            Ch(0x0000), Ch(0x0001), Ch(0x001F), Ch(0x007F), Ch(0x00A0), Ch(0x2028), Ch(0x2029),
            "æøå", "ÆØÅ", "é", "ß",
            char.ConvertFromUtf32(0x1F600), char.ConvertFromUtf32(0x1F1E9),
            "abc", "0", "-1", "null", "true", "",
        };

        /// <summary>One character by code point, for <see cref="HostileFragments"/>.</summary>
        private static string Ch(int codePoint) => ((char)codePoint).ToString();

        private static readonly Gen<string> HostileText =
            Gen.OneOfConst(HostileFragments).Array[0, 4].Select(parts => string.Concat(parts));

        /// <summary>Hostile text, or null — an absent value is its own round-trip case.</summary>
        /// <summary>Hostile text, or null — an absent value is its own round-trip case. The null is produced by
        /// mapping rather than <c>Gen.Const</c>, whose value and factory overloads are ambiguous for a null.</summary>
        private static readonly Gen<string> MaybeText =
            Gen.OneOf(HostileText, Gen.Bool.Select(_ => (string)null!));

        private static readonly Gen<IhcUser> UserGen = Gen.Select(
            MaybeText.Array[7, 7],
            Gen.OneOfConst(IhcUserGroup.Administrators, IhcUserGroup.Users),
            Gen.Long[0, 4_000_000_000],
            Gen.Long[0, 4_000_000_000],
            (text, group, created, login) => new IhcUser
            {
                Username = text[0], Password = text[1], Email = text[2], Firstname = text[3],
                Lastname = text[4], Phone = text[5], Project = text[6], Group = group,
                CreatedDate = DateTimeOffset.FromUnixTimeSeconds(created),
                LoginDate = DateTimeOffset.FromUnixTimeSeconds(login),
            });

        private static readonly Gen<EmailControlSettings> EmailControlGen = Gen.Select(
            MaybeText.Array[4, 4], Gen.Int[0, 65535].Array[2, 2], Gen.Bool.Array[2, 2],
            (text, numbers, flags) => new EmailControlSettings
            {
                ServerIpAddress = text[0], Pop3Username = text[1], Pop3Password = text[2], EmailAddress = text[3],
                ServerPortNumber = numbers[0], PollInterval = numbers[1],
                RemoveEmailsAfterUsage = flags[0], Ssl = flags[1],
            });

        private static readonly Gen<SMTPSettings> SmtpGen = Gen.Select(
            MaybeText.Array[4, 4], Gen.Int[0, 65535], Gen.Bool.Array[2, 2],
            (text, port, flags) => new SMTPSettings
            {
                Hostname = text[0], Username = text[1], Password = text[2],
                SendLowBatteryNotificationRecipient = text[3],
                Hostport = port, Ssl = flags[0], SendLowBatteryNotification = flags[1],
            });

        private static readonly Gen<DNSServers> DnsGen = MaybeText.Array[2, 2]
            .Select(text => new DNSServers { PrimaryDNS = text[0], SecondaryDNS = text[1] });

        private static readonly Gen<NetworkSettings> NetworkGen = Gen.Select(
            MaybeText.Array[3, 3], Gen.Int[0, 65535].Array[2, 2],
            (text, ports) => new NetworkSettings
            {
                IpAddress = text[0], Netmask = text[1], Gateway = text[2],
                HttpPort = ports[0], HttpsPort = ports[1],
            });

        private static readonly Gen<WLanSettings> WLanGen = Gen.Select(
            MaybeText.Array[7, 7], Gen.Bool,
            (text, enabled) => new WLanSettings
            {
                Enabled = enabled, Ssid = text[0], Key = text[1], SecurityType = text[2], EncryptionType = text[3],
                IpAddress = text[4], Netmask = text[5], Gateway = text[6],
            });

        /// <summary>All 29 access flags, so none of them can silently fail to round-trip.</summary>
        private static readonly Gen<WebAccessControl> WebAccessGen = Gen.Bool.Array[29, 29]
            .Select(f => new WebAccessControl
            {
                UsbLoginRequired = f[0],
                AdministratorUsb = f[1], AdministratorInternal = f[2], AdministratorExternal = f[3],
                TreeviewUsb = f[4], TreeviewInternal = f[5], TreeviewExternal = f[6],
                SceneviewUsb = f[7], SceneviewInternal = f[8], SceneviewExternal = f[9],
                ScenedesignUsb = f[10], ScenedesignInternal = f[11], ScenedesignExternal = f[12],
                ServerstatusUsb = f[13], ServerstatusInternal = f[14], ServerstatusExternal = f[15],
                IhcvisualUsb = f[16], IhcvisualInternal = f[17], IhcvisualExternal = f[18],
                OnlinedocumentationUsb = f[19], OnlinedocumentationInternal = f[20],
                OnlinedocumentationExternal = f[21],
                WebsceneviewUsb = f[22], WebsceneviewInternal = f[23], WebsceneviewExternal = f[24],
                OpenapiUsb = f[25], OpenapiInternal = f[26], OpenapiExternal = f[27], OpenapiUsed = f[28],
            });

        private static readonly Gen<MutableAdminModel> AdminModelGen = Gen.Select(
            UserGen.Array[0, 3], EmailControlGen, SmtpGen, DnsGen, NetworkGen, WebAccessGen, WLanGen,
            (users, email, smtp, dns, network, webAccess, wlan) => new MutableAdminModel
            {
                // Stamped exactly as every real producer stamps it (GetModel does this), because SaveAsJson
                // carries the caller's metadata rather than writing its own, and LoadFromJson refuses a file whose
                // metadata does not name this model type.
                ModelMetadata = ModelMetadata.Current(typeof(MutableAdminModel)),
                Users = new HashSet<IhcUser>(users),
                EmailControl = email,
                SmtpSettings = smtp,
                DnsServers = dns,
                NetworkSettings = network,
                WebAccess = webAccess,
                WLanSettings = wlan,
            });

        [Test]
        public async Task SaveAsJson_WithEncryptionEnabled_EncryptsSensitiveFieldsOnly()
        {
            // Arrange - Verify environment variable is set
            var envVar = Environment.GetEnvironmentVariable("IHC_ENCRYPT_PASSPHRASE");
            Assert.That(envVar, Is.Not.Null, "IHC_ENCRYPT_PASSPHRASE environment variable should be set");

            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
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
            var service = new AdminAppService(settings, fileEnryption: false, fakeAuthService, fakeUserService, fakeConfigService);
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
            var service = new AdminAppService(settings, fileEnryption: true, fakeAuthService, fakeUserService, fakeConfigService);
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
