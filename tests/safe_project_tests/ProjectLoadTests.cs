using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Load-correctness for the reader: the parsed <see cref="Project"/> model exposes the canonical skeleton,
    /// metadata and built-in enums of a vendor file, and every decoded element id agrees with the schema
    /// registry's type-code map across the whole complex sample.
    /// </summary>
    public class ProjectLoadTests
    {
        private static Project Load(string file)
        {
            using var ms = new MemoryStream(TestData.ReadBytes(file));
            return new ProjectAppService(TestSetup.Settings).Load(ms).GetAwaiter().GetResult();
        }

        [Test]
        public void Load_ProjectEmpty_HasCanonicalSkeleton()
        {
            Project project = Load("ProjectEmpty.vis");

            Assert.Multiple(() =>
            {
                Assert.That(project.Version, Is.EqualTo("4.0"));
                Assert.That(project.Id1, Is.EqualTo("_0x1b100533"));
                Assert.That(project.Id2, Is.EqualTo("_0x1b100605"));
                Assert.That(project.LastUniqueId, Is.EqualTo("_0x50"));

                // The seven fixed root children, in document order.
                Assert.That(project.Children.Select(c => c.Tag), Is.EqualTo(new[]
                {
                    "modified", "customer_info", "installer_info", "project_info",
                    "enum_definitions", "groups", "documentation_modules",
                }));

                Assert.That(project.Programmer, Is.EqualTo("Morten Christensen"));
                Assert.That(project.InstallerName, Is.EqualTo("Morten"));
                Assert.That(project.InstallerCountry, Is.EqualTo("Danmark"));

                Assert.That(project.Groups, Has.Count.EqualTo(10), "the ten default rooms");
                Assert.That(project.Groups.First().GetAttribute("name"), Is.EqualTo("Stue"));
            });
        }

        [Test]
        public void Load_ProjectEmpty_MatchesBuiltInEnumsByTypeid()
        {
            Project project = Load("ProjectEmpty.vis");
            ProjectElement enums = project.Child("enum_definitions")!;

            IEnumerable<string?> typeids = enums.Children.Select(d => d.GetAttribute("typeid"));

            // The two built-in enums must be present and keyed by their stable typeids (never by id).
            Assert.That(typeids, Does.Contain("_0x10"), "Persienne tilstand");
            Assert.That(typeids, Does.Contain("_0x16"), "Logning");
        }

        [Test]
        public void Load_Project1_EveryElementIdMatchesRegistryTypeCode()
        {
            Project project = Load("Project1.vis");

            var offenders = new List<string>();
            foreach (ProjectElement e in Descendants(project.Root))
            {
                if (e.Id is not ElementId id)
                {
                    continue; // the five id-less elements (root, modified, the three *_info)
                }
                int? expected = TypeCode.ForTag(e.Tag);
                if (expected != id.TypeCode)
                {
                    offenders.Add($"{e.Tag}: id type-code 0x{id.TypeCode:x2} but registry says {(expected is null ? "none" : "0x" + expected.Value.ToString("x2"))}");
                }
            }

            Assert.That(offenders, Is.Empty,
                "every decoded element id's type-code byte must match the schema registry");
        }

        private static IEnumerable<ProjectElement> Descendants(ProjectElement root)
        {
            yield return root;
            if (root.Children.IsDefaultOrEmpty)
            {
                yield break;
            }
            foreach (ProjectElement child in root.Children)
            {
                foreach (ProjectElement d in Descendants(child))
                {
                    yield return d;
                }
            }
        }
    }
}
