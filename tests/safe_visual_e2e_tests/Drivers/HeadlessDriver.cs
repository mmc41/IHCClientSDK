using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Ihc;
using Ihc.Tests.Shared;
using Ihc.Vis;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;

using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// The HEADLESS mode: the same <see cref="MainWindow"/> the shipped application shows, hosted in this process on
/// Avalonia's headless backend, answering the same verb vocabulary as <see cref="AuiProcessDriver"/>.
/// </summary>
/// <remarks>
/// <para><b>What this mode is for, and what it is not.</b> It exists so the scenario paths can be gated by CI,
/// which cannot host a desktop session. It reads Avalonia's own automation peers and view-models directly, so it
/// says nothing about the Avalonia-to-UIA bridge, about real focus, or about <c>aui.ps1</c> — every one of which
/// is part of the system under test in the real mode. See <see cref="IE2EDriver"/>.</para>
///
/// <para><b>A verb this mode cannot honestly answer REFUSES.</b> It does not approximate. A scenario that needs
/// the desktop fails in headless mode with <see cref="UnsupportedCode"/> naming the verb, which is a readable
/// gap; a driver that faked an answer would turn that gap into a false pass, and the whole point of gating this
/// suite is to be told the truth by it.</para>
///
/// <para>The session runs at <see cref="AvaloniaTestIsolationLevel.PerAssembly"/> deliberately — unlike a
/// headless unit test, a driver's verbs are separate dispatches over ONE living window, so the application and
/// its dispatcher have to outlive each call.</para>
/// </remarks>
internal sealed class HeadlessDriver : IE2EDriver, IDisposable
{
    /// <summary>The refusal a verb gives when only a real desktop could answer it.</summary>
    internal const string UnsupportedCode = "NotSupportedHeadless";

    public string Name => "headless (in-process)";

    /// <summary>None: Avalonia's headless backend hosts the window on any platform the runtime reaches.</summary>
    public string? UnmetRequirement => null;

    private HeadlessUnitTestSession? session;
    private MainWindow? window;
    private MainWindowViewModel? shell;
    private ProjectWorkflow? workflow;
    private string? scratchDir;

    /// <summary>
    /// The most recent activation, still running because it opened something modal. Observed on the next verb so
    /// a fault inside it is reported rather than swallowed as an unobserved task.
    /// </summary>
    private Task? pendingActivation;

    private HeadlessUnitTestSession Session => session ??=
        HeadlessUnitTestSession.StartNew(typeof(OpenVisualHeadlessApp), AvaloniaTestIsolationLevel.PerAssembly);

    public E2E.Envelope Run(string[] args)
    {
        string command = "aui " + string.Join(' ', args);
        TestContext.Out.WriteLine($"> {command}  [{Name}]");
        E2E.Envelope envelope = Session
            .Dispatch(() => ExecuteAsync(args), CancellationToken.None)
            .GetAwaiter().GetResult() with { Command = command };
        TestContext.Out.WriteLine($"  {envelope.Code}: {envelope.Message}");
        return envelope;
    }

    public void KillApp() => Session.Dispatch(() =>
    {
        ReleaseShell();
        if (scratchDir is { } dir)
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort temp cleanup */ }
            scratchDir = null;
        }
    }, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Drops the window's content and the objects behind it. THE one release path — <see cref="KillApp"/>,
    /// <see cref="LaunchAsync"/> and <see cref="Dispose"/> all reach it, so a field added to this driver is
    /// released on every route rather than on whichever one its author remembered.
    /// </summary>
    private void ReleaseShell()
    {
        RetireContent();
        shell?.Dispose();
        workflow?.Dispose();
        shell = null;
        workflow = null;
    }

    /// <summary>Closes the window and ends the headless session; the run is over.</summary>
    /// <remarks>
    /// Reached from the assembly's <c>[OneTimeTearDown]</c>, which is the only thing that owns the driver — it
    /// lives behind a lazily-initialised static. Left uncalled, as it was, this released nothing and the session
    /// with its dispatcher thread ran to process exit.
    /// <para>The shell and the workflow are released again HERE, after <see cref="KillApp"/> has already done
    /// it: the analyzer cannot see an owned field released inside a lambda dispatched to another thread, and a
    /// suppression would have hidden a real leak just as effectively as this satisfies it.</para>
    /// </remarks>
    public void Dispose()
    {
        // Only when there is something to tear down. KillApp dispatches through the Session property, which
        // STARTS a session on first touch — so disposing a driver that never ran a verb (every test filtered
        // out, or the suite ignored) would spin up a headless application purely to close it again.
        if (session is not null)
        {
            KillApp();
        }

        shell?.Dispose();
        shell = null;
        workflow?.Dispose();
        workflow = null;
        session?.Dispose();
        session = null;
    }

    // ---- verb dispatch -------------------------------------------------------------------------------------

    /// <summary>
    /// Runs one verb, and NEVER lets a fault escape onto the dispatcher thread.
    /// </summary>
    /// <remarks>
    /// An exception that leaves a dispatched action takes the headless session's loop with it, and every later
    /// verb then blocks for ever on a thread that is gone. That presents as the whole run hanging with no
    /// attribution — measured, and the reason this wrapper exists. Turned into a refusal, the same fault fails
    /// one scenario and names itself.
    /// </remarks>
    private async Task<E2E.Envelope> ExecuteAsync(string[] args)
    {
        if (pendingActivation is { IsFaulted: true } faulted)
        {
            pendingActivation = null;
            return Refuse("ActivationFaulted",
                $"the previous activation failed: {faulted.Exception?.GetBaseException().Message}");
        }

        try
        {
            return await ExecuteCoreAsync(args);
        }
        catch (Exception ex)
        {
            return Refuse("DriverFault", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task<E2E.Envelope> ExecuteCoreAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return Refuse("BadRequest", "no verb given");
        }

        string domain = args[0];
        string verb = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal) ? args[1] : string.Empty;

        // ONE running check, here, rather than the same four-line prologue opening every verb below. The two
        // exempt verbs are the ones that answer BEFORE there is a shell: `doctor` is what creates it, and
        // `session status` reports the empty context a caller uses to discover that nothing is open.
        if (shell is null && (domain, verb) is not (("doctor", _) or ("session", "status")))
        {
            return NotRunning();
        }

        return (domain, verb) switch
        {
            ("doctor", _) => await LaunchAsync(Option(args, "--path")),
            ("session", "status") => Ok(),
            ("problems", "state") => await ProblemsStateAsync(args),
            ("problems", "rows") => ProblemsRows(),
            ("problems", "click") => await ProblemsClickAsync(args),
            ("problems", "sort") => ProblemsSort(Option(args, "--column")),
            ("problems", "toggle") => ProblemsToggle(Option(args, "--tier")),
            ("view", "problems-toggle") => await InvokeAsync(Sync(vm => vm.ToggleProblemsCommand.Execute(null))),
            ("view", "configuration") => await InvokeAsync(Sync(vm => vm.LeaveProgrammingModeCommand.Execute(null))),
            ("edit", "undo") => await InvokeAsync(vm => vm.UndoCommand.ExecuteAsync(null)),
            ("project", "new") => await InvokeAsync(vm => vm.NewCommand.ExecuteAsync(null)),
            ("tree", "dump") => TreeDump(),
            ("capture", "window") => CaptureWindow(),
            ("dialog", "cancel") => DialogCancel(),
            _ => Refuse(UnsupportedCode,
                $"'{domain} {verb}' needs a real desktop; this run is headless. Drive it with the default "
                + "(real-GUI) mode, or assert the same behaviour in safe_visual_tests."),
        };
    }

    // ---- verbs ---------------------------------------------------------------------------------------------

    private async Task<E2E.Envelope> LaunchAsync(string? projectPath)
    {
        KillInPlace();

        scratchDir = Path.Combine(Path.GetTempPath(), "ihc_e2e_headless", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratchDir);

        AvaloniaDialogService dialogs = new();
        ProjectAppService service = new(new IhcSettings(), new Ihc.Vis.Catalog.BuiltInCatalog(), TimeProvider.System);
        RecentProjectsStore recent = new(Path.Combine(scratchDir, "recent.json"));
        // The REAL clock: the panel's debounce is part of what a scenario waits on, and `problems state --wait`
        // is the wait. A fake clock would need the driver to decide when time moves, which is a decision the
        // scenario is supposed to make by asking.
        workflow = new ProjectWorkflow(
            service, recent, dialogs, catalogDir: Path.Combine(scratchDir, "catalog"),
            // Background priority because that is what the composition root uses. App.axaml.cs declares the one
            // marshal for every background result the shell binds, and warns in place that a priority changed in
            // one copy gives two background results two different orderings — this driver is that second copy,
            // and at the default priority it was ordering the panel's binds against input and render differently
            // from the application it is supposed to be standing in for.
            post: action => Dispatcher.UIThread.Post(action, DispatcherPriority.Background));
        shell = new MainWindowViewModel(workflow, dialogs, recent, new NullThemeService());

        // ONE window for the whole session, re-pointed at each launch's shell. A second Show() in a headless
        // session never comes back — measured: the run hangs on the first verb of the second fixture, with no
        // attribution beyond a blame dump. Avalonia headless renders the first window shown and nothing after
        // it, so a per-launch window is not merely wasteful here, it is the deadlock.
        if (window is null)
        {
            window = new MainWindow();
            window.DataContext = shell;
            dialogs.Owner = window;
            window.Show();
        }
        else
        {
            window.DataContext = shell;
            dialogs.Owner = window;
        }

        Dispatcher.UIThread.RunJobs();
        await shell.InitializeAsync(projectPath);
        Dispatcher.UIThread.RunJobs();

        return Ok(new Dictionary<string, object?> { ["ready"] = true });
    }

    private async Task<E2E.Envelope> ProblemsStateAsync(string[] args)
    {
        MainWindowViewModel vm = shell!;

        if (Has(args, "--wait"))
        {
            int timeout = int.TryParse(Option(args, "--timeout"), out int ms) ? ms : 30_000;
            await WaitForBoundAsync(vm, TimeSpan.FromMilliseconds(timeout));
        }

        if (!vm.IsProblemsPanelVisible)
        {
            // The real driver cannot read a panel that is not on screen, and reports it as a control it cannot
            // FIND rather than a state it cannot read. A scenario's [SetUp] uses exactly that refusal to decide
            // whether to re-show the panel, so the two modes have to agree on the word; answering happily here
            // would make the panel's own visibility untestable.
            return Refuse("ControlNotFound",
                "the Problemer panel is hidden; show it again with view.problems.toggle");
        }

        ProblemsPanelViewModel panel = vm.Problems;
        return Ok(new Dictionary<string, object?>
        {
            // Always true on this path: the guard above refuses when the panel is hidden. The key stays because
            // the envelope's shape is the contract, and the real driver reports it too.
            ["visible"] = true,
            ["state"] = panel.State.ToString().ToLowerInvariant(),
            ["bound"] = panel.State != ProblemsState.Validating,
            ["warnings"] = panel.Warnings.Count,
            ["errors"] = panel.Errors.Count,
            ["infos"] = panel.Infos.Count,
            ["fatals"] = panel.Fatals.Count,
        });
    }

    /// <summary>
    /// Waits until a result is bound. Polls the panel's own <c>Idle</c> and its state rather than sleeping a
    /// fixed span: the debounce plus a whole-project run is not a duration the driver gets to guess.
    /// </summary>
    private static async Task WaitForBoundAsync(MainWindowViewModel vm, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            TimeSpan left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero)
            {
                return;
            }

            try
            {
                await vm.Problems.Idle.WaitAsync(left);
            }
            catch (TimeoutException)
            {
                return;
            }

            Dispatcher.UIThread.RunJobs();
            if (vm.Problems.State != ProblemsState.Validating)
            {
                return;
            }

            // Paced against the ~300 ms debounce this is chasing, not against nothing. `Idle` hands out a task
            // that STAYS completed until the next notify, so once it has fired the await above returns at once
            // and this delay is the only thing bounding the loop — at 25 ms that was a spin of a thousand-odd
            // iterations on the dispatcher thread across a full timeout.
            await Task.Delay(100);
        }
    }

    private E2E.Envelope ProblemsRows()
    {
        MainWindowViewModel vm = shell!;

        List<object> rows = [];
        int index = 0;
        foreach (ProblemsPanelRowViewModel row in RealizedRows(vm))
        {
            rows.Add(RowPayload(row, index++));
        }

        return Ok(new Dictionary<string, object?> { ["rows"] = rows });
    }

    /// <summary>
    /// One row as the envelope reports it, for every verb that reports one.
    /// </summary>
    /// <remarks>
    /// Written once because <c>E2E.ToRow</c> reads all six keys with <c>GetProperty</c>, which throws on a
    /// missing one. Keyed by hand in each verb, a column added to <c>problems rows</c> and forgotten in
    /// <c>problems click</c> would fail only on the click path — and only in this mode, which is the one CI
    /// gates.
    /// </remarks>
    private static Dictionary<string, object?> RowPayload(ProblemsPanelRowViewModel row, int index) => new()
    {
        ["index"] = index,
        ["code"] = row.Code,
        ["occurrence"] = row.OccurrenceId,
        ["severity"] = row.TierLabel,
        ["message"] = row.Message,
        ["element"] = row.ElementName,
    };

    /// <summary>
    /// The rows a reader can actually reach: the containers the virtualizing list has REALIZED, in visual order.
    /// </summary>
    /// <remarks>
    /// Not <c>Problems.Rows</c>, which is the whole bound collection. The distinction is load-bearing rather
    /// than pedantic — the corpus fixture carries more findings than fit the panel's default height, and a
    /// scenario that asserts "the rows on screen are all Warnings" is asserting about the realized window into
    /// that collection. Reporting all of them made that assertion fail on a row nobody can see.
    /// </remarks>
    private IEnumerable<ProblemsPanelRowViewModel> RealizedRows(MainWindowViewModel vm)
    {
        ItemsControl? list = window?.GetVisualDescendants()
            .OfType<ItemsControl>()
            .FirstOrDefault(c => Avalonia.Automation.AutomationProperties.GetAutomationId(c) == ProblemsListId);

        return list?.GetRealizedContainers() is { } containers
            ? containers.OrderBy(c => c.Bounds.Top).Select(c => c.DataContext).OfType<ProblemsPanelRowViewModel>()
            : vm.Problems.Rows;
    }

    /// <summary>The list's own automation id, as the view declares it and the GUI suite's audit asserts it.</summary>
    private const string ProblemsListId = "ProblemsList";

    private async Task<E2E.Envelope> ProblemsClickAsync(string[] args)
    {
        MainWindowViewModel vm = shell!;

        string? selector = Option(args, "--row");
        ProblemsPanelRowViewModel? row = vm.Problems.Rows.FirstOrDefault(
            r => r.OccurrenceId == selector || r.Code == selector);
        if (row is null)
        {
            return Refuse("RowNotFound", $"no row addressed by '{selector}'");
        }

        vm.Problems.SelectedRow = row;
        if (Has(args, "--double"))
        {
            // NOT awaited, and that is the contract rather than a shortcut. An activation may open a MODAL
            // dialog, and in-process `ShowDialog` does not return until something closes it — so awaiting here
            // deadlocks the driver against a dialog only a later verb could dismiss. The real driver does not
            // await either: it is another process, so a click returns as soon as it is delivered. The task is
            // kept rather than discarded so a fault in it surfaces on the next verb instead of vanishing.
            pendingActivation = vm.Problems.ActivateRowAsync(row);
        }

        Dispatcher.UIThread.RunJobs();
        return Ok(new Dictionary<string, object?>
        {
            ["clicked"] = RowPayload(row, vm.Problems.Rows.IndexOf(row)),
        });
    }

    private E2E.Envelope ProblemsSort(string? column)
    {
        MainWindowViewModel vm = shell!;

        if (!Enum.TryParse(column, ignoreCase: true, out ProblemsColumn parsed))
        {
            return Refuse("BadRequest", $"unknown column '{column}'");
        }

        List<string> before = [.. vm.Problems.Rows.Select(r => r.OccurrenceId)];
        vm.Problems.SortBy(parsed);
        Dispatcher.UIThread.RunJobs();
        List<string> after = [.. vm.Problems.Rows.Select(r => r.OccurrenceId)];

        return Ok(new Dictionary<string, object?> { ["reordered"] = !before.SequenceEqual(after) });
    }

    private E2E.Envelope ProblemsToggle(string? tier)
    {
        MainWindowViewModel vm = shell!;

        if (!Enum.TryParse(tier, ignoreCase: true, out ProblemsTier parsed))
        {
            return Refuse("BadRequest", $"unknown tier '{tier}'");
        }

        ProblemsTierViewModel row = vm.Problems.Tiers.Single(t => t.Tier == parsed);
        row.IsShown = !row.IsShown;
        Dispatcher.UIThread.RunJobs();
        return Ok();
    }

    /// <summary>
    /// A verb whose whole body is "run this shell command, let the UI settle, report OK".
    /// </summary>
    /// <remarks>
    /// The four verbs that dispatch to it used to be four methods identical but for the command they named, so
    /// the settle-then-report ritual had to stay in step across all of them. Naming the command at the dispatch
    /// table instead puts the one fact each verb carries where a reader looks for it.
    /// </remarks>
    private async Task<E2E.Envelope> InvokeAsync(Func<MainWindowViewModel, Task> run)
    {
        await run(shell!);
        Dispatcher.UIThread.RunJobs();
        return Ok();
    }

    /// <summary>Adapts a synchronous shell command to <see cref="InvokeAsync"/>.</summary>
    private static Func<MainWindowViewModel, Task> Sync(Action<MainWindowViewModel> run) =>
        vm => { run(vm); return Task.CompletedTask; };

    private E2E.Envelope TreeDump()
    {
        MainWindowViewModel vm = shell!;

        TreeNodeViewModel? root = vm.InstallationNodes.FirstOrDefault();
        return Ok(new Dictionary<string, object?>
        {
            ["root"] = new Dictionary<string, object?>
            {
                ["label"] = root?.DisplayName ?? string.Empty,
                ["children"] = root is null
                    ? new List<object>()
                    : [.. root.Children.Select(c => (object)new Dictionary<string, object?> { ["label"] = c.DisplayName })],
            },
        });
    }

    /// <summary>
    /// Dismisses the topmost modal. Supported because <see cref="E2E.CloseAllModals"/> is how every navigation
    /// scenario ends, and because an activation here does not await its dialog — so without this the next
    /// scenario inherits an open window and asserts against it.
    /// </summary>
    private E2E.Envelope DialogCancel()
    {
        Window? top = OpenModalWindows().LastOrDefault();
        if (top is null)
        {
            return Refuse("NoModal", "no modal window is open");
        }

        top.Close();
        Dispatcher.UIThread.RunJobs();
        return Ok();
    }

    private IEnumerable<Window> OpenModalWindows() =>
        (window?.OwnedWindows ?? []).Where(w => w.IsVisible);

    /// <summary>Renders the headless frame to a PNG, so a scenario can keep evidence exactly as the real driver does.</summary>
    private E2E.Envelope CaptureWindow()
    {
        if (window is not { } shown)
        {
            return NotRunning();
        }

        // NOT under scratchDir: KillApp deletes that tree recursively at teardown, so a capture written there was
        // gone before anyone could open it — and evidence nobody can look at is not evidence.
        string captures = Path.Combine(TestContext.CurrentContext.TestDirectory, "E2ECaptures");
        Directory.CreateDirectory(captures);
        string path = Path.Combine(captures, $"capture-{Guid.NewGuid():N}.png");
        using (Avalonia.Media.Imaging.WriteableBitmap? frame = shown.CaptureRenderedFrame())
        {
            if (frame is null)
            {
                return Refuse("CaptureFailed", "the headless surface produced no frame");
            }

            frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }

        TestContext.AddTestAttachment(path, "E2E window capture");

        // The PATH goes in the message, where the real driver puts it and where the scenario reads it from.
        return Envelope(true, "OK", path, new Dictionary<string, object?> { ["path"] = path });
    }

    // ---- envelope construction -----------------------------------------------------------------------------

    private E2E.Envelope NotRunning() =>
        Refuse("AppNotRunning", "no window is hosted; launch first");

    private E2E.Envelope Ok(object? data = null) => Envelope(true, "OK", string.Empty, data);

    private E2E.Envelope Refuse(string code, string message) => Envelope(false, code, message, null);

    /// <summary>
    /// Serializes the envelope and parses it straight back through the SAME reader the process driver uses.
    /// </summary>
    /// <remarks>
    /// The round trip is not waste. Every consumer reads either <c>Data</c> or the <c>context</c> block beside
    /// it, so building the string is the only way both halves are guaranteed to describe the same envelope, and
    /// it makes a shape mismatch with <c>aui.ps1</c> a JSON difference rather than a C# one.
    /// <para>Read back by <see cref="E2E.Envelope.Parse"/> rather than field by field here, so the two drivers
    /// cannot come to disagree about the shape. Reading it here independently is exactly how they did: this one
    /// populated no context, and every reader of the modal stack, the window title and the selections quietly
    /// saw nothing in headless mode.</para>
    /// </remarks>
    private E2E.Envelope Envelope(bool ok, string code, string message, object? data) =>
        E2E.Envelope.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["ok"] = ok,
            ["code"] = code,
            ["message"] = message,
            ["data"] = data ?? new Dictionary<string, object?>(),
            ["context"] = Context(),
        }));

    /// <summary>The block every envelope carries: what document is open, what is selected, what is modal.</summary>
    private Dictionary<string, object?> Context() => new()
    {
        ["windowTitle"] = window?.Title ?? string.Empty,
        ["openModals"] = OpenModals(),
        ["selections"] = Selections(),
    };

    private List<object> OpenModals() =>
    [
        .. OpenModalWindows().Select(w => (object)new Dictionary<string, object?>
        {
            ["id"] = Avalonia.Automation.AutomationProperties.GetAutomationId(w) ?? w.GetType().Name,
            ["title"] = w.Title ?? string.Empty,
        }),
    ];

    private List<object> Selections()
    {
        if (shell is not { } vm)
        {
            return [];
        }

        List<object> selections = [];
        Add(vm.IsInstallationPaneActive ? "InstallationTree" : "FunctionsTree", vm.SelectedNode?.DisplayName);
        return selections;

        void Add(string tree, string? name)
        {
            if (name is not null)
            {
                selections.Add(new Dictionary<string, object?> { ["tree"] = tree, ["name"] = name });
            }
        }
    }

    /// <summary>Retires the previous launch's state. The WINDOW stays; see <see cref="RetireContent"/>.</summary>
    private void KillInPlace()
    {
        foreach (Window modal in OpenModalWindows().ToList())
        {
            modal.Close();
        }

        Dispatcher.UIThread.RunJobs();
        ReleaseShell();
    }

    /// <summary>
    /// Retires the window WITHOUT closing it. Closing the only window ends the headless session's dispatcher
    /// loop, and every later verb then blocks for ever on a thread that has gone — which presents as the whole
    /// run hanging rather than as a failure anyone can read.
    /// </summary>
    private void RetireContent()
    {
        if (window is { } shown)
        {
            shown.DataContext = null;
            Dispatcher.UIThread.RunJobs();
        }
    }

    // ---- argument parsing ----------------------------------------------------------------------------------

    private static string? Option(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static bool Has(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;
}
