using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

using Ihc.Tests.Shared;

namespace safe_visual_e2e_tests;

/// <summary>
/// The shared harness for every end-to-end test in this suite: it opens a fixture, drives the application
/// through one <see cref="IE2EDriver"/> or the other, and tears it down again.
///
/// <para><b>Why these tests are a project of their own.</b> They drive a whole application rather than a
/// view-model, and in their default mode they take over the desktop and cost seconds each. Inside the GUI suite
/// they made every unrelated run slower and flakier. A separate project is what lets a caller choose them by
/// NAME: every suite is run by project path, so the expensive default mode is reached deliberately rather than
/// by a filter someone has to remember. CI names this project too, but only for the headless leg and only with
/// the desktop-bound scenarios filtered out.</para>
///
/// <para><b>Why the driver rather than direct UI Automation.</b> The driver is the surface the project already
/// maintains and documents, so an E2E test written through it exercises the same vocabulary a person debugging
/// the app types by hand — and a failure here is reproducible by copying one command line out of the assertion
/// message. Reaching around it into raw UIA would test a path nothing else uses.</para>
///
/// <para><b>The one rule every E2E assertion obeys: WAIT FOR A BOUND RESULT FIRST.</b> The Problemer panel
/// validates in the background, so for at least the debounce plus one whole-project run after a launch or an open
/// it reports <c>validating</c> and its counts mean nothing. <see cref="WaitForBoundProblems"/> is the gate; an
/// assertion fired before it is not testing the panel, it is racing it.</para>
/// </summary>
public static class E2E
{
    /// <summary>
    /// The category carried by a scenario that only the REAL driver can run — one reaching a dialog's controls,
    /// the menu bar, a tree node by path, or the project-information window.
    /// </summary>
    /// <remarks>
    /// A category rather than a run-time skip, and that was learned the hard way: <c>Assert.Ignore</c> is
    /// illegal inside <c>Assert.Multiple</c>, so a driver that skipped from the call site turned one scenario
    /// into an error about NUnit instead of a statement about the mode. Declared here, the gap is greppable, it
    /// is visible in the source beside the scenario it constrains, and CI excludes it with a filter.
    /// </remarks>
    public const string DesktopOnly = "DesktopOnly";

    /// <summary>
    /// The run parameter that swaps the real GUI for the in-process headless window. FALSE unless asked for:
    /// the real driver is what this suite is for, and headless is the reduced mode CI settles for.
    /// </summary>
    /// <example>
    /// <code>dotnet test … -- 'TestRunParameters.Parameter(name="headless",value="true")'</code>
    /// </example>
    public const string HeadlessParameter = "headless";

    /// <summary>Whether this run drives the headless window rather than the real application.</summary>
    public static bool Headless =>
        string.Equals(TestContext.Parameters.Get(HeadlessParameter, "false"), "true",
            StringComparison.OrdinalIgnoreCase);

    private static IE2EDriver? driver;

    /// <summary>
    /// The driver this run uses, decided once. Held statically because the headless one owns a window and an
    /// Avalonia dispatcher that outlive a single fixture, exactly as the real one owns a process.
    /// </summary>
    internal static IE2EDriver Driver => driver ??= Headless
        ? new HeadlessDriver()
        : new AuiProcessDriver();

    /// <summary>A fixture under tests/testdata/projects, as an absolute path the app can open.</summary>
    public static string Fixture(params string[] relativeParts) => Path.Combine(
        new[] { TestRepository.RequireRoot(), "tests", "testdata", "projects" }.Concat(relativeParts).ToArray());

    /// <summary>One driver envelope, parsed. Every field the tests read comes from here.</summary>
    public sealed record Envelope(bool Ok, string Code, string Message, JsonElement Data, string Raw)
    {
        /// <summary>The command line that produced it — quoted into failure messages so a red test is reproducible.</summary>
        public string Command { get; init; } = string.Empty;

        /// <summary>
        /// The driver's own <c>context</c> block, which sits BESIDE <c>data</c> rather than inside it: what
        /// document is open, what each pane has selected, what is modal. Absent as an undefined element.
        /// </summary>
        /// <remarks>
        /// Cloned at parse time so the readers below share one answer. They used to re-open a
        /// <see cref="JsonDocument"/> over <see cref="Raw"/> each — three readers, three different policies for
        /// a missing block (throw, empty string, empty list), which is three ways for the same absent context to
        /// present itself.
        /// </remarks>
        public JsonElement Context { get; init; }

        /// <summary>
        /// The one reader of the envelope SHAPE. Both drivers come through here, so they cannot drift over which
        /// fields exist or what a missing one means — they had already begun to, one guarding <c>data</c> with
        /// <c>TryGetProperty</c> and the other not.
        /// </summary>
        public static Envelope Parse(string raw)
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            JsonElement root = document.RootElement;
            return new Envelope(
                root.GetProperty("ok").GetBoolean(),
                root.GetProperty("code").GetString() ?? string.Empty,
                root.GetProperty("message").GetString() ?? string.Empty,
                root.TryGetProperty("data", out JsonElement data) ? data.Clone() : default,
                raw)
            {
                Context = root.TryGetProperty("context", out JsonElement context) ? context.Clone() : default,
            };
        }

        public JsonElement Field(string name) =>
            Data.ValueKind == JsonValueKind.Object && Data.TryGetProperty(name, out JsonElement value)
                ? value
                : throw new InvalidOperationException($"envelope for '{Command}' has no data.{name}: {Raw}");

        public int Number(string name) => Field(name).GetInt32();

        public string Text(string name) => Field(name).GetString() ?? string.Empty;

        public bool Flag(string name) => Field(name).GetBoolean();
    }

    /// <summary>
    /// Runs one driver command and returns its envelope. It does NOT assert success: several tests are about a
    /// refusal, and a helper that threw on <c>ok:false</c> would make those unwritable.
    /// </summary>
    /// <remarks>
    /// The command stamp and the trace live HERE, at the one call site both drivers pass through, rather than in
    /// each of them. Written per driver they had already diverged — only one named the mode — and an envelope
    /// that reached <see cref="RunOk"/> unstamped would report its failure as <c>'' failed with …</c>.
    /// </remarks>
    public static Envelope Run(params string[] args)
    {
        string command = "aui " + string.Join(' ', args);
        TestContext.Out.WriteLine($"> {command}  [{Driver.Name}]");
        Envelope envelope = Driver.Run(args) with { Command = command };
        TestContext.Out.WriteLine($"  {envelope.Code}: {envelope.Message}");
        return envelope;
    }

    /// <summary>Runs a command and fails the test if the driver refused, quoting the command and its reason.</summary>
    public static Envelope RunOk(params string[] args)
    {
        Envelope envelope = Run(args);
        Assert.That(envelope.Ok, Is.True,
            $"'{envelope.Command}' failed with {envelope.Code}: {envelope.Message}");
        return envelope;
    }

    /// <summary>
    /// Launches the application on a fixture and waits until it is drivable. Kills any survivor from a previous
    /// run first: a stale instance would be driven instead of the one under test, and its DLL locks would break
    /// the next build.
    /// </summary>
    public static void Launch(string fixturePath)
    {
        KillApp();
        Envelope doctor = Run("doctor", "--launch", "--path", fixturePath);
        Assert.That(doctor.Ok, Is.True,
            $"could not launch the app: {doctor.Code} — {doctor.Message}. E2E needs a desktop session, and the "
            + "driver must run at the SAME ELEVATION as the app.");
        Assert.That(doctor.Flag("ready"), Is.True, $"driver reports not ready: {doctor.Raw}");

        WaitForDocument(Path.GetFileName(fixturePath));
    }

    /// <summary>
    /// Waits until the window title names the expected document.
    /// </summary>
    /// <remarks>
    /// READY IS NOT OPEN. The driver's readiness answers "is there a usable main window?", and the app reaches
    /// that state BEFORE it has opened the start-up document — it opens the command-line file after the window is
    /// shown, so a failure dialog has an owner. A test that starts asserting on `ready` alone therefore reads the
    /// empty untitled project the shell begins with and gets a real, self-consistent, completely wrong answer:
    /// the first version of this harness saw ten findings from the untitled project instead of the fixture's
    /// hundred and fifty, and every assertion downstream was about the wrong document.
    /// </remarks>
    private static void WaitForDocument(string fileName)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        string lastTitle = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            Envelope status = Run("session", "status");
            lastTitle = TitleOf(status);
            if (lastTitle.StartsWith(fileName, StringComparison.OrdinalIgnoreCase))
                return;
            System.Threading.Thread.Sleep(250);
        }

        Assert.Fail($"the app never opened '{fileName}' — the title bar still reads '{lastTitle}'.");
    }

    /// <summary>
    /// The open modal STACK, topmost first, as the driver reports it — read from the envelope's own
    /// <c>context</c>, which sits beside <c>data</c> rather than inside it.
    /// </summary>
    public static IReadOnlyList<string> OpenModalIds() => ModalIdsIn(RunOk("session", "status"));

    /// <summary>The modal stack an envelope ALREADY reports — every envelope carries the context block.</summary>
    private static IReadOnlyList<string> ModalIdsIn(Envelope envelope) =>
        ContextArray(envelope, "openModals").Select(m => m.GetProperty("id").GetString() ?? string.Empty).ToList();

    /// <summary>One array out of an envelope's context block, empty when the block or the array is absent.</summary>
    private static IEnumerable<JsonElement> ContextArray(Envelope envelope, string name) =>
        envelope.Context.ValueKind == JsonValueKind.Object
        && envelope.Context.TryGetProperty(name, out JsonElement array)
        && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : [];

    /// <summary>
    /// Dismisses whatever modals are open, innermost first — the cleanup every route scenario ends with.
    /// <para>Bounded rather than looped until empty: a modal the driver cannot dismiss would otherwise spin here
    /// for ever, and a scenario that leaves one behind should fail on its own assertion, not hang.</para>
    /// </summary>
    public static void CloseAllModals(int guard = 4)
    {
        // The cancel's OWN envelope reports what is still open, so the loop costs one command per modal rather
        // than a status read between each.
        IReadOnlyList<string> open = OpenModalIds();
        for (int i = 0; i < guard && open.Count > 0; i++)
        {
            open = ModalIdsIn(RunOk("dialog", "cancel"));
        }
    }

    private static string TitleOf(Envelope envelope) =>
        envelope.Context.ValueKind == JsonValueKind.Object
        && envelope.Context.TryGetProperty("windowTitle", out JsonElement title)
        && title.ValueKind == JsonValueKind.String
            ? title.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// Blocks until the panel has a bound result, and returns that state envelope. THE FIRST STEP OF EVERY
    /// PANEL ASSERTION — see the type summary.
    /// </summary>
    public static Envelope WaitForBoundProblems()
    {
        Envelope state = RunOk("problems", "state", "--wait", "--timeout", BoundTimeoutMs);
        Assert.That(state.Flag("bound"), Is.True,
            $"the panel never bound a result: {state.Raw}");
        return state;
    }

    /// <summary>
    /// How long to wait for a bound result, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Deliberately an ORDER below CI's <c>--blame-hang-timeout 120s</c>, because one scenario reaches this gate
    /// three times. At the 30 s it used to be, three waits plus the scenario's own verbs overrun that cap, and
    /// the blame collector then kills the whole test host: the leg ends with a hang dump instead of the
    /// assertion above naming the panel, and every remaining test in the suite is lost with it. What is being
    /// waited on is a 300 ms debounce plus one whole-project validation pass.
    /// </remarks>
    private const string BoundTimeoutMs = "10000";

    /// <summary>The realized rows, as the driver reports them.</summary>
    public static IReadOnlyList<Row> Rows()
    {
        Envelope rows = RunOk("problems", "rows");
        return [.. rows.Field("rows").EnumerateArray().Select(ToRow)];
    }

    /// <summary>One row of a <c>problems rows</c> or <c>problems click</c> envelope.</summary>
    public static Row ToRow(JsonElement r) => new(
        r.GetProperty("index").GetInt32(),
        r.GetProperty("code").GetString() ?? string.Empty,
        r.GetProperty("occurrence").GetString() ?? string.Empty,
        r.GetProperty("severity").GetString() ?? string.Empty,
        r.GetProperty("message").GetString() ?? string.Empty,
        r.GetProperty("element").GetString() ?? string.Empty);

    /// <param name="Occurrence">
    /// The row's per-occurrence identity. <see cref="Code"/> names a GROUP — several codes fire many times over
    /// this fixture — so this is what addresses one row.
    /// </param>
    public sealed record Row(
        int Index, string Code, string Occurrence, string Severity, string Message, string Element);

    /// <summary>
    /// What each tree pane currently has selected, as <c>(tree, name)</c> pairs — read from the driver's own
    /// context block, which every envelope carries.
    /// </summary>
    /// <remarks>
    /// PANE-SPECIFIC on purpose. The shell has two trees with their own selected-node properties, and a
    /// navigation that updated some aggregate instead would move nothing on screen. Reading per pane here is the
    /// end-to-end counterpart of the headless tests' rule never to assert on the aggregate.
    /// </remarks>
    public static IReadOnlyList<Selection> Selections() =>
    [
        .. ContextArray(RunOk("session", "status"), "selections").Select(s => new Selection(
            s.GetProperty("tree").GetString() ?? string.Empty,
            s.GetProperty("name").GetString() ?? string.Empty)),
    ];

    public sealed record Selection(string Tree, string Name);

    /// <summary>
    /// The Installation pane's root label — the shell's mode indicator.
    /// </summary>
    /// <remarks>
    /// In configuration view both panes are rooted at <c>Lokaliteter</c>; entering a block's program re-roots
    /// them on that block. So the root label answers "which mode is this?" without a mode-reading verb, and it is
    /// the same signal the driver's own <c>view.configuration</c> row documents for verifying a mode change.
    /// </remarks>
    public static string PaneRootLabel()
    {
        Envelope dump = RunOk("tree", "dump", "--depth", "1");
        return dump.Field("root").GetProperty("label").GetString() ?? string.Empty;
    }

    /// <summary>The configuration view's root label; anything else means a block's program is open.</summary>
    public const string ConfigurationRootLabel = "Lokaliteter";

    /// <summary>
    /// Whether <c>Controller ▸ Send projekt</c> is offered, read from the menu bar itself.
    /// </summary>
    /// <remarks>
    /// The menu dump reports each row's enabled state but not its REASON, and the reason cannot be read another
    /// way either: it surfaces as a tooltip on a disabled row, or in the status bar after a refused F5 — and the
    /// driver refuses to synthesize F5 at all, because that gesture is the controller transfer. So an E2E test
    /// can observe that the transfer is withheld, not why. Which gate withheld it is proved headlessly.
    /// </remarks>
    public static bool SendProjectEnabled()
    {
        Envelope menu = RunOk("menu", "dump-bar", "--menu", "Controller", "--with-id");
        foreach (JsonElement title in menu.Field("titles").EnumerateArray())
        {
            foreach (JsonElement item in title.GetProperty("children").EnumerateArray())
            {
                if (item.TryGetProperty("id", out JsonElement id) && id.GetString() == "controller.send")
                    return item.GetProperty("enabled").GetBoolean();
            }
        }

        Assert.Fail($"the Controller menu has no controller.send row: {menu.Raw}");
        return false;
    }

    /// <summary>
    /// Kills the application. Called in every teardown, and not only for tidiness: a surviving instance holds
    /// locks on its own binaries, so a later build fails with a file-in-use error that names nothing about tests.
    /// </summary>
    public static void KillApp() => Driver.KillApp();

    /// <summary>
    /// Releases the driver at the END of the assembly's run — the counterpart to the lazy
    /// <see cref="Driver"/> above, called from the one fixture that spans the whole assembly.
    /// </summary>
    /// <remarks>
    /// Without an owner the static simply outlived the run: in headless mode that leaves the Avalonia session,
    /// its dedicated dispatcher thread and the last window alive to process exit, and it left
    /// <c>HeadlessDriver.Dispose</c> unreachable — a documented teardown that never ran, satisfying the analyzer
    /// with a method nothing called.
    /// </remarks>
    internal static void DisposeDriver()
    {
        (driver as IDisposable)?.Dispose();
        driver = null;
    }
}
