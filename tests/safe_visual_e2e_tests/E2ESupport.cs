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

    /// <summary>
    /// The run parameter choosing WHICH real-GUI driver drives the desktop. Ignored when
    /// <see cref="HeadlessParameter"/> is set, which has no desktop to drive.
    /// </summary>
    /// <example>
    /// <code>dotnet test … -- 'TestRunParameters.Parameter(name="driver",value="uia")'</code>
    /// </example>
    public const string DriverParameter = "driver";

    /// <summary>The suite's own in-process driver, over Windows UI Automation.</summary>
    public const string UiaDriverName = "uia";

    /// <summary>
    /// What a run gets when it names no driver: the suite's OWN driver, which needs nothing outside this
    /// repository.
    /// </summary>
    public const string DefaultDriverName = UiaDriverName;

    private static IE2EDriver? driver;

    /// <summary>
    /// The driver this run uses, decided once. Held statically because the headless one owns a window and an
    /// Avalonia dispatcher that outlive a single fixture, exactly as the real one owns a process.
    /// </summary>
    internal static IE2EDriver Driver => driver ??=
        CreateDriver(Headless, TestContext.Parameters.Get(DriverParameter, DefaultDriverName));

    /// <summary>
    /// Which driver a given pair of run parameters selects. Separate from <see cref="Driver"/> so the choice can
    /// be asserted without a live application, and so a typo in the parameter fails LOUDLY rather than silently
    /// falling back to the default.
    /// </summary>
    /// <param name="headless">Whether the run drives the in-process window. Wins over <paramref name="requested"/>.</param>
    /// <param name="requested">The requested real-GUI driver.</param>
    internal static IE2EDriver CreateDriver(bool headless, string requested)
    {
        if (headless)
        {
            return new HeadlessDriver();
        }

        return requested switch
        {
            // D16: the type is Windows-only at COMPILE time, so off Windows there is nothing to construct —
            // the run still needs a driver object to ask for its unmet requirement, which is what the stub is.
            UiaDriverName when OperatingSystem.IsWindowsVersionAtLeast(6, 1) => new UiaDriver(),
            UiaDriverName => new UnavailableDriver(
                $"{UiaDriverName} (real GUI)", DriverRequirements.NeedsWindowsUiAutomation()),
            _ => throw new ArgumentException(
                $"unknown driver '{requested}': this suite drives the desktop through '{UiaDriverName}', or "
                + "runs in-process with "
                + $"-- 'TestRunParameters.Parameter(name=\"{HeadlessParameter}\",value=\"true\")'",
                nameof(requested)),
        };
    }

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
    /// What a traced command line reads as. It names the SUITE's verb vocabulary, which every driver answers,
    /// and deliberately not a tool: there is no executable to retype it into, and a prefix that named one sent
    /// a reader to the <c>aui-openvisual</c> skill — which this suite does not use, and which
    /// <see cref="SkillIndependenceGuard"/> exists to keep it from using.
    /// </summary>
    private const string CommandPrefix = "e2e ";

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
        string command = CommandPrefix + string.Join(' ', args);
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

        string fileName = Path.GetFileName(fixturePath);
        WaitForDocument(fileName);
        AssertStartedClean(fileName);
    }

    /// <summary>
    /// Fails the launch when the application faulted while starting up or opening its document, and otherwise
    /// makes that clean start the baseline for the first scenario.
    /// </summary>
    /// <remarks>
    /// Asserted HERE, against zero, because this is the only window in which anyone looks. The count runs from
    /// process start and is never reset — the application keeps its start-up faults on purpose, as the set
    /// with no other record — so a baseline merely TAKEN after the open would fold every one of them into the
    /// number all later comparisons subtract, and no scenario would ever report them. Failing the launch also
    /// charges them to the launch, which is where they belong, rather than to whichever scenario runs first. A
    /// fixture that launches inside its test body gets its baseline from here and needs no arrangement of its
    /// own.
    /// </remarks>
    private static void AssertStartedClean(string fileName)
    {
        (FaultReading? reading, string? refusal) = ReadFaults();
        if (reading is null)
        {
            Assert.Fail($"the application's fault record could not be read after launching on '{fileName}', so "
                + $"no scenario on this launch could say whether it faulted: {refusal}");
            return;
        }

        Assert.That(reading.Appended, Is.Zero,
            $"the application recorded {reading.Appended} internal fault(s) while starting up and opening "
            + $"'{fileName}'; the most recent was '{reading.LastCode}'. Read it as a product signal about "
            + "start-up, and triage it before treating it as a test defect.");
        faultBaseline = reading;
        baselineRefusal = null;
    }

    /// <summary>One reading of the application's fault record: the count and the last code, from ONE moment.</summary>
    private sealed record FaultReading(long Appended, string LastCode);

    /// <summary>What the last baseline read, or null when the driver refused it.</summary>
    private static FaultReading? faultBaseline;

    /// <summary>Why the last baseline could not be taken, kept so the assertion that needs it can say so.</summary>
    private static string? baselineRefusal;

    /// <summary>
    /// Marks the point a later <see cref="AssertNoNewFaults"/> compares against. Called by
    /// <see cref="Launch"/> and again before each scenario, because three of this suite's four fixtures share
    /// ONE launch across their tests — without a per-test baseline the first scenario's faults would be
    /// re-reported against every scenario after it.
    /// </summary>
    /// <remarks>
    /// A refusal here is recorded rather than asserted: a scenario that launches inside its own body has no
    /// application to ask at set-up time, and its baseline comes from <see cref="Launch"/> instead. The teardown
    /// is where a baseline that is still missing becomes a failure.
    /// </remarks>
    public static void TakeFaultBaseline() => (faultBaseline, baselineRefusal) = ReadFaults();

    /// <summary>
    /// Fails when the application recorded an internal fault since the last baseline — and fails just as hard
    /// when nothing can say whether it did.
    /// </summary>
    /// <remarks>
    /// <para><b>OBSERVED, not CAUSED.</b> A non-zero delta says a fault was seen during this scenario's window,
    /// never that this scenario produced it. The application says so itself: the unobserved-task layer fires
    /// when the GC reaches a faulted task, which may be long after the fault and after any number of user
    /// actions, so a task dropped by one scenario can be discovered inside the next and charged to it. Triage a
    /// red one against the whole fixture's route, not only its own. It is a smoke detector, not a forensic
    /// tool.</para>
    ///
    /// <para><b>A driver that cannot report the count FAILS this assertion</b>, naming the refusal. Both
    /// drivers publish the count from the moment the application is launched, so a refusal is never a mode
    /// that cannot see faults: it is the observer broken — a parser that rejected the snapshot, an application
    /// started without the test surface, a transport that stopped answering. Returning quietly there would
    /// switch off every fault assertion in the suite while every scenario stayed green, which is precisely the
    /// false green this assertion exists to remove.</para>
    /// </remarks>
    public static void AssertNoNewFaults()
    {
        if (faultBaseline is not { } before)
        {
            Assert.Fail("no fault baseline was taken for this scenario, so nothing can say whether it faulted: "
                + baselineRefusal);
            return;
        }

        (FaultReading? after, string? refusal) = ReadFaults();
        if (after is null)
        {
            Assert.Fail("the application's fault record could not be read at the end of this scenario, so its "
                + $"fault assertion is unverifiable: {refusal}");
            return;
        }

        Assert.That(after.Appended, Is.EqualTo(before.Appended),
            $"the application recorded {after.Appended - before.Appended} internal fault(s) while this scenario "
            + $"ran; the most recent was '{after.LastCode}'. Something faulted on this route — read it as a "
            + "product signal and triage it before treating it as a test defect.");
    }

    /// <summary>
    /// The count and the last code in ONE reading, so a failure message cannot quote a code from a different
    /// moment than the count it is explaining — or, when the driver refuses the verb, the refusal in words.
    /// </summary>
    private static (FaultReading? Reading, string? Refusal) ReadFaults()
    {
        Envelope faults = Run("session", "faults");
        if (!faults.Ok)
        {
            return (null, $"'{faults.Command}' was refused with {faults.Code}: {faults.Message}");
        }

        if (faults.Data.ValueKind != JsonValueKind.Object
            || !faults.Data.TryGetProperty("appended", out JsonElement appended))
        {
            return (null, $"'{faults.Command}' answered without a count: {faults.Raw}");
        }

        string lastCode =
            faults.Data.TryGetProperty("last", out JsonElement code) && code.ValueKind == JsonValueKind.String
                ? code.GetString() ?? "none"
                : "none";
        return (new FaultReading(appended.GetInt64(), lastCode), null);
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
        string lastSeen = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            Envelope status = Run("session", "status");
            // What the application SAYS is open, when it publishes it; the title bar only when it does not.
            // The title is a rendering, and reading a filename off the front of one is an inference: it also
            // carries the dirty bullet and the application's own name, and the first version of this harness
            // matched the untitled project the shell starts with and then asserted about the wrong document.
            string document = ContextText(status, "document");
            lastSeen = document.Length > 0 ? document : TitleOf(status);
            if (document.Length > 0
                ? string.Equals(document, fileName, StringComparison.OrdinalIgnoreCase)
                : lastSeen.StartsWith(fileName, StringComparison.OrdinalIgnoreCase))
                return;
            System.Threading.Thread.Sleep(250);
        }

        Assert.Fail($"the app never opened '{fileName}' — it still reports '{lastSeen}'.");
    }

    /// <summary>One string out of an envelope's context block, empty when the block or the field is absent.</summary>
    private static string ContextText(Envelope envelope, string name) =>
        envelope.Context.ValueKind == JsonValueKind.Object
        && envelope.Context.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

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
