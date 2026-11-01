using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Ihc.Stubbing;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The controller-stub surface stays confined. Outside the stub namespace, only the sites that route, refuse
    /// and answer "is this a stub endpoint" may reach into it, and they all ask one predicate. A second reader of
    /// the endpoint text would be a second answer to that question, and the day the two disagree a call meant for a
    /// stub reaches a socket, or the stub indicator stops telling the truth.
    /// </summary>
    /// <remarks>
    /// Read from IL rather than the ArchUnitNET model, because the answer is per BODY: the routing site is an async
    /// method, so what it calls lives in a compiler-generated state machine, which is named here by the method it
    /// was written as. The caller set is held to EQUAL its allow-list, so the rule arms itself: a dropped call
    /// fails as surely as an extra one.
    /// </remarks>
    [TestFixture]
    public class StubSurfaceArchitectureTests
    {
        private const string SdkRoot = "Ihc";
        private static readonly string StubNamespace = typeof(IControllerStub).Namespace!;
        private static readonly Assembly SdkAssembly = typeof(IhcSettings).Assembly;

        /// <summary>
        /// The SDK's sites outside the stub namespace that may use it: the routing in the controller client, the
        /// refusal at service construction, and the one predicate a host reads. Named as text because the client is
        /// internal and this suite is no friend of the SDK; the equality below fails on a stale name.
        /// </summary>
        private static readonly ContainmentSite[] SdkAllowList =
        [
            new(SdkRoot + ".Client", "Post"),
            new(typeof(ServiceBase).FullName!, ".ctor"),
            new(typeof(IhcSettings).FullName!, nameof(IhcSettings.IsStubConfigured)),
        ];

        [Test]
        public void OutsideTheStubNamespace_TheSdksStubCallers_AreExactlyTheAllowList()
        {
            Assert.That(StubSurfaceCallers(SdkAssembly), Is.EquivalentTo(SdkAllowList),
                "outside the stub namespace, only routing, the construction-time refusal and the one predicate may "
                + "use the stub surface; a site missing from the scan is a dropped guard, an extra one is a second "
                + "answer to 'is this a stub endpoint'");
        }

        [Test]
        public void OutsideTheStubNamespace_NoSdkCodeLoadsAStubEndpointLiteral()
        {
            var offending = StubEndpointLiterals(SdkAssembly, SdkRoot).ToList();

            Assert.That(offending, Is.Empty,
                "only the stub namespace may spell a stub endpoint; elsewhere, ask IhcSettings.IsStubConfigured — "
                + "offenders: " + string.Join("; ", offending.Select(hit => $"{hit.Site} loads \"{hit.Literal}\"")));
        }

        /// <summary>
        /// Seeded control for the caller scan's halves the SDK leaves empty, a predicate reader and a stub
        /// implementation: the same scan over this assembly must report both seeds.
        /// </summary>
        [Test]
        public void StubCallerScan_IsArmed() =>
            Assert.That(StubSurfaceCallers(typeof(StubSurfaceArchitectureTests).Assembly),
                Is.SupersetOf(new ContainmentSite[]
                {
                    new(typeof(Ihc.Transport.Seeded.SeededPredicateReader).FullName!,
                        nameof(Ihc.Transport.Seeded.SeededPredicateReader.Read)),
                    new(typeof(Ihc.Transport.Seeded.SeededControllerStub).FullName!, ImplementsStub),
                }));

        /// <summary>
        /// Seeded control for the literal scan, whose production verdict is empty: it must report a literal written
        /// in place and one inlined from a <c>const</c>, each spelling a stub endpoint its own way.
        /// </summary>
        [Test]
        public void StubEndpointLiteralScan_IsArmed() =>
            Assert.That(StubEndpointLiterals(typeof(StubSurfaceArchitectureTests).Assembly, SdkRoot).Select(hit => hit.Site),
                Is.SupersetOf(new ContainmentSite[]
                {
                    new(typeof(Ihc.Transport.Seeded.SeededStubEndpointLiterals).FullName!,
                        nameof(Ihc.Transport.Seeded.SeededStubEndpointLiterals.MockKey)),
                    new(typeof(Ihc.Transport.Seeded.SeededStubEndpointLiterals).FullName!,
                        nameof(Ihc.Transport.Seeded.SeededStubEndpointLiterals.InlinedStubHost)),
                }));

        /// <summary>
        /// The executables the switch is wired into, by assembly name, each with the namespace root its authored code
        /// lives under, and the shared bootstrap that holds the console stub line. Loaded by name because the three
        /// console utilities declare no public type to anchor on, and two of them declare the same internal one.
        /// The executables' outbound rule reads the same roster.
        /// </summary>
        internal static readonly (string Assembly, string Root)[] Executables =
        [
            ("ihc_openvisual", "ihc_openvisual"),
            ("ihc_lab", "IhcLab"),
            ("ihc_info", SdkRoot),
            ("ihc_admin", SdkRoot),
            ("ihc_ProjectDownloadUpload", SdkRoot),
            ("ihc_hostbootstrap", SdkRoot),
        ];

        /// <summary>
        /// Executables that reference the SDK and are deliberately not switched, each with its reason. Every other
        /// executable referencing the SDK must be one the scan above loads.
        /// </summary>
        private static readonly Dictionary<string, string> NotWired = new(StringComparer.Ordinal)
        {
            ["ihc_settings_encrypt"] = "references the SDK only for settings encryption and makes no service call",
            ["example1"] = "an SDK usage sample, kept minimal so a reader copies only production code",
            ["example2"] = "an SDK usage sample, kept minimal so a reader copies only production code",
            ["ihcclient_stub"] = "serves the switch and never takes it",
        };

        private const string SwitchRequested = nameof(ControllerStubs) + "." + nameof(ControllerStubs.SwitchRequested);
        private const string ApplySwitch = nameof(ControllerStubs) + "." + nameof(ControllerStubs.ApplySwitch);
        private const string Predicate = nameof(IhcSettings) + "." + nameof(IhcSettings.IsStubConfigured);

        /// <summary>
        /// What each executable may do with the stub surface, per call: every Main asks whether the switch was given
        /// and every Main but Lab's applies it; Lab applies it in <c>Program.Compose</c>, which also asks whether the
        /// switch was given, since only the switch makes a stub process; the GET/STORE command line two utilities
        /// share, and <c>ihc_info</c>'s Main, ask of each argument whether it is the switch, so they can check the
        /// rest before the settings file is read; the composition roots and Lab's title ask the predicate; and the
        /// console stub line's helper asks it once, before printing.
        /// </summary>
        private static readonly ExecutableStubCall[] ExecutableAllowList =
        [
            new("ihc_openvisual", new("ihc_openvisual.Program", "Main"), SwitchRequested),
            new("ihc_openvisual", new("ihc_openvisual.Program", "Main"), ApplySwitch),
            new("ihc_openvisual", new("ihc_openvisual.App", "OnFrameworkInitializationCompleted"), Predicate),
            new("ihc_lab", new("IhcLab.Program", "Main"), SwitchRequested),
            new("ihc_lab", new("IhcLab.Program", "Compose"), ApplySwitch),
            new("ihc_lab", new("IhcLab.Program", "Compose"), SwitchRequested),
            new("ihc_lab", new("IhcLab.MainWindow", "LoginUpdated"), Predicate),
            new("ihc_lab", new("IhcLab.MainWindow", "TitleFor"), Predicate),
            new("ihc_info", new("Ihc.example.Program", "Main"), SwitchRequested),
            new("ihc_info", new("Ihc.example.Program", "Main (lambda)"), SwitchRequested),
            new("ihc_info", new("Ihc.example.Program", "Main"), ApplySwitch),
            new("ihc_admin", new("Ihc.download_upload_example.Program", "Main"), SwitchRequested),
            new("ihc_admin", new("Ihc.download_upload_example.Program", "Main"), ApplySwitch),
            new("ihc_admin", new("Ihc.Utilities.GetStoreCommandLine", "WithoutSwitch (lambda)"), SwitchRequested),
            new("ihc_ProjectDownloadUpload", new("Ihc.download_upload_example.Program", "Main"), SwitchRequested),
            new("ihc_ProjectDownloadUpload", new("Ihc.download_upload_example.Program", "Main"), ApplySwitch),
            new("ihc_ProjectDownloadUpload", new("Ihc.Utilities.GetStoreCommandLine", "WithoutSwitch (lambda)"), SwitchRequested),
            new("ihc_hostbootstrap", new("Ihc.Bootstrap.ControllerStubConsole", "AnnounceIfStub"), Predicate),
        ];

        /// <summary>One use of the stub surface: which assembly, which authored site, and what it called.</summary>
        private readonly record struct ExecutableStubCall(string Assembly, ContainmentSite Site, string Called)
        {
            public override string ToString() => $"{Assembly}: {Site} -> {Called}";
        }

        [Test]
        public void InTheSwitchedExecutables_TheStubCallers_AreExactlyTheAllowList()
        {
            var found = Executables.SelectMany(executable => ExecutableStubCalls(executable.Assembly, executable.Root)).ToList();

            Assert.That(found, Is.EquivalentTo(ExecutableAllowList),
                "every executable asks for the switch and applies it in Main, Lab through Program.Compose, and only the "
                + "composition roots, Lab's title and the console stub line ask the predicate; a missing call is an "
                + "executable that ignores the switch, an extra one is a second door to the stub — found: "
                + string.Join("; ", found));
        }

        /// <summary>
        /// An executable never spells a stub endpoint. One that did would carry a stub address of its own beside the
        /// switch, and could point itself at a stub, or claim to be on one, by its own code rather than the switch's.
        /// </summary>
        [Test]
        public void InTheSwitchedExecutables_NoCodeLoadsAStubEndpointLiteral()
        {
            var offending = Executables
                .SelectMany(executable => StubEndpointLiterals(Assembly.Load(new AssemblyName(executable.Assembly)), executable.Root)
                    .Select(hit => $"{executable.Assembly}: {hit.Site} loads \"{hit.Literal}\""))
                .ToList();

            Assert.That(offending, Is.Empty,
                "an executable reaches a stub only through the switch, and asks IhcSettings.IsStubConfigured "
                + "whether it is on one — offenders: " + string.Join("; ", offending));
        }

        /// <summary>Lab applies the switch in <c>Program.Compose</c>, so the rule above holds only while Main calls it.</summary>
        [Test]
        public void LabsMain_ComposesThroughProgramCompose()
        {
            MethodBase main = AuthoredMembers.Of(Assembly.Load(new AssemblyName("ihc_lab")), "IhcLab")
                .Single(member => member.DeclaringType?.FullName == "IhcLab.Program" && member.Name == "Main");

            Assert.That(IlBody.CalledMethods(main).Select(called => $"{called.DeclaringType?.FullName}.{called.Name}"),
                Does.Contain("IhcLab.Program.Compose"),
                "a Main that stopped calling Compose would start Lab without the switch, and the allow-list would not see it");
        }

        /// <summary>
        /// The console stub line has one print site, so every utility announces a stub the same way: no switched
        /// executable spells the line's text but the helper.
        /// </summary>
        [Test]
        public void TheConsoleStubLine_HasOnePrintSite()
        {
            var printing = Executables
                .SelectMany(executable => AuthoredMembers.Of(Assembly.Load(new AssemblyName(executable.Assembly)), executable.Root)
                    .Where(member => IlBody.Instructions(member).Any(instruction =>
                        instruction.Literal?.Contains(Ihc.Bootstrap.ControllerStubConsole.LinePrefix, StringComparison.Ordinal) == true))
                    .Select(ContainmentSite.For))
                .Distinct();

            Assert.That(printing, Is.EquivalentTo(new ContainmentSite[] { new("Ihc.Bootstrap.ControllerStubConsole", "AnnounceIfStub") }));
        }

        /// <summary>
        /// Every executable in the solution outside <c>tests/</c> that references the SDK is either one the scans above
        /// load, or on the not-wired list with its reason. A new executable is then a decision someone has to make.
        /// </summary>
        [Test]
        public void EveryExecutableReferencingTheSdk_IsScannedOrDeliberatelyNotWired()
        {
            string root = TestRepository.RequireRoot();
            var projects = WindowsProjectFileScan.Projects(Path.Combine(root, "IHCClientSDK.sln"))
                .Select(project => (project.Name, Path.GetRelativePath(root, project.Path), ProjectFileReader.Load(project.Path)))
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(projects.Select(project => project.Name), Does.Contain("ihc_lab").And.Contain("ihcclient_stub"),
                    "the roster must find the projects it judges, or it judges nothing");
                Assert.That(UnaccountedExecutables(projects), Is.Empty);
            });
        }

        /// <summary>Positive control for the roster: only seeded documents can show it is able to fail.</summary>
        [Test]
        public void TheRoster_ReportsAnUnaccountedExecutable_AndNothingElse()
        {
            static XDocument Project(string outputType, string reference) => XDocument.Parse(
                $"<Project><PropertyGroup><OutputType>{outputType}</OutputType></PropertyGroup>"
                + $"<ItemGroup><ProjectReference Include=\"{reference}\" /></ItemGroup></Project>");
            const string Sdk = @"..\..\ihcclient\ihcclient.csproj";

            Assert.That(UnaccountedExecutables(
            [
                ("seeded_tool", Path.Combine("utilities", "seeded_tool", "seeded_tool.csproj"), Project("Exe", Sdk)),
                ("seeded_app", Path.Combine("applications", "seeded_app", "seeded_app.csproj"), Project("WinExe", Sdk)),
                ("seeded_library", Path.Combine("shared", "seeded_library", "seeded_library.csproj"), Project("Library", Sdk)),
                ("seeded_suite", Path.Combine("tests", "seeded_suite", "seeded_suite.csproj"), Project("Exe", Sdk)),
                // Named like the SDK but not it, and still reaching it: the stub references the SDK.
                ("seeded_offline", Path.Combine("utilities", "seeded_offline", "seeded_offline.csproj"),
                    Project("Exe", @"..\..\ihcclient_stub\ihcclient_stub.csproj")),
                // Named like the SDK but not it, and reaching nothing: a name is no reference.
                ("seeded_lookalike", Path.Combine("utilities", "seeded_lookalike", "seeded_lookalike.csproj"),
                    Project("Exe", @"..\..\shared\ihcclient_extras\ihcclient_extras.csproj")),
                ("ihcclient_extras", Path.Combine("shared", "ihcclient_extras", "ihcclient_extras.csproj"),
                    Project("Library", @"..\ihcclient_extras_core\ihcclient_extras_core.csproj")),
                ("ihc_info", Path.Combine("utilities", "ihc_info", "ihc_info.csproj"), Project("Exe", Sdk)),
                ("ihcclient_stub", Path.Combine("ihcclient_stub", "ihcclient_stub.csproj"), Project("Exe", @"..\ihcclient\ihcclient.csproj")),
                // The SDK reached only through a library, as a tool built on the host bootstrap reaches it.
                ("seeded_hosted", Path.Combine("utilities", "seeded_hosted", "seeded_hosted.csproj"),
                    Project("Exe", @"..\..\shared\ihc_hostbootstrap\ihc_hostbootstrap.csproj")),
                ("ihc_hostbootstrap", Path.Combine("shared", "ihc_hostbootstrap", "ihc_hostbootstrap.csproj"), Project("Library", Sdk)),
            ]), Is.EqualTo(new[] { "seeded_app", "seeded_hosted", "seeded_offline", "seeded_tool" }));
        }

        private sealed record RosterProject(string Name, string RelativePath, XDocument Project);

        /// <summary>
        /// The executables outside <c>tests/</c> that reference the SDK, directly or through a project they reference,
        /// and are neither scanned nor on the not-wired list, by name.
        /// </summary>
        private static IReadOnlyList<string> UnaccountedExecutables(
            IEnumerable<(string Name, string RelativePath, XDocument Project)> projects)
        {
            var roster = projects.Select(project => new RosterProject(project.Name, project.RelativePath, project.Project)).ToList();
            var byPath = roster.ToDictionary(project => Normalised(project.RelativePath), StringComparer.OrdinalIgnoreCase);
            return [.. roster
                .Where(project => !Normalised(project.RelativePath).StartsWith("tests/", StringComparison.Ordinal))
                .Where(project => ProjectFileReader.IsExecutable(project.Project))
                .Where(project => ReachesTheSdk(project, byPath, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .Select(project => project.Name)
                .Where(name => !Executables.Any(executable => executable.Assembly == name) && !NotWired.ContainsKey(name))
                .Order(StringComparer.Ordinal)];
        }

        /// <summary>
        /// Whether <paramref name="project"/> references the SDK, itself or through a project it references: a tool
        /// that gets the SDK through the host bootstrap builds services from it as surely as one that names it.
        /// </summary>
        private static bool ReachesTheSdk(RosterProject project, IReadOnlyDictionary<string, RosterProject> byPath, HashSet<string> visited)
        {
            foreach (string reference in ProjectFileReader.ProjectReferences(project.Project))
            {
                if (reference.EndsWith("/ihcclient.csproj", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                string target = Normalised(Path.Combine(Path.GetDirectoryName(project.RelativePath) ?? "", reference));
                if (visited.Add(target) && byPath.TryGetValue(target, out RosterProject? referenced)
                    && ReachesTheSdk(referenced, byPath, visited))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A relative path with <c>/</c> separators and its <c>.</c> and <c>..</c> segments folded away, so a
        /// reference written from one project's folder names the same key as the project it reaches.</summary>
        private static string Normalised(string relativePath)
        {
            var segments = new List<string>();
            foreach (string segment in relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == "..")
                {
                    if (segments.Count > 0 && segments[^1] != "..")
                    {
                        segments.RemoveAt(segments.Count - 1);
                        continue;
                    }
                }
                else if (segment == ".")
                {
                    continue;
                }
                segments.Add(segment);
            }
            return string.Join('/', segments);
        }

        /// <summary>Every use of the stub surface in one executable assembly, per call.</summary>
        private static IEnumerable<ExecutableStubCall> ExecutableStubCalls(string assemblyName, string root)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
            var calls = AuthoredMembers.Of(assembly, root)
                .Where(member => !InStubNamespace(member.DeclaringType))
                .SelectMany(member => IlBody.CalledMethods(member)
                    .Where(called => InStubNamespace(called.DeclaringType) || IsThePredicate(called))
                    .Select(called => new ExecutableStubCall(assemblyName, ContainmentSite.For(member),
                        $"{called.DeclaringType!.Name}.{called.Name}")));
            var implementing = AuthoredMembers.Types(assembly, root)
                .Where(type => !InStubNamespace(type) && type.IsClass && typeof(IControllerStub).IsAssignableFrom(type))
                .Select(type => new ExecutableStubCall(assemblyName,
                    new ContainmentSite(ArchRuleHelpers.OutermostTypeName(type.FullName!), ImplementsStub), ImplementsStub));
            return calls.Concat(implementing).Distinct();
        }

        private const string ImplementsStub = "implements " + nameof(IControllerStub);

        /// <summary>
        /// Every site outside the stub namespace that calls into it, calls the predicate, or implements the stub
        /// interface.
        /// </summary>
        private static IEnumerable<ContainmentSite> StubSurfaceCallers(Assembly assembly)
        {
            var calling = AuthoredMembers.Of(assembly, SdkRoot)
                .Where(member => !InStubNamespace(member.DeclaringType))
                .Where(member => IlBody.CalledMethods(member).Any(called =>
                    InStubNamespace(called.DeclaringType) || IsThePredicate(called)))
                .Select(ContainmentSite.For);

            var implementing = AuthoredMembers.Types(assembly, SdkRoot)
                .Where(type => !InStubNamespace(type) && type.IsClass && typeof(IControllerStub).IsAssignableFrom(type))
                .Select(type => new ContainmentSite(ArchRuleHelpers.OutermostTypeName(type.FullName!), ImplementsStub));

            return calling.Concat(implementing).Distinct();
        }

        /// <summary>Every string outside the stub namespace that spells a stub endpoint, inlined constants included,
        /// in the authored code under <paramref name="root"/>.</summary>
        private static IEnumerable<(ContainmentSite Site, string Literal)> StubEndpointLiterals(Assembly assembly, string root) =>
            AuthoredMembers.Of(assembly, root)
                .Where(member => !InStubNamespace(member.DeclaringType))
                .SelectMany(member => IlBody.Instructions(member)
                    .Select(instruction => instruction.Literal)
                    .OfType<string>()
                    .Where(SpellsAStubEndpoint)
                    .Select(literal => (ContainmentSite.For(member), literal)));

        private static bool SpellsAStubEndpoint(string literal) =>
            literal.Contains(SpecialEndpoints.MockedPrefix, StringComparison.OrdinalIgnoreCase)
            || literal.Contains(".controllerstub.invalid", StringComparison.OrdinalIgnoreCase);

        private static bool IsThePredicate(MethodBase called) =>
            called.DeclaringType == typeof(IhcSettings) && called.Name == nameof(IhcSettings.IsStubConfigured);

        private static bool InStubNamespace(Type? type) =>
            type?.Namespace is { } ns
            && (ns == StubNamespace || ns.StartsWith(StubNamespace + ".", StringComparison.Ordinal));
    }
}
