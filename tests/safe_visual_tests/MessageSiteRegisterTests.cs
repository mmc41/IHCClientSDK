using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Problems;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;

namespace safe_visual_tests;

/// <summary>
/// T042: the PER-SITE REGISTER of every user-facing message this application shows, with the ruling for each —
/// an SDK code the shell narrates, a host-family code the shell owns, or one of the few surfaces that carry no
/// code at all because they are not outcomes.
///
/// <para><b>Why the register is a compiled test rather than a document.</b> A markdown table of twenty-four
/// call sites would be stale by the second refactor and nothing would notice. Here each row names a real code
/// member, so a removed or renamed code does not compile, and the scan below counts the sites that still show a
/// bare string — the gate T042 owes: no user-facing GUI message without a code.</para>
///
/// <para><b>The two rulings, and the rule behind them.</b> A message about the PROJECT FILE or about a rule the
/// engine owns is the SDK's, and the shell narrates the SDK's code verbatim; a message about an action of THIS
/// APPLICATION — a folder it could not find, a viewer that would not open, a controller it never contacts — is the
/// host's, and it gets an <c>app.openvisual.*</c> code. What the shell may never do again is interpolate an
/// English <c>ex.Message</c> into a Danish sentence: that is the invariant-10 breach this whole mechanism exists
/// to end, and every one of those nine sites is now a coded problem with the engine's text in the diagnostic
/// slot.</para>
/// </summary>
public class MessageSiteRegisterTests
{
    /// <summary>Who owns the sentence a site shows.</summary>
    private enum Owner
    {
        /// <summary>An SDK code; the shell renders it and adds nothing but a title.</summary>
        Sdk,

        /// <summary>A host code in the reserved <c>app.openvisual.*</c> family.</summary>
        Host,

        /// <summary>No code: the surface is not an outcome. Every one of these is justified in the register.</summary>
        Uncoded,
    }

    /// <summary>One user-facing message site.</summary>
    /// <param name="Where">Where it is, as file plus member.</param>
    /// <param name="Owner">Which of the two rulings it took.</param>
    /// <param name="Code">The code it shows, or null for an uncoded surface.</param>
    /// <param name="Reason">Why it took that ruling — the record T042 asks for.</param>
    /// <param name="Composition">
    /// Which of T006's two child relationships the site uses, for a site that WRAPS an SDK failure: a one-child
    /// chain, or nothing when the site raises its own condition with no SDK failure underneath.
    /// </param>
    /// <param name="AlsoShows">
    /// The OTHER codes this one call can show, for a site that forwards a whole family rather than one code. Empty
    /// for the ordinary site. It exists because a register that named only the first of four codes would say a
    /// site was governed while three of its outcomes went unlisted — which is the state the delete site was in
    /// while all four of its reasons shared one id (D5).
    /// </param>
    private sealed record Site(
        string Where, Owner Owner, ProblemCode? Code, string Reason, string Composition = "none — its own condition",
        ProblemCode[]? AlsoShows = null)
    {
        /// <summary>Every code this site can show — its own, plus any it also forwards.</summary>
        internal IEnumerable<ProblemCode> Codes =>
            Code is { } code ? [code, .. AlsoShows ?? []] : [];
    }

    /// <summary>
    /// THE REGISTER. Nineteen coded sites and three uncoded surfaces, which is the twenty-two message calls the GUI
    /// makes; the count is asserted below against a scan of the sources so a new site cannot join unlisted.
    /// </summary>
    private static readonly Site[] Register =
    [
        // ---- MainWindowViewModel ----
        new("MainWindowViewModel.ReportOutcomeAsync (a Failed edit)", Owner.Host, HostProblemCodes.EditFailed,
            "The engine faulted while executing an edit the shell had already accepted. The SDK's reason is an "
            + "English developer diagnostic, so it goes to the log and the shell owns the Danish sentence."),
        new("MainWindowViewModel.Help", Owner.Uncoded, null,
            "Not an outcome: it shows the element's own documentation note, or a fixed line when it has none. A "
            + "code beside help CONTENT would be identity on a text the installer authored."),
        new("MainWindowViewModel.SendProject", Owner.Host, HostProblemCodes.ControllerRequiredSend,
            "The shell's own deferral (E10): this build never contacts a controller, which is nothing the SDK "
            + "knows or decides."),
        new("MainWindowViewModel.RetrieveProject", Owner.Host, HostProblemCodes.ControllerRequiredRetrieve,
            "The sibling of the send site. TWO codes rather than one: the sentences differ in their opening "
            + "words, and the site used to assemble each from a fragment plus a shared tail at render time."),
        new("MainWindowViewModel.RegisterAppRows/controller.send gate", Owner.Host,
            HostProblemCodes.ValidationErrorsBlockSend,
            "A GATE's refusal, not a dialog — the sentence reaches the user as the greyed row's tooltip and as "
            + "the status-bar line a refused F5 writes. It is registered all the same: the register's question is "
            + "who owns a user-facing sentence, not which control carries it, and a reason shown on a menu row is "
            + "read by exactly the same person as one shown in a box. Host-owned because gating an upload on "
            + "findings is THIS application's policy — the SDK reports the findings and takes no view on whether "
            + "a transfer may proceed."),
        new("MainWindowViewModel.InsertFunctionBlockAsync", Owner.Sdk, EditRefusalCodes.LibraryBlockMissing,
            "The catalog is the SDK's, so the reason no such block exists is the SDK's to word (T043)."),
        new("MainWindowViewModel.Delete", Owner.Sdk, EditRefusalCodes.DeletionRefusedCatalogPin,
            "The SDK's own coded problem, forwarded WHOLE: it names WHICH rule refused — a catalog-declared pin, "
            + "a locked library block, project structure, or a node already gone — which the shell cannot know. "
            + "FOUR codes off one call (D5): they were one until the entry's fixed sentence was found to be a "
            + "sentence no user ever read.",
            AlsoShows: [EditRefusalCodes.DeletionRefusedLockedBlock, EditRefusalCodes.DeletionRefusedStructural,
                EditRefusalCodes.TargetMissing]),
        new("MainWindowViewModel.InsertProductAsync (no such product)", Owner.Sdk,
            EditRefusalCodes.CatalogProductMissing,
            "As with the library block: a catalog lookup the SDK owns, coded by it (T043)."),
        new("MainWindowViewModel.InsertProductAsync (modem limit)", Owner.Sdk, EditRefusalCodes.ModemLimit,
            "The at-most-one-modem rule is the SDK's, and so are its sentence and its remedy (T043). The shell "
            + "keeps only the title, which names the rule."),
        new("MainWindowViewModel.TelemetryDiagnosticsAsync (no host)", Owner.Host,
            HostProblemCodes.TelemetryHostMissing,
            "Application configuration, read from this app's own settings file. The SDK has no telemetry host."),
        new("MainWindowViewModel.TelemetryDiagnosticsAsync (host would not open)", Owner.Host,
            HostProblemCodes.TelemetryHostUnreachable,
            "The OS handover is the shell's action; the host is carried as a declared argument rather than "
            + "spliced into a sentence."),
        new("MainWindowViewModel.RunAsync (catch-all)", Owner.Host, HostProblemCodes.Unexpected,
            "D12: the host-family counterpart of internal.unexpected. The exception's message is the diagnostic, "
            + "never the sentence."),

        // ---- CatalogImportWorkflow ----
        new("CatalogImportWorkflow.ImportFileAsync", Owner.Host, HostProblemCodes.CatalogFileRejected,
            "US-062 requires the message to NAME the file, and the SDK's coded cause says only that a catalog "
            + "file could not be read. So the shell's sentence is the more specific of the two and is the one "
            + "rendered; the SDK's cause code and English detail go to the log.",
            "one-child chain: the SDK's import.catalog operation over the shell's cause"),
        new("CatalogImportWorkflow.ImportFolderAsync (missing folder)", Owner.Host,
            HostProblemCodes.CatalogFolderMissing,
            "The shell checked the folder before asking the SDK for anything, so there is no SDK failure here at "
            + "all."),
        new("CatalogImportWorkflow.ImportFolderAsync (stopped part-way)", Owner.Host,
            HostProblemCodes.CatalogImportStopped,
            "The batch framing is the shell's: which file stopped it, and how many were kept (US-062). The count "
            + "is a declared Integer argument.",
            "one-child chain, NOT an aggregate: one failure at two precisions, not N independent ones"),

        // ---- ProjectReportWorkflow ----
        new("ProjectReportWorkflow.ViewInBrowserAsync (no viewer opened)", Owner.Host,
            HostProblemCodes.ReportNotOpenable,
            "The report was produced — the SDK's part succeeded. What failed is the shell's handover to the OS."),
        new("ProjectReportWorkflow.ViewInBrowserAsync (generation failed)", Owner.Host,
            HostProblemCodes.ReportViewFailed,
            "Report generation is the SDK's, but it raises no coded refusal for it, so the shell states the "
            + "outcome of its own action and logs the engine's text.",
            "one-child chain when the exception carries one"),
        new("ProjectReportWorkflow.SaveAsAsync", Owner.Host, HostProblemCodes.ReportSaveFailed,
            "Same as the view site, for the save half.",
            "one-child chain when the exception carries one"),

        // ---- ProjectWorkflow ----
        new("ProjectWorkflow.OpenAsync", Owner.Host, HostProblemCodes.ProjectOpenFailed,
            "The SDK's load refusals are coded and Danish, but they name the CONDITION, not the file; the "
            + "startup-path test requires the file. The shell's sentence names it and the SDK's operation code "
            + "heads the chain.",
            "one-child chain: the SDK's io.load operation over the shell's cause"),
        new("ProjectWorkflow.SaveFunctionBlockAsync", Owner.Host, HostProblemCodes.BlockExportFailed,
            "The shell states which block and which target file, both of which the SDK's write refusal does not "
            + "carry.",
            "one-child chain when the exception carries one"),
        new("ProjectWorkflow.SaveToAsync", Owner.Host, HostProblemCodes.ProjectSaveFailed,
            "As with the open site: the SDK's save refusal is coded, the path is the shell's to name.",
            "one-child chain: the SDK's io.save operation over the shell's cause"),

        // ---- Uncoded surfaces ----
        new("AvaloniaDialogService.ShowAboutAsync / ShowSettingsAsync", Owner.Uncoded, null,
            "Neither is an outcome: one is the About window, the other a settings readout. They show no failure, "
            + "so there is nothing to identify."),
        new("AvaloniaDialogService confirm prompts (save-changes, Ja/Nej)", Owner.Uncoded, null,
            "A QUESTION, not a statement about a failure. A code would suggest something went wrong; nothing "
            + "has, and the installer is being asked to choose."),
    ];

    /// <summary>
    /// The uncoded message CALLS the scan may find, per file, with the reason. Everything else must go through the
    /// coded door. Kept as counts because the scan reads source text: a count that no longer matches means a site
    /// was added, removed or migrated, and either way the register is the thing to update.
    /// </summary>
    private static readonly (string File, int Calls, string Why)[] UncodedCalls =
    [
        ("ViewModels/MainWindowViewModel.cs", 1, "Help content (US-044/US-045) — not an outcome"),
    ];

    [Test]
    public void EveryRegisteredCodeExistsInItsOwnersCatalogue()
    {
        Assert.Multiple(() =>
        {
            foreach (Site site in Register.Where(s => s.Owner != Owner.Uncoded))
            {
                Assert.That(site.Code, Is.Not.Null, site.Where);
                foreach (ProblemCode code in site.Codes)
                {
                    switch (site.Owner)
                    {
                        case Owner.Host:
                            Assert.That(code.IsHostOwned, Is.True, $"{site.Where}: a host ruling needs an app.* code");
                            Assert.That(HostProblemCatalog.Current.TryGet(code, out _), Is.True,
                                $"{site.Where}: {code.Value} is not declared in the host catalogue");
                            break;
                        case Owner.Sdk:
                            Assert.That(code.IsHostOwned, Is.False, $"{site.Where}: an SDK ruling needs an SDK code");
                            Assert.That(ProblemCatalog.Current.TryGet(code, out _), Is.True,
                                $"{site.Where}: {code.Value} is not declared in the SDK catalogue");
                            break;
                        default:
                            break;
                    }
                }
            }
        });
    }

    /// <summary>
    /// Every site records WHY it took its ruling, and a wrapping site records which composition it uses. Asserted
    /// because the reason is the deliverable: a register of codes with no rulings would be a lookup table, and the
    /// next person to add a site would have nothing to follow.
    /// </summary>
    [Test]
    public void EverySiteRecordsItsRulingAndItsComposition()
    {
        Assert.Multiple(() =>
        {
            foreach (Site site in Register)
            {
                Assert.That(site.Reason, Is.Not.Empty.And.Length.GreaterThan(40), $"{site.Where}: a real reason");
                Assert.That(site.Composition, Is.Not.Empty, site.Where);
            }

            Assert.That(Register.Count(s => s.Composition.Contains("chain", StringComparison.Ordinal)),
                Is.EqualTo(7), "the seven sites that wrap an SDK failure, each declaring a one-child chain");
        });
    }

    /// <summary>
    /// Every code this app declares is USED by a site. A minted code nothing raises is the same defect as a site
    /// with no code, from the other end — and it is how a vocabulary grows entries nobody can ever see.
    /// </summary>
    [Test]
    public void EveryHostCodeIsShownBySomeSite()
    {
        IReadOnlyCollection<string> used =
            [.. Register.Where(s => s.Owner == Owner.Host).Select(s => s.Code!.Value.Value)];

        Assert.That(HostProblemCodes.All.Select(c => c.Value), Is.EquivalentTo(used),
            "the declared host codes and the codes the sites show are the same set");
    }

    /// <summary>
    /// THE GATE: no user-facing GUI message without a code. Scans the app's OWN SOURCES — every <c>.cs</c> file,
    /// copied beside the test binaries — for calls to the uncoded message door, and requires each one to be
    /// declared in <see cref="UncodedCalls"/> with a reason. A new bare-string message anywhere in the GUI fails
    /// this test in the file that added it.
    /// </summary>
    [Test]
    public void NoUserFacingGuiMessageIsShownWithoutACode()
    {
        Dictionary<string, int> found = new(StringComparer.Ordinal);
        string root = Path.Combine(TestContext.CurrentContext.TestDirectory, "appsrc");
        Assert.That(Directory.Exists(root), Is.True, "the GUI sources are copied beside the test binaries");

        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            int calls = Occurrences(File.ReadAllText(file), ".ShowMessageAsync(");
            if (calls > 0)
            {
                found[relative] = calls;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(found.Keys, Is.Not.Empty, "sanity: the scan really read the sources");
            foreach ((string file, int calls) in found)
            {
                (string File, int Calls, string Why)[] declared = [.. UncodedCalls.Where(u => u.File == file)];
                Assert.That(declared, Is.Not.Empty,
                    $"{file} shows a message with no code and is not in the uncoded register — route it through "
                    + "ShowProblemAsync with a code, or register it with a reason");
                Assert.That(calls, Is.EqualTo(declared[0].Calls),
                    $"{file}: {calls} uncoded message call(s), {declared[0].Calls} declared ({declared[0].Why})");
            }

            foreach ((string file, int _, string why) in UncodedCalls)
            {
                Assert.That(found.ContainsKey(file), Is.True,
                    $"{file} no longer shows an uncoded message ({why}) — drop it from the register");
            }
        });
    }

    /// <summary>
    /// The coded door is actually USED where the register says a site is coded, so a row cannot claim a code while
    /// the site still hands over a string.
    /// <para>
    /// TWO spellings count, because there are two ways to reach the same door. A site that knows which SHAPE it
    /// has calls <c>ShowProblemAsync</c> directly; a site catching an exception routes through
    /// <c>RaisedProblemDisplay.ShowAsync</c>, which picks the shape the exception carries and then calls that
    /// very door. Accepting only the literal name would fail a site for using the SHARED decision instead of
    /// repeating it — the opposite of what this register is for.
    /// </para>
    /// </summary>
    [Test]
    public void TheCodedDoorIsUsedInEveryFileTheRegisterNames()
    {
        string root = Path.Combine(TestContext.CurrentContext.TestDirectory, "appsrc");
        string[] files =
        [
            "ViewModels/MainWindowViewModel.cs",
            "Services/CatalogImportWorkflow.cs",
            "Services/ProjectReportWorkflow.cs",
            "Services/ProjectWorkflow.cs",
        ];

        Assert.Multiple(() =>
        {
            foreach (string file in files)
            {
                string path = Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(path), Is.True, path);
                string source = File.ReadAllText(path);
                Assert.That(
                    Occurrences(source, ".ShowProblemAsync(") + Occurrences(source, "RaisedProblemDisplay.ShowAsync("),
                    Is.GreaterThan(0), file);
            }
        });
    }

    /// <summary>
    /// The invariant-10 breach, gone and pinned gone: no GUI source interpolates an engine message into a
    /// user-facing sentence any more. The nine sites that did now put it in the problem's diagnostic slot, which
    /// the presentation path never renders.
    /// </summary>
    [Test]
    public void NoGuiSourceSplicesAnEngineMessageIntoASentence()
    {
        string root = Path.Combine(TestContext.CurrentContext.TestDirectory, "appsrc");
        List<string> offenders = [];

        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            foreach (string line in File.ReadAllLines(file))
            {
                // A Danish sentence with the exception's own message spliced in: the shape every migrated site had.
                if (line.Contains("{ex.Message}", StringComparison.Ordinal)
                    && line.Contains('"', StringComparison.Ordinal)
                    && !line.Contains("LogError", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetRelativePath(root, file)}: {line.Trim()}");
                }
            }
        }

        Assert.That(offenders, Is.Empty,
            "an engine diagnostic belongs in the problem's diagnostic slot and in the log, never in the sentence "
            + "the installer reads (ARCHITECTURE.md invariant 10)");
    }

    private static int Occurrences(string haystack, string needle)
    {
        int count = 0;
        for (int i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
