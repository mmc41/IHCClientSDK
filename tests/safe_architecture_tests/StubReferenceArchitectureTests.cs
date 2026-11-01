using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// No stub and no fake ships. A project outside <c>tests/</c> may reference neither the controller stub, nor a
    /// project inside <c>tests/</c>, nor the FakeItEasy package. An application reaches a stub only through the SDK's
    /// switch, which hands control to a stub process; it never carries one.
    /// </summary>
    /// <remarks>
    /// The second edge is what makes the first hold transitively: a path from an application to the stub through
    /// another project would have to pass through a test project, and that edge is refused. The rule reads project
    /// files rather than assemblies because a reference is a fact of the project file, and because an application
    /// the suite does not load is still in the solution.
    /// </remarks>
    [TestFixture]
    public class StubReferenceArchitectureTests
    {
        private const string StubProject = "ihcclient_stub.csproj";
        private const string FakePackage = "FakeItEasy";

        [Test]
        public void NoProjectOutsideTests_ReferencesTheStub_ATestProject_OrTheFakePackage()
        {
            string root = TestRepository.RequireRoot();
            var projects = WindowsProjectFileScan.Projects(Path.Combine(root, "IHCClientSDK.sln"));
            var shipped = projects.Where(project => !IsUnderTests(root, project.Path)).ToList();

            Assert.That(shipped.Select(project => project.Name), Does.Contain("ihc_lab").And.Contain("ihc_openvisual"),
                "the scan must find the applications it judges, or it judges nothing");

            var findings = shipped
                .SelectMany(project => Findings(root, project.Path, ProjectFileReader.Load(project.Path)))
                .ToList();

            Assert.That(findings, Is.Empty, "a shipped project carries no stub and no fake — findings: " + string.Join("; ", findings));
        }

        /// <summary>
        /// Positive control: no real project violates the rule by design, so only seeded documents can show it is
        /// able to fail. Each seed breaks it by one edge.
        /// </summary>
        [Test]
        public void TheRule_RefusesEachSeededEdge()
        {
            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "seeded-repo"));
            string app = Path.Combine(root, "applications", "seeded_app", "seeded_app.csproj");
            string suite = Path.Combine(root, "tests", "seeded_suite", "seeded_suite.csproj");

            Assert.Multiple(() =>
            {
                Assert.That(Findings(root, app, Project(projects: [@"..\..\ihcclient_stub\ihcclient_stub.csproj"])),
                    Has.One.Contains(StubProject), "an application referencing the stub");
                Assert.That(Findings(root, app, Project(projects: [@"..\..\tests\safe_unit_tests\safe_unit_tests.csproj"])),
                    Has.One.Contains("tests/"), "an application referencing a test project");
                Assert.That(Findings(root, app, Project(packages: [FakePackage])),
                    Has.One.Contains(FakePackage), "an application referencing the fake package");

                Assert.That(Findings(root, app, Project(projects: [@"..\..\ihcclient\ihcclient.csproj"], packages: ["Avalonia"])),
                    Is.Empty, "the compliant seed, which shows the three above fail for their own reason");
                Assert.That(Findings(root, suite, Project(projects: [@"..\..\ihcclient_stub\ihcclient_stub.csproj"], packages: [FakePackage])),
                    Is.Empty, "a test project is not judged: tests are where the stub and the fake belong");
            });
        }

        /// <summary>Every refused edge of the project at <paramref name="projectPath"/>, each as a sentence; none for
        /// a project inside <c>tests/</c>.</summary>
        internal static IReadOnlyList<string> Findings(string repositoryRoot, string projectPath, XDocument project)
        {
            if (IsUnderTests(repositoryRoot, projectPath))
            {
                return [];
            }
            string name = Path.GetFileNameWithoutExtension(projectPath);
            var findings = new List<string>();
            foreach (string include in ProjectFileReader.ProjectReferences(project))
            {
                string target = ProjectFileReader.Resolve(projectPath, include);
                if (string.Equals(Path.GetFileName(target), StubProject, StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add($"{name} references {StubProject}");
                }
                else if (IsUnderTests(repositoryRoot, target))
                {
                    findings.Add($"{name} references a project under tests/: {include}");
                }
            }
            findings.AddRange(ProjectFileReader.PackageReferences(project)
                .Where(package => string.Equals(package, FakePackage, StringComparison.OrdinalIgnoreCase))
                .Select(_ => $"{name} references the {FakePackage} package"));
            return findings;
        }

        private static bool IsUnderTests(string repositoryRoot, string path)
        {
            string relative = Path.GetRelativePath(repositoryRoot, Path.GetFullPath(path)).Replace('\\', '/');
            return relative.StartsWith("tests/", StringComparison.OrdinalIgnoreCase);
        }

        private static XDocument Project(string[]? projects = null, string[]? packages = null) =>
            new(new XElement("Project",
                new XElement("ItemGroup",
                    (projects ?? []).Select(include => new XElement("ProjectReference", new XAttribute("Include", include))),
                    (packages ?? []).Select(id => new XElement("PackageReference", new XAttribute("Include", id))))));
    }
}
