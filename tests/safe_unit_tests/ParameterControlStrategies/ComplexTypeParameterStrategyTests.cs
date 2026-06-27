using System;
using System.Linq;
using FakeItEasy;
using Ihc;
using IhcLab.ParameterControls.Strategies;

namespace Ihc.Tests
{
    /// <summary>
    /// Round-trip tests for <see cref="ComplexTypeParameterStrategy"/> covering construction of SDK
    /// setting records from the values extracted out of the generated GUI sub-controls.
    /// </summary>
    /// <remarks>
    /// The <see cref="FieldMetaData"/> used here is production-accurate: it is derived from the real
    /// service interfaces via <see cref="ServiceMetadata.GetOperations"/>, exactly as the running Lab does.
    /// The high-level SDK setting records (<see cref="NetworkSettings"/>, <see cref="TimeManagerSettings"/>,
    /// <see cref="SmsModemSettings"/>) are all property-only (init-style) records exposing only a
    /// parameterless constructor, so they exercise the by-name construction path - which is the norm and
    /// reproduces the original defect where <c>ExtractValue</c> threw because it always required a positional
    /// constructor. The positional (constructor-count) branch remains in the strategy as a defensive
    /// fallback but is not exercised by any current production type.
    /// </remarks>
    [TestFixture]
    public class ComplexTypeParameterStrategyTests
    {
        private ComplexTypeParameterStrategy strategy;

        [SetUp]
        public void SetUp()
        {
            strategy = new ComplexTypeParameterStrategy();
        }

        [Test]
        public void ExtractValue_PropertyOnlyRecord_NetworkSettings_RoundTripsAllValues()
        {
            // Arrange - NetworkSettings is property-only (no positional ctor); HttpPort/HttpsPort are int sub-fields.
            var field = GetParameterField<IConfigurationService>(nameof(IConfigurationService.SetNetworkSettings));
            var known = new NetworkSettings
            {
                IpAddress = "192.168.1.50",
                Netmask = "255.255.255.0",
                Gateway = "192.168.1.1",
                HttpPort = 8080,
                HttpsPort = 8443
            };

            // Act
            var result = RoundTrip(field, known);

            // Assert - value-equality across every property proves the by-name construction path works.
            Assert.That(result, Is.EqualTo(known));
        }

        [Test]
        public void ExtractValue_PropertyOnlyRecord_TimeManagerSettings_RoundTripsAllValues()
        {
            // Arrange - TimeManagerSettings is property-only and includes a DateTimeOffset sub-field
            // (TimeAndDateInUTC) plus bool/int/string fields, so all sub-strategies flow through construction.
            var field = GetParameterField<ITimeManagerService>(nameof(ITimeManagerService.SetSettings));
            var known = new TimeManagerSettings
            {
                SynchroniseTimeAgainstServer = true,
                UseDST = true,
                GmtOffsetInHours = 2,
                ServerName = "pool.ntp.org",
                SyncIntervalInHours = 24,
                TimeAndDateInUTC = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero),
                OnlineCalendarUpdateOnline = true,
                OnlineCalendarCountry = "DK",
                OnlineCalendarValidUntil = 2030
            };

            // Act
            var result = RoundTrip(field, known);

            // Assert
            Assert.That(result, Is.EqualTo(known));
        }

        [Test]
        public void ExtractValue_PropertyOnlyRecord_SmsModemSettings_RoundTripsAllValues()
        {
            // Arrange - SmsModemSettings is a property-only (init-style) record exposing only a parameterless
            // constructor, so it flows through the by-name construction path like the other settings records.
            var field = GetParameterField<ISmsModemService>(nameof(ISmsModemService.SetSmsModemSettings));
            var known = new SmsModemSettings
            {
                PowerupMessage = "powered up",
                PowerdownMessage = "powered down",
                PowerdownNumber = "12345678",
                RelaySMS = true,
                ForceStandAloneMode = false,
                SendLowBatteryNotification = true,
                SendLowBatteryNotificationLanguage = false,
                SendLEDDimmerErrorNotification = true
            };

            // Act
            var result = RoundTrip(field, known);

            // Assert
            Assert.That(result, Is.EqualTo(known));
        }

        /// <summary>
        /// Resolves the production <see cref="FieldMetaData"/> for the (single) parameter of the named
        /// operation on a faked service, mirroring how the Lab builds parameter controls.
        /// </summary>
        private static FieldMetaData GetParameterField<TService>(string operationName)
            where TService : class, IIHCApiService
        {
            var service = A.Fake<TService>();
            var operation = ServiceMetadata.GetOperations(service).First(op => op.Name == operationName);
            return operation.Parameters[0];
        }

        /// <summary>
        /// Builds the complex control for the field, pushes a known value into it, then extracts it back.
        /// </summary>
        private object? RoundTrip(FieldMetaData field, object knownValue)
        {
            var control = strategy.CreateControl(field, "Param0");
            strategy.SetValue(control, knownValue, field);
            return strategy.ExtractValue(control, field);
        }
    }
}
