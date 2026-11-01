using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Stubbing;
using Ihc.Tests.Shared;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The switch an executable is started with to point it at a stub host: what it may name, and that the settings
    /// it produces replace only the endpoint and the login. The rules are run through the internal core, which hands
    /// back the registration so each case can release it; the public entry keeps its registrations for the life of
    /// the process, which here would latch every later test.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ControllerStubSwitchTests
    {
        /// <summary>Settings whose every property differs from a blank one's, so a property the switch failed to
        /// carry would show.</summary>
        private static IhcSettings Loaded() => new()
        {
            Endpoint = "http://loaded-controller.invalid",
            UserName = "loaded-user",
            Password = "loaded-password",
            Application = Application.administrator,
            LogSensitiveData = true,
            AsyncContinueOnCapturedContext = true,
            AllowDangerousInternTestCalls = true,
            IhcVisualInstallDir = "loaded-install-dir",
        };

        [Test]
        public void TheLoadedSettings_SetEveryProperty()
        {
            var blank = new IhcSettings();
            IhcSettings loaded = Loaded();

            Assert.That(typeof(IhcSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property => property.CanWrite && Equals(property.GetValue(loaded), property.GetValue(blank)))
                    .Select(property => property.Name),
                Is.Empty, "a property left at its default here could be dropped by the switch unnoticed");
        }

        [Test]
        public void WithoutTheSwitch_TheLoadedSettingsAndArguments_AreReturnedUnchanged()
        {
            var loaded = new IhcSettings { Endpoint = "mock://from-the-file" };
            string[] args = ["project.vis", "--test"];

            var (effective, remaining, registration) = ControllerStubSwitch.ApplySwitchCore(args, loaded);

            Assert.Multiple(() =>
            {
                Assert.That(effective, Is.SameAs(loaded), "a mock:// endpoint from the file stays as it is");
                Assert.That(remaining, Is.EqualTo(args));
                Assert.That(registration, Is.Null);
                Assert.That(ControllerStubs.SwitchRequested(args), Is.False);
            });
        }

        [Test]
        public async Task ALoopbackSwitch_ReplacesTheEndpointAndTheLogin_AndCarriesEverythingElse()
        {
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(new ControllerStub(), CancellationToken.None);
            IhcSettings loaded = Loaded();
            string[] args = ["project.vis", ControllerStubs.SwitchArgument + "=" + host.BaseAddress, "--test"];
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            var (effective, remaining, registration) = ControllerStubSwitch.ApplySwitchCore(args, loaded);
            using (registration)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(RequestDestinations.SdkRequests(capture.Spans).Select(span => span.GetTagItem("url.full")),
                        Is.EqualTo(new[] { new Uri(host.BaseAddress, "identify").ToString() }),
                        "the switch blocks on the identity check, which is still traced as one SDK request");
                    Assert.That(effective, Is.EqualTo(loaded with
                    {
                        Endpoint = registration!.Endpoint,
                        UserName = ControllerStubSwitch.SwitchUserName,
                        Password = ControllerStubSwitch.SwitchPassword,
                    }));
                    Assert.That(SensitiveValuesKept(loaded, effective), Is.Empty,
                        "no secret the executable loaded reaches the stub");
                    Assert.That(remaining, Is.EqualTo(new[] { "project.vis", "--test" }));
                    Assert.That(ControllerStubs.SwitchRequested(args), Is.True);
                });
                Assert.That(await LoginFrom(effective), Is.True, "the stub-only login is one a default stub accepts");
            }
        }

        /// <summary>OpenVisual with no settings file: blank settings take the stub endpoint and login, and nothing
        /// reads a file, which would throw here.</summary>
        [Test]
        public async Task BlankSettings_WithNoSettingsFile_TakeTheStubEndpointAndLogin()
        {
            Assert.That(File.Exists(Path.Combine(AppContext.BaseDirectory, "ihcsettings.json")), Is.False,
                "the case needs a run with no settings file");
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(new ControllerStub(), CancellationToken.None);
            var blank = new IhcSettings();

            var (effective, _, registration) = ControllerStubSwitch.ApplySwitchCore(
                [ControllerStubs.SwitchArgument + "=" + host.BaseAddress], blank);
            using (registration)
            {
                Assert.That(effective, Is.EqualTo(blank with
                {
                    Endpoint = registration!.Endpoint,
                    UserName = ControllerStubSwitch.SwitchUserName,
                    Password = ControllerStubSwitch.SwitchPassword,
                }));
            }
        }

        [TestCase("mock://x", TestName = "mock")]
        [TestCase("", TestName = "an empty value")]
        [TestCase("not a url", TestName = "not a URL")]
        [TestCaseSource(typeof(LoopbackRegistrationTests), nameof(LoopbackRegistrationTests.NotALoopbackStubHost))]
        public void ASwitchNamingNoLoopbackStubHost_IsRefused(string value) =>
            AssertRefused([ControllerStubs.SwitchArgument + "=" + value], Errors.CONTROLLER_STUB_ENDPOINT_INVALID);

        [Test]
        public void TheTwoTokenForm_IsRefused() =>
            AssertRefused([ControllerStubs.SwitchArgument, "http://127.0.0.1:50123/token/"], Errors.CONTROLLER_STUB_ENDPOINT_INVALID);

        [Test]
        public void TheSwitchGivenTwice_IsRefused() =>
            AssertRefused([ControllerStubs.SwitchArgument + "=http://127.0.0.1:50123/a/", ControllerStubs.SwitchArgument + "=http://127.0.0.1:50124/b/"],
                Errors.CONTROLLER_STUB_ENDPOINT_INVALID);

        /// <summary>
        /// A switch spelled any way but the exact one is refused rather than ignored: ignored, it would leave the
        /// executable on its settings file's controller, with its real credentials, in a run whose author believes
        /// it is stubbed. The refusal says how the switch is written and repeats only the name, never the value,
        /// which carries the host's token.
        /// </summary>
        [TestCase("--controller-stub:http://127.0.0.1:50123/" + Token + "/", TestName = "a colon for the equals sign")]
        [TestCase("--Controller-Stub=http://127.0.0.1:50123/" + Token + "/", TestName = "another case")]
        [TestCase("-controller-stub=http://127.0.0.1:50123/" + Token + "/", TestName = "one dash")]
        [TestCase("--controllerstub=http://127.0.0.1:50123/" + Token + "/", TestName = "no hyphen")]
        [TestCase("--controller_stub=http://127.0.0.1:50123/" + Token + "/", TestName = "an underscore")]
        [TestCase("/controller-stub=http://127.0.0.1:50123/" + Token + "/", TestName = "a slash for the dashes")]
        [TestCase("--CONTROLLER-STUB", TestName = "no value, upper case")]
        public void ASwitchSpelledAnotherWay_IsRefused_SayingHowItIsWritten(string argument)
        {
            AssertRefused([argument], Errors.CONTROLLER_STUB_ENDPOINT_INVALID);
            var refusal = Assert.Throws<ErrorWithCodeException>(() => ControllerStubSwitch.ApplySwitchCore([argument], new IhcSettings()));
            Assert.That(refusal!.Message, Does.Contain(ControllerStubs.SwitchArgument + "=http://127.0.0.1:").And.Not.Contain(Token));
        }

        /// <summary>The exact switch beside a misspelt one is two switches, which is refused as any repeat is.</summary>
        [Test]
        public void TheSwitchBesideAMisspeltOne_IsRefusedAsGivenTwice() =>
            AssertRefused([ControllerStubs.SwitchArgument + "=http://127.0.0.1:50123/a/", "--Controller-Stub=http://127.0.0.1:50124/b/"],
                Errors.CONTROLLER_STUB_ENDPOINT_INVALID);

        /// <summary>Only an option can be a misspelt switch: an argument that merely contains its words, such as a
        /// project file, is the application's own, and so is an option with a longer name.</summary>
        [TestCase("controller-stub.vis")]
        [TestCase("--controller-stubs=http://127.0.0.1:50123/a/")]
        [TestCase("--test")]
        public void AnArgumentThatOnlyResemblesTheSwitch_IsNoSwitch(string argument)
        {
            var (_, remaining, registration) = ControllerStubSwitch.ApplySwitchCore([argument], new IhcSettings());

            Assert.Multiple(() =>
            {
                Assert.That(ControllerStubs.SwitchRequested([argument]), Is.False);
                Assert.That(remaining, Is.EqualTo(new[] { argument }));
                Assert.That(registration, Is.Null);
            });
        }

        /// <summary>A value a refusal must never repeat, standing in for a host's token.</summary>
        private const string Token = "token-never-printed";

        [Test]
        public async Task ASwitchNamingAnAddressNothingListensOn_IsRefused()
        {
            string address;
            await using (LoopbackTestServer gone = await LoopbackTestServer.StartAsync((_, _) => Task.CompletedTask))
            {
                address = gone.Address;
            }

            AssertRefused([ControllerStubs.SwitchArgument + "=" + address + "/token/"], Errors.CONTROLLER_STUB_IDENTITY_FAILED);
        }

        /// <summary>The public entry refuses as the core does, an identity failure from under its blocking wait included,
        /// which surfaces as itself rather than wrapped, and without the switch hands the settings straight back; no path
        /// leaves a registration held.</summary>
        [Test]
        public async Task ThePublicEntry_RefusesAndPassesThroughAsTheCoreDoes()
        {
            await using LoopbackTestServer notAStubHost = await LoopbackTestServer.StartAsync((context, _) =>
                context.Response.WriteAsync("an ordinary local web server"));
            var loaded = new IhcSettings();

            Assert.That(ControllerStubs.ApplySwitch(["--test"], loaded).Effective, Is.SameAs(loaded));
            Assert.That(() => ControllerStubs.ApplySwitch([ControllerStubs.SwitchArgument + "=mock://x"], loaded),
                Throws.TypeOf<ErrorWithCodeException>());
            var identityRefusal = Assert.Throws<ErrorWithCodeException>(() =>
                ControllerStubs.ApplySwitch([ControllerStubs.SwitchArgument + "=" + notAStubHost.Address + "/token/"], loaded));
            Assert.That(identityRefusal!.ErrorCode, Is.EqualTo(Errors.CONTROLLER_STUB_IDENTITY_FAILED));
            Assert.That(ControllerStubRegistry.AnyLive, Is.False);
        }

        [Test]
        public async Task TwoSwitchedStarts_GetTwoEndpoints_AndReleasingTheFirstLeavesTheSecondRouting()
        {
            await using ControllerStubLoopbackHost first = await ControllerStubLoopbackHost.StartAsync(new ControllerStub(), CancellationToken.None);
            await using ControllerStubLoopbackHost second = await ControllerStubLoopbackHost.StartAsync(new ControllerStub(), CancellationToken.None);

            var (_, _, one) = ControllerStubSwitch.ApplySwitchCore([ControllerStubs.SwitchArgument + "=" + first.BaseAddress], new IhcSettings());
            // Disposing is idempotent, so this only acts when the case fails before it releases the first itself, and
            // keeps that failure from latching every later test in this unlatched suite.
            using var firstRegistration = one;
            var (effective, _, two) = ControllerStubSwitch.ApplySwitchCore([ControllerStubs.SwitchArgument + "=" + second.BaseAddress], new IhcSettings());
            using (two)
            {
                Assert.That(two!.Endpoint, Is.Not.EqualTo(one!.Endpoint));
                one.Dispose();

                Assert.That(await LoginFrom(effective), Is.True);
            }
        }

        /// <summary>
        /// The public entry hands back no registration, so nothing an executable holds keeps the switch's registration
        /// alive. Dropped the same way here, from the core, it still routes after a forced collection. The core is used
        /// so the case can dispose the registration at the end, through a weak handle that does not itself keep it
        /// alive, and leave the later tests unlatched; the public entry's own hold could only add a reference.
        /// </summary>
        [Test]
        public async Task ASwitchsDroppedRegistration_StillRoutes_ThroughACollection()
        {
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(new ControllerStub(), CancellationToken.None);
            IhcSettings switched = SwitchAndDrop(host.BaseAddress, out WeakReference<ControllerStubRegistration> handle);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            try
            {
                Assert.That(await LoginFrom(switched), Is.True, "the dropped registration still routes");
                Assert.That(handle.TryGetTarget(out _), Is.True, "the SDK holds the registration strongly");
            }
            finally
            {
                // A registration left live latches every later test in this unlatched suite, hiding this failure.
                if (handle.TryGetTarget(out ControllerStubRegistration? registration))
                {
                    registration.Dispose();
                }
            }
        }

        /// <summary>
        /// Applies the switch and keeps only a weak handle on its registration. Kept out of line, because a local the
        /// JIT kept alive in the test would make the case unable to fail.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IhcSettings SwitchAndDrop(Uri hostAddress, out WeakReference<ControllerStubRegistration> handle)
        {
            var (effective, _, registration) = ControllerStubSwitch.ApplySwitchCore(
                [ControllerStubs.SwitchArgument + "=" + hostAddress], new IhcSettings());
            handle = new WeakReference<ControllerStubRegistration>(registration!);
            return effective;
        }

        private static void AssertRefused(string[] args, int code)
        {
            Assert.That(ControllerStubs.SwitchRequested(args), Is.True, "a refused switch is still a requested one");
            var refusal = Assert.Throws<ErrorWithCodeException>(() => ControllerStubSwitch.ApplySwitchCore(args, new IhcSettings()));
            Assert.That(refusal!.ErrorCode, Is.EqualTo(code));
        }

        /// <summary>The names of the [SensitiveData] properties whose value <paramref name="effective"/> still shares
        /// with <paramref name="loaded"/>.</summary>
        private static IEnumerable<string> SensitiveValuesKept(IhcSettings loaded, IhcSettings effective) =>
            typeof(IhcSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttribute<SensitiveDataAttribute>() is not null)
                .Where(property => Equals(property.GetValue(loaded), property.GetValue(effective)))
                .Select(property => property.Name);

        /// <summary>
        /// Logs in on the switched settings. Admitted in ControllerReachGuard: the endpoint is a stub endpoint the
        /// switch registered, which the SDK routes only to that registration.
        /// </summary>
        internal static async Task<bool> LoginFrom(IhcSettings switched)
        {
            using var authentication = new AuthenticationService(switched);
            await authentication.Authenticate();
            return true;
        }
    }
}
