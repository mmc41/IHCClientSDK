using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// The declarative command registry (crudarch T011, proposal §3.3): holds one <see cref="CommandSpec"/> row per
/// user-facing command, MATERIALIZES each <see cref="IRelayCommand"/> from the row's single Gate predicate
/// (D02 — commands and availability share one evaluation source; D06 — a command's CanExecute follows the GATE,
/// i.e. context-menu semantics, so gestures fire exactly what the flyout offers), and computes the per-surface
/// <see cref="Availability"/> snapshots the XAML binds to. <see cref="OnContextChanged"/> is the ONE
/// invalidation signal (C-BP-06): it re-evaluates every row against the current <see cref="ShellContext"/>,
/// replaces the snapshots, and raises CanExecuteChanged on every command. Avalonia-free.
/// </summary>
public sealed class CommandRegistry : ObservableObject
{
    private static readonly IReadOnlyDictionary<string, Availability> NoSnapshot = new Dictionary<string, Availability>();

    // What a refusal that carries no reason of its own still tells the user (QC-06 — a disabled command explains
    // itself). ONE home: both the per-surface grey and the keyboard-route explanation fall back to it.
    private const string NoReason = "Ikke tilgængelig nu.";

    private readonly Func<ShellContext> _context;
    private readonly Action<object?>? _beforeExecute;
    private readonly Dictionary<string, CommandSpec> _rows = new();
    private readonly Dictionary<string, IAsyncRelayCommand> _commands = new();
    private readonly Dictionary<string, IAsyncRelayCommand> _gestureCommands = new();
    // The per-row gate memo (review F01): the verdict depends only on (row, context), so it is computed ONCE per
    // row per ShellContext INSTANCE — keyed by reference, since RebuildContext always mints a fresh immutable
    // snapshot, so a new context is always a cache miss and a rebuilt context never serves a stale verdict.
    // Without it a single context change re-ran every gate up to 3× for the snapshots plus once per bound control
    // for the CanExecute re-query NotifyCanExecuteChanged triggers — and gates reach the SDK (document.CanApply).
    private readonly Dictionary<string, (ShellContext Context, EditVerdict Verdict)> _verdicts = new();

    /// <summary>Creates the registry over the view-model's live context accessor — every gate and evaluation
    /// reads the CURRENT <see cref="ShellContext"/>, never a captured one. The optional
    /// <paramref name="beforeExecute"/> is the command-parameter bridge: surfaces that address a specific row
    /// (a context-menu click, the Delete-key route, existing test call sites) still pass it as the ICommand
    /// parameter, and the host maps it into the context (e.g. selects the node) BEFORE the row's Execute reads
    /// the context — rows themselves stay parameterless functions of ShellContext.</summary>
    public CommandRegistry(Func<ShellContext> currentContext, Action<object?>? beforeExecute = null)
    {
        _context = currentContext;
        _beforeExecute = beforeExecute;
    }

    /// <summary>The rows, enumerable for the data-driven spec tests and the registry↔XAML consistency test.</summary>
    public IReadOnlyCollection<CommandSpec> Rows => _rows.Values;

    /// <summary>The materialized commands by row id — what XAML items bind their Command to. Typed as
    /// <see cref="IAsyncRelayCommand"/> (which extends <see cref="IRelayCommand"/>) because <see cref="Register"/>
    /// always materializes one: every awaitable call site and every view-model bridge reads it straight, with no
    /// downcast (review F08).</summary>
    public IReadOnlyDictionary<string, IAsyncRelayCommand> Commands => _commands;

    /// <summary>
    /// The KEYBOARD-route commands by row id — what <c>&lt;Window.KeyBindings&gt;</c> bind to. Each runs the same
    /// action as its <see cref="Commands"/> peer, but its CanExecute follows the MENU BAR's availability instead
    /// of the bare gate (D06, owner ruling 2026-08-02): on a locked block the bar greys Cut while the context
    /// flyout still offers it (D13), so Ctrl+X must refuse where the flyout's Cut still runs.
    /// </summary>
    /// <remarks>
    /// The gating MUST live here rather than in a window key handler that vetoes with <c>e.Handled</c>: Avalonia
    /// services a TopLevel's KeyBindings BEFORE any instance KeyDown handler, tunnel included, so by the time a
    /// handler runs the command has already executed. A KeyBinding never invokes a disabled command, so a
    /// bar-refused gesture simply does not fire — and the window handler, which now only ever sees gestures that
    /// did not fire, explains the refusal via <see cref="Explain"/> (T021 branch B).
    /// </remarks>
    public IReadOnlyDictionary<string, IAsyncRelayCommand> GestureCommands => _gestureCommands;

    /// <summary>Menu-bar availability by row id (persistent surface: greys with a reason, US-044).</summary>
    public IReadOnlyDictionary<string, Availability> Bar { get; private set; } = NoSnapshot;

    /// <summary>Context-flyout availability by row id (transient surface: omits, US-068).</summary>
    public IReadOnlyDictionary<string, Availability> ContextMenu { get; private set; } = NoSnapshot;

    /// <summary>Toolbar availability by row id (persistent surface: greys with a reason, US-044).</summary>
    public IReadOnlyDictionary<string, Availability> Toolbar { get; private set; } = NoSnapshot;

    /// <summary>
    /// The SINGLE availability evaluator (proposal §3.3): unplaced → <see cref="Availability.Hidden"/>; a failed
    /// gate → hidden on the transient context menu (US-068 omits) but disabled-with-reason on the persistent
    /// bar/toolbar (US-044 greys); a passed gate → the row's <see cref="CommandSpec.SurfacePolicy"/> restriction
    /// if it speaks for this surface, else <see cref="Availability.Allow"/>. The policy runs only AFTER the gate
    /// passed, so it can restrict but structurally never widen.
    /// </summary>
    public static Availability For(CommandSpec row, ShellContext context, Surface surface)
    {
        ArgumentNullException.ThrowIfNull(row);

        return For(row, context, row.Gate(context), surface);
    }

    /// <summary>The same evaluator over an ALREADY-computed gate verdict — the surface only decides hide-vs-grey,
    /// so the three per-surface answers of one row share one gate run instead of re-asking the SDK three times.</summary>
    public static Availability For(CommandSpec row, ShellContext context, EditVerdict gate, Surface surface)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.Placement.Contains(surface))
        {
            return Availability.Hidden;
        }
        if (!gate.Ok)
        {
            return surface == Surface.ContextMenu
                ? Availability.Hidden
                : Availability.Disabled(gate.Reason ?? NoReason);
        }
        return row.SurfacePolicy?.Invoke(context, surface) ?? Availability.Allow;
    }

    /// <summary>Why the row's KEYBOARD route is refused right now, or null when the gesture may run (and for an
    /// unknown id) — the text T021 branch B writes to the status bar so a dead shortcut always explains itself
    /// (QC-06). Per D06 a gesture follows the MENU BAR where the row is placed there, so a locked block's Cut —
    /// which the bar greys while the flyout still offers it (D13) — is refused from the keyboard too; a
    /// keybinding-only row the bar never shows is judged by its own gate. Registry-owned so availability keeps
    /// ONE home: the same gate and the same <see cref="NoReason"/> fallback the surfaces use, rather than a
    /// second evaluation path in the view that must be hand-mirrored on every evaluator change (review F07).</summary>
    public string? Explain(string id)
    {
        string? reason = null;
        if (_rows.TryGetValue(id, out CommandSpec? row))
        {
            ShellContext context = _context();
            EditVerdict gate = Gate(row, context);
            Availability bar = For(row, context, gate, Surface.MenuBar);
            reason = bar.Visible
                ? bar.Enabled ? null : bar.Reason ?? NoReason
                : gate.Ok ? null : gate.Reason ?? NoReason;
        }
        return reason;
    }

    /// <summary>The registry's entry point into the instrumentation core.</summary>
    private readonly Ihc.OperationTelemetry _telemetry =
        new(ihc_openvisual.Configuration.AppTelemetryRegistry.Surface, nameof(CommandRegistry));

    /// <summary>The binding is IMMUTABLE and its instruments are static, so it is built once rather than per operation.</summary>
    private static readonly Ihc.MetricBinding InvokeMetrics =
        Ihc.MetricBinding.For(occurrences: ihc_openvisual.Configuration.AppTelemetryRegistry.CommandInvocation);

    /// <summary>The binding is IMMUTABLE and its instruments are static, so it is built once rather than per operation.</summary>
    private static readonly Ihc.MetricBinding ContextRebuildMetrics =
        Ihc.MetricBinding.For(ihc_openvisual.Configuration.AppTelemetryRegistry.ContextRebuildDuration);

    /// <summary>Adds a row and materializes its command from the row's Gate (D02): CanExecute IS the gate,
    /// evaluated against the current context on every ask (any ICommand parameter first flows through the
    /// beforeExecute bridge, then Execute reads the context). Throws on a duplicate id — one row per command.</summary>
    public IAsyncRelayCommand Register(CommandSpec row)
    {
        ArgumentNullException.ThrowIfNull(row);

        _rows.Add(row.Id, row);
        async Task Execute(object? parameter)
        {
            // ONE place for all four surfaces. Menu bar, toolbar, context flyout and gesture all materialize
            // from this same local function, so counting here counts every route without the four having to
            // agree on anything.
            //
            // WHAT THIS DOES AND DOES NOT CLAIM, stated where it is declared because the name invites a
            // wider reading. It counts REGISTERED ROWS being invoked - not feature usage, and not user
            // gestures. There is no surface dimension, because this function structurally cannot observe
            // WHICH of the four surfaces invoked it. Anything the shell does outside a registered row is
            // invisible here by construction.
            //
            // AWAITED, not handed back. This span is the ROOT of the gesture's trace, and a root that closes
            // at the row's first await times none of what it parents - measured on a live save, the root read
            // 10 ms over work that ran 20 s, which is a tree with a stub for a root. Awaiting costs one state
            // machine per invocation and buys three things: the root's duration IS the gesture's, a fault that
            // escapes the row reaches the span and the count as an error.type, and the trace root that a red
            // span deep in a workflow has to be joined back to carries the command id for the whole gesture.
            //
            // What it also changes, said plainly: the count is now recorded when the row FINISHES rather than
            // when it starts, so a gesture whose process dies mid-modal is no longer counted. That is the cost
            // OperationScope already documents for every other operation, and paying it here keeps the count
            // and the span describing the same event.
            using Ihc.OperationScope scope = _telemetry.Start("Invoke", metrics:
                InvokeMetrics);
            scope.AddSharedTag(ihc_openvisual.Configuration.AppTelemetryRegistry.Attributes.CommandId, row.Id);

            try
            {
                // INSIDE the guard, along with the row itself. The pre-execute hook is part of what a gesture
                // does, so a throw from it is a gesture that failed - and left outside, it disposed the scope
                // with the default outcome and recorded that failure as a success, which is the one thing the
                // outcome machinery exists to prevent.
                _beforeExecute?.Invoke(parameter);
                // The row's ANSWER, applied to the root. A failure the shell handled - a save that could not be
                // written, a prompt the installer cancelled - never throws past the view-model's error
                // boundary, so awaiting alone leaves this span reading ok for a gesture that did not work.
                // This is the line that carries such an outcome up to the span a reader starts from, and onto
                // the invocation count's dimensions with it.
                scope.SetOutcome(await row.Execute(_context()));
            }
            catch (Exception ex)
            {
                // Rethrown UNCHANGED - the row's caller is owed its own exception. Recording is additive, and
                // it is the only record there would be: a row that faults past the view-model's error boundary
                // is by definition one that boundary did not handle.
                scope.SetOutcome(Ihc.OperationOutcome.Failed(ex));
                throw;
            }
        }
        var command = new AsyncRelayCommand<object?>(Execute, _ => Gate(row, _context()).Ok);
        _commands.Add(row.Id, command);
        // Same action, gated by what the BAR shows rather than by the bare gate (D06) — see GestureCommands.
        _gestureCommands.Add(row.Id, new AsyncRelayCommand<object?>(Execute, _ => Explain(row.Id) is null));
        return command;
    }

    // The row's gate verdict against a context, evaluated at most ONCE per row per context instance (see _verdicts).
    private EditVerdict Gate(CommandSpec row, ShellContext context)
    {
        if (!(_verdicts.TryGetValue(row.Id, out (ShellContext Context, EditVerdict Verdict) memo)
              && ReferenceEquals(memo.Context, context)))
        {
            memo = (context, row.Gate(context));
            _verdicts[row.Id] = memo;
        }
        return memo.Verdict;
    }

    /// <summary>The ONE invalidation signal (C-BP-06), wired to the view-model's ContextChanged: re-evaluates
    /// every row, replaces the three per-surface snapshots (fresh instances so bindings re-read), and raises
    /// CanExecuteChanged on every materialized command.</summary>
    public void OnContextChanged()
    {
        // The sweep re-evaluates EVERY registered row against the new context and raises CanExecuteChanged on
        // each command. It runs on every selection change, so its cost scales with the row count and is paid
        // constantly - which makes it the other half of the same performance question as the tree refresh.
        using Ihc.OperationScope scope = _telemetry.Start(nameof(OnContextChanged), metrics:
            ContextRebuildMetrics);

        ShellContext context = _context();
        var bar = new Dictionary<string, Availability>(_rows.Count);
        var contextMenu = new Dictionary<string, Availability>(_rows.Count);
        var toolbar = new Dictionary<string, Availability>(_rows.Count);
        foreach ((string id, CommandSpec row) in _rows)
        {
            EditVerdict gate = Gate(row, context);
            bar[id] = For(row, context, gate, Surface.MenuBar);
            contextMenu[id] = For(row, context, gate, Surface.ContextMenu);
            toolbar[id] = For(row, context, gate, Surface.Toolbar);
        }
        Bar = bar;
        ContextMenu = contextMenu;
        Toolbar = toolbar;
        OnPropertyChanged(nameof(Bar));
        OnPropertyChanged(nameof(ContextMenu));
        OnPropertyChanged(nameof(Toolbar));
        foreach (IAsyncRelayCommand command in _commands.Values)
        {
            command.NotifyCanExecuteChanged();
        }
        foreach (IAsyncRelayCommand command in _gestureCommands.Values)
        {
            command.NotifyCanExecuteChanged();
        }
    }
}
