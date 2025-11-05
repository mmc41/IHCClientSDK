using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Ihc;
using Ihc.App;
using FakeItEasy;

namespace Ihc.Tests
{
    /// <summary>
    /// Unit tests for InformationAppService that verify basic functionality
    /// using FakeItEasy mocked services (no actual controller connection).
    /// </summary>
    [TestFixture]
    public class InformationServiceTests
    {
        #pragma warning disable NUnit1032 // Fakes from FakeItEasy don't need disposal
        private IAuthenticationService fakeAuthService;
        #pragma warning restore NUnit1032
        private IConfigurationService fakeConfigService;
        private ITimeManagerService fakeTimeService;
        private IControllerService fakeControllerService;
        private ISmsModemService fakeSmsModemService;
        private IhcSettings settings;

        [SetUp]
        public void Setup()
        {
            // Create fake services
            fakeAuthService = A.Fake<IAuthenticationService>();
            fakeConfigService = A.Fake<IConfigurationService>();
            fakeTimeService = A.Fake<ITimeManagerService>();
            fakeControllerService = A.Fake<IControllerService>();
            fakeSmsModemService = A.Fake<ISmsModemService>();

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

        [Test]
        public void Constructor_WithSettings_CreatesInstance()
        {
            // Act
            var service = new InformationAppService(settings);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithServicesAndSettings_CreatesInstance()
        {
            // Act
            var service = new InformationAppService(settings, fakeAuthService, fakeConfigService, fakeTimeService, fakeControllerService, fakeSmsModemService);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public async Task GetInformationModel_ReturnsModelWithData()
        {
            // Arrange
            var testSystemInfo = new SystemInfo
            {
                Uptime = 3600000, // 1 hour in milliseconds
                Realtimeclock = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
                Version = "2.8.31",
                SerialNumber = "SN123456",
                ProductionDate = "2024-01-01",
                HWRevision = "1.0",
                DatalineVersion = "1.2",
                RFModuleSoftwareVersion = "2.1",
                RFModuleSerialNumber = "RF789",
                SmsModemSoftwareVersion = "3.0",
                SWDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
            };

            var testControllerState = ControllerState.Ready;

            var testSdInfo = new SDInfo
            {
                Size = 8192,
                Free = 7168
            };

            var testSmsModemInfo = new SmsModemInfo
            {
                FirmwareVersion = "1.5.0",
                GSMChipVersion = "2.1.3",
                HardwareRevision = "1.0",
                ProductionDate = "2023-12-01",
                Detected = true,
                SerialNumber = "SMS123",
                IMEINumber = "123456789012345"
            };

            A.CallTo(() => fakeConfigService.GetSystemInfo()).Returns(Task.FromResult(testSystemInfo));
            A.CallTo(() => fakeControllerService.GetControllerState()).Returns(Task.FromResult(testControllerState));
            A.CallTo(() => fakeControllerService.GetSDCardInfo()).Returns(Task.FromResult(testSdInfo));
            A.CallTo(() => fakeSmsModemService.GetSmsModemInfo()).Returns(Task.FromResult(testSmsModemInfo));

            var service = new InformationAppService(settings, fakeAuthService, fakeConfigService, fakeTimeService, fakeControllerService, fakeSmsModemService);

            // Act
            var model = await service.GetInformationModel();

            // Assert
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Uptime, Is.EqualTo(TimeSpan.FromMilliseconds(3600000)));
            Assert.That(model.ControllerTime, Is.EqualTo(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero)));
            Assert.That(model.SoftwareVersion, Is.EqualTo("2.8.31"));
            Assert.That(model.SerialNumber, Is.EqualTo("SN123456"));
            Assert.That(model.ProductionDate, Is.EqualTo("2024-01-01"));
            Assert.That(model.HardwareVersion, Is.EqualTo("1.0"));
            Assert.That(model.IoVersion, Is.EqualTo("1.2"));
            Assert.That(model.RfVersion, Is.EqualTo("2.1"));
            Assert.That(model.RfSerialNumber, Is.EqualTo("RF789"));
            Assert.That(model.SmsModemVersion, Is.EqualTo("3.0"));
            Assert.That(model.SoftwareDate, Is.EqualTo(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(model.ControllerStatus, Is.EqualTo(testControllerState));
            Assert.That(model.SdCard, Is.EqualTo(testSdInfo));

            // Verify API calls
            A.CallTo(() => fakeConfigService.GetSystemInfo()).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeControllerService.GetControllerState()).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeControllerService.GetSDCardInfo()).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeSmsModemService.GetSmsModemInfo()).MustHaveHappenedOnceExactly();
        }
    }
}
