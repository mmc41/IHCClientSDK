using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CsCheck;
using FakeItEasy;
using Ihc.App;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The metamorphic law of <see cref="AdminAppService.Store"/>'s change tracking: storing a model in ONE step
    /// must leave the controller in the same state as storing it through an intermediate step. Store(A→C) against
    /// Store(A→B) then Store(B→C).
    /// <para>
    /// It is a law worth stating because <c>Store</c> deliberately does NOT write what it is given — it diffs the
    /// model against a snapshot and applies only the difference, so what reaches the controller depends on the
    /// route taken to get there, while the destination must not. The two routes issue genuinely different API
    /// calls: a field edited A→B→C is written twice by the sequence and once by the single step, and a field
    /// edited away and back (A→B→A) is written twice by the sequence and NOT AT ALL by the single step. Only the
    /// resulting controller state is required to agree.
    /// </para>
    /// <para>
    /// Follows the pattern set by <c>CompositeCommandMetamorphicTests</c> in <c>safe_project_tests</c> — mutable
    /// carrier, explicit <c>equal:</c>, independence by construction, <c>threads: 1</c>; see its class remarks for
    /// why each is load-bearing. The carrier here is <see cref="Controller"/>, the recorded state the fakes read
    /// and write, since <see cref="MutableAdminModel"/> alone would record nothing and pass vacuously.
    /// </para>
    /// <para>
    /// Per the project's mocking rule the IHC API services are faked and the application service is REAL: a faked
    /// <see cref="AdminAppService"/> would be a fake of the change tracking under test.
    /// </para>
    /// </summary>
    [TestFixture]
    public class AdminStoreMetamorphicTests
    {
        // Small value domains on purpose: with a wide space, B and C would differ from A in every field on every
        // iteration and the "unchanged, so never written" path — the whole point of change tracking — would
        // hardly ever be taken.
        private static readonly Gen<string> Hosts = Gen.OneOfConst("a.example", "b.example");
        private static readonly Gen<int> Ports = Gen.OneOfConst(25, 465);
        private static readonly Gen<string> Emails = Gen.OneOfConst("one@test", "two@test");
        private static readonly string[] UserPool = ["alice", "bob"];

        /// <summary>The controller's state as the fakes hold it — the mutable carrier the two paths write into.</summary>
        private sealed class Controller
        {
            public EmailControlSettings Email { get; set; } = Baseline.Email;
            public SMTPSettings Smtp { get; set; } = Baseline.Smtp;
            public DNSServers Dns { get; set; } = Baseline.Dns;
            public NetworkSettings Network { get; set; } = Baseline.Network;
            public WebAccessControl WebAccess { get; set; } = Baseline.WebAccess;
            public WLanSettings WLan { get; set; } = Baseline.WLan;
            public Dictionary<string, IhcUser> Users { get; } =
                Baseline.Users().ToDictionary(u => u.Username);

            /// <summary>How many write calls this route issued, across EVERY settings block and not just one of
            /// them — a counter that watched a single block would report "the fakes are recording" on the strength
            /// of one fake. NOT part of the compared state: the two routes are expected to differ here, and that
            /// difference is the reason the law is about the resulting state rather than about the calls.</summary>
            public int WriteCount { get; set; }

            /// <summary>The service under test, real, wired to fakes over THIS state. Set immediately after
            /// construction — the fakes close over the controller, so the two cannot be built in one expression.</summary>
            public AdminAppService? Service { get; set; }

            public void Store(MutableAdminModel model) => Service!.Store(model).GetAwaiter().GetResult();

            /// <summary>Records one write of any kind. Every faked setter goes through here, so no block can be
            /// silently left out of the count.</summary>
            public Task Write(Action apply)
            {
                apply();
                WriteCount++;
                return Task.CompletedTask;
            }
        }

        /// <summary>State A: what the controller holds before either path runs, and the snapshot
        /// <see cref="AdminAppService.Store"/> loads for itself when it has none.</summary>
        private static class Baseline
        {
            public static readonly EmailControlSettings Email =
                new() { ServerIpAddress = "a.example", ServerPortNumber = 25, EmailAddress = "ctrl@test" };
            public static readonly SMTPSettings Smtp = new() { Hostname = "a.example", Hostport = 25, Username = "smtp" };
            public static readonly DNSServers Dns = new() { PrimaryDNS = "a.example", SecondaryDNS = "a.example" };
            public static readonly NetworkSettings Network = new() { IpAddress = "a.example" };
            public static readonly WebAccessControl WebAccess = new() { UsbLoginRequired = false, AdministratorUsb = false };
            public static readonly WLanSettings WLan = new() { Enabled = false, Ssid = "a.example" };

            public static IEnumerable<IhcUser> Users() => [User("alice", "one@test", IhcUserGroup.Administrators)];
        }

        /// <summary>Only the ADMIN-CHANGEABLE properties vary; the timestamps and the rest are pinned. Change
        /// tracking compares users by <c>EqualsChangeableProperties</c>, so a user differing only in a
        /// non-changeable field is deliberately NOT written — which would leave the two paths holding
        /// different-but-equivalent objects and turn a correct service into a red property.</summary>
        private static IhcUser User(string username, string email, IhcUserGroup group) => new()
        {
            Username = username,
            Email = email,
            Group = group,
            Password = "pw",
            Firstname = "F",
            Lastname = "L",
            Phone = "0",
            Project = "p",
            CreatedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            LoginDate = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
        };

        /// <summary>A whole model, every block varying over its small domain — built by <c>with</c> from the
        /// baseline so the properties this test does not vary keep valid controller values.</summary>
        private static readonly Gen<MutableAdminModel> Models =
            Gen.Select(
                Gen.Select(Hosts, Ports, (h, p) => Baseline.Email with { ServerIpAddress = h, ServerPortNumber = p }),
                Gen.Select(Hosts, Ports, (h, p) => Baseline.Smtp with { Hostname = h, Hostport = p }),
                Gen.Select(Hosts, Hosts, (a, b) => Baseline.Dns with { PrimaryDNS = a, SecondaryDNS = b }),
                Hosts.Select(ip => Baseline.Network with { IpAddress = ip }),
                Gen.Select(Gen.Bool, Gen.Bool, (u, a) => Baseline.WebAccess with { UsbLoginRequired = u, AdministratorUsb = a }),
                Gen.Select(Gen.Bool, Hosts, (e, s) => Baseline.WLan with { Enabled = e, Ssid = s }),
                Gen.Select(Gen.Bool.Array[UserPool.Length], Emails.Array[UserPool.Length],
                    Gen.OneOfConst(IhcUserGroup.Administrators, IhcUserGroup.Users).Array[UserPool.Length],
                    (present, emails, groups) => UserSet(present, emails, groups)),
                (email, smtp, dns, network, web, wlan, users) => new MutableAdminModel
                {
                    ModelMetadata = ModelMetadata.Current(typeof(MutableAdminModel)),
                    Users = users,
                    EmailControl = email,
                    SmtpSettings = smtp,
                    DnsServers = dns,
                    NetworkSettings = network,
                    WebAccess = web,
                    WLanSettings = wlan,
                });

        private static HashSet<IhcUser> UserSet(bool[] present, string[] emails, IhcUserGroup[] groups)
        {
            var users = new HashSet<IhcUser>();
            for (int i = 0; i < present.Length; i++)
            {
                if (present[i])
                {
                    users.Add(User(UserPool[i], emails[i], groups[i]));
                }
            }
            return users;
        }

        private static Gen<Controller> Controllers() =>
            Gen.Const(() =>
            {
                var controller = new Controller();
                controller.Service = ServiceOver(controller);
                return controller;
            });

        /// <summary>A REAL AdminAppService over fakes that read and write <paramref name="controller"/> — the
        /// recording half. Every getter answers from the live state rather than from a fixed value, which is what
        /// makes the snapshot Store loads for itself be state A rather than a constant.</summary>
        private static AdminAppService ServiceOver(Controller controller)
        {
            var auth = A.Fake<IAuthenticationService>();
            var users = A.Fake<IUserManagerService>();
            var config = A.Fake<IConfigurationService>();

            A.CallTo(() => auth.IsAuthenticated()).Returns(Task.FromResult(true));

            A.CallTo(() => users.GetUsers(A<bool>._))
                .ReturnsLazily(() => Task.FromResult<IReadOnlySet<IhcUser>>(new HashSet<IhcUser>(controller.Users.Values)));
            A.CallTo(() => users.AddUser(A<IhcUser>._))
                .ReturnsLazily((IhcUser u) => controller.Write(() => controller.Users[u.Username] = u));
            A.CallTo(() => users.UpdateUser(A<IhcUser>._))
                .ReturnsLazily((IhcUser u) => controller.Write(() => controller.Users[u.Username] = u));
            A.CallTo(() => users.RemoveUser(A<string>._))
                .ReturnsLazily((string name) => controller.Write(() => controller.Users.Remove(name)));

            A.CallTo(() => config.GetEmailControlSettings()).ReturnsLazily(() => Task.FromResult(controller.Email));
            A.CallTo(() => config.SetEmailControlSettings(A<EmailControlSettings>._))
                .ReturnsLazily((EmailControlSettings s) => controller.Write(() => controller.Email = s));
            A.CallTo(() => config.GetSMTPSettings()).ReturnsLazily(() => Task.FromResult(controller.Smtp));
            A.CallTo(() => config.SetSMTPSettings(A<SMTPSettings>._))
                .ReturnsLazily((SMTPSettings s) => controller.Write(() => controller.Smtp = s));
            A.CallTo(() => config.GetDNSServers()).ReturnsLazily(() => Task.FromResult(controller.Dns));
            A.CallTo(() => config.SetDNSServers(A<DNSServers>._))
                .ReturnsLazily((DNSServers s) => controller.Write(() => controller.Dns = s));
            A.CallTo(() => config.GetNetworkSettings()).ReturnsLazily(() => Task.FromResult(controller.Network));
            A.CallTo(() => config.SetNetworkSettings(A<NetworkSettings>._))
                .ReturnsLazily((NetworkSettings s) => controller.Write(() => controller.Network = s));
            A.CallTo(() => config.GetWebAccessControl()).ReturnsLazily(() => Task.FromResult(controller.WebAccess));
            A.CallTo(() => config.SetWebAccessControl(A<WebAccessControl>._))
                .ReturnsLazily((WebAccessControl s) => controller.Write(() => controller.WebAccess = s));
            A.CallTo(() => config.GetWLanSettings()).ReturnsLazily(() => Task.FromResult(controller.WLan));
            A.CallTo(() => config.SetWLanSettings(A<WLanSettings>._))
                .ReturnsLazily((WLanSettings s) => controller.Write(() => controller.WLan = s));

            return new AdminAppService(Settings, fileEnryption: false, auth, users, config);
        }

        private static IhcSettings Settings => new()
        {
            Endpoint = "http://test",
            UserName = "testuser",
            Password = "testpass",
            Application = Application.administrator,
            LogSensitiveData = false,
            AsyncContinueOnCapturedContext = false,
        };

        /// <summary>The explicit equality: the whole recorded controller state, block by block and user by user.
        /// Record equality throughout, so this compares VALUES and not which of the two routes happened to write
        /// the object that is sitting there.</summary>
        private static bool SameControllerState(Controller a, Controller b) =>
            Equals(a.Email, b.Email)
            && Equals(a.Smtp, b.Smtp)
            && Equals(a.Dns, b.Dns)
            && Equals(a.Network, b.Network)
            && Equals(a.WebAccess, b.WebAccess)
            && Equals(a.WLan, b.WLan)
            && a.Users.Count == b.Users.Count
            && a.Users.OrderBy(u => u.Key, StringComparer.Ordinal)
                .SequenceEqual(b.Users.OrderBy(u => u.Key, StringComparer.Ordinal));

        private static string Describe(Controller controller) =>
            $"email={controller.Email?.ServerIpAddress}:{controller.Email?.ServerPortNumber} "
            + $"smtp={controller.Smtp?.Hostname}:{controller.Smtp?.Hostport} "
            + $"dns={controller.Dns?.PrimaryDNS}/{controller.Dns?.SecondaryDNS} "
            + $"net={controller.Network?.IpAddress} wlan={controller.WLan?.Enabled}/{controller.WLan?.Ssid} "
            + $"users=[{string.Join(", ", controller.Users.Values.Select(u => $"{u.Username}:{u.Email}:{u.Group}"))}]";

        /// <summary>
        /// <see cref="AdminAppService"/> builds a <c>SimpleSecret</c> in its constructor, and that reads this
        /// variable. Today it is only VALIDATED when file encryption is on and these fixtures pass
        /// <c>fileEnryption: false</c> — but that flag is a construction argument, not a property of the service, so
        /// the day a fixture here flips it the failure would be an unrelated-looking throw from a constructor.
        /// Set once for the fixture rather than per test, so it cannot be added to one test and forgotten in the next.
        /// </summary>
        [OneTimeSetUp]
        public void SetEncryptionPassphrase() =>
            Environment.SetEnvironmentVariable(
                SimpleSecret.EncryptPassphaseEnvName, "test-passphrase-for-unit-tests");

        [Test]
        public void StoringInOneStep_ReachesTheSameControllerState_AsStoringThroughAnIntermediateStep()
        {
            Controllers().SampleMetamorphic(
                Gen.Select(Models, Models, (b, c) => (Intermediate: b, Destination: c))
                    .Metamorphic<Controller>(
                        step => $"via {step.Intermediate.SmtpSettings.Hostname} to {step.Destination.SmtpSettings.Hostname}",
                        (controller, step) => controller.Store(step.Destination),
                        (controller, step) => { controller.Store(step.Intermediate); controller.Store(step.Destination); }),
                equal: SameControllerState,
                print: Describe,
                iter: 100,
                threads: 1);
        }

        /// <summary>
        /// The case the law exists for, pinned as behaviour rather than left for the property to stumble on: a
        /// setting edited AWAY AND BACK. The single step detects no change and issues NO call at all; the sequence
        /// issues two. The controller ends in the same state either way — which is exactly what the law claims,
        /// and exactly what a comparison of API CALLS instead of resulting state would have called a failure.
        /// <para>It doubles as this file's proof that the recording fakes record: a property whose fakes silently
        /// dropped every write would pass with both routes sitting untouched at state A.</para>
        /// </summary>
        [Test]
        public void ASettingEditedAwayAndBack_CostsTwoWritesInSequence_AndNoneInOneStep()
        {
            MutableAdminModel atRest = BaselineModel();
            MutableAdminModel movedAway = BaselineModel();
            movedAway.EmailControl = Baseline.Email with { ServerIpAddress = "b.example" };

            var inOneStep = new Controller();
            inOneStep.Service = ServiceOver(inOneStep);
            inOneStep.Store(atRest);

            var viaTheDetour = new Controller();
            viaTheDetour.Service = ServiceOver(viaTheDetour);
            viaTheDetour.Store(movedAway);
            viaTheDetour.Store(atRest);

            Assert.Multiple(() =>
            {
                Assert.That(inOneStep.WriteCount, Is.Zero,
                    "storing what the controller already holds must not touch it — the point of change tracking");
                Assert.That(viaTheDetour.WriteCount, Is.EqualTo(2),
                    "the detour really did write, out and back — so the fakes are recording");
                Assert.That(SameControllerState(inOneStep, viaTheDetour), Is.True,
                    "…and both routes leave the controller in the same state, which is all the law requires");
            });
        }

        private static MutableAdminModel BaselineModel() => new()
        {
            ModelMetadata = ModelMetadata.Current(typeof(MutableAdminModel)),
            Users = new HashSet<IhcUser>(Baseline.Users()),
            EmailControl = Baseline.Email,
            SmtpSettings = Baseline.Smtp,
            DnsServers = Baseline.Dns,
            NetworkSettings = Baseline.Network,
            WebAccess = Baseline.WebAccess,
            WLanSettings = Baseline.WLan,
        };
    }
}
