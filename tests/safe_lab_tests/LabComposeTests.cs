using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Stubbing;
using IhcLab;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// Lab's start-up composition: the switch applied to the configuration Lab's own loader read, and that
    /// configuration rebuilt with every property but the controller settings carried.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LabComposeTests
    {
        /// <summary>A loaded configuration whose logging and telemetry are both set, as the file-reading constructor
        /// leaves them, so a copy that dropped either would show.</summary>
        private static Configuration Loaded() =>
            new(new IhcSettings
            {
                Endpoint = "http://loaded-controller.invalid",
                UserName = "loaded-user",
                Password = "loaded-password",
                Application = Application.administrator,
                IhcVisualInstallDir = "loaded-install-dir",
            })
            {
                loggingConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Warning" })
                    .Build().GetSection("Logging"),
                telemetryConfig = new TelemetryConfiguration { Environment = "loaded" },
            };

        /// <remarks>
        /// The registration the switch makes here is held by the SDK for the rest of this run, which is the latched
        /// state the whole suite already runs in.
        /// </remarks>
        [Test]
        public async Task ComposingWithTheSwitch_ReplacesOnlyTheController_AndMarksTheProcess()
        {
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(new ControllerStub(), CancellationToken.None);
            Configuration loaded = Loaded();

            var (config, remaining, stubProcess) = Program.Compose(
                ["--verbose", ControllerStubs.SwitchArgument + "=" + host.BaseAddress], loaded);

            IhcSettings settings = config.ihcSettings;
            Assert.Multiple(() =>
            {
                Assert.That(settings.IsStubConfigured(), Is.True, "the stub endpoint");
                Assert.That((settings.UserName, settings.Password), Is.Not.EqualTo((loaded.ihcSettings.UserName, loaded.ihcSettings.Password)),
                    "the stub-only login, never the loaded one");
                Assert.That(settings with
                {
                    Endpoint = loaded.ihcSettings.Endpoint,
                    UserName = loaded.ihcSettings.UserName,
                    Password = loaded.ihcSettings.Password,
                }, Is.EqualTo(loaded.ihcSettings), "every other controller setting is the loaded one's");
                Assert.That(OtherPropertiesDiffering(loaded, config), Is.Empty, "every other property is carried");
                Assert.That(remaining, Is.EqualTo(new[] { "--verbose" }));
                Assert.That(stubProcess, Is.True);
                Assert.That(host.IdentifyRequests, Is.EqualTo(1));
            });
        }

        [Test]
        public void ComposingWithoutTheSwitch_KeepsTheLoadedSettings_AndIsNoStubProcess()
        {
            Configuration loaded = Loaded();

            var (config, remaining, stubProcess) = Program.Compose(["--verbose"], loaded);

            Assert.Multiple(() =>
            {
                Assert.That(config.ihcSettings, Is.SameAs(loaded.ihcSettings));
                Assert.That(OtherPropertiesDiffering(loaded, config), Is.Empty);
                Assert.That(remaining, Is.EqualTo(new[] { "--verbose" }));
                Assert.That(stubProcess, Is.False);
            });
        }

        /// <summary>
        /// Only the switch makes a stub process, because only the switch holds the latch that refuses real
        /// endpoints. A stub endpoint in the settings file holds nothing, so a real endpoint entered later is not
        /// refused, and a title claiming it is would be false.
        /// </summary>
        [Test]
        public void ComposingWithoutTheSwitch_OnAStubEndpointFromTheFile_IsNoStubProcess()
        {
            Configuration loaded = new(new IhcSettings { Endpoint = "http://s1.controllerstub.invalid" });

            var (config, _, stubProcess) = Program.Compose([], loaded);

            Assert.That(config.ihcSettings.IsStubConfigured(), Is.True, "the file's endpoint is a stub endpoint");
            Assert.That(stubProcess, Is.False, "but nothing latched the process");
        }

        /// <summary>A refused switch stops Lab: Main's catch logs it as Critical through the pipeline and answers
        /// with the fatal exit code.</summary>
        [Test]
        public void ARefusedSwitch_IsLoggedAsCritical_AndEndsInTheFatalExitCode()
        {
            var refusal = Assert.Throws<ErrorWithCodeException>(() =>
                Program.Compose([ControllerStubs.SwitchArgument + "=mock://x"], Loaded()));
            var captured = new SelectionFaultLoggingTests.CapturingProvider();
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(captured));

            int exitCode = Program.ReportFatalStartup(refusal!, factory);

            Assert.That(exitCode, Is.EqualTo(Program.FatalStartupExitCode));
            Assert.That(captured.Entries, Is.EqualTo(new[] { "Critical|Lab could not start|ErrorWithCodeException" }));
        }

        /// <summary>The public properties, other than the controller settings, whose values differ between the two.</summary>
        private static IEnumerable<string> OtherPropertiesDiffering(Configuration source, Configuration copy) =>
            typeof(Configuration).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name != nameof(Configuration.ihcSettings))
                .Where(property => !ReferenceEquals(property.GetValue(source), property.GetValue(copy)))
                .Select(property => property.Name);
    }
}
