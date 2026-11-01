using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The two safety nets a controller-free suite runs under are linked files, and a linked file is one a new
    /// suite simply does not have until someone adds it. This pin makes adding them part of starting a suite: every
    /// test project links the reach guard, which stops the run before any test executes when the suite can build a
    /// real service or transport, and every one but the stub suite links the run-wide latch fixture, which refuses a
    /// real endpoint at run time.
    /// </summary>
    /// <remarks>
    /// <para>Two projects are outside it by design. The integration suite is the one allowed to reach a controller.
    /// The architecture suite hosts the reach scan's seeded violator, which the guard would fail.</para>
    /// <para>The stub suite links the guard but not the latch fixture: its seam tests have to see the latch both
    /// released and held, which a latch held for the whole run would make impossible. For the same reason it may not
    /// stage the developer's settings file: with no latch behind it, a service a product factory built from that
    /// file would reach its controller, and the reach scan sees only what a suite constructs itself.</para>
    /// <para>Neither linked file may declare a namespace. Each holds a setup fixture, and NUnit applies a setup
    /// fixture only to its own namespace unless it is declared in none, running it only when a test below it runs: a
    /// namespaced latch would cover one suite's tests and pass the rest, and a namespaced guard would never run in a
    /// suite whose tests live elsewhere.</para>
    /// </remarks>
    [TestFixture]
    public partial class TestSafetyLinkPinArchitectureTests
    {
        private const string GuardFile = "ControllerReachGuard.cs";
        private const string LatchFixtureFile = "ControllerStubLatchFixture.cs";
        private const string StagesSettings = "CopyIhcSettingsToOutput";

        private static readonly HashSet<string> Unguarded = new(StringComparer.OrdinalIgnoreCase)
        {
            "safe_integration_tests",
            "safe_architecture_tests",
        };

        private const string UnlatchedSuite = "safe_stub_tests";

        [Test]
        public void EveryGuardedTestProject_LinksTheReachGuardAndTheLatchFixture()
        {
            string testsRoot = Path.Combine(TestRepository.RequireRoot(), "tests");
            var guarded = Directory.EnumerateFiles(testsRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Where(path => !Unguarded.Contains(Path.GetFileNameWithoutExtension(path)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.That(guarded.Select(Path.GetFileNameWithoutExtension), Does.Contain(UnlatchedSuite).And.Contain("safe_unit_tests"),
                "the scan must find the test projects it pins, or it pins nothing");

            var findings = guarded
                .SelectMany(path => PinFindings(Path.GetFileNameWithoutExtension(path), ProjectFileReader.Load(path),
                    include => File.ReadAllText(ProjectFileReader.Resolve(path, include))))
                .ToList();

            Assert.That(findings, Is.Empty,
                "a test project starts with both safety nets linked from tests/shared — findings: "
                + string.Join("; ", findings));
        }

        /// <summary>
        /// Positive control: no real project violates the pin by design, so only seeded documents can show it is
        /// able to fail. Each seed breaks it one way.
        /// </summary>
        [Test]
        public void ThePin_RefusesEachSeededBreach()
        {
            Assert.Multiple(() =>
            {
                Assert.That(PinFindings("seeded_suite", Project(LatchFixtureFile), Global),
                    Has.One.Contains(GuardFile), "a suite lacking the reach guard");
                Assert.That(PinFindings("seeded_suite", Project(GuardFile), Global),
                    Has.One.Contains(LatchFixtureFile), "a suite lacking the latch fixture");
                Assert.That(PinFindings("seeded_suite", Project(GuardFile, LatchFixtureFile), NamespacedOnly(LatchFixtureFile)),
                    Has.One.Contains(LatchFixtureFile + " that declares a namespace"), "a latch fixture that declares a namespace");
                Assert.That(PinFindings("seeded_suite", Project(GuardFile, LatchFixtureFile), NamespacedOnly(GuardFile)),
                    Has.One.Contains(GuardFile + " that declares a namespace"), "a reach guard that declares a namespace");
                Assert.That(PinFindings(UnlatchedSuite, StagingSettings(Project(GuardFile)), Global),
                    Has.One.Contains("ihcsettings.json"), "the unlatched suite staging the developer's settings");

                Assert.That(PinFindings("seeded_suite", Project(GuardFile, LatchFixtureFile), Global),
                    Is.Empty, "the compliant seed, which shows the breaches above fail for their own reason");
                Assert.That(PinFindings("seeded_suite", StagingSettings(Project(GuardFile, LatchFixtureFile)), Global),
                    Is.Empty, "a latched suite may stage the settings: the latch refuses every real endpoint they name");
                Assert.That(PinFindings(UnlatchedSuite, Project(GuardFile), Global),
                    Is.Empty, "the stub suite is held to the guard alone");
                Assert.That(PinFindings("safe_unit_tests", Project(GuardFile), Global),
                    Has.One.Contains(LatchFixtureFile), "the unit suite is not: it runs latched like the rest");
            });
        }

        /// <summary>Everything wrong with one test project's safety links, each as a sentence.</summary>
        internal static IReadOnlyList<string> PinFindings(string projectName, XDocument project, Func<string, string> readInclude)
        {
            var findings = new List<string>();
            if (ProjectFileReader.SharedInclude(project, GuardFile) is { } guardInclude)
            {
                if (DeclaresNamespace(readInclude(guardInclude)))
                {
                    findings.Add($"{projectName} links a {GuardFile} that declares a namespace, so its gate runs only for "
                        + "that namespace's tests, and in a suite whose tests live elsewhere not at all");
                }
            }
            else
            {
                findings.Add($"{projectName} does not link tests/shared/{GuardFile}");
            }

            bool latched = !string.Equals(projectName, UnlatchedSuite, StringComparison.OrdinalIgnoreCase);
            if (ProjectFileReader.SharedInclude(project, LatchFixtureFile) is { } fixtureInclude)
            {
                if (DeclaresNamespace(readInclude(fixtureInclude)))
                {
                    findings.Add($"{projectName} links a {LatchFixtureFile} that declares a namespace, so it latches "
                        + "only that namespace's tests");
                }
            }
            else
            {
                if (latched)
                {
                    findings.Add($"{projectName} does not link tests/shared/{LatchFixtureFile}");
                }
                if (ProjectFileReader.SetsTrue(project, StagesSettings))
                {
                    findings.Add($"{projectName} stages the developer's ihcsettings.json ({StagesSettings}) without "
                        + "holding the latch for its run, so a service a product factory builds from it reaches that "
                        + "file's controller");
                }
            }
            return findings;
        }

        private static bool DeclaresNamespace(string source) => NamespaceDeclaration().IsMatch(source);

        // A namespace declaration, block or file-scoped, at the start of a line; a comment that mentions the word
        // starts with "//" and does not match.
        [GeneratedRegex(@"^[ \t]*namespace[ \t]+[\w.]+", RegexOptions.Multiline)]
        private static partial Regex NamespaceDeclaration();

        private const string NamespacedFixture = "namespace Ihc.Tests\n{\n    [NUnit.Framework.SetUpFixture]\n    public sealed class SeededFixture { }\n}\n";
        private const string GlobalFixture = "// A comment that merely says namespace is not a declaration.\n[NUnit.Framework.SetUpFixture]\npublic sealed class SeededFixture { }\n";

        /// <summary>Every linked file declares no namespace.</summary>
        private static readonly Func<string, string> Global = _ => GlobalFixture;

        /// <summary>Only <paramref name="fileName"/> declares a namespace.</summary>
        private static Func<string, string> NamespacedOnly(string fileName) =>
            include => include.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) ? NamespacedFixture : GlobalFixture;

        private static XDocument Project(params string[] sharedFiles) =>
            new(new XElement("Project",
                new XElement("ItemGroup", sharedFiles.Select(file =>
                    new XElement("Compile", new XAttribute("Include", @"..\shared\" + file))))));

        /// <summary><paramref name="project"/>, staging the developer's settings file beside its output.</summary>
        private static XDocument StagingSettings(XDocument project)
        {
            project.Root!.AddFirst(new XElement("PropertyGroup", new XElement(StagesSettings, "true")));
            return project;
        }

        private static bool IsBuildOutput(string path)
        {
            string normalised = path.Replace('\\', '/');
            return normalised.Contains("/bin/", StringComparison.Ordinal) || normalised.Contains("/obj/", StringComparison.Ordinal);
        }
    }
}
