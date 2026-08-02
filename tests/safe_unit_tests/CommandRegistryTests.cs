using System;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Session;

namespace safe_unit_tests;

/// <summary>
/// crudarch T011 (proposal §3.3, decisions D02/D08/D10): the registry core — the SINGLE For(row, ctx, surface)
/// evaluator (hidden-when-unplaced; gate-fail → context-menu omits / bar+toolbar grey with reason; SurfacePolicy
/// restricts only), and the registry materializing each IRelayCommand from the row's ONE Gate predicate so two
/// authorities can never disagree. Every evaluator branch is covered.
/// </summary>
public class CommandRegistryTests
{
    private static readonly ShellContext Open = ShellContext.Empty with { ProjectOpen = true };

    private static CommandSpec Row(
        string id = "test.row",
        Surfaces placement = Surfaces.MenuBar | Surfaces.ContextMenu | Surfaces.Toolbar,
        Func<ShellContext, EditVerdict>? gate = null,
        Func<ShellContext, Surface, Availability?>? surfacePolicy = null,
        Func<ShellContext, Task>? execute = null) =>
        new(id, "Ctrl+T", placement,
            execute ?? (_ => Task.CompletedTask),
            gate ?? (_ => EditVerdict.Allow),
            surfacePolicy);

    [Test]
    public void Evaluator_UnplacedSurface_IsHidden()
    {
        CommandSpec row = Row(placement: Surfaces.MenuBar);

        Assert.Multiple(() =>
        {
            Assert.That(CommandRegistry.For(row, Open, Surface.Toolbar), Is.EqualTo(Availability.Hidden),
                "a surface outside Placement never shows the command");
            Assert.That(CommandRegistry.For(row, Open, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(CommandRegistry.For(row, Open, Surface.MenuBar), Is.EqualTo(Availability.Allow));
        });
    }

    [Test]
    public void Evaluator_GateFail_ContextMenuOmits_BarAndToolbarGreyWithReason()
    {
        CommandSpec row = Row(gate: _ => EditVerdict.Refuse("Locked."));

        Assert.Multiple(() =>
        {
            Assert.That(CommandRegistry.For(row, Open, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                "the transient surface omits a gate-failed command (US-068)");
            Assert.That(CommandRegistry.For(row, Open, Surface.MenuBar),
                Is.EqualTo(Availability.Disabled("Locked.")), "the persistent surface greys with the reason (US-044)");
            Assert.That(CommandRegistry.For(row, Open, Surface.Toolbar), Is.EqualTo(Availability.Disabled("Locked.")));
        });
    }

    [Test]
    public void Evaluator_GateFailWithoutReason_GreysWithFallbackText()
    {
        CommandSpec row = Row(gate: _ => new EditVerdict(false, null));

        Assert.That(CommandRegistry.For(row, Open, Surface.MenuBar),
            Is.EqualTo(Availability.Disabled("Not available now.")),
            "a reasonless refusal still explains itself with the fallback text");
    }

    [Test]
    public void Evaluator_GatePass_NoPolicy_AllowsEverywherePlaced()
    {
        CommandSpec row = Row();

        Assert.Multiple(() =>
        {
            Assert.That(CommandRegistry.For(row, Open, Surface.MenuBar), Is.EqualTo(Availability.Allow));
            Assert.That(CommandRegistry.For(row, Open, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(CommandRegistry.For(row, Open, Surface.Toolbar), Is.EqualTo(Availability.Allow));
        });
    }

    [Test]
    public void Evaluator_SurfacePolicy_RestrictsOnlyWhereItSpeaks()
    {
        CommandSpec row = Row(surfacePolicy: (_, surface) =>
            surface == Surface.MenuBar ? Availability.Disabled("Bar-restricted.") : null);

        Assert.Multiple(() =>
        {
            Assert.That(CommandRegistry.For(row, Open, Surface.MenuBar),
                Is.EqualTo(Availability.Disabled("Bar-restricted.")), "the policy restricts the bar (D13 divergences)");
            Assert.That(CommandRegistry.For(row, Open, Surface.ContextMenu), Is.EqualTo(Availability.Allow),
                "a null policy answer falls through to the gate's Allow — restrict-only, never widen");
            Assert.That(CommandRegistry.For(row, Open, Surface.Toolbar), Is.EqualTo(Availability.Allow));
        });
    }

    [Test]
    public void Registry_MaterializesCommandFromGate_AgainstTheCurrentContext()
    {
        ShellContext current = ShellContext.Empty;
        var registry = new CommandRegistry(() => current);
        bool executed = false;
        var command = registry.Register(Row(
            gate: c => c.ProjectOpen ? EditVerdict.Allow : EditVerdict.Refuse("No project."),
            execute: _ => { executed = true; return Task.CompletedTask; }));

        Assert.That(command.CanExecute(null), Is.False, "the materialized CanExecute IS the gate (D02)");

        current = Open;
        Assert.That(command.CanExecute(null), Is.True, "the gate re-evaluates against the CURRENT context");

        command.Execute(null);
        Assert.That(executed, Is.True, "Execute runs the row's one action");
    }

    [Test]
    public void Registry_OnContextChanged_RaisesCanExecuteChanged_AndRebuildsSnapshots()
    {
        ShellContext current = Open;
        var registry = new CommandRegistry(() => current);
        var command = registry.Register(Row(
            gate: c => c.ProjectOpen ? EditVerdict.Allow : EditVerdict.Refuse("No project.")));
        int raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        registry.OnContextChanged();

        Assert.Multiple(() =>
        {
            Assert.That(raised, Is.EqualTo(1), "the ONE invalidation signal notifies every materialized command");
            Assert.That(registry.Bar["test.row"], Is.EqualTo(Availability.Allow));
            Assert.That(registry.ContextMenu["test.row"], Is.EqualTo(Availability.Allow));
            Assert.That(registry.Toolbar["test.row"], Is.EqualTo(Availability.Allow));
        });

        current = ShellContext.Empty;
        registry.OnContextChanged();

        Assert.Multiple(() =>
        {
            Assert.That(registry.Bar["test.row"], Is.EqualTo(Availability.Disabled("No project.")),
                "the bar snapshot greys with the gate's reason");
            Assert.That(registry.ContextMenu["test.row"], Is.EqualTo(Availability.Hidden),
                "the context snapshot omits");
            Assert.That(command.CanExecute(null), Is.False);
        });
    }

    // review F07: the keyboard route's "why is this shortcut dead?" answer is the REGISTRY's, computed off the
    // same gate the commands and the surface snapshots read — including the reasonless-refusal fallback, which
    // a view recomposing the answer from the Bar snapshot skipped for keybinding-only (unplaced) rows.
    [Test]
    public void Registry_Explain_AnswersOnlyForRefusals_WithTheSameFallbackTheSurfacesGreyWith()
    {
        ShellContext current = ShellContext.Empty;
        var registry = new CommandRegistry(() => current);
        registry.Register(Row("gated", gate: c => c.ProjectOpen ? EditVerdict.Allow : EditVerdict.Refuse("No project.")));
        registry.Register(Row("silent", placement: Surfaces.None, gate: _ => new EditVerdict(false, null)));

        Assert.Multiple(() =>
        {
            Assert.That(registry.Explain("gated"), Is.EqualTo("No project."), "a refusal explains itself");
            Assert.That(registry.Explain("silent"), Is.EqualTo("Not available now."),
                "an unplaced row's reasonless refusal still gets the fallback the bar would have greyed with");
            Assert.That(registry.Explain("nosuchrow"), Is.Null, "an unknown row has nothing to explain");
        });

        current = Open;

        Assert.That(registry.Explain("gated"), Is.Null, "an executable command has nothing to explain");
    }

    // D06 (owner ruling 2026-08-02): the KEYBOARD route follows the MENU BAR, while the row's own command stays
    // the gate (D02) — so one row legitimately answers differently on the two, which is the whole point: the
    // flyout's Cut on a locked block runs, Ctrl+X does not. A row the bar never shows falls back to its gate.
    // Binding a KeyBinding to Commands[...] instead would let the shortcut run what the bar refuses, and no key
    // handler can undo that — Avalonia invokes a TopLevel's KeyBindings before any instance KeyDown handler.
    [Test]
    public void Registry_GestureCommand_FollowsTheBar_WhileTheRowCommandFollowsTheGate()
    {
        var registry = new CommandRegistry(() => Open);
        registry.Register(Row("barRestricted", surfacePolicy: (_, surface) =>
            surface == Surface.ContextMenu ? null : Availability.Disabled("Not from the bar.")));
        registry.Register(Row("keysOnly", placement: Surfaces.None));
        registry.Register(Row("refused", gate: _ => EditVerdict.Refuse("No.")));

        Assert.Multiple(() =>
        {
            Assert.That(registry.Commands["barRestricted"].CanExecute(null), Is.True,
                "the row command IS the gate (D02) — the flyout still runs it");
            Assert.That(registry.GestureCommands["barRestricted"].CanExecute(null), Is.False,
                "…while the gesture obeys the bar's restriction (D06)");
            Assert.That(registry.GestureCommands["keysOnly"].CanExecute(null), Is.True,
                "a row the bar never shows is judged by its own gate");
            Assert.That(registry.GestureCommands["refused"].CanExecute(null), Is.False,
                "a failed gate refuses both routes");
        });
    }

    [Test]
    public void Registry_DuplicateId_Throws()
    {
        var registry = new CommandRegistry(() => Open);
        registry.Register(Row());

        Assert.Throws<ArgumentException>(() => registry.Register(Row()),
            "one row per command id — two authorities must never exist (D02)");
    }
}
