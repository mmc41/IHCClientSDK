using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

using Ihc.Tests.Shared;

namespace safe_visual_tests;

/// <summary>
/// The shared harness for every end-to-end test in this suite: it launches the REAL application, drives it
/// exclusively through the <c>aui</c> UI-Automation driver, and kills it again.
///
/// <para><b>Why these tests are <see cref="ExplicitAttribute"/>.</b> They start a desktop application, take over
/// the foreground and cost seconds each. Left in the ordinary suite they would make every unrelated run slower
/// and flakier, and they cannot run at all on a machine without a session to draw on. They are therefore opt-in:
/// the plain suite command skips them and <c>--filter "TestCategory=E2E"</c> is the only way to select them.</para>
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
    /// <summary>The NUnit category every end-to-end test carries, and the only way to select them.</summary>
    public const string Category = "E2E";

    private static string DriverPath() => Path.Combine(
        ProblemsTestData.RepositoryRoot(), ".claude", "skills", "aui-openvisual", "scripts", "aui.ps1");

    /// <summary>A fixture under tests/testdata/projects, as an absolute path the app can open.</summary>
    public static string Fixture(params string[] relativeParts) => Path.Combine(
        new[] { ProblemsTestData.RepositoryRoot(), "tests", "testdata", "projects" }.Concat(relativeParts).ToArray());

    /// <summary>One driver envelope, parsed. Every field the tests read comes from here.</summary>
    public sealed record Envelope(bool Ok, string Code, string Message, JsonElement Data, string Raw)
    {
        /// <summary>The command line that produced it — quoted into failure messages so a red test is reproducible.</summary>
        public string Command { get; init; } = string.Empty;

        public JsonElement Field(string name) =>
            Data.ValueKind == JsonValueKind.Object && Data.TryGetProperty(name, out JsonElement value)
                ? value
                : throw new InvalidOperationException($"envelope for '{Command}' has no data.{name}: {Raw}");

        public int Int(string name) => Field(name).GetInt32();

        public string Text(string name) => Field(name).GetString() ?? string.Empty;

        public bool Flag(string name) => Field(name).GetBoolean();
    }

    /// <summary>
    /// Runs one driver command and returns its envelope. It does NOT assert success: several tests are about a
    /// refusal, and a helper that threw on <c>ok:false</c> would make those unwritable.
    /// </summary>
    public static Envelope Run(params string[] args)
    {
        // Forced to UTF-8 on the way out, not just on the way in. PowerShell encodes a redirected stream with
        // the console's legacy code page, so a Danish letter in an envelope arrives as a stray byte — 'ø' came
        // back as a bare 0x9B, which is a C1 CONTROL CHARACTER inside a JSON string. .NET happens to absorb it
        // as U+FFFD, so assertions comparing two mojibaked strings still matched each other and the corruption
        // stayed invisible; a stricter parser rejects the envelope outright. Since every message this panel
        // shows is Danish, that is not an edge case here.
        string invocation = $"[Console]::OutputEncoding=[Text.Encoding]::UTF8; & {Quote(DriverPath())} "
                            + string.Join(' ', args.Select(Quote));
        // The -Command payload is DOUBLE-quoted for the process argument parser, and everything inside it is
        // single-quoted for PowerShell. Quoting the payload the same way as its contents made the whole line a
        // literal string, which pwsh dutifully echoed instead of running.
        string arguments = $"-NoProfile -Command \"{invocation}\"";

        ProcessStartInfo start = new("pwsh", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = ProblemsTestData.RepositoryRoot(),
        };

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("could not start pwsh");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);

        string command = "aui " + string.Join(' ', args);
        TestContext.Out.WriteLine($"> {command}");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(stdout);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"'{command}' produced no JSON envelope.\nstdout: {stdout}\nstderr: {stderr}");
        }

        JsonElement root = document.RootElement;
        Envelope envelope = new(
            root.GetProperty("ok").GetBoolean(),
            root.GetProperty("code").GetString() ?? string.Empty,
            root.GetProperty("message").GetString() ?? string.Empty,
            root.TryGetProperty("data", out JsonElement data) ? data.Clone() : default,
            stdout)
        { Command = command };

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

    private static string TitleOf(Envelope envelope)
    {
        using JsonDocument document = JsonDocument.Parse(envelope.Raw);
        return document.RootElement.TryGetProperty("context", out JsonElement context)
               && context.ValueKind == JsonValueKind.Object
               && context.TryGetProperty("windowTitle", out JsonElement title)
               && title.ValueKind == JsonValueKind.String
            ? title.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// Blocks until the panel has a bound result, and returns that state envelope. THE FIRST STEP OF EVERY
    /// PANEL ASSERTION — see the type summary.
    /// </summary>
    public static Envelope WaitForBoundProblems()
    {
        Envelope state = RunOk("problems", "state", "--wait", "--timeout", "30000");
        Assert.That(state.Flag("bound"), Is.True,
            $"the panel never bound a result: {state.Raw}");
        return state;
    }

    /// <summary>The realized rows, as the driver reports them.</summary>
    public static IReadOnlyList<Row> Rows()
    {
        Envelope rows = RunOk("problems", "rows");
        return [.. rows.Field("rows").EnumerateArray().Select(r => new Row(
            r.GetProperty("index").GetInt32(),
            r.GetProperty("code").GetString() ?? string.Empty,
            r.GetProperty("severity").GetString() ?? string.Empty,
            r.GetProperty("message").GetString() ?? string.Empty,
            r.GetProperty("element").GetString() ?? string.Empty))];
    }

    public sealed record Row(int Index, string Code, string Severity, string Message, string Element);

    /// <summary>
    /// What each tree pane currently has selected, as <c>(tree, name)</c> pairs — read from the driver's own
    /// context block, which every envelope carries.
    /// </summary>
    /// <remarks>
    /// PANE-SPECIFIC on purpose. The shell has two trees with their own selected-node properties, and a
    /// navigation that updated some aggregate instead would move nothing on screen. Reading per pane here is the
    /// end-to-end counterpart of the headless tests' rule never to assert on the aggregate.
    /// </remarks>
    public static IReadOnlyList<Selection> Selections()
    {
        Envelope status = RunOk("session", "status");
        using JsonDocument document = JsonDocument.Parse(status.Raw);
        if (!document.RootElement.TryGetProperty("context", out JsonElement context)
            || !context.TryGetProperty("selections", out JsonElement selections)
            || selections.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return
        [
            .. selections.EnumerateArray().Select(s => new Selection(
                s.GetProperty("tree").GetString() ?? string.Empty,
                s.GetProperty("name").GetString() ?? string.Empty)),
        ];
    }

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
    public static void KillApp()
    {
        foreach (Process process in Process.GetProcessesByName("ihc_openvisual"))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            catch (Exception ex)
            {
                TestContext.Out.WriteLine($"could not kill a surviving app instance: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// The findings the corpus oracle records for a case, in the oracle's own (production) order. Read at RUN
    /// TIME so an expected value here is never a second, hand-kept copy of a committed file.
    /// </summary>
    /// <param name="caseName">
    /// The oracle's CASE name — the first column, e.g. <c>Project6-Errors</c>. Note it carries no <c>.vis</c>
    /// suffix and is prefixed by a corpus folder (<c>fixture/</c>, <c>authentic/</c>), so a caller passing a
    /// file name would match nothing. That is exactly what happened when this helper was first written, and it
    /// failed LOUDLY rather than silently because every test using it asserts the row count is non-zero first.
    /// </param>
    public static IReadOnlyList<OracleRow> OracleRows(string caseName) =>
    [
        .. ByCase()[caseName].Select(finding => new OracleRow(
            finding.Severity, finding.Code, finding.Category, finding.Locator ?? NoLocator, finding.Message)),
    ];

    /// <summary>
    /// The whole corpus, grouped by case leaf and read ONCE. The files are committed oracles and cannot change
    /// under a run, while a fixture asks for its case repeatedly — so re-opening and re-parsing all eighteen per
    /// question was work no caller wanted. An <see cref="ILookup{TKey, TElement}"/> also answers an unknown case
    /// with an empty sequence rather than throwing, which is the behaviour the previous filter had.
    /// <para>
    /// Assigned on first use rather than in a field initializer: a missing or corrupt oracle then surfaces as
    /// that reader's own exception on the test that asked, instead of a type-initializer failure on every other
    /// test this support class serves.
    /// </para>
    /// </summary>
    private static ILookup<string, RecordedFinding>? _byCase;

    private static ILookup<string, RecordedFinding> ByCase() =>
        _byCase ??= FindingOracles.ReadAll().ToLookup(finding => CaseLeaf(finding.Case));

    /// <summary>
    /// A case name without its corpus folder, so a caller may pass <c>Project6-Errors</c> for a case the oracle
    /// records as <c>fixture/Project6-Errors</c>.
    /// <para>
    /// Load-bearing, and quietly so: both classes that consume these rows are <c>[Explicit]</c>, so dropping
    /// this would not fail any default run — it would simply match nothing, and every assertion built on an
    /// empty list would pass. That is why the non-explicit row-count test exists beside it.
    /// </para>
    /// </summary>
    private static string CaseLeaf(string caseName) =>
        caseName.LastIndexOf('/') is var slash && slash >= 0 ? caseName[(slash + 1)..] : caseName;

    /// <summary>
    /// What a row shows for a finding that names no element. The oracle records absence as a MISSING attribute,
    /// which reads back as null; these rows keep the non-null shape their four consumers were written against.
    /// </summary>
    private const string NoLocator = "-";

    /// <summary>One recorded finding: the oracle's own six columns.</summary>
    public sealed record OracleRow(string Severity, string Code, string Category, string Locator, string Message);

    /// <summary>The rule ids the oracle records for a case, in production order.</summary>
    public static IReadOnlyList<string> OracleCodes(string caseName) =>
        [.. OracleRows(caseName).Select(r => r.Code)];

    /// <summary>
    /// Quotes an argument for the <c>-Command</c> string. Single quotes, because the whole invocation is itself
    /// double-quoted for the process arguments, and PowerShell does not expand inside single quotes — so a
    /// fixture path or a Danish row text cannot be re-interpreted as syntax.
    /// </summary>
    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
}
