using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Every refusal the SDK can put in front of an installer is Danish (FR-2.6 / D13).
    /// <para>The refusal channel is user-facing text that happens to live in the engine: <c>EditVerdict.Refuse</c>
    /// reasons and <c>EditRefusedException</c> messages are forwarded to the GUI and shown verbatim, rather than
    /// re-authored there. An English sentence in this channel is not an internal detail — it is English text on a
    /// Danish screen.</para>
    /// <para>Asserted over the SOURCE rather than by provoking each refusal one at a time: there are refusals only
    /// reachable through states this suite cannot easily build (a corrupt typedef, a section tag no builder emits),
    /// and those are exactly the ones a per-case test set would quietly omit. The per-condition behaviour tests
    /// below cover the reachable ones with their actual sentences.</para>
    /// </summary>
    public class RefusalLanguageTests
    {
        // The gate's own regex (backlog T015), plus the interpolated and exception-carried forms it does not name:
        // a refusal is refused if it opens with one of these English fragments.
        private static readonly Regex EnglishRefusal = new(
            @"(EditVerdict\.Refuse|EditRefusedException)\(\$?""(The |A |An |That |This |Not |No |is not |cannot |must |already |does not )",
            RegexOptions.Compiled);

        private static IEnumerable<string> SdkSources() =>
            Directory.EnumerateFiles(SdkRoot(), "*.cs", SearchOption.AllDirectories);

        // tests run from bin/, so walk up to the repo and back down into the SDK's vis layer.
        private static string SdkRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "ihcclient", "src", "vis")))
                dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "could not locate ihcclient/src/vis from the test directory");
            return Path.Combine(dir!.FullName, "ihcclient", "src", "vis");
        }

        [Test]
        public void NoRefusalInTheSdkOpensWithAnEnglishSentence()
        {
            var offenders = new List<string>();
            foreach (string file in SdkSources())
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (EnglishRefusal.IsMatch(lines[i]))
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {lines[i].Trim()}");
                }
            }
            Assert.That(offenders, Is.Empty,
                "these refusals are shown to a Danish installer verbatim:\n  " + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// The source scan above can only see literals. This proves the composed sentence — guard noun plus guard
        /// template — comes out as Danish prose and not as "Locality findes ikke længere".
        /// </summary>
        [Test]
        public async Task AStaleIdRefusal_ReadsAsOneDanishSentence()
        {
            Project project = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId.TryParse("_0xdead01", out ElementId absent);

            EditOutcome outcome = session.Apply(new RenameLocality(absent, "X", ""));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                // RenameLocality passes the generic noun (it renames any named element, not only a locality),
                // so the composed sentence is "Elementet findes ikke længere." — Danish prose either way.
                Assert.That(outcome.Reason, Is.EqualTo("Elementet findes ikke længere."));
            });
        }

        /// <summary>The tag guard's composed sentence, same reasoning.</summary>
        [Test]
        public async Task AWrongTagRefusal_ReadsAsOneDanishSentence()
        {
            Project project = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;   // a locality, not a function block
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(new UnlockFunctionBlock(locality, "me", new System.DateOnly(2026, 1, 1)));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Is.EqualTo("Målet er ikke en funktionsblok."));
            });
        }

        /// <summary>The out-of-range terminal refusal, which has its own sentence rather than a shared guard's.</summary>
        [Test]
        public async Task AnOutOfRangeTerminalRefusal_IsDanish()
        {
            Project project = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project3-KompleksWired.vis");
            ProjectElement pin = project.Root.Descendants()
                .First(e => e.Tag is "dataline_input" or "dataline_output" && e.Id is not null);
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(new UpdatePin(pin.Id!.Value,
                new PinPropertiesResult(DataLine: 99, Terminal: 99, "", "", InitialValueOn: false)));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Is.EqualTo("Klemmenummeret ligger uden for datalinjens område."));
            });
        }

        /// <summary>
        /// The locked-block refusal is shared by the engine throw and the session verdict (T003), so it is the one
        /// sentence most likely to be re-authored in the GUI. Pinning its exact text is what makes T016's deletion
        /// of the GUI copies safe.
        /// </summary>
        [Test]
        public async Task TheLockedBlockRefusal_IsDanish_AndIsTheSameSentenceEverywhere()
        {
            Project project = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project2-CustomBlock.vis");
            ProjectElement locked = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") == "yes");
            ProjectElement inside = locked.Descendants().First(e => e.Id is not null && e.Tag.StartsWith("resource_"));
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(new DeleteNode(inside.Id!.Value, Cascade: false));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("låst funktionsblok"));
                Assert.That(outcome.Reason, Does.Contain("lås blokken op"));
            });
        }
    }
}
