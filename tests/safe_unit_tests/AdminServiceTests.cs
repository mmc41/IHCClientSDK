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
        private IUserManagerService fakeUserService;
        private IConfigurationService fakeConfigService;
        private IhcSettings settings;

        [SetUp]
        public void Setup()
        {
            // Create fake services
            fakeUserService = A.Fake<IUserManagerService>();
            fakeConfigService = A.Fake<IConfigurationService>();

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
            var service = new AdminService(settings);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithServicesAndSettings_CreatesInstance()
        {
            // Act
            var service = new AdminService(settings, fakeUserService, fakeConfigService);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AdminService(null));
        }

        [Test]
        public void Constructor_WithNullServices_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new AdminService(settings, null, fakeConfigService));
            Assert.Throws<ArgumentNullException>(() => new AdminService(settings, fakeUserService, null));
        }

        [Test]
        public async Task GetAdminModel_ReturnsModelWithData()
        {
            // Arrange
            ISet<IhcUser> testUsers = new HashSet<IhcUser>
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

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult(testUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(testEmailControl));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(testSmtp));

            var service = new AdminService(settings, fakeUserService, fakeConfigService);

            // Act
            var model = await service.GetAdminModel();

            // Assert
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Users, Is.Not.Null);
            Assert.That(model.Users.Count, Is.EqualTo(2));
            Assert.That(model.EmailControl, Is.EqualTo(testEmailControl));
            Assert.That(model.SmtpSettings, Is.EqualTo(testSmtp));

            // Verify API calls
            A.CallTo(() => fakeUserService.GetUsers(true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_NoChanges_NoApiCalls()
        {
            // Arrange
            ISet<IhcUser> testUsers = new HashSet<IhcUser>
            {
                new IhcUser { Username = "user1", Email = "user1@test.com", Group = IhcUserGroup.Administrators }
            };

            var testEmailControl = new EmailControlSettings { ServerIpAddress = "mail.test.com" };
            var testSmtp = new SMTPSettings { Hostname = "smtp.test.com" };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult(testUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(testEmailControl));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(testSmtp));

            var service = new AdminService(settings, fakeUserService, fakeConfigService);
            var model = await service.GetAdminModel();

            // Act - save same model without changes
            await service.SaveAdminModel(model);

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
            ISet<IhcUser> initialUsers = new HashSet<IhcUser>
            {
                new IhcUser { Username = "user1", Email = "user1@test.com", Group = IhcUserGroup.Users }
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult(initialUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings()));

            var service = new AdminService(settings, fakeUserService, fakeConfigService);
            var model = await service.GetAdminModel();

            // Act - add a new user
            var newUser = new IhcUser { Username = "user2", Email = "user2@test.com", Group = IhcUserGroup.Users };
            model.Users.Add(newUser);
            await service.SaveAdminModel(model);

            // Assert
            A.CallTo(() => fakeUserService.AddUser(A<IhcUser>.That.Matches(u => u.Username == "user2")))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_UserDeleted_CallsRemoveUser()
        {
            // Arrange
            ISet<IhcUser> initialUsers = new HashSet<IhcUser>
            {
                new IhcUser { Username = "user1", Email = "user1@test.com", Group = IhcUserGroup.Users },
                new IhcUser { Username = "user2", Email = "user2@test.com", Group = IhcUserGroup.Users }
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult(initialUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings()));

            var service = new AdminService(settings, fakeUserService, fakeConfigService);
            var model = await service.GetAdminModel();

            // Act - remove a user
            var userToRemove = model.Users.First(u => u.Username == "user2");
            model.Users.Remove(userToRemove);
            await service.SaveAdminModel(model);

            // Assert
            A.CallTo(() => fakeUserService.RemoveUser("user2")).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_UserUpdated_CallsUpdateUser()
        {
            // Arrange
            ISet<IhcUser> initialUsers = new HashSet<IhcUser>
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

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult(initialUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings()));

            var service = new AdminService(settings, fakeUserService, fakeConfigService);
            var model = await service.GetAdminModel();

            // Act - update user with new properties (using HashSet operations)
            var oldUser = model.Users.First(u => u.Username == "user1");
            model.Users.Remove(oldUser);
            var updatedUser = oldUser with { Email = "new@test.com", Firstname = "New" };
            model.Users.Add(updatedUser);
            await service.SaveAdminModel(model);

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

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<ISet<IhcUser>>(new HashSet<IhcUser>()));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(initialEmailControl));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings()));

            var service = new AdminService(settings, fakeUserService, fakeConfigService);
            var model = await service.GetAdminModel();

            // Act - change email control settings (using record 'with' expression)
            model.EmailControl = model.EmailControl with { ServerIpAddress = "new.mail.com", ServerPortNumber = 995 };
            await service.SaveAdminModel(model);

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

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult<ISet<IhcUser>>(new HashSet<IhcUser>()));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(initialSmtp));

            var service = new AdminService(settings, fakeUserService, fakeConfigService);
            var model = await service.GetAdminModel();

            // Act - change SMTP settings (using record 'with' expression)
            model.SmtpSettings = model.SmtpSettings with { Hostname = "new.smtp.com", Hostport = 587 };
            await service.SaveAdminModel(model);

            // Assert - verify SetSMTPSettings was called
            A.CallTo(() => fakeConfigService.SetSMTPSettings(A<SMTPSettings>._))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task SaveAdminModel_WithoutGetAdminModel_LoadsSnapshotAutomatically()
        {
            // Arrange
            ISet<IhcUser> testUsers = new HashSet<IhcUser>
            {
                new IhcUser { Username = "user1", Email = "user1@test.com", Group = IhcUserGroup.Users }
            };

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult(testUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(new EmailControlSettings()));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(new SMTPSettings()));

            var service = new AdminService(settings, fakeUserService, fakeConfigService);

            // Act - call SaveAdminModel without calling GetAdminModel first
            var newModel = new AdminModel
            {
                Users = new HashSet<IhcUser> { new IhcUser { Username = "user2", Email = "user2@test.com", Group = IhcUserGroup.Users } },
                EmailControl = new EmailControlSettings(),
                SmtpSettings = new SMTPSettings()
            };
            await service.SaveAdminModel(newModel);

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
            ISet<IhcUser> initialUsers = new HashSet<IhcUser>
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

            A.CallTo(() => fakeUserService.GetUsers(true)).Returns(Task.FromResult(initialUsers));
            A.CallTo(() => fakeConfigService.GetEmailControlSettings()).Returns(Task.FromResult(initialEmailControl));
            A.CallTo(() => fakeConfigService.GetSMTPSettings()).Returns(Task.FromResult(initialSmtp));

            var service = new AdminService(settings, fakeUserService, fakeConfigService);
            var model = await service.GetAdminModel();

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
            await service.SaveAdminModel(model);

            // Assert - all changes should be applied
            A.CallTo(() => fakeUserService.AddUser(A<IhcUser>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.SetEmailControlSettings(A<EmailControlSettings>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeConfigService.SetSMTPSettings(A<SMTPSettings>._)).MustHaveHappenedOnceExactly();
        }
    }
}
