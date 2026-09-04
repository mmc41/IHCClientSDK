using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;

using Ihc.UiAutomation;
using Ihc.Vis.Validation;
using ihc_openvisual.Configuration;
using ihc_openvisual.ViewModels;

using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// THE REAL MODE: launches <c>ihc_openvisual.exe</c> and drives it through Windows UI Automation, in this
/// process.
/// </summary>
/// <remarks>
/// <para>What this buys over the headless driver is the whole reason the suite exists: it reads what UI
/// Automation actually publishes, clicks with real pointer input at real screen coordinates, and sees the
/// desktop's own modal stack — so it can fail on a defect in the Avalonia-to-UIA bridge, which no in-process
/// test can reach.</para>
///
/// <para><b>Mechanics live in <c>shared/ihc_uiautomation</c>; POLICY lives here.</b> The toolkit knows how to
/// find an element, press a chord and take a window; it knows nothing about OpenVisual. The verb vocabulary,
/// the result envelope, the automation ids and the gesture grammar are this file's, because none of them means
/// anything outside this application.</para>
///
/// <para>The envelope is built exactly as <see cref="HeadlessDriver"/> builds it — serialized from a dictionary
/// and parsed straight back through <see cref="E2E.Envelope.Parse"/> — so the two drivers cannot drift over
/// what an envelope looks like.</para>
/// </remarks>
[SupportedOSPlatform("windows6.1")]
internal sealed partial class UiaDriver : IE2EDriver, IDisposable
{
    /// <summary>The application's process name, without the extension, as <c>Process</c> reports it.</summary>
    private const string ProcessName = "ihc_openvisual";

    /// <summary>How long a launched application has to publish a driveable window.</summary>
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long a killed application has to actually go.</summary>
    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How long an action is given to reach the screen before the next verb reads the result. The application
    /// answers a gesture asynchronously, so this belongs in the driver rather than in every scenario.
    /// </summary>
    private static readonly TimeSpan ClickSettle = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// How long an ACTIVATION is given, as opposed to a selection.
    /// </summary>
    /// <remarks>
    /// A single click moves a selection and nothing else, which is immediate. A double click is the gesture
    /// that makes the application DO something — switch mode and rebuild a tree, or compose and open a dialog —
    /// and the next verb reads the result of that work. Measured on the desktop: a quarter of a second is
    /// enough for the selection and not for the work, which reads as an activation that silently did nothing.
    /// </remarks>
    private static readonly TimeSpan ActivationSettle = TimeSpan.FromMilliseconds(1500);

    private UiaSession? session;
    private Process? app;
    private UiaElement? mainWindow;

    private readonly EnvelopeWriter envelopes;

    internal UiaDriver() => envelopes = new EnvelopeWriter(Context);

    public string Name => "uia (real GUI)";

    /// <summary>Windows only, and the type says so — off Windows the suite gets a stub instead of this.</summary>
    public string? UnmetRequirement => null;

    private UiaSession Session => session ??= new UiaSession();

    public E2E.Envelope Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // The command stamp and the console trace are NOT written here. E2E.Run does both, at the one call
        // site every driver passes through, and says so — a second copy here printed every verb twice.
        try
        {
            // THE WHOLE VERB runs in one DPI context, and that is not a detail. UI Automation reports
            // rectangles to a DPI-UNAWARE client in virtualized coordinates — scaled down by the display's
            // factor — while synthesized input is interpreted in the calling thread's own context. Read a
            // rectangle outside the scope and click inside it and the two are different spaces: on this
            // machine's 175% display the click lands at four sevenths of where the element actually is.
            // Measuring and acting inside ONE scope is what makes them the same space, whatever the scaling.
            using DpiScope scope = DpiScope.Enter();
            return Execute(args);
        }
        catch (Exception fault) when (fault is not NUnit.Framework.SuccessException)
        {
            // A driver fault is an ENVELOPE, not an exception: a scenario asserts on codes, and an escaping
            // exception would report the driver's stack instead of what the application did.
            return Refuse("DriverFault", $"{fault.GetType().Name}: {fault.Message}");
        }
    }

    private E2E.Envelope Execute(string[] args)
    {
        if (args.Length == 0)
        {
            return Refuse("InvalidInput", "no verb given");
        }

        string domain = args[0];
        string verb = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal) ? args[1] : string.Empty;

        // One running check, as the headless driver does it. `doctor` is what creates the application and
        // `session status` reports the empty context a caller uses to discover that nothing is open.
        if (mainWindow is null && (domain, verb) is not (("doctor", _) or ("session", "status")))
        {
            return NotRunning();
        }

        return (domain, verb) switch
        {
            ("doctor", _) => Launch(DriverArguments.Option(args, "--path")),
            ("session", "status") => Ok(),
            ("capture", "window") => CaptureWindow(),
            ("problems", "state") => ProblemsState(args),
            ("problems", "rows") => ProblemsRows(),
            ("problems", "click") => ProblemsClick(args),
            ("view", "problems-toggle") => ToggleProblemsPanel(),
            ("tree", "dump") => TreeDump(args),
            ("node", "select") => NodeSelect(args),
            ("node", "get-properties") => InvokeMenuItem(AutomationIds.MenuEdit, "node.properties"),
            ("projectInfo", "get") => InvokeMenuItem(AutomationIds.MenuDocumentation, "project.info"),
            ("view", "configuration") => SendToShell(new UiaGesture(UiaKey.Escape), "{ESC}"),
            ("edit", "undo") => SendToShell(new UiaGesture(UiaKey.Z, UiaModifiers.Control), "^z"),
            ("key", "send") => KeySend(args),
            ("dialog", "read") => DialogRead(),
            ("dialog", "select-item") => DialogSelectItem(args),
            ("dialog", "click") => DialogClick(args),
            ("dialog", "cancel") => DialogCancel(),
            _ => Refuse("InvalidInput", $"'{domain} {verb}' is not a verb this driver implements"),
        };
    }

    // ---- verbs -------------------------------------------------------------------------------------------

    /// <summary>
    /// Starts the application on a fixture, after making sure no earlier instance is still holding the screen.
    /// </summary>
    private E2E.Envelope Launch(string? projectPath)
    {
        string executable = ExecutablePath();
        if (!File.Exists(executable))
        {
            return Refuse("PreconditionMissing",
                $"no application at {executable} — build applications/ihc_openvisual first");
        }

        // Any survivor is killed BEFORE launching, not after: two instances mean two candidate windows, and the
        // driver would attach to whichever the window manager happened to list first.
        KillApp();

        List<string> arguments = [];
        if (projectPath is { Length: > 0 })
        {
            arguments.Add(projectPath);
        }

        LaunchedApp launched = AppLauncher.Start(
            Session, executable, arguments, IsDriveableShell, LaunchTimeout);

        app = launched.Process;
        mainWindow = launched.MainWindow;

        if (mainWindow is null)
        {
            string how = launched.Process.HasExited
                ? $"it exited with code {launched.Process.ExitCode}"
                : $"it is still running but published no {AutomationIds.InstallationTree}";
            return Refuse("AppNotRunning",
                $"{Path.GetFileName(executable)} produced no driveable window within "
                + $"{LaunchTimeout.TotalSeconds:0} s — {how}");
        }

        return Ok(new Dictionary<string, object?> { ["ready"] = true });
    }

    /// <summary>
    /// What makes a window the one to drive: it publishes the installation tree, which only a fully built shell
    /// does. A splash or a half-constructed window passes "is visible" and fails this.
    /// </summary>
    private static bool IsDriveableShell(UiaElement window) =>
        window.FindFirstById(AutomationIds.InstallationTree) is not null;

    /// <summary>Saves the application window as a PNG and attaches it to the test result.</summary>
    private E2E.Envelope CaptureWindow()
    {
        if (mainWindow is not { } shown)
        {
            return NotRunning();
        }

        // The capture reads the composited desktop, so anything in front of the window is captured INSTEAD of
        // it. Taking the foreground first is part of capturing, not a separate concern — and failing to take it
        // has to refuse, because the PNG that would be written is indistinguishable from a real one: it is a
        // valid image of the window's rectangle, attached to the test result, showing another application. A
        // capture is evidence, and evidence nobody can tell is wrong is worse than none.
        if (!Foreground.Acquire(shown.NativeWindowHandle))
        {
            return Refuse("PreconditionMissing",
                "the application is not in the foreground, so a capture of its rectangle would show whatever is "
                + "in front of it");
        }

        string captures = Path.Combine(TestContext.CurrentContext.TestDirectory, "E2ECaptures");
        string path = Path.Combine(captures, $"capture-{Guid.NewGuid():N}.png");

        ScreenCapture.Save(shown.BoundingRectangle, path);
        TestContext.AddTestAttachment(path, "E2E window capture");

        // The PATH goes in the message, where the scenarios read it from.
        return envelopes.Build(true, "OK", path, new Dictionary<string, object?> { ["path"] = path });
    }

    // ---- the Problemer panel -----------------------------------------------------------------------------

    /// <summary>
    /// The panel's four-state model, read off the two things it shows.
    /// </summary>
    /// <remarks>
    /// <b>The ORDER of the tests is the contract, not an implementation detail.</b> <c>validating</c> wins over
    /// everything: it means no result is bound yet, and reporting that as <c>clean</c> is exactly the lie the
    /// panel itself refuses to tell. <c>stale</c> is next, because a stale panel is showing a PREVIOUS result.
    /// Only then do the two up-to-date states divide.
    /// </remarks>
    private E2E.Envelope ProblemsState(string[] args)
    {
        if (DriverArguments.Has(args, "--wait"))
        {
            int milliseconds = int.TryParse(DriverArguments.Option(args, "--timeout"), out int given)
                ? given
                : 30_000;
            WaitForBound(TimeSpan.FromMilliseconds(milliseconds));
        }

        if (Panel() is not { } panel)
        {
            return PanelHidden();
        }

        string state = ReadState(panel);

        return Ok(new Dictionary<string, object?>
        {
            // Always true on this path: the guard above refuses when the panel cannot be found at all.
            ["visible"] = true,
            ["state"] = state,
            ["bound"] = ProblemsStates.IsBound(state),
            ["warnings"] = TierCount(panel, ProblemsTier.Warning),
            ["errors"] = TierCount(panel, ProblemsTier.Error),
            ["infos"] = TierCount(panel, ProblemsTier.Info),
            ["fatals"] = TierCount(panel, ProblemsTier.Fatal),
        });
    }

    /// <summary>
    /// The panel's state, from the two things it shows.
    /// </summary>
    /// <remarks>
    /// <b>The ORDER is the contract.</b> <c>validating</c> wins over everything: it means no result is bound
    /// yet, and reporting that as <c>clean</c> is exactly the lie the panel itself refuses to tell.
    /// <c>stale</c> is next, because a stale panel is showing a PREVIOUS result. Only then do the two
    /// up-to-date states divide.
    ///
    /// <para>A VISIBLE BUT EMPTY sentence means stale, and reading it any other way is how an empty
    /// re-validating panel gets reported as having findings. The view-model gives Validating and Clean their
    /// own words and every other state the empty string, and the sentence is shown only while there are no
    /// rows — so "shown, and says nothing" can only be a panel that is between results.</para>
    /// </remarks>
    private static string ReadState(UiaElement panel)
    {
        UiaElement? stateText = panel.FindFirstById(AutomationIds.ProblemsStateText);
        bool stateShown = stateText is { IsOffscreen: false };
        string text = stateText?.Name ?? string.Empty;

        // The spinner being on screen is what "a run is in flight" looks like from outside — and it is the
        // ONLY such signal once the panel has rows, because the sentence is hidden then.
        bool busy = panel.FindFirstById(AutomationIds.ProblemsSpinner) is { IsOffscreen: false };

        return
            stateShown && text == ProblemsPanelViewModel.ValidatingText ? ProblemsStates.Validating
            : busy || (stateShown && text.Length == 0) ? ProblemsStates.Stale
            : stateShown && text == ProblemsPanelViewModel.CleanText ? ProblemsStates.Clean
            : ProblemsStates.Findings;
    }

    /// <summary>
    /// Polls until the panel is at REST — neither validating nor re-validating.
    /// </summary>
    /// <remarks>
    /// <b>It must watch the spinner, not only the sentence.</b> The sentence is bound to
    /// <c>IsVisible="{Binding !Rows.Count}"</c>, so on any fixture that has findings — which is every scenario
    /// here — it is hidden and says nothing at all. A wait that only asked the sentence returned on its FIRST
    /// poll, every time, and the verb after it then read the counts from BEFORE the edit it was waiting for.
    /// The spinner is the observable that survives a panel with rows in it.
    /// </remarks>
    private void WaitForBound(TimeSpan timeout)
    {
        long deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (Panel() is not { } panel)
            {
                return;
            }

            if (ProblemsStates.IsBound(ReadState(panel)))
            {
                return;
            }

            Thread.Sleep(100);
        }
    }

    /// <summary>A tier's count, read off the number the panel publishes as that toggle's accessible name.</summary>
    private static int? TierCount(UiaElement panel, ProblemsTier tier)
    {
        string id = AutomationIds.ProblemsCountPrefix + tier.ToString().ToLowerInvariant();
        return panel.FindFirstById(id) is { } count && int.TryParse(count.Name, out int value) ? value : null;
    }

    private E2E.Envelope ProblemsRows()
    {
        if (RowList() is not { } list)
        {
            return PanelHidden();
        }

        List<object> rows = [];
        int index = 0;
        foreach (UiaElement row in RowElements(list))
        {
            rows.Add(RowPayload(row, index++));
        }

        return Ok(new Dictionary<string, object?> { ["rows"] = rows });
    }

    /// <summary>
    /// Clicks one row with REAL pointer input, scrolling it into view first.
    /// </summary>
    /// <remarks>
    /// A real click, not an Invoke: this panel distinguishes a single click, which moves only its own
    /// selection, from an activation, which reveals the element and carries on to the fix. Only synthesized
    /// input can tell those apart.
    /// </remarks>
    private E2E.Envelope ProblemsClick(string[] args)
    {
        if (DriverArguments.Option(args, "--row") is not { Length: > 0 } selector)
        {
            return Refuse("InvalidInput", "problems click needs --row <index|occurrence|code|text>");
        }

        if (RowList() is not { } list)
        {
            return PanelHidden();
        }

        if (mainWindow is not { } shell || !Foreground.Acquire(shell.NativeWindowHandle))
        {
            return NotForeground();
        }

        (UiaElement? found, string search) = FindRow(list, selector);
        if (found is not { } target)
        {
            // Says what it DID see, and HOW IT LOOKED. "No row matches" alone cannot distinguish a wrong
            // selector from a list whose rows were never realized, nor either of those from a scroll that
            // moved nothing — and the three need opposite fixes.
            IReadOnlyList<UiaElement> seen = RowElements(list);
            string sample = seen.Count == 0
                ? "the list published no rows at all"
                : "rows seen: " + string.Join(", ", seen.Take(5).Select(row => $"{row.AutomationId}/{row.ItemStatus}"));
            return Refuse("RowNotFound", $"no row matches '{selector}' among {seen.Count}; {sample}; {search}");
        }

        _ = target.ScrollIntoView();

        // Measured AFTER the list has come to rest. The search above just moved the viewport, and a rectangle
        // read while it is still settling describes where the row WAS — so the click lands a row or two off,
        // which reads as an activation that hit the wrong finding rather than as a timing fault.
        Thread.Sleep(ScrollSettle);

        Rectangle rect = target.BoundingRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return Refuse("ControlNotFound", "the row has no clickable bounds");
        }

        // A row can be REALIZED without being on screen — virtualization keeps a buffer either side of the
        // viewport, and this list exposes no ScrollItem pattern to bring one in. Its rectangle is then real
        // but outside the list, so a click there lands on the tree, the panel's chrome, or another application
        // entirely, and the scenario goes on to assert about a navigation that never happened. Refusing is the
        // only honest answer; clicking is worse than not clicking.
        if (target.IsOffscreen || !list.BoundingRectangle.Contains(rect.X, rect.Y + (rect.Height / 2)))
        {
            return Refuse("PreconditionMissing",
                $"the row for '{selector}' is realized but outside the visible list, so it cannot be clicked "
                + "where it claims to be");
        }

        // Capped at 60 px from the left edge: a row spans the panel, and its midpoint can land on a later
        // column — or past the window — on a wide panel. Near the left edge is where a person clicks a row.
        int x = rect.X + Math.Min(60, rect.Width / 2);
        int y = rect.Y + (rect.Height / 2);

        bool activating = DriverArguments.Has(args, "--double");
        if (!Mouse.Click(x, y, activating ? 2 : 1))
        {
            return NotInjected(activating ? "a double click" : "a click");
        }

        // The application answers a click asynchronously; the next verb reads the result, so the settle belongs
        // here rather than in every scenario.
        Thread.Sleep(activating ? ActivationSettle : ClickSettle);

        return Ok(new Dictionary<string, object?>
        {
            ["clicked"] = RowPayload(target, 0),
            ["point"] = new Dictionary<string, object?> { ["x"] = x, ["y"] = y },
        });
    }

    /// <summary>
    /// The row a selector names, paging the list to realize rows that are not on screen yet.
    /// </summary>
    /// <remarks>
    /// A virtualizing list publishes only what it has realized, so a row further down does not exist to search
    /// until the list has been scrolled to it. Paging with the Scroll pattern rather than Page Down: paging by
    /// key moves the SELECTION, and in this application a selection is wired to navigation.
    /// </remarks>
    private static (UiaElement? Row, string Diagnostics) FindRow(UiaElement list, string selector)
    {
        UiaElement? scroller = Scrollable(list);
        UiaElement? bar = scroller is null ? VerticalScrollBar(list) : null;
        Rectangle listBounds = list.BoundingRectangle;

        // What the search had to work with, reported whether or not it succeeds. Without this a failure cannot
        // be told apart from a list that was never scrollable in the first place.
        string how =
            scroller is not null ? $"scrolling {scroller.ControlType}/'{scroller.AutomationId}'"
            : bar is not null ? $"paging the {bar.ControlType} over {bar.Range?.Maximum ?? 0:0} of travel"
            : $"no scrollable and no scroll bar under {AutomationIds.ProblemsList}; "
              + DescribeScrollCandidates(list);
        how += $"; list bounds {listBounds}";

        // An INDEX addresses the rows that are REALIZED RIGHT NOW and nothing else — past the realized window
        // "row 3" would name a different row on every page. Answered before any scrolling, so it cannot drift.
        if (int.TryParse(selector, out int wanted))
        {
            IReadOnlyList<UiaElement> realized = RowElements(list);
            return wanted >= 0 && wanted < realized.Count
                ? (realized[wanted], how)
                : (null, $"{how}; index {wanted} is outside the {realized.Count} realized rows");
        }

        // SWEEP UP, then DOWN. The upward sweep is the rewind — and it SEARCHES while it rewinds, so nothing
        // depends on the rewind being believed. That matters: this list keeps its selected row in view and
        // pulls itself back, and its scroll bar reports a position the rows do not agree with, so a search
        // that rewound first and then only looked downward missed everything ABOVE the selected row — which,
        // after an activation, is where the first row of the panel sits.
        if (Sweep(list, selector, scroller, bar, listBounds.Height, up: true) is { } above)
        {
            return (above, how);
        }

        if (Sweep(list, selector, scroller, bar, listBounds.Height, up: false) is { } below)
        {
            return (below, how);
        }

        return (null, $"{how}; swept the whole list in both directions");
    }

    /// <summary>
    /// Scans the list in one direction, page by page, until a row matches or the view stops changing.
    /// </summary>
    private static UiaElement? Sweep(
        UiaElement list, string selector, UiaElement? scroller, UiaElement? bar, int viewport, bool up)
    {
        string previous = string.Empty;

        for (int page = 0; page < MaxScrollPages; page++)
        {
            IReadOnlyList<UiaElement> rows = RowElements(list);
            foreach (UiaElement row in rows)
            {
                if (Matches(row, selector))
                {
                    return row;
                }
            }

            // What is realized right now. When a scroll does not change it, this end of the list is reached;
            // the ROWS are the honest signal, because the scroll bar is not.
            string realized = rows.Count == 0
                ? string.Empty
                : $"{rows[0].ItemStatus}|{rows[^1].ItemStatus}|{rows.Count}";

            if (realized == previous || !Move(list, scroller, bar, viewport, up))
            {
                return null;
            }

            previous = realized;
            Thread.Sleep(ScrollSettle);
        }

        return null;
    }

    /// <summary>Moves the list one page, by whichever mechanism this control actually offers.</summary>
    private static bool Move(UiaElement list, UiaElement? scroller, UiaElement? bar, int viewport, bool up)
    {
        if (scroller is not null)
        {
            return scroller.ScrollPage(down: !up);
        }

        if (bar?.Range is { } range)
        {
            double step = Math.Max(range.LargeChange, viewport * PageOverlap);
            if (step <= 0)
            {
                step = (range.Maximum - range.Minimum) / 10;
            }

            double next = up
                ? Math.Max(range.Minimum, range.Value - step)
                : Math.Min(range.Maximum, range.Value + step);

            return Math.Abs(next - range.Value) > 0.5 && bar.SetRangeValue(next);
        }

        return WheelOver(list, up ? WheelNotchesPerPage : -WheelNotchesPerPage);
    }

    /// <summary>
    /// What actually scrolls a list: the list itself if it can, otherwise the scrollable inside its template.
    /// </summary>
    /// <remarks>
    /// A themed list gives its own peer no Scroll pattern and puts one on the ScrollViewer inside the control
    /// template — which the control-view walker hides, so it has to be reached through an unfiltered
    /// descendant query. Null when nothing here scrolls, and the caller falls back to the wheel.
    /// </remarks>
    private static UiaElement? Scrollable(UiaElement list) =>
        list.IsVerticallyScrollable
            ? list
            : list.FindAllDescendants().FirstOrDefault(element => element.IsVerticallyScrollable);

    /// <summary>
    /// The list's own vertical scroll bar, which is how this list is moved.
    /// </summary>
    /// <remarks>
    /// <para>Measured on the desktop: this <c>TableView</c> exposes NO Scroll pattern anywhere in its subtree,
    /// and a wheel event injected over it — confirmed accepted by <c>SendInput</c> — moves nothing, because a
    /// wheel goes to whatever holds the keyboard focus rather than to whatever is under the pointer. What the
    /// subtree does have is a scroll bar, and a scroll bar has a RANGE.</para>
    ///
    /// <para>Setting that range is not a way of faking the gesture under test. The gesture under test is the
    /// CLICK; scrolling is only how the row is brought within reach, and moving a scroll bar moves nothing but
    /// the viewport — no selection, no command, exactly like the wheel it stands in for.</para>
    ///
    /// <para>Vertical is decided by shape: the bar taller than it is wide. A list can carry both.</para>
    /// </remarks>
    private static UiaElement? VerticalScrollBar(UiaElement list) =>
        list.FindAllDescendants()
            .Where(element => element.ControlType == UiaControlType.ScrollBar && element.Range is not null)
            .FirstOrDefault(element => element.BoundingRectangle.Height >= element.BoundingRectangle.Width);

    /// <summary>How much of a viewport one page advances — the remainder is the overlap that skips no row.</summary>
    private const double PageOverlap = 0.8;

    /// <summary>
    /// What the list's subtree DOES offer, for a refusal that has to explain why nothing scrolled: the control
    /// types present, and any element exposing the Scroll pattern even though it declines to scroll vertically.
    /// </summary>
    private static string DescribeScrollCandidates(UiaElement list)
    {
        IReadOnlyList<UiaElement> descendants = list.FindAllDescendants();

        string kinds = string.Join(", ", descendants
            .GroupBy(element => element.ControlType)
            .OrderByDescending(group => group.Count())
            .Select(group => $"{group.Key}x{group.Count()}"));

        string withPattern = string.Join(", ", descendants
            .Where(element => element.ExposesScrollPattern)
            .Select(element => $"{element.ControlType}/'{element.AutomationId}'"));

        return $"{descendants.Count} descendants [{kinds}]; scroll-pattern holders: "
            + (withPattern.Length == 0 ? "none" : withPattern);
    }

    /// <summary>Turns the wheel over the middle of a control. False when it has no on-screen rectangle.</summary>
    private static bool WheelOver(UiaElement control, int notches)
    {
        Rectangle bounds = control.BoundingRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        return Mouse.Wheel(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2), notches);
    }

    private static readonly TimeSpan ScrollSettle = TimeSpan.FromMilliseconds(120);

    /// <summary>How far one search step scrolls, and how many steps a search will take before giving up.</summary>
    private const int WheelNotchesPerPage = 3;

    private const int MaxScrollPages = 400;

    /// <summary>
    /// The panel's finding rows, in visual order.
    /// </summary>
    /// <remarks>
    /// The findings list is a <c>TableView</c>, not a list box, so its rows are not guaranteed to publish
    /// <c>ListItem</c>. Asked for the two control types a data row can present as, cheapest first — one
    /// provider-side query each, rather than walking the whole subtree and reading a property off every cell.
    /// </remarks>
    private static IReadOnlyList<UiaElement> RowElements(UiaElement list)
    {
        IReadOnlyList<UiaElement> rows = list.FindAllByControlType(UiaControlType.ListItem);
        return rows.Count > 0 ? rows : list.FindAllByControlType(UiaControlType.DataItem);
    }

    /// <summary>
    /// Whether a row answers to a selector. Occurrence first, because it is the only one that addresses a
    /// SINGLE row — a code names a group, and the authored corpus emits several of them many times over.
    /// </summary>
    private static bool Matches(UiaElement row, string selector) =>
        string.Equals(row.ItemStatus, selector, StringComparison.Ordinal)
        || string.Equals(row.AutomationId, selector, StringComparison.Ordinal)
        || row.Name.Contains(selector, StringComparison.OrdinalIgnoreCase);

    /// <summary>Shows or hides the panel through the menu, as a person would.</summary>
    private E2E.Envelope ToggleProblemsPanel() => InvokeMenuItem(AutomationIds.MenuView, "view.toggleProblems");

    /// <summary>
    /// Opens a bar menu and invokes one of its leaves, which is the route a person takes.
    /// </summary>
    /// <remarks>
    /// <para>Menu leaves are addressed by their <c>CommandRegistry</c> row id — <c>view.toggleProblems</c> and
    /// friends — which the markup publishes as the item's AutomationId. Those ids stay string literals here on
    /// purpose: the registry is their single source, and <c>RegistryXamlConsistencyTests</c> already pins the
    /// markup to it, so a constant would be a second declaration of the same fact.</para>
    ///
    /// <para>A menu needs no foreground and no coordinates: expanding and invoking are control-pattern calls,
    /// not synthesized input.</para>
    /// </remarks>
    private E2E.Envelope InvokeMenuItem(string menuTitleId, string itemId)
    {
        if (mainWindow is not { } shell)
        {
            return NotRunning();
        }

        if (shell.FindFirstById(AutomationIds.MenuBar) is not { } bar)
        {
            return Refuse("ControlNotFound", $"the shell publishes no {AutomationIds.MenuBar}");
        }

        if (bar.FindFirstById(menuTitleId) is not { } title)
        {
            return Refuse("ControlNotFound", $"no bar menu {menuTitleId}");
        }

        if (!title.Expand())
        {
            return Refuse("ControlNotFound", $"{menuTitleId} does not open");
        }

        UiaElement? item = title.FindFirstById(itemId);
        if (item is null || !item.Invoke())
        {
            // Only closed on the failure path. After a successful invoke the menu closes itself, and collapsing
            // a menu that has already handed focus to a dialog would take the focus back off it.
            _ = title.Collapse();
            return Refuse("TargetNotFound", $"{menuTitleId} has no invokable item {itemId}");
        }

        Thread.Sleep(ClickSettle);
        return Ok(new Dictionary<string, object?> { ["invoked"] = itemId });
    }

    private UiaElement? Panel() => mainWindow?.FindFirstById(AutomationIds.ProblemsPanel);

    private UiaElement? RowList() => Panel()?.FindFirstById(AutomationIds.ProblemsList);

    /// <summary>
    /// The refusal a hidden panel earns. The word <c>view.problems.toggle</c> is load-bearing: a scenario's
    /// setup keys on it to decide whether to re-show the panel, so both drivers must say it.
    /// </summary>
    private E2E.Envelope PanelHidden() =>
        Refuse("ControlNotFound", "the Problemer panel is hidden; show it again with view.problems.toggle");

    /// <summary>
    /// One row as the envelope reports it, for every verb that reports one — written once because the reader
    /// takes all six keys and throws on a missing one.
    /// </summary>
    private static Dictionary<string, object?> RowPayload(UiaElement row, int index)
    {
        (string severity, string message, string element) = SplitRowName(row.Name);
        return new Dictionary<string, object?>
        {
            ["index"] = index,
            ["code"] = row.AutomationId,
            // The row's per-OCCURRENCE identity, published on ItemStatus BESIDE the code. The code does not
            // address a row: several codes fire many times over the corpus, so "the cable-colour row" names
            // eight of them. This one is unique, and it is what a scenario passes to --row.
            ["occurrence"] = row.ItemStatus,
            ["severity"] = severity,
            ["message"] = message,
            ["element"] = element,
        };
    }

    /// <summary>
    /// Splits a row's accessible name, which the application composes as <c>&lt;Alvor&gt;: &lt;Besked&gt;
    /// (&lt;Element&gt;)</c> precisely so one read answers what the row says.
    /// </summary>
    /// <remarks>
    /// Reading the NAME rather than walking the row's cells: cell text is layout, the name is contract. The
    /// message is matched LAZILY and the element GREEDILY, so the split lands on the FIRST " (" rather than the
    /// last — element names contain parentheses of their own (a terminal reads "Tryk (øverst venstre)"), and a
    /// greedy message would swallow "Ikke forbundet (Tryk" and leave "øverst venstre" as the element.
    /// </remarks>
    private static (string Severity, string Message, string Element) SplitRowName(string name)
    {
        Match match = RowName().Match(name);
        return match.Success
            ? (match.Groups["sev"].Value, match.Groups["msg"].Value, match.Groups["el"].Value)
            : (string.Empty, name, string.Empty);
    }

    [GeneratedRegex(@"^(?<sev>[^:]+):\s*(?<msg>.*?)\s\((?<el>.*)\)$")]
    private static partial Regex RowName();

    // ---- the trees ---------------------------------------------------------------------------------------

    /// <summary>Which pane a <c>--tree</c> option names. <c>TV1</c> is the locality tree, <c>TV2</c> functions.</summary>
    private static string TreeId(string? requested) => requested switch
    {
        "TV2" or AutomationIds.FunctionsTree => AutomationIds.FunctionsTree,
        _ => AutomationIds.InstallationTree,
    };

    /// <summary>The tree's root row and its children by label.</summary>
    private E2E.Envelope TreeDump(string[] args)
    {
        string treeId = TreeId(DriverArguments.Option(args, "--tree"));
        if (mainWindow?.FindFirstById(treeId) is not { } tree)
        {
            return Refuse("ControlNotFound", $"the shell publishes no {treeId}");
        }

        int depth = int.TryParse(DriverArguments.Option(args, "--depth"), out int given) ? given : 1;

        if (tree.Children().FirstOrDefault() is not { } root)
        {
            return Ok(new Dictionary<string, object?>
            {
                ["root"] = new Dictionary<string, object?>
                {
                    ["label"] = string.Empty,
                    ["children"] = new List<object>(),
                },
            });
        }

        List<object> children = [];
        if (depth >= 1)
        {
            // A collapsed row publishes no children at all, so reading them means opening it first. Expanding
            // is a gesture a person takes to see the same thing, not a reach around the UI.
            _ = root.Expand();
            children.AddRange(root.Children()
                .Select(child => (object)new Dictionary<string, object?> { ["label"] = child.Name }));
        }

        return Ok(new Dictionary<string, object?>
        {
            ["root"] = new Dictionary<string, object?>
            {
                ["label"] = root.Name,
                ["children"] = children,
            },
        });
    }

    /// <summary>
    /// Selects a node by its LABEL PATH, walking the control view one segment at a time.
    /// </summary>
    /// <remarks>
    /// Each ancestor is expanded on the way down because a collapsed row's children are not in the tree yet —
    /// so a path cannot be resolved in one search, only walked. The selection is then read BACK through the
    /// tree's own Selection pattern: asking the row whether it thinks it is selected would believe the row, and
    /// what a scenario asserts on is what the pane reports.
    /// </remarks>
    private E2E.Envelope NodeSelect(string[] args)
    {
        if (DriverArguments.Option(args, "--path") is not { Length: > 0 } path)
        {
            return Refuse("InvalidInput", "node select needs --path a/b/c");
        }

        string treeId = TreeId(DriverArguments.Option(args, "--tree"));
        if (mainWindow?.FindFirstById(treeId) is not { } tree)
        {
            return Refuse("ControlNotFound", $"the shell publishes no {treeId}");
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return Refuse("InvalidInput", $"'{path}' names no node");
        }

        UiaElement current = tree;
        for (int i = 0; i < segments.Length; i++)
        {
            IReadOnlyList<UiaElement> matches = ChildrenNamed(current, segments[i]);
            if (matches.Count == 0)
            {
                return Refuse("TargetNotFound", $"'{segments[i]}' is not under '{PathSoFar(segments, i)}' in {treeId}");
            }

            if (matches.Count > 1)
            {
                return Refuse("TargetAmbiguous",
                    $"'{segments[i]}' names {matches.Count} siblings under '{PathSoFar(segments, i)}' in {treeId}");
            }

            current = matches[0];

            // Only ANCESTORS are opened. Expanding the target as well would change what the pane shows beyond
            // what was asked for, and a scenario asserting on the visible rows would see the difference.
            if (i < segments.Length - 1)
            {
                _ = current.Expand();
            }
        }

        if (!current.Select())
        {
            return Refuse("TargetNotFound", $"'{path}' exposes no SelectionItem pattern and cannot be selected");
        }

        Thread.Sleep(ClickSettle);

        string wanted = segments[^1];
        UiaElement? selected = SelectedRow(tree);
        return selected is not null && string.Equals(selected.Name, wanted, StringComparison.Ordinal)
            ? Ok(new Dictionary<string, object?> { ["selected"] = path, ["tree"] = treeId })
            : Refuse("TargetNotFound",
                $"'{path}' did not become the selection of {treeId}; it now reads "
                + $"'{selected?.Name ?? "nothing"}'");
    }

    /// <summary>
    /// The row a tree pane currently has selected, asked of the ROWS rather than of the pane.
    /// </summary>
    /// <remarks>
    /// These trees expose <c>SelectionItem</c> on each row and no <c>Selection</c> pattern on the container, so
    /// the obvious question — "what does the tree say is selected" — answers empty however healthy the pane is.
    /// One provider-side query that stops at the hit, rather than walking the subtree and reading each row.
    /// </remarks>
    private static UiaElement? SelectedRow(UiaElement tree) =>
        tree.FindFirstSelected(UiaControlType.TreeItem);

    /// <summary>
    /// A row's children carrying a given label. Exact first; only if nothing matches exactly is case ignored,
    /// so a tree holding both "Kontakt" and "kontakt" resolves the exact one instead of reporting ambiguity.
    /// </summary>
    private static IReadOnlyList<UiaElement> ChildrenNamed(UiaElement parent, string label)
    {
        IReadOnlyList<UiaElement> children = parent.Children();

        List<UiaElement> exact =
            [.. children.Where(child => string.Equals(child.Name, label, StringComparison.Ordinal))];

        return exact.Count > 0
            ? exact
            : [.. children.Where(child => string.Equals(child.Name, label, StringComparison.OrdinalIgnoreCase))];
    }

    private static string PathSoFar(string[] segments, int index) =>
        index == 0 ? "the tree root" : string.Join('/', segments[..index]);

    // ---- keys --------------------------------------------------------------------------------------------

    /// <summary>
    /// The gestures this driver will send, and the ones it refuses.
    /// </summary>
    /// <remarks>
    /// A closed vocabulary, not a parser. <c>key send</c> bypasses every per-verb gate, so what it can express
    /// is the whole safety boundary — anything not listed is refused rather than guessed at.
    /// </remarks>
    private static readonly Dictionary<string, UiaGesture> Gestures =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["{ENTER}"] = new UiaGesture(UiaKey.Enter),
            ["{ESC}"] = new UiaGesture(UiaKey.Escape),
            ["^z"] = new UiaGesture(UiaKey.Z, UiaModifiers.Control),
        };

    /// <summary>Gestures refused BY NAME, each for its own reason.</summary>
    private static readonly Dictionary<string, string> Forbidden =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["{F5}"] = "it is the controller Send-project gesture, and this suite may reach no controller",
            ["{DELETE}"] = "the shell routes it to an irreversible removal",
            ["{DEL}"] = "the shell routes it to an irreversible removal",
        };

    private E2E.Envelope KeySend(string[] args)
    {
        if (DriverArguments.Option(args, "--gesture") is not { Length: > 0 } gesture)
        {
            return Refuse("InvalidInput", $"key send needs --gesture, one of {KnownGestures()}");
        }

        if (Forbidden.TryGetValue(gesture, out string? why))
        {
            return Refuse("NotAllowed", $"refusing to send {gesture}: {why}");
        }

        if (!Gestures.TryGetValue(gesture, out UiaGesture parsed))
        {
            return Refuse("InvalidInput", $"unknown gesture '{gesture}'; this driver sends {KnownGestures()}");
        }

        // To whatever the application has in FRONT — a dialog if one is open, the shell otherwise. Typing at
        // the shell while a dialog is up would send the keystroke to a window the person is not looking at.
        if (FrontWindow() is not { } target)
        {
            return NotRunning();
        }

        if (!Foreground.Acquire(target.NativeWindowHandle))
        {
            return NotForeground();
        }

        if (!Keyboard.Send(parsed))
        {
            return NotInjected(gesture);
        }

        Thread.Sleep(ClickSettle);
        return Ok(new Dictionary<string, object?> { ["gesture"] = gesture });
    }

    private static string KnownGestures() => string.Join(", ", Gestures.Keys);

    /// <summary>Sends a gesture to the SHELL specifically, for the verbs that mean "to the main window".</summary>
    private E2E.Envelope SendToShell(UiaGesture gesture, string spelling)
    {
        if (mainWindow is not { } shell)
        {
            return NotRunning();
        }

        if (!Foreground.Acquire(shell.NativeWindowHandle))
        {
            return NotForeground();
        }

        if (!Keyboard.Send(gesture))
        {
            return NotInjected(spelling);
        }

        Thread.Sleep(ClickSettle);
        return Ok(new Dictionary<string, object?> { ["gesture"] = spelling });
    }

    /// <summary>
    /// The refusal for input the SYSTEM declined to deliver.
    /// </summary>
    /// <remarks>
    /// Distinct from "the application ignored it": <c>SendInput</c> reports how many events it injected, and
    /// zero means the gesture never happened at all — UIPI blocks injection into a more privileged window, so
    /// this is what an elevated OpenVisual looks like from a non-elevated test host. Reporting it as success
    /// would fail the scenario several verbs later, against a UI that was never touched.
    ///
    /// <para>A pointer gesture answers the same way when the CURSOR MOVE was refused, which the same conditions
    /// cause. That case is worse than an uninjected button and is refused for the same reason: the buttons carry
    /// no coordinates of their own, so they would have landed on whatever was under the pointer already.</para>
    /// </remarks>
    private E2E.Envelope NotInjected(string gesture) =>
        Refuse("PreconditionMissing",
            $"the system refused to inject {gesture}; the application is most likely running at a higher "
            + "elevation than this test host");

    /// <summary>
    /// The application's front window: the topmost DIALOG if one is open, else the shell.
    /// </summary>
    /// <remarks>
    /// Through <see cref="AppDialogs"/>, never the raw z-ordered list — that list contains the application's
    /// zero-area, title-less helper windows, and one of those sits at the TOP of it. Targeting a phantom
    /// either fails to take the foreground, or takes it and types into a window nobody is looking at.
    /// </remarks>
    private UiaElement? FrontWindow() => AppDialogs().FirstOrDefault() ?? mainWindow;

    private E2E.Envelope NotForeground() =>
        Refuse("PreconditionMissing",
            "the application is not in the foreground, so synthesized input would land in another window");

    // ---- dialogs -----------------------------------------------------------------------------------------

    /// <summary>
    /// THE modal: the topmost of the application's windows other than the shell.
    /// </summary>
    /// <remarks>
    /// Topmost by the window manager's Z-ORDER, never by UI-Automation sibling order. Sibling order is creation
    /// order, so in a stack — a terminal editor opened from a product dialog — it names the one UNDERNEATH, and
    /// every dialog verb would then operate the window the person is not looking at.
    /// </remarks>
    private UiaElement? TopmostModal() => AppDialogs().FirstOrDefault();

    private E2E.Envelope DialogRead()
    {
        if (TopmostModal() is not { } modal)
        {
            return Refuse("NoModal", "no dialog is open");
        }

        List<object> controls =
        [
            .. Descendants(modal).Select(control => (object)new Dictionary<string, object?>
            {
                ["id"] = control.AutomationId,
                ["name"] = control.Name,
                ["controlType"] = control.ControlType.ToString(),
                ["focused"] = control.HasKeyboardFocus,
                ["value"] = control.Value,
            }),
        ];

        return Ok(new Dictionary<string, object?>
        {
            ["dialog"] = new Dictionary<string, object?>
            {
                ["title"] = modal.Name,
                ["id"] = modal.AutomationId,
            },
            ["controls"] = controls,
            ["focused"] = FocusedInApp(),
        });
    }

    /// <summary>
    /// What has keyboard focus, but only when it belongs to the application under test.
    /// </summary>
    /// <remarks>
    /// UI Automation answers the focus question for the whole DESKTOP, so without the process check a dialog
    /// that never took focus would be reported as focusing whatever the person had in front — and the probe
    /// scenario would pass on another program's control.
    /// </remarks>
    private Dictionary<string, object?>? FocusedInApp()
    {
        if (app is not { } running || Session.FocusedElement() is not { } focused)
        {
            return null;
        }

        return focused.ProcessId == running.Id
            ? new Dictionary<string, object?> { ["id"] = focused.AutomationId, ["name"] = focused.Name }
            : null;
    }

    /// <summary>Every element beneath one, in control-view order.</summary>
    private static IEnumerable<UiaElement> Descendants(UiaElement root)
    {
        foreach (UiaElement child in root.Children())
        {
            yield return child;
            foreach (UiaElement deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    /// <summary>
    /// Picks an item in a combo box by its text.
    /// </summary>
    /// <remarks>
    /// The result is read back from the CONTROL, not from the item that was selected: an Avalonia combo box
    /// virtualizes its list away when it collapses, so the item element is gone by the time there is an answer
    /// to report. What the control now reads is also what a person would see.
    /// </remarks>
    private E2E.Envelope DialogSelectItem(string[] args)
    {
        if (TopmostModal() is not { } modal)
        {
            return Refuse("NoModal", "no dialog is open");
        }

        if (DriverArguments.Option(args, "--control") is not { Length: > 0 } controlId)
        {
            return Refuse("InvalidInput", "dialog select-item needs --control <id>");
        }

        if (DriverArguments.Option(args, "--item") is not { Length: > 0 } itemText)
        {
            return Refuse("InvalidInput", "dialog select-item needs --item <text>");
        }

        if (modal.FindFirstById(controlId) is not { } combo)
        {
            return Refuse("ControlNotFound", $"the dialog has no control '{controlId}'");
        }

        if (!combo.Expand())
        {
            return Refuse("ControlNotFound", $"'{controlId}' does not open; it is a {combo.ControlType}");
        }

        UiaElement? item = combo.FindAllByName(itemText).FirstOrDefault();
        if (item is null)
        {
            _ = combo.Collapse();
            return Refuse("TargetNotFound", $"'{controlId}' offers no item '{itemText}'");
        }

        bool selected = item.Select();
        _ = combo.Collapse();
        Thread.Sleep(ClickSettle);

        if (!selected)
        {
            return Refuse("TargetNotFound", $"'{itemText}' exposes no SelectionItem pattern");
        }

        return Ok(new Dictionary<string, object?>
        {
            ["control"] = controlId,
            ["selected"] = combo.Value ?? combo.Name,
        });
    }

    /// <summary>Invokes a button in the dialog, addressed by id or, failing that, by its visible text.</summary>
    private E2E.Envelope DialogClick(string[] args)
    {
        if (TopmostModal() is not { } modal)
        {
            return Refuse("NoModal", "no dialog is open");
        }

        if (DriverArguments.Option(args, "--button") is not { Length: > 0 } wanted)
        {
            return Refuse("InvalidInput", "dialog click needs --button <id|name>");
        }

        if (FindButton(modal, wanted) is not { } button)
        {
            return Refuse("ControlNotFound", $"the dialog has no button '{wanted}'");
        }

        if (!button.Invoke())
        {
            return Refuse("TargetNotFound", $"'{wanted}' exposes no Invoke pattern");
        }

        Thread.Sleep(ClickSettle);
        return Ok(new Dictionary<string, object?> { ["clicked"] = wanted });
    }

    /// <summary>
    /// By id first, then by visible text. The id is the stable address; the name is what a scenario can write
    /// when a button has no id of its own, and it is Danish, so it is the weaker of the two.
    /// </summary>
    private static UiaElement? FindButton(UiaElement modal, string wanted) =>
        modal.FindFirstById(wanted)
        ?? modal.FindAllByControlType(UiaControlType.Button)
            .FirstOrDefault(button => string.Equals(button.Name, wanted, StringComparison.Ordinal));

    /// <summary>
    /// Dismisses the topmost dialog, by the gesture a person uses and then by its button.
    /// </summary>
    /// <remarks>
    /// Escape first because that is the route, and it is the one that proves the dialog HANDLES escape. The
    /// button is the fallback for a dialog that does not, so that a scenario's teardown still leaves a clean
    /// stack rather than poisoning the next one.
    /// </remarks>
    private E2E.Envelope DialogCancel()
    {
        if (TopmostModal() is not { } modal)
        {
            return Refuse("NoModal", "no dialog is open");
        }

        nint handle = modal.NativeWindowHandle;

        if (Foreground.Acquire(handle))
        {
            Keyboard.Send(new UiaGesture(UiaKey.Escape));
            Thread.Sleep(ClickSettle);

            if (Closed(handle))
            {
                // The context block is built AFTER this returns, so it reports the stack as it now stands —
                // which is what E2E.CloseAllModals reads to decide whether to go round again.
                return Ok(new Dictionary<string, object?> { ["closed"] = true, ["by"] = "{ESC}" });
            }
        }

        // By id, then by the button's Danish text — which is what a dialog without an explicit id offers.
        UiaElement? cancel = FindButton(modal, AutomationIds.CancelButton)
            ?? modal.FindAllByControlType(UiaControlType.Button)
                .FirstOrDefault(button => string.Equals(button.Name, "Annuller", StringComparison.Ordinal));

        if (cancel is null)
        {
            return Refuse("TargetNotFound", "the dialog ignored Escape and offers no cancel button");
        }

        if (!cancel.Invoke())
        {
            return Refuse("TargetNotFound", "the dialog's cancel button exposes no Invoke pattern");
        }

        Thread.Sleep(ClickSettle);
        return Closed(handle)
            ? Ok(new Dictionary<string, object?> { ["closed"] = true, ["by"] = AutomationIds.CancelButton })
            : Refuse("TargetNotFound", "the dialog is still open after being cancelled");
    }

    /// <summary>Whether a window handle no longer names one of the application's live top-level windows.</summary>
    private bool Closed(nint handle) =>
        app is not { } running
        || DesktopWindows.OfProcess(Session, running.Id).All(window => window.NativeWindowHandle != handle);

    public void KillApp()
    {
        _ = AppLauncher.KillAll(ProcessName, KillTimeout);

        // The handle is this driver's to release: AppLauncher.Start hands the caller a LIVE Process so a failed
        // launch can be reported with its exit code, and KillAll disposes only the objects it enumerated itself.
        // Dropping this one instead would leak an OS handle per launch, and every scenario launches.
        app?.Dispose();
        app = null;
        mainWindow = null;
    }

    public void Dispose()
    {
        KillApp();
        session?.Dispose();
        session = null;
    }

    // ---- the application under test ----------------------------------------------------------------------

    /// <summary>
    /// Where the application was built, baked in at compile time (D04) so the path is deterministic and names
    /// the same configuration these tests were built in.
    /// </summary>
    private static string ExecutablePath() =>
        typeof(UiaDriver).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "OpenVisualExecutable")
            ?.Value
        ?? throw new InvalidOperationException(
            "the test assembly carries no OpenVisualExecutable metadata; see safe_visual_e2e_tests.csproj");

    // ---- envelope construction ---------------------------------------------------------------------------

    private E2E.Envelope NotRunning() =>
        Refuse("AppNotRunning", "the application is not running; launch first");

    private E2E.Envelope Ok(object? data = null) => envelopes.Ok(data);

    private E2E.Envelope Refuse(string code, string message) => envelopes.Refuse(code, message);

    /// <summary>The block every envelope carries: what window is in front, what is modal, what is selected.</summary>
    private Dictionary<string, object?> Context() => new()
    {
        ["windowTitle"] = mainWindow?.Name ?? string.Empty,
        ["openModals"] = OpenModals(),
        ["selections"] = Selections(),
    };

    /// <summary>
    /// The application's top-level windows other than the shell, TOPMOST FIRST.
    /// </summary>
    /// <remarks>
    /// Z-order, from the window manager, never UI-Automation sibling order — that is creation order, and in a
    /// stack of dialogs it names the one UNDERNEATH.
    /// </remarks>
    private List<object> OpenModals() =>
    [
        .. AppDialogs().Select(window => (object)new Dictionary<string, object?>
        {
            ["id"] = window.AutomationId,
            ["title"] = window.Name,
        }),
    ];

    /// <summary>
    /// The application's open dialogs, topmost first — its visible top-level windows other than the shell,
    /// with what is not a dialog filtered out.
    /// </summary>
    /// <remarks>
    /// <b>Both filters were earned by measurement.</b> A tooltip and a drop-down shadow are visible top-level
    /// windows of this process too, so only a <c>Window</c> counts. And a Window with NO AREA is not a dialog
    /// either: the application keeps invisible-but-"visible" layered helpers around — one reports an empty
    /// title, an empty AutomationId and a zero rectangle — and counting it puts a PHANTOM at the top of the
    /// stack. Every dialog verb then addresses the phantom instead of the real modal, and every scenario that
    /// asserts "nothing is open" fails against a window that does not exist to the person using the app.
    /// </remarks>
    private IReadOnlyList<UiaElement> AppDialogs()
    {
        if (app is not { } running || mainWindow is not { } shell)
        {
            return [];
        }

        nint shellHandle = shell.NativeWindowHandle;
        return
        [
            .. DesktopWindows.OfProcess(Session, running.Id)
                .Where(window => window.NativeWindowHandle != shellHandle && IsDialog(window)),
        ];
    }

    private static bool IsDialog(UiaElement window)
    {
        if (window.ControlType != UiaControlType.Window)
        {
            return false;
        }

        Rectangle bounds = window.BoundingRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        // …and it must SAY WHAT IT IS. The layered helpers this application keeps around publish neither a
        // title nor an automation id; every window it actually shows a person publishes both — each dialog
        // type declares an id, and each sets a Danish title. A window that identifies itself as nothing is
        // not something the person can be looking at, and counting it makes "nothing is open" permanently
        // false.
        return window.AutomationId.Length > 0 || window.Name.Length > 0;
    }

    /// <summary>What each tree pane currently has selected, read from its own Selection pattern.</summary>
    private List<object> Selections()
    {
        if (mainWindow is not { } shell)
        {
            return [];
        }

        List<object> selections = [];
        Add(AutomationIds.InstallationTree);
        Add(AutomationIds.FunctionsTree);
        return selections;

        void Add(string treeId)
        {
            if (shell.FindFirstById(treeId) is not { } tree)
            {
                return;
            }

            // Asked of the ROWS, not of the tree: these panes expose SelectionItem on each row and no Selection
            // pattern on the container, so reading the container answers empty for a pane that plainly has a
            // selected row. Measured on the desktop — it is what made every selection assertion see nothing.
            if (SelectedRow(tree) is { } selected)
            {
                selections.Add(new Dictionary<string, object?>
                {
                    ["tree"] = treeId,
                    ["name"] = selected.Name,
                });
            }
        }
    }

}
