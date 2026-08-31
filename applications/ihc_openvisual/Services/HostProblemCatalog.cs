using System;
using System.Collections.Immutable;
using System.Linq;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace ihc_openvisual.Services;

/// <summary>
/// The codes THIS APPLICATION mints, in the reserved host family <c>app.openvisual.*</c> (D7, D09). A code whose
/// first dotted segment is <c>app</c> is host-owned; every other code is the SDK's, and
/// <see cref="ProblemCode.IsHostOwned"/> is the one predicate that answers it.
/// <para>
/// Codes live in their own class, apart from the entries, for the reason the SDK's families do: a code is raised
/// from wherever the condition arises, while its entry is read only by governance and by presentation.
/// </para>
/// <para>
/// Every one of these is an OPERATION OUTCOME of the shell's own — an action of this application that could not be
/// carried through. Where the SDK owns the condition, the shell narrates the SDK's code instead of minting one
/// here; the per-site register in <c>MessageSiteRegisterTests</c> records which is which, and why.
/// </para>
/// </summary>
internal static class HostProblemCodes
{
    /// <summary>
    /// The app's own catch-all: a command handler ended in an exception the shell did not expect. The host-family
    /// counterpart of the SDK's <c>internal.unexpected</c> (D12) — same posture, different owner, so a support
    /// question can say WHOSE code failed without reading the sentence.
    /// </summary>
    public static ProblemCode Unexpected { get; } = new("app.openvisual.unexpected");

    /// <summary>An edit the SDK accepted for execution ended in an engine fault, so nothing was committed.</summary>
    public static ProblemCode EditFailed { get; } = new("app.openvisual.edit-failed");

    /// <summary>Sending the project needs a connected controller, which this build never contacts (E10).</summary>
    public static ProblemCode ControllerRequiredSend { get; } = new("app.openvisual.controller-required-send");

    /// <summary>Retrieving a project needs a connected controller (E10).</summary>
    public static ProblemCode ControllerRequiredRetrieve { get; } = new("app.openvisual.controller-required-retrieve");

    /// <summary>
    /// The project carries validation Errors, so the shell withholds the transfer. HOST-owned rather than SDK:
    /// the SDK reports the findings, but the decision to gate an upload on them is this application's policy —
    /// a console tool over the same engine is free to transfer anyway.
    /// </summary>
    public static ProblemCode ValidationErrorsBlockSend { get; } = new("app.openvisual.validation-errors-block-send");

    /// <summary>The telemetry pipeline is down, so nothing this run records will be exported.</summary>
    public static ProblemCode TelemetryPipelineDown { get; } = new("app.openvisual.telemetry-pipeline-down");

    /// <summary>An exception crossed a native platform boundary and was discarded there.</summary>
    public static ProblemCode PlatformFault { get; } = new("app.openvisual.platform-fault");

    /// <summary>A whole validation run failed, so the listed findings no longer describe the document.</summary>
    public static ProblemCode ValidationFaulted { get; } = new("app.openvisual.validation-faulted");

    /// <summary>There was no clipboard to copy to, so nothing was copied.</summary>
    public static ProblemCode ClipboardUnavailable { get; } = new("app.openvisual.clipboard-unavailable");

    /// <summary>No telemetry host is configured, so there is nothing to open.</summary>
    public static ProblemCode TelemetryHostMissing { get; } = new("app.openvisual.telemetry-host-missing");

    /// <summary>The configured telemetry host would not open in a viewer.</summary>
    public static ProblemCode TelemetryHostUnreachable { get; } = new("app.openvisual.telemetry-host-unreachable");

    /// <summary>The file offered for catalog import is not a definition file this app can take in (US-062).</summary>
    public static ProblemCode CatalogFileRejected { get; } = new("app.openvisual.catalog-file-rejected");

    /// <summary>The folder offered for a catalog import does not exist (US-060).</summary>
    public static ProblemCode CatalogFolderMissing { get; } = new("app.openvisual.catalog-folder-missing");

    /// <summary>A folder import stopped at a file it could not read, keeping the ones before it (US-062).</summary>
    public static ProblemCode CatalogImportStopped { get; } = new("app.openvisual.catalog-import-stopped");

    /// <summary>The report was produced, but the OS opened no viewer for it.</summary>
    public static ProblemCode ReportNotOpenable { get; } = new("app.openvisual.report-not-openable");

    /// <summary>The report could not be produced for viewing.</summary>
    public static ProblemCode ReportViewFailed { get; } = new("app.openvisual.report-view-failed");

    /// <summary>The report could not be produced or written to the chosen file.</summary>
    public static ProblemCode ReportSaveFailed { get; } = new("app.openvisual.report-save-failed");

    /// <summary>The project file could not be opened.</summary>
    public static ProblemCode ProjectOpenFailed { get; } = new("app.openvisual.project-open-failed");

    /// <summary>The project could not be written to the chosen file.</summary>
    public static ProblemCode ProjectSaveFailed { get; } = new("app.openvisual.project-save-failed");

    /// <summary>The function block could not be exported to a library file (US-021).</summary>
    public static ProblemCode BlockExportFailed { get; } = new("app.openvisual.block-export-failed");

    /// <summary>The findings list could not be written to the chosen file (US-085).</summary>
    public static ProblemCode FindingsExportFailed { get; } = new("app.openvisual.findings-export-failed");

    /// <summary>
    /// Every code this app declares — the list the governance checks read, taken from the CATALOGUE rather
    /// than retyped beside it. The two lists named the same fifteen members in the same order and had to be
    /// edited together, which is one list too many for a rule that exists to catch a forgotten code.
    /// </summary>
    /// <remarks>
    /// A COMPUTED property, not a static field, and the difference is not stylistic. As a field it ran inside this
    /// type's static initializer, which reads <see cref="HostProblemCatalog.Current"/>, whose own initializer
    /// reads the codes above — a static-init cycle whose outcome depends on which of the two types a process
    /// touches first. Touch a code first and both initialize; touch the catalogue first and this line observes a
    /// half-built catalogue, dereferences null, and the whole family fails to initialize with a
    /// <see cref="TypeInitializationException"/> that names neither cause. Computing on demand removes the cycle
    /// outright; the list is read by governance and presentation, never in a loop.
    /// </remarks>
    public static EquatableArray<ProblemCode> All =>
        HostProblemCatalog.Current.Entries.Select(e => e.Code).ToImmutableArray();
}

/// <summary>
/// The host family's MASTER ARTIFACT: every <c>app.openvisual.*</c> code as a compiled declaration, in the same
/// schema the SDK's catalogue uses (<see cref="ProblemCatalogEntry"/>) and under the same governance. A reserved
/// family buys this app its own code space, not an exemption — ids are unique across every family (not merely
/// within this one), every code has an entry, arguments are declared and typed (the compiler enforces arity and
/// type at each factory's call site), retirement keeps an id reserved, and the user-facing text is Danish.
///
/// <para><b>Operation outcomes only, never findings.</b> Every entry here is
/// <see cref="ProblemCatalogSection.OperationOutcomes"/> / <see cref="RuleKind.OperationOutcome"/> /
/// <see cref="CatalogDisposition.Refusal"/> with no category and no face. A finding is a statement about the
/// <c>.vis</c> file, the SDK owns the file, and a second opinion about it minted in an app is how two catalogues
/// start disagreeing. <c>HostProblemCatalogTests</c> pins that, and pins it against a seeded finding rather than
/// only over the rows that happen to exist.</para>
///
/// <para><b>These declarations are the only truth.</b> The family has no published row table to keep in step:
/// enumerate <see cref="Current"/> to learn what this app can report.</para>
/// </summary>
internal static class HostProblemCatalog
{
    /// <summary>
    /// This app's declarations, frozen. Its own catalogue object rather than entries added to the SDK's: the SDK
    /// must not know the host's rows, and the two are checked TOGETHER for id uniqueness instead.
    /// </summary>
    // Validated, not From: this is a composition root, so a defective row is refused HERE rather than
    // reported by whichever test happens to run. The SDK's own root does the same.
    public static ProblemCatalog Current { get; } = ProblemCatalog.Validated(EquatableArray.Create<ProblemCatalogEntry>(
    [
        Unexpected, EditFailed, ControllerRequiredSend, ControllerRequiredRetrieve, ValidationErrorsBlockSend,
        ClipboardUnavailable, ValidationFaulted, PlatformFault, TelemetryPipelineDown,
        TelemetryHostMissing, TelemetryHostUnreachable,
        CatalogFileRejected, CatalogFolderMissing, CatalogImportStopped,
        ReportNotOpenable, ReportViewFailed, ReportSaveFailed,
        ProjectOpenFailed, ProjectSaveFailed, BlockExportFailed,
        FindingsExportFailed,
    ]));

    /// <summary>
    /// The shell's own catch-all (D12): an unhandled exception escaping a command handler. The Danish sentence is
    /// fixed text and the exception's own message is the English <see cref="ProblemCatalogEntry.Diagnostic"/> —
    /// invariant 10, and the reason this app never shows an engine diagnostic to an installer.
    /// </summary>
    internal static ProblemCatalogEntry Unexpected => Outcome(
        HostProblemCodes.Unexpected,
        "Handlingen kunne ikke gennemføres på grund af en intern fejl. Detaljerne er skrevet til loggen.",
        "An exception escaped a command handler; the shell caught it at the boundary.");

    /// <summary>
    /// RETIRED. An edit that passed its preconditions and then faulted inside the engine is the SDK's fault to
    /// word, and since T023 the SDK does: a <c>Failed</c> outcome carries an <c>InternalError</c> with
    /// <c>internal.edit-failed</c>'s Danish sentence already bound, so the shell renders it whole and adds only
    /// a title. This row said the same thing in a second voice.
    /// <para>
    /// KEPT rather than deleted, which is what retirement is for: the duplicate-code invariant reads every entry,
    /// so an id that stays occupied can never be handed to a different condition.
    /// </para>
    /// </summary>
    internal static ProblemCatalogEntry EditFailed => Outcome(
        HostProblemCodes.EditFailed,
        "Redigeringen kunne ikke gennemføres på grund af en intern fejl. Ændringen blev ikke gemt.",
        "An accepted edit ended in an engine fault; the outcome's reason is the English engine diagnostic.")
        with { Status = ProblemCodeStatus.Retired };

    /// <summary>
    /// Sending needs a controller. TWO codes rather than one with a verb argument: the two sentences differ in
    /// their opening words, and assembling one from a fragment plus a shared tail is exactly the render-time
    /// assembly the fixed-label convention forbids — which is what the two sites did before T042.
    /// </summary>
    internal static ProblemCatalogEntry ControllerRequiredSend => Outcome(
        HostProblemCodes.ControllerRequiredSend,
        "Afsendelse kræver en tilsluttet controller. Denne version kontakter ingen controller.",
        "Controller transfer is deferred (E10); this build never contacts a controller.");

    /// <summary>Retrieving needs a controller. The sibling of <see cref="ControllerRequiredSend"/>.</summary>
    internal static ProblemCatalogEntry ControllerRequiredRetrieve => Outcome(
        HostProblemCodes.ControllerRequiredRetrieve,
        "Hentning kræver en tilsluttet controller. Denne version kontakter ingen controller.",
        "Controller transfer is deferred (E10); this build never contacts a controller.");

    /// <summary>
    /// The upload gate's refusal. It names the PANEL rather than the findings, and carries no count: the sentence
    /// is shown on a greyed menu row and in the status bar, where the panel is already on screen with the live
    /// numbers — repeating a count here would be a second copy of a figure that changes on every edit, and the
    /// one thing a user needs from this sentence is where to go and fix it.
    /// </summary>
    internal static ProblemCatalogEntry ValidationErrorsBlockSend => Outcome(
        HostProblemCodes.ValidationErrorsBlockSend,
        "Projektet indeholder fejl. Ret dem i Problemer-panelet, før projektet sendes.",
        "The latest completed validation bound at least one Error finding; the shell withholds the transfer.");

    /// <summary>
    /// The start-up self-check found the telemetry endpoint unreachable, rejecting, or misconfigured — so every
    /// span, metric and log line this run produces is being dropped.
    /// </summary>
    /// <remarks>
    /// This row is the ONE finding that cannot rely on telemetry to be seen, which is the whole reason it needs a
    /// surface of its own: the mechanism that would otherwise report it is the mechanism that is down. The
    /// self-check's own message is an operator-facing English diagnostic naming an endpoint and a status, so it
    /// travels as the diagnostic; the Danish sentence says the consequence.
    /// </remarks>
    internal static ProblemCatalogEntry TelemetryPipelineDown => Outcome(
        HostProblemCodes.TelemetryPipelineDown,
        "Telemetri kunne ikke sendes. Diagnostik for denne kørsel gemmes ikke.",
        "The telemetry self-check reported a problem status, so exported telemetry is being dropped.");

    /// <summary>
    /// An exception crossed a native platform boundary and was DISCARDED there — the app neither handled it nor
    /// died of it, because the boundary cannot propagate a managed exception at all.
    /// </summary>
    /// <remarks>
    /// Its own code, and its own wording, because every other outcome sentence in this catalogue would be a lie
    /// here. "Handlingen kunne ikke gennemføres" tells the reader one action failed and the rest is fine; nobody
    /// knows that. What is actually true is narrower and stranger: something failed, it was thrown away by a
    /// layer that had no choice, and the state of the program afterwards is not established. The row is a RECORD,
    /// never a recovery — and it is worth saying precisely because nothing else in the app will.
    /// </remarks>
    internal static ProblemCatalogEntry PlatformFault => Outcome(
        HostProblemCodes.PlatformFault,
        "En fejl i platformslaget blev registreret og kasseret. Programmets tilstand er ukendt.",
        "An exception crossed a native platform boundary; the boundary discarded it, so no recovery happened.");

    /// <summary>
    /// A validation RUN failed outright — not a rule inside it, which the engine catches and reports for itself,
    /// but the run around them. Its own code rather than the shell's catch-all, because the catch-all says only
    /// that something unexpected happened, and the fact that matters here is the CONSEQUENCE: the rows still on
    /// screen describe a document state the run never reached, and they read as current.
    /// </summary>
    internal static ProblemCatalogEntry ValidationFaulted => Outcome(
        HostProblemCodes.ValidationFaulted,
        "Valideringen stoppede. Listen er muligvis forældet.",
        "A whole validation run faulted; the bound findings are kept but no longer describe the document.");

    /// <summary>
    /// The platform offered no clipboard, so the copy could not happen. Its sentence is short on purpose: it is
    /// rendered ON the copy button, in place of its label, because the dialog raising it is modal over every
    /// other surface the shell could have said it on.
    /// </summary>
    internal static ProblemCatalogEntry ClipboardUnavailable => Outcome(
        HostProblemCodes.ClipboardUnavailable,
        "Udklipsholderen er ikke tilgængelig.",
        "TopLevel exposed no IClipboard, so there was nothing to copy to.");

    /// <summary>No telemetry host is configured in <c>ihcsettings.json</c>.</summary>
    internal static ProblemCatalogEntry TelemetryHostMissing => Outcome(
        HostProblemCodes.TelemetryHostMissing,
        "Der er ikke konfigureret nogen telemetri-vært i ihcsettings.json.",
        "No telemetry host is configured, so there is nothing to open.");

    /// <summary>The configured telemetry host would not open.</summary>
    internal static ProblemCatalogEntry TelemetryHostUnreachable => Outcome(
        HostProblemCodes.TelemetryHostUnreachable,
        "Telemetri-værten '{host}' kunne ikke åbnes.",
        "The OS handler declined to open the configured telemetry host.",
        new ProblemArgumentSlot("host", ProblemArgumentType.Path));

    /// <summary>
    /// The offered file is not a definition file (US-059/US-062). It NAMES the file, which is US-062's own
    /// requirement — and is why the shell's sentence is the one rendered where the SDK's coded cause says only
    /// that a catalog file could not be read.
    /// </summary>
    internal static ProblemCatalogEntry CatalogFileRejected => Outcome(
        HostProblemCodes.CatalogFileRejected,
        "Filen '{file}' er ikke en gyldig produkt- eller funktionsblok-definitionsfil.",
        "The catalog import refused the file; the SDK's coded cause and its English detail go to the log.",
        new ProblemArgumentSlot("file", ProblemArgumentType.Path));

    /// <summary>The folder offered for a catalog import does not exist (US-060).</summary>
    internal static ProblemCatalogEntry CatalogFolderMissing => Outcome(
        HostProblemCodes.CatalogFolderMissing,
        "Mappen '{folder}' findes ikke.",
        "The folder offered for a catalog import does not exist.",
        new ProblemArgumentSlot("folder", ProblemArgumentType.Path));

    /// <summary>
    /// A folder import stopped at the first unreadable file, keeping the ones before it (US-062). The count is a
    /// declared <see cref="ProblemArgumentType.Integer"/>, not a spliced word.
    /// </summary>
    internal static ProblemCatalogEntry CatalogImportStopped => Outcome(
        HostProblemCodes.CatalogImportStopped,
        "Filen '{file}' kunne ikke importeres. {count} fil(er) blev importeret før den.",
        "A folder import stopped at the first unreadable file; the SDK's coded cause goes to the log.",
        new ProblemArgumentSlot("file", ProblemArgumentType.Path),
        new ProblemArgumentSlot("count", ProblemArgumentType.Integer));

    /// <summary>The report was written but no viewer opened — a handover that did not happen (UX review CORE-03).</summary>
    internal static ProblemCatalogEntry ReportNotOpenable => Outcome(
        HostProblemCodes.ReportNotOpenable,
        "Rapporten blev dannet, men kunne ikke åbnes i en fremviser.\nFilen ligger her:\n{path}",
        "The OS handler declined to open the generated report.",
        new ProblemArgumentSlot("path", ProblemArgumentType.Path));

    /// <summary>The report could not be produced for viewing.</summary>
    internal static ProblemCatalogEntry ReportViewFailed => Outcome(
        HostProblemCodes.ReportViewFailed,
        "Rapporten kunne ikke vises.",
        "Report generation for viewing failed; the engine's English text goes to the log.");

    /// <summary>The report could not be produced or written to the chosen file.</summary>
    internal static ProblemCatalogEntry ReportSaveFailed => Outcome(
        HostProblemCodes.ReportSaveFailed,
        "Rapporten kunne ikke gemmes.",
        "Report generation or writing failed; the engine's English text goes to the log.");

    /// <summary>
    /// The findings list could not be written (US-085). Its sentence says LIST rather than report, because the
    /// panel's export and the documentation reports fail for different reasons, and a user who sees this one
    /// never asked for a report.
    /// </summary>
    internal static ProblemCatalogEntry FindingsExportFailed => Outcome(
        HostProblemCodes.FindingsExportFailed,
        "Fejllisten kunne ikke gemmes.",
        "Writing the findings export failed; the engine's English text goes to the log.");

    /// <summary>The project file could not be opened. It names the file, which the startup-path test requires.</summary>
    internal static ProblemCatalogEntry ProjectOpenFailed => Outcome(
        HostProblemCodes.ProjectOpenFailed,
        "Projektet '{path}' kunne ikke åbnes.",
        "Opening the project failed; where the SDK refused with a code, its cause is the sentence shown.",
        new ProblemArgumentSlot("path", ProblemArgumentType.Path));

    /// <summary>The project could not be written.</summary>
    internal static ProblemCatalogEntry ProjectSaveFailed => Outcome(
        HostProblemCodes.ProjectSaveFailed,
        "Projektet kunne ikke gemmes som '{path}'.",
        "Saving the project failed; where the SDK refused with a code, that code is the chain's operation.",
        new ProblemArgumentSlot("path", ProblemArgumentType.Path));

    /// <summary>The function block could not be exported to a library file (US-021).</summary>
    internal static ProblemCatalogEntry BlockExportFailed => Outcome(
        HostProblemCodes.BlockExportFailed,
        "Funktionsblokken '{name}' kunne ikke gemmes som '{path}'.",
        "Exporting the function block failed; the engine's English text goes to the log.",
        new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
        new ProblemArgumentSlot("path", ProblemArgumentType.Path));

    /// <summary>
    /// The one entry shape this family has: an operation outcome, no category, no face, refusing rather than
    /// reporting. Written once because every row here is that shape BY RULE — a host authors no findings — so
    /// fifteen literal repetitions of the same five arguments would only be fifteen chances to get one wrong.
    /// </summary>
    private static ProblemCatalogEntry Outcome(
        ProblemCode code, string template, string diagnostic, params ProblemArgumentSlot[] slots) =>
        new(code,
            ProblemCatalogSection.OperationOutcomes,
            null,
            CatalogDisposition.Refusal,
            RuleKind.OperationOutcome,
            RuleFaces.None,
            default,
            FindingShape.OneFinding,
            EquatableArray.Create<ProblemArgumentSlot>(slots),
            template)
        {
            Diagnostic = diagnostic,
        };
}

/// <summary>
/// The typed factory per host code — one method per entry, taking that entry's declared argument slots as real
/// parameters. This is where the arity-and-type gate lives for the host family exactly as it does for the SDK's: a
/// wrong argument count or type does not compile at the call site, so no analyzer and no drift test is needed.
/// <para>
/// Each factory returns a problem whose message is already BOUND, because binding is the producer's job: the
/// shell's one presentation path renders the message as it stands and never re-derives it.
/// </para>
/// </summary>
internal static class HostProblems
{
    /// <summary>The app's catch-all, carrying the English engine text as diagnostic detail.</summary>
    /// <param name="cause">The exception that escaped. Its message becomes the diagnostic, never the sentence.</param>
    public static Problem Unexpected(Exception cause) =>
        Bind(HostProblemCatalog.Unexpected, Detail(cause), cause);

    /// <summary>Sending the project needs a connected controller.</summary>
    public static Problem ControllerRequiredSend() => Bind(HostProblemCatalog.ControllerRequiredSend, null, null);

    /// <summary>Retrieving a project needs a connected controller.</summary>
    public static Problem ControllerRequiredRetrieve() =>
        Bind(HostProblemCatalog.ControllerRequiredRetrieve, null, null);

    /// <summary>Telemetry is not reaching its endpoint, so this run leaves no exported diagnostics.</summary>
    /// <param name="diagnostic">The self-check's own English message, naming the endpoint and what it answered.</param>
    public static Problem TelemetryPipelineDown(string diagnostic) =>
        Bind(HostProblemCatalog.TelemetryPipelineDown, diagnostic, null);

    /// <summary>An exception was discarded at a native platform boundary; nothing recovered from it.</summary>
    public static Problem PlatformFault() => Bind(HostProblemCatalog.PlatformFault, null, null);

    /// <summary>A validation run failed, so what the panel lists may no longer describe the document.</summary>
    public static Problem ValidationFaulted() => Bind(HostProblemCatalog.ValidationFaulted, null, null);

    /// <summary>Nothing was copied, because the platform offered no clipboard.</summary>
    public static Problem ClipboardUnavailable() => Bind(HostProblemCatalog.ClipboardUnavailable, null, null);

    /// <summary>No telemetry host is configured.</summary>
    public static Problem TelemetryHostMissing() => Bind(HostProblemCatalog.TelemetryHostMissing, null, null);

    /// <summary>The configured telemetry host would not open.</summary>
    /// <param name="host">The configured host, as given.</param>
    public static Problem TelemetryHostUnreachable(string host) =>
        Bind(HostProblemCatalog.TelemetryHostUnreachable, null, null, new ProblemArgument("host", host));

    /// <summary>The offered file is not a catalog definition file (US-062).</summary>
    /// <param name="file">The file's name, as the installer chose it.</param>
    /// <param name="cause">The refusal the SDK raised; its English text becomes the diagnostic.</param>
    public static Problem CatalogFileRejected(string file, Exception cause) =>
        Bind(HostProblemCatalog.CatalogFileRejected, Detail(cause), cause, new ProblemArgument("file", file));

    /// <summary>The folder offered for a catalog import does not exist.</summary>
    /// <param name="folder">The folder, as given.</param>
    public static Problem CatalogFolderMissing(string folder) =>
        Bind(HostProblemCatalog.CatalogFolderMissing, null, null, new ProblemArgument("folder", folder));

    /// <summary>A folder import stopped at a file it could not read (US-062).</summary>
    /// <param name="file">The file it stopped at.</param>
    /// <param name="count">How many files were imported before it.</param>
    /// <param name="cause">The refusal the SDK raised; its English text becomes the diagnostic.</param>
    public static Problem CatalogImportStopped(string file, int count, Exception cause) =>
        Bind(HostProblemCatalog.CatalogImportStopped, Detail(cause), cause,
            new ProblemArgument("file", file), new ProblemArgument("count", count));

    /// <summary>The report was produced but no viewer opened.</summary>
    /// <param name="path">Where the report was written.</param>
    public static Problem ReportNotOpenable(string path) =>
        Bind(HostProblemCatalog.ReportNotOpenable, null, null, new ProblemArgument("path", path));

    /// <summary>The report could not be produced for viewing.</summary>
    /// <param name="cause">The originating exception; its English text becomes the diagnostic.</param>
    public static Problem ReportViewFailed(Exception cause) =>
        Bind(HostProblemCatalog.ReportViewFailed, Detail(cause), cause);

    /// <summary>The report could not be saved.</summary>
    /// <param name="cause">The originating exception; its English text becomes the diagnostic.</param>
    public static Problem ReportSaveFailed(Exception cause) =>
        Bind(HostProblemCatalog.ReportSaveFailed, Detail(cause), cause);

    /// <summary>The panel's findings list could not be written to the chosen file (US-085).</summary>
    public static Problem FindingsExportFailed(Exception cause) =>
        Bind(HostProblemCatalog.FindingsExportFailed, Detail(cause), cause);

    /// <summary>The project file could not be opened.</summary>
    /// <param name="path">The file the installer asked for.</param>
    /// <param name="cause">The originating exception; its English text becomes the diagnostic.</param>
    public static Problem ProjectOpenFailed(string path, Exception cause) =>
        Bind(HostProblemCatalog.ProjectOpenFailed, Detail(cause), cause, new ProblemArgument("path", path));

    /// <summary>The project could not be written.</summary>
    /// <param name="path">The target file.</param>
    /// <param name="cause">The originating exception; its English text becomes the diagnostic.</param>
    public static Problem ProjectSaveFailed(string path, Exception cause) =>
        Bind(HostProblemCatalog.ProjectSaveFailed, Detail(cause), cause, new ProblemArgument("path", path));

    /// <summary>The function block could not be exported.</summary>
    /// <param name="name">The block's name.</param>
    /// <param name="path">The target file.</param>
    /// <param name="cause">The originating exception; its English text becomes the diagnostic.</param>
    public static Problem BlockExportFailed(string name, string path, Exception cause) =>
        Bind(HostProblemCatalog.BlockExportFailed, Detail(cause), cause,
            new ProblemArgument("name", name), new ProblemArgument("path", path));

    /// <summary>
    /// How an SDK-raised exception is SHOWN: the shell's own framing as the OPERATION, over the SDK's coded cause
    /// as the cause — a one-child CHAIN, so exactly one sentence reaches the installer and it is the more
    /// specific one.
    /// <para>
    /// Which of the two is the more specific is the whole decision, and it is the SDK's (D01). Its cause says
    /// which KIND of failure this was — <i>Filen er tom</i>, <i>Ugyldig katalogfil</i> — where the shell's
    /// framing names the FILE the installer picked a moment ago and therefore already knows. Only the cause is
    /// rendered, so framing it as the cause showed an installer <i>which</i> file failed and never <i>why</i>.
    /// </para>
    /// <para>
    /// The framing is not lost by becoming the operation: it keeps its code and its bound path for the dialog
    /// title and the log, and every one of these sites logs the exception besides. Where the SDK raised no coded
    /// cause there is nothing more specific to show, so the framing stays the rendered cause under the shell's
    /// catch-all — the asymmetry is deliberate and is pinned by <c>NarratedProblemTests</c>.
    /// </para>
    /// </summary>
    /// <param name="framing">The shell's coded problem for this site.</param>
    /// <param name="raised">The exception the SDK raised.</param>
    public static ProblemChain Narrate(Problem framing, Exception raised)
    {
        ArgumentNullException.ThrowIfNull(raised);
        return raised is IProblemCarrier { Problems: { } chain }
            ? new ProblemChain(framing, chain.Cause)
            : new ProblemChain(Unexpected(raised), framing);
    }

    /// <summary>
    /// THE ONE PLACE in this application that reads an exception's message, and it moves it into the DIAGNOSTIC
    /// slot — never into the sentence an installer reads (ARCHITECTURE.md invariant 10). Every site hands over the
    /// exception itself, so there is exactly one call to review instead of nine, and the architecture rule that
    /// bans the read carries one named exemption instead of a growing list.
    /// </summary>
    /// <param name="cause">The exception whose English text becomes diagnostic detail.</param>
    private static string Detail(Exception cause) => cause.Message;

    /// <summary>
    /// Builds the problem for an entry: the entry's template bound with its arguments, the English detail in the
    /// diagnostic slot. Binding happens HERE, at the producer, so the presentation path renders the message as it
    /// stands (T040) and no site assembles a sentence.
    /// </summary>
    private static Problem Bind(
        ProblemCatalogEntry entry, string? diagnostic, Exception? cause, params ProblemArgument[] arguments)
    {
        Problem problem = new(entry.Code, entry.MessageTemplate, EquatableArray.Create(arguments),
            diagnostic ?? entry.Diagnostic, cause);
        return problem with { Message = entry.BindTemplate(problem) };
    }
}
