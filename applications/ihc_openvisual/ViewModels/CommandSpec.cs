using System;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace ihc_openvisual.ViewModels;

/// <summary>A concrete command surface: where a registry row is being rendered right now. The evaluator's
/// hide-vs-grey default keys on this — the transient <see cref="ContextMenu"/> omits a gate-failed command
/// (US-068), the persistent <see cref="MenuBar"/>/<see cref="Toolbar"/> grey it with a reason (US-044).</summary>
public enum Surface { MenuBar, ContextMenu, Toolbar }

/// <summary>The placement flags of a registry row (proposal §3.3 review F6): on which surfaces the command
/// exists AT ALL. Presence is registry data; XAML keeps only structure/order/grouping.</summary>
[Flags]
public enum Surfaces { None = 0, MenuBar = 1, ContextMenu = 2, Toolbar = 4 }

public static class SurfaceExtensions
{
    /// <summary>The placement flag of a concrete surface.</summary>
    public static Surfaces Flag(this Surface surface) => surface switch
    {
        Surface.MenuBar => Surfaces.MenuBar,
        Surface.ContextMenu => Surfaces.ContextMenu,
        _ => Surfaces.Toolbar,
    };

    /// <summary>Whether the placement includes the given surface.</summary>
    public static bool Contains(this Surfaces placement, Surface surface) => (placement & surface.Flag()) != 0;
}

/// <summary>A row's availability on ONE surface (crudarch D10): hidden, allowed, or disabled with the reason
/// the control's tooltip/status hint shows (QC-06 — a disabled item explains itself). Named <see cref="Allow"/>
/// rather than "Enabled" to avoid the CS0102 clash with the positional property.</summary>
[CommandContextValue]
public sealed record Availability(bool Visible, bool Enabled, string? Reason)
{
    /// <summary>Not rendered on this surface at all (unplaced, or omitted by the transient-surface default).</summary>
    public static readonly Availability Hidden = new(false, false, null);

    /// <summary>Visible and executable.</summary>
    public static readonly Availability Allow = new(true, true, null);

    /// <summary>Visible but greyed, carrying the reason the surface shows (US-044/QC-06).</summary>
    public static Availability Disabled(string reason) => new(true, false, reason);
}

/// <summary>
/// One declarative registry row per user-facing command (crudarch D02, proposal §3.3): the single home for the
/// command's stable <paramref name="Id"/> ("edit.cut" — addressable from tests and XAML), keyboard
/// <paramref name="Gesture"/> (a plain STRING per D08 — parsing to an Avalonia KeyGesture is view-side, so rows
/// stay headless-testable), <paramref name="Placement"/> (where it exists at all), the ONE
/// <paramref name="Execute"/> action, the ONE <paramref name="Gate"/> execution predicate (reasons included —
/// the three SDK idioms normalise inside it: factory-null → Refuse, gateway queries and document.CanApply →
/// verdicts), and an optional <paramref name="SurfacePolicy"/> that may only RESTRICT past the gate (the D13
/// per-surface spec divergences), never widen. The registry materializes the IRelayCommand from
/// <paramref name="Gate"/> — a row author never builds a command, so two authorities cannot disagree.
/// </summary>
/// <remarks>A row carries no CAPTION: a command's text can legitimately differ per surface (Undo/Redo show the
/// action-decorated "_Fortryd Indsæt lokalitet" where the flyout has no such row), so the menu text belongs to the
/// markup that renders it, and a row-level copy could only ever be a second, unread home for it (review F12).
/// (The flyout insert labels themselves now match the bar's bare nouns — the vendor uses the same word on both
/// surfaces; measured 2026-08-09, alignment F-18.)</remarks>
/// <remarks>
/// <para><paramref name="Execute"/> ANSWERS an <see cref="Ihc.OperationOutcome"/>, and that is what lets a
/// gesture's trace root say whether the gesture worked. The registry's invocation funnel is the root span of
/// every menu, toolbar, flyout and keyboard route; a row that handed back a bare task left that root recording
/// success for a save that had failed and reported itself, because nothing threw past the view-model's error
/// boundary. A row with nothing to declare answers <see cref="Ihc.OperationOutcome.Ok"/>, which is what the
/// boundary returns when no body said otherwise — so most rows say nothing and are unchanged.</para>
/// </remarks>
[CommandContextValue]
public sealed record CommandSpec(
    string Id,
    string? Gesture,
    Surfaces Placement,
    Func<ShellContext, Task<Ihc.OperationOutcome>> Execute,
    Func<ShellContext, EditVerdict> Gate,
    Func<ShellContext, Surface, Availability?>? SurfacePolicy = null);
