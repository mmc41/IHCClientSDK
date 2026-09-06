using System.Collections.Generic;
using System.IO;
using System.Linq;

using Ihc.Tests.Shared;

using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// Every authentic project oracle, opened one after another into ONE running application, with the Problemer
/// panel read between loads: no fault in the tool, and no finding that refuses an operation.
///
/// <para><b>What it is for.</b> A `.vis` file the vendor wrote is the closest thing this repository has to
/// "what a real installation looks like", and the corpus spans an empty project, wired and complex
/// installations, custom blocks, program tokens, documentation and a fixture authored to fail. Opening all of
/// them through the shipped application asks one question the cheaper levels do not: does the whole stack —
/// load, normalize, index, project into two trees, validate, and publish through the accessibility surface —
/// survive every shape the vendor actually produces?</para>
///
/// <para><b>Why ONE session rather than a launch per file.</b> The second load is the point. A relaunch per
/// fixture would test twenty first loads and never the thing a person actually does, which is open one project
/// after another in a session that is already running — where a stale index, a retained element id or
/// generation bookkeeping that did not follow the document would show up and nowhere else. That is also why the
/// launch opens the LAST file of the scan: every case is then a genuine document CHANGE, including the first,
/// where launching on a corpus member would make one case a re-open that passes without opening anything.</para>
///
/// <para><b>What it does NOT assert.</b> How many warnings, errors or information rows a file produces. Those
/// are the oracles' business — <c>tests/testdata/validation/</c> pins them per corpus case, byte for byte, one
/// level down — and a second inventory here would be a copy with nothing comparing it. This fixture asserts
/// only what must be true of EVERY authentic file whatever it contains: the tool did not fault, and nothing in
/// it refuses a save.</para>
/// </summary>
/// <remarks>
/// <para><b>The corpus is SCANNED, never listed.</b> A hardcoded list is a second inventory of the directory,
/// and the failure it produces is silence: an oracle added to the corpus is simply never opened, and the sweep
/// stays green while covering less than it says. Scanning also means the floor test below is the only thing
/// standing between "the directory moved" and twenty vacuously green cases.</para>
///
/// <para><b>Top directory only.</b> <c>projects/Synthetic/</c> holds hand-crafted files that are authored to
/// carry defects — one of them exists precisely to produce a refusing finding — so sweeping it would turn this
/// fixture red for the right reason at the wrong level. <c>projects/LiveAuthored/</c> is authentic but is a
/// different corpus, captured for enum-deduplication experiments.</para>
/// </remarks>
public class ProjectCorpusLoadE2ETests : E2EScenario
{
    /// <summary>Where the authentic oracles live, as the application must be given them: absolute paths.</summary>
    private static string CorpusDirectory() =>
        Path.Combine(TestRepository.RequireRoot(), "tests", "testdata", "projects");

    /// <summary>
    /// The corpus, in a fixed order.
    /// </summary>
    /// <remarks>
    /// Ordinal rather than the machine's culture, so the load ORDER is the same everywhere. It matters more
    /// here than in an ordinary parameterised fixture: what one case leaves behind is what the next one opens
    /// onto, so a run that ordered the files differently would be exercising a different sequence — and a
    /// cross-load failure found on one machine would not reproduce on another.
    /// </remarks>
    private static IReadOnlyList<string> Corpus() =>
    [
        .. Directory.EnumerateFiles(CorpusDirectory(), "*.vis", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, System.StringComparer.Ordinal),
    ];

    private static IEnumerable<TestCaseData> AuthenticProjects() =>
        Corpus().Select(path => new TestCaseData(path).SetName($"Loads {Path.GetFileName(path)}"));

    /// <summary>
    /// The fewest files this sweep is worth running at all.
    /// </summary>
    /// <remarks>
    /// A FLOOR rather than an exact count, so adding an oracle does not fail a suite that has nothing to say
    /// about it — and deliberately well under the corpus as it stands, because the number here is not an
    /// inventory. What it defends against is the scan finding nothing: a moved or renamed directory turns
    /// <see cref="AuthenticProjects"/> into an empty source, NUnit reports zero cases, and a fixture that opened
    /// no project at all is indistinguishable from one where everything passed.
    /// </remarks>
    private const int FewestOracles = 15;

    /// <summary>
    /// The scan is armed: it finds the corpus, and the corpus is big enough to be the corpus.
    /// </summary>
    /// <remarks>
    /// Not <c>DesktopOnly</c> and needs no application — it reads a directory. That is the point: the one thing
    /// that can silently empty this fixture is checked on every push, in the leg CI gates.
    /// </remarks>
    [Test]
    public void TheCorpusScanFindsTheAuthenticOracles()
    {
        IReadOnlyList<string> corpus = Corpus();

        Assert.That(corpus, Has.Count.AtLeast(FewestOracles),
            $"the scan of {CorpusDirectory()} found {corpus.Count} project oracle(s), which is too few to be "
            + "the authentic corpus — every load case below is silently vacuous when this is wrong");
    }

    /// <summary>
    /// The scan stays OUT of the subdirectories, whatever they come to hold.
    /// </summary>
    /// <remarks>
    /// The depth is one enum value in <see cref="Corpus"/>, and widening it is a one-word edit that nothing
    /// else would notice: the count would go up, the floor above would still pass, and the sweep would quietly
    /// start asserting over files that are not authentic. <c>projects/Synthetic/</c> is hand-crafted and partly
    /// authored to FAIL — one of its files carries a refusing finding and another cannot be opened at all — so
    /// what that edit produces is a red suite blaming the application for fixtures built to break it.
    /// <c>projects/LiveAuthored/</c> is authentic but is a different corpus, captured for enum-deduplication
    /// experiments.
    /// <para>Checked by where each hit LIVES rather than by counting, so it keeps holding as either directory
    /// grows.</para>
    /// </remarks>
    [Test]
    public void TheCorpusScanReachesNoSubdirectory()
    {
        string corpusDirectory = CorpusDirectory();

        var nested = Corpus()
            .Where(path => !string.Equals(
                Path.GetDirectoryName(path), corpusDirectory, System.StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(corpusDirectory, path))
            .ToList();

        Assert.That(nested, Is.Empty,
            "the sweep opens only the authentic oracles directly under "
            + $"{corpusDirectory}; these came from a subdirectory, which holds fixtures that are hand-crafted, "
            + "authored to fail, or simply a different corpus: " + string.Join(", ", nested));
    }

    /// <summary>
    /// Opens the LAST file of the scan, so the first swept case is a real change. See the type's remarks.
    /// </summary>
    [OneTimeSetUp]
    public void LaunchOnAProjectOutsideTheSweptOrder() => E2E.Launch(Corpus()[^1]);

    /// <summary>
    /// <c>OneTime</c>, not per test: NUnit runs a derived <c>[TearDown]</c> BEFORE the base one, so killing the
    /// application per case would take it down before <see cref="E2EScenario"/> could ask it what faulted.
    /// </summary>
    [OneTimeTearDown]
    public void CloseApp() => E2E.KillApp();

    /// <summary>
    /// Hands the next case an application with nothing modal standing in front of it.
    /// </summary>
    /// <remarks>
    /// The cases share one session, so what a broken fixture leaves behind is what the next one opens onto: the
    /// error dialog an unreadable project raises blocks the open after it, and one bad oracle would otherwise
    /// fail every case that follows it for a reason that is about the previous file. The case that raised the
    /// dialog still fails on its own assertion — this only stops that failure spreading. Per test rather than
    /// inside the body so it runs after a failed assertion too, and BEFORE <see cref="E2EScenario"/>'s fault
    /// comparison, which NUnit runs last.
    /// </remarks>
    [TearDown]
    public void DismissAnythingLeftStanding() => E2E.CloseAllModals();

    /// <summary>
    /// One oracle, opened into the running session and read back.
    /// </summary>
    /// <remarks>
    /// <para><b>The two preconditions are not extra assertions, they are what makes the other two mean
    /// anything.</b> A file the application cannot read is reported through a modal, and the PREVIOUS document
    /// stays bound — so a sweep that only read the panel would find the last file's clean result, pass, and
    /// report that a broken oracle loads fine. Asserting the document actually changed is what turns that into
    /// a failure; asserting no modal is left standing is what stops one bad file cascading into every case
    /// after it.</para>
    ///
    /// <para>The third assertion every case makes is not written here: <see cref="E2EScenario"/> compares the
    /// application's cumulative fault record across this test's whole window. It is the load-bearing one, and
    /// deliberately not the panel — the Intern fejl list is capacity-bounded, collapses repeats, and is CLEARED
    /// by the very document change this case performs, so a fault raised while opening can be wiped from it by
    /// its own open. The tally has none of those properties. The panel count below is asserted beside it
    /// because a fault the user cannot SEE is a second defect.</para>
    /// </remarks>
    [TestCaseSource(nameof(AuthenticProjects))]
    public void OpeningAnAuthenticOracleRaisesNoInternalOrFatalProblem(string path)
    {
        string file = Path.GetFileName(path);

        E2E.RunOk("project", "open", "--path", path);
        E2E.Envelope state = E2E.WaitForBoundProblems();
        E2E.Envelope status = E2E.RunOk("session", "status");

        Assert.Multiple(() =>
        {
            Assert.That(E2E.OpenModalIds(), Is.Empty,
                $"opening '{file}' left a dialog standing — the application reports a file it cannot read that "
                + "way, so read this as the open having FAILED rather than as a stray window");

            Assert.That(E2E.DocumentOf(status), Is.EqualTo(file),
                $"the application is not showing '{file}' after being asked to open it, so everything below is "
                + "about whichever document it is still showing");

            Assert.That(E2E.TierCount(state, "internals"), Is.Zero,
                $"the Problemer panel shows an internal fault after opening '{file}' — the TOOL failed, not the "
                + "project, and rows may be missing from a list the user will read as complete");

            Assert.That(E2E.TierCount(state, "fatals"), Is.Zero,
                $"'{file}' produced a finding that REFUSES an operation — an authentic vendor project cannot be "
                + "one the application would decline to save; triage the rule before treating this as a test "
                + "defect");
        });
    }
}
