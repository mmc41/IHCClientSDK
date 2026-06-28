using System.Collections.Immutable;
using System.IO;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Locks the attribute escaping contract (spec ch. 01 §6, rule S10): all five XML specials and an embedded
    /// CRLF survive a serialize→reload round-trip, and are written as entities on disk. The byte-fidelity tests
    /// already cover <c>&amp;gt;</c>/<c>&amp;quot;</c>/<c>&amp;apos;</c>/<c>&amp;#xD;&amp;#xA;</c> and Latin-1 bytes;
    /// this adds the two entities absent from the testdata (<c>&amp;amp;</c>, <c>&amp;lt;</c>) and proves the
    /// writer/reader are exact inverses for arbitrary content.
    /// </summary>
    public class ProjectEscapingTests
    {
        [Test]
        public void Serialize_EscapesAllFiveSpecialsAndCrlf_RoundTrips()
        {
            // All five specials + an embedded CRLF + a Latin-1 letter in one value.
            const string tricky = "a & b < c > d \" e ' f\r\ng é";
            Project edited = WithProgrammer(LoadEmpty(), tricky);

            byte[] bytes = ProjectSerializer.Serialize(edited);
            string onDisk = ProjectFile.Encoding.GetString(bytes);
            Project reread;
            using (var ms = new MemoryStream(bytes))
            {
                reread = new ProjectAppService(TestSetup.Settings).Load(ms).GetAwaiter().GetResult();
            }

            Assert.Multiple(() =>
            {
                Assert.That(reread.Programmer, Is.EqualTo(tricky), "logical value survives escape→unescape");
                Assert.That(onDisk, Does.Contain("&amp;").And.Contain("&lt;").And.Contain("&gt;")
                    .And.Contain("&quot;").And.Contain("&apos;").And.Contain("&#xD;&#xA;"),
                    "all five specials and the CRLF pair are written as entities");
            });
        }

        private static Project LoadEmpty()
        {
            using var ms = new MemoryStream(TestData.ReadBytes("ProjectEmpty.vis"));
            return new ProjectAppService(TestSetup.Settings).Load(ms).GetAwaiter().GetResult();
        }

        /// <summary>Returns a copy of the project with <c>project_info/@programmer</c> set to the given value.</summary>
        private static Project WithProgrammer(Project project, string value)
        {
            ProjectElement root = project.Root;
            ImmutableArray<ProjectElement> children = root.Children;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].Tag == "project_info")
                {
                    ProjectElement info = children[i] with
                    {
                        Attrs = ImmutableArray.Create<(string, string)>(("programmer", value)),
                    };
                    return new Project(root with { Children = children.SetItem(i, info) });
                }
            }
            return project;
        }
    }
}
