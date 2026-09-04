using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Ihc.Tests.Shared;
using NUnit.Framework;
using static Ihc.Tests.WindowsReachScan;

namespace Ihc.Tests
{
    /// <summary>
    /// <b>Windows is reached only from a project named for it.</b> A project whose name contains <c>windows</c> —
    /// today <c>shared/ihc_uiautomation_windows</c> — may reference a Windows package and call a Windows-only
    /// API; every other project may do neither, and gets Windows behaviour by referencing such a project.
    ///
    /// <para><b>Why the name carries the rule.</b> The toolkit is plain <c>net10.0</c> on purpose: it compiles
    /// everywhere and runs only on Windows, and a <c>-windows</c> target framework would have forced every
    /// consumer to multi-target. So the platform fact is in the project's NAME, and nothing else in the build
    /// distinguishes that project from a neutral one. This rule is what makes the name load-bearing: it reads the
    /// same licence off the name, and holds everything not so named to the opposite.</para>
    ///
    /// <para><b>Why the platform analyzer is not enough.</b> CA1416 stops an UNGUARDED call to a Windows-only API
    /// in a neutral project. It is satisfied by a platform guard around the call and by a member that declares
    /// itself Windows-only, and it never sees a <c>DllImport</c> at all — three ways a neutral project quietly
    /// grows a Windows dependency that compiles on every platform and fails on two. The IL scan behind this rule
    /// judges the edge, not the guard.</para>
    ///
    /// <para><b>Two halves, one rule.</b> <see cref="WindowsProjectFileScan"/> reads every project file the
    /// solution lists, because a package reference — and the generator that matters most, CsWin32 — leaves no
    /// trace in an assembly. <see cref="WindowsReachScan"/> reads the IL of every neutral assembly this suite
    /// loads: the SDK, the GUI, both bootstrap halves and the download/upload utility. The remaining utilities,
    /// the examples and the sibling suites are held by the project-file half alone; extending the IL half to one
    /// of them is a project reference and a row in <see cref="NeutralAssemblies"/>.</para>
    /// </summary>
    [TestFixture]
    public class WindowsQuarantineArchitectureTests
    {
        private static readonly Lazy<IReadOnlyList<WindowsProjectFileScan.SolutionProject>> Projects = new(() =>
            WindowsProjectFileScan.Projects(Path.Combine(TestRepository.RequireRoot(), "IHCClientSDK.sln")));

        /// <summary>The projects licensed to reach Windows, by assembly name — the doors every other assembly's
        /// calls may go through.</summary>
        private static readonly Lazy<IReadOnlySet<string>> Doors = new(() =>
            Projects.Value.Where(project => project.IsWindowsProject)
                .Select(project => project.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        /// <summary>The one project named for Windows today, referenced so the scans have a REAL positive
        /// control: the same detectors run over it must report what they are built to find.</summary>
        private static Assembly TheWindowsProject => typeof(global::Ihc.UiAutomation.UiaWait).Assembly;

        /// <summary>The neutral assemblies whose IL this suite can read, each with the namespace root its
        /// authored code lives under.</summary>
        private static readonly IReadOnlyList<(Assembly Assembly, string AuthoredRoot)> NeutralAssemblies =
        [
            (typeof(global::Ihc.IhcSettings).Assembly, "Ihc"),
            (typeof(global::ihc_openvisual.App).Assembly, typeof(global::ihc_openvisual.App).Namespace!),
            (typeof(global::Ihc.Bootstrap.TelemetryBootstrap).Assembly, "Ihc"),
            (typeof(global::Ihc.Bootstrap.AppTelemetryBootstrap).Assembly, "Ihc"),
            (typeof(global::Ihc.download_upload_example.ProblemConsoleFormat).Assembly, "Ihc"),
        ];

        private static Scope ScopeOf(Assembly assembly, string authoredRoot) => new(assembly, authoredRoot, Doors.Value);

        // ── The rule ────────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void OnlyAProjectNamedForWindows_ReferencesAWindowsPackage()
        {
            var offences = Projects.Value
                .Where(project => !project.IsWindowsProject)
                .SelectMany(project => WindowsProjectFileScan.WindowsDependencies(project.Path)
                    .Select(dependency => $"{project.Name}: {dependency}"))
                .ToList();

            Assert.That(offences, Is.Empty,
                "a project not named for Windows binds itself to Windows in its project file. Move what needs "
                + "the package into a project whose name contains 'windows', and reference that project instead");
        }

        [Test]
        public void OnlyAProjectNamedForWindows_ReachesAWindowsOnlyApi()
        {
            var offences = NeutralAssemblies
                .Where(scanned => !WindowsProjectFileScan.IsWindowsProject(scanned.Assembly.GetName().Name!))
                .SelectMany(scanned => Sites(ScopeOf(scanned.Assembly, scanned.AuthoredRoot))
                    .Select(reach => $"{scanned.Assembly.GetName().Name}: {reach}"))
                .ToList();

            Assert.That(offences, Is.Empty,
                "a neutral assembly reaches Windows on its own — through a call, a held field, a P/Invoke or a "
                + "declaration — and a platform guard around it does not change that. Windows is reached through "
                + "a project named for it: move the reach there and call that project");
        }

        // ── The anchors the rule stands on ──────────────────────────────────────────────────────────────────────

        /// <summary>The solution parse yields the projects the rule is about. A parse that quietly matched
        /// nothing would pass the project-file rule over an empty population.</summary>
        [Test]
        public void TheSolution_ListsTheProjectsTheRuleIsAbout()
        {
            IEnumerable<string> names = Projects.Value.Select(project => project.Name);
            Assert.Multiple(() =>
            {
                Assert.That(names, Does.Contain(typeof(global::Ihc.IhcSettings).Assembly.GetName().Name),
                    "the SDK is missing from the parsed solution — the parse is broken, not the solution");
                Assert.That(names, Does.Contain(typeof(WindowsQuarantineArchitectureTests).Assembly.GetName().Name),
                    "this suite is missing from the parsed solution — the parse is broken, not the solution");
                Assert.That(Projects.Value.Select(project => project.Path), Has.All.Matches<string>(File.Exists),
                    "a project path the solution lists does not exist under the checkout");
                Assert.That(Doors.Value, Is.Not.Empty,
                    "no project is named for Windows, so the licence the rule grants goes to nobody and the "
                    + "toolkit this suite references would be an offender under its own rule");
                Assert.That(Doors.Value, Does.Contain(TheWindowsProject.GetName().Name),
                    "the toolkit's assembly name no longer matches a project named for Windows, so calls into it "
                    + "would be reported as reach rather than as going through a door");
            });
        }

        /// <summary>Every neutral assembly the IL half scans is one the licence does not cover, and the scan sees
        /// its authored code: a root that matched nothing would leave that assembly's rule green over nothing.</summary>
        [Test]
        public void EveryScannedAssembly_IsNeutralAndPopulated() =>
            Assert.Multiple(() =>
            {
                foreach ((Assembly assembly, string root) in NeutralAssemblies)
                {
                    string name = assembly.GetName().Name!;
                    Assert.That(WindowsProjectFileScan.IsWindowsProject(name), Is.False,
                        $"{name} is named for Windows and belongs on the door side of the rule, not the neutral side");
                    Assert.That(AuthoredMembers.Of(assembly, root).Any(), Is.True,
                        $"'{root}' matched no authored member of {name}; the rule would pass vacuously for it");
                }
            });

        /// <summary>
        /// The assembly-level mark is read from the reference pack, and this pins why: the LOADED shared framework
        /// on Windows stamps its portable assemblies Windows too, so a verdict read from it would call every
        /// console write a reach. The registry library is the genuine article and must still read as Windows.
        /// </summary>
        [Test]
        public void TheAssemblyLevelVerdict_ComesFromTheReferencePack() =>
            Assert.Multiple(() =>
            {
                Assert.That(ReferencePack.IsSharedFramework(typeof(Console).Assembly), Is.True,
                    "System.Console loads from the shared framework; if it no longer does, the detection is wrong");
                Assert.That(ReferencePack.IsSharedFramework(TheWindowsProject), Is.False,
                    "a first-party assembly is judged by the mark authored in this repository");
                Assert.That(AssemblyVerdict(typeof(Console).Assembly), Is.Null,
                    "the reference assembly of System.Console declares no platform, whatever its implementation says");
                Assert.That(AssemblyVerdict(typeof(System.Net.Http.HttpClient).Assembly), Is.Null,
                    "nor does System.Net.Http's");
                Assert.That(AssemblyVerdict(typeof(Microsoft.Win32.RegistryKey).Assembly), Is.True,
                    "the registry library is Windows-only by its reference assembly's own declaration");
                Assert.That(IsWindowsOnly(typeof(System.Threading.Thread).GetMethod(nameof(System.Threading.Thread.SetApartmentState))!), Is.True,
                    "a member-level mark is authored, present in the loaded assembly, and read from there");
            });

        // ── Positive controls: the project this suite references BECAUSE it reaches Windows ─────────────────────

        /// <summary>The project-file detector fires on the real thing: the toolkit's own project file names the
        /// generator that leaves no runtime trace, and the imaging package that does.</summary>
        [Test]
        public void TheProjectFileDetector_FlagsTheWindowsProjectItself()
        {
            WindowsProjectFileScan.SolutionProject toolkit = Projects.Value.Single(project =>
                string.Equals(project.Name, TheWindowsProject.GetName().Name, StringComparison.OrdinalIgnoreCase));

            Assert.That(WindowsProjectFileScan.WindowsDependencies(toolkit.Path),
                Is.SupersetOf(new[] { "PackageReference Microsoft.Windows.CsWin32", "PackageReference System.Drawing.Common" }),
                "the toolkit references both by design; a detector that cannot see them there sees nothing anywhere");
        }

        /// <summary>The IL detector fires on the real thing: the toolkit declares itself Windows-only at the
        /// assembly, and its screen capture draws through the Windows-only imaging package.</summary>
        [Test]
        public void TheReachDetector_FlagsTheWindowsProjectItself()
        {
            IReadOnlyList<WindowsReach> reach = Sites(ScopeOf(TheWindowsProject, "Ihc"));

            Assert.Multiple(() =>
            {
                Assert.That(reach, Does.Contain(
                    new WindowsReach(ReachKind.Declares, TheWindowsProject.GetName().Name!, "[assembly: SupportedOSPlatform]")),
                    "the assembly-level declaration is the toolkit's whole platform contract");
                Assert.That(reach, Has.Some.Matches<WindowsReach>(r =>
                        r.Kind == ReachKind.Calls && r.Target.StartsWith("System.Drawing.", StringComparison.Ordinal)),
                    "a package marked Windows-only at the ASSEMBLY level and nowhere else must still be seen at the call");
            });
        }

        // ── Seeded controls: each shape the scan must tell apart ─────────────────────────────────────────────────

        private static IReadOnlyList<WindowsReach> SeededReach() =>
            Sites(ScopeOf(typeof(WindowsQuarantineArchitectureTests).Assembly, typeof(WindowsSeeded.GuardedCaller).Namespace!));

        private static IEnumerable<string> SeededSites(ReachKind kind) =>
            SeededReach().Where(reach => reach.Kind == kind).Select(reach => reach.Site);

        private static string SiteOf(Type seeded, string member) => $"{seeded.FullName}.{member}";

        /// <summary>The shapes the rule exists to catch, each proven to be reported.</summary>
        [Test]
        public void TheReachDetector_IsArmed() =>
            Assert.Multiple(() =>
            {
                Assert.That(SeededSites(ReachKind.Calls),
                    Does.Contain(SiteOf(typeof(WindowsSeeded.GuardedCaller), nameof(WindowsSeeded.GuardedCaller.Call))),
                    "a platform guard satisfies the compiler and changes nothing about the edge");
                Assert.That(SeededSites(ReachKind.Calls),
                    Does.Contain(SiteOf(typeof(WindowsSeeded.AnnotatedCaller), nameof(WindowsSeeded.AnnotatedCaller.Call))),
                    "a member that declares itself Windows-only still reaches Windows when it calls");
                Assert.That(SeededSites(ReachKind.Declares),
                    Does.Contain(SiteOf(typeof(WindowsSeeded.AnnotatedCaller), nameof(WindowsSeeded.AnnotatedCaller.Call))),
                    "the declaration is reported in its own right, being the cheapest way to make such calls compile");
                Assert.That(SeededSites(ReachKind.PInvokes),
                    Does.Contain(SiteOf(typeof(WindowsSeeded.Importer), "GetSystemMetrics")),
                    "a DllImport into a Windows system library carries no platform attribute and must be caught by name");
                Assert.That(SeededSites(ReachKind.Holds),
                    Does.Contain(SiteOf(typeof(WindowsSeeded.Holder), "key")),
                    "a field of a type that is Windows-only at the ASSEMBLY level makes its holder Windows-bound");
            });

        /// <summary>The shapes that look like reach and are not: each pinned, because flagging one would make the
        /// rule forbid the very code a portable project is written with.</summary>
        [Test]
        public void TheReachDetector_LeavesPortableShapesAlone()
        {
            IReadOnlyList<WindowsReach> reach = SeededReach();

            Assert.Multiple(() =>
            {
                Assert.That(reach.Select(r => r.Site), Does.Not.Contain(SiteOf(typeof(WindowsSeeded.PrimitivesUser), nameof(WindowsSeeded.PrimitivesUser.Call))),
                    "System.Drawing.Color and Rectangle live in the cross-platform primitives and carry no platform mark");
                Assert.That(reach.Select(r => r.Site), Does.Not.Contain(SiteOf(typeof(WindowsSeeded.PlatformQuerier), nameof(WindowsSeeded.PlatformQuerier.Call))),
                    "asking which platform this is runs everywhere; it is how portable code adapts");
                Assert.That(reach.Select(r => r.Site), Does.Not.Contain(SiteOf(typeof(WindowsSeeded.ElsewhereCaller), nameof(WindowsSeeded.ElsewhereCaller.Call))),
                    "an API unsupported ON Windows is the opposite of a Windows-only one");
                Assert.That(reach.Select(r => r.Site), Does.Not.Contain(SiteOf(typeof(WindowsSeeded.DoorUser), nameof(WindowsSeeded.DoorUser.Call))),
                    "calling into the project named for Windows is how a neutral project is supposed to reach it");
                Assert.That(reach.Select(r => r.Site), Does.Not.Contain(SiteOf(typeof(WindowsSeeded.PrimitivesUser), "bounds")),
                    "a field of a cross-platform primitive is not Windows state");
            });
        }

        /// <summary>The project-file detector reads every shape a project file can bind itself with, on a file
        /// that exists only here so no checked-in project has to carry the shapes to prove them.</summary>
        [Test]
        public void TheProjectFileDetector_ReadsEveryBindingShape()
        {
            XDocument project = XDocument.Parse(
                "<Project Sdk=\"Microsoft.NET.Sdk\">"
                + "<PropertyGroup><TargetFrameworks>net10.0;net10.0-windows10.0.19041.0</TargetFrameworks>"
                + "<UseWPF>true</UseWPF><UseWindowsForms>false</UseWindowsForms>"
                + "<RuntimeIdentifier>win-x64</RuntimeIdentifier></PropertyGroup>"
                + "<ItemGroup><PackageReference Include=\"Microsoft.Win32.Registry\" />"
                + "<PackageReference Include=\"System.Drawing.Common\" />"
                + "<PackageReference Include=\"NUnit\" />"
                + "<PackageReference Include=\"Avalonia.Desktop\" /></ItemGroup></Project>");

            Assert.That(WindowsProjectFileScan.WindowsDependencies(project), Is.EqualTo(new[]
            {
                "PackageReference Microsoft.Win32.Registry",
                "PackageReference System.Drawing.Common",
                "RuntimeIdentifier win-x64",
                "TargetFrameworks net10.0-windows10.0.19041.0",
                "UseWPF true",
            }).AsCollection, "every binding shape is named once, and a neutral package or a false flag is not");
        }

        /// <summary>The library judgement behind the P/Invoke arm: the Windows naming convention and the system
        /// libraries by name, and nothing for a native library a portable project might legitimately carry.</summary>
        [Test]
        public void TheLibraryJudgement_NamesWindowsAndNothingElse() =>
            Assert.Multiple(() =>
            {
                Assert.That(IsWindowsLibrary("user32.dll"), Is.True);
                Assert.That(IsWindowsLibrary("User32"), Is.True);
                Assert.That(IsWindowsLibrary("api-ms-win-core-file-l1-1-0"), Is.True);
                Assert.That(IsWindowsLibrary("libc"), Is.False, "a P/Invoke into libc is not a Windows reach, whatever else it is");
                Assert.That(IsWindowsLibrary("libSkiaSharp"), Is.False, "a cross-platform native library carries no extension");
            });
    }
}

namespace Ihc.Tests.WindowsSeeded
{
    using System.Drawing;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading;
    using Ihc.UiAutomation;
    using Microsoft.Win32;

    // Controls for the Windows quarantine detector (WindowsQuarantineArchitectureTests). They live in the TEST
    // assembly, so they can never reach the production scan — which is anchored to the neutral product
    // assemblies — while being run through the exact same scan with the same doors. Nothing here is ever CALLED:
    // the scan reads the IL, and the guards exist so the platform analyzer lets the shapes compile at all.

    /// <summary>Positive control: a Windows-only call behind the guard the platform analyzer accepts. The guard
    /// is the shape a neutral project would use to smuggle the call in, and the edge is there regardless.</summary>
    internal static class GuardedCaller
    {
        internal static void Call()
        {
            if (OperatingSystem.IsWindows())
            {
                Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
            }
        }
    }

    /// <summary>Positive control, twice over: the member declares itself Windows-only, which the analyzer accepts
    /// in place of a guard, and then calls an API marked on the MEMBER rather than the assembly.</summary>
    internal static class AnnotatedCaller
    {
        [SupportedOSPlatform("windows")]
        internal static void Call() => Console.Beep(440, 100);
    }

    /// <summary>Positive control: a P/Invoke into a Windows system library. No platform attribute anywhere, so
    /// nothing but the library name says where this runs.</summary>
    internal static class Importer
    {
        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int GetSystemMetrics(int index);

        internal static int Call() => GetSystemMetrics(0);
    }

    /// <summary>Positive control: a field of a type whose assembly, and nothing below it, carries the mark.</summary>
    internal sealed class Holder
    {
        private readonly RegistryKey? key;

        internal Holder(RegistryKey? key) => this.key = key;

        internal bool Call() => key is not null;
    }

    /// <summary>Negative control: the cross-platform drawing primitives, which share a namespace prefix with the
    /// Windows-only imaging package and carry none of its mark.</summary>
    internal sealed class PrimitivesUser
    {
        private readonly Rectangle bounds = new(0, 0, 1, 1);

        internal int Call() => Color.FromArgb(bounds.Width, bounds.Height, 0).R;
    }

    /// <summary>Negative control: asking which platform this is.</summary>
    internal static class PlatformQuerier
    {
        internal static bool Call() => OperatingSystem.IsWindows();
    }

    /// <summary>Negative control: an API the platform attributes mark as unsupported ON Windows. Carrying only
    /// <c>Unsupported</c> marks, it is Windows-only's opposite, and the guard is the analyzer's price for it.</summary>
    internal static class ElsewhereCaller
    {
        internal static void Call(string path)
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead);
            }
        }
    }

    /// <summary>Negative control: a call into the project named for Windows — the door. The toolkit's assembly
    /// marks every member Windows-only, so without the door exclusion this would be reach.</summary>
    internal static class DoorUser
    {
        internal static bool Call()
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                return false;
            }
            return UiaWait.Until(() => true, TimeSpan.Zero).Satisfied;
        }
    }
}
