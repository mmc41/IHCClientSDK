namespace ihc_openvisual.ViewModels;

/// <summary>The effect a drag-over yields, kept independent of Avalonia's <c>DragDropEffects</c> so the drop
/// legality stays in the (Avalonia-free) controller and testable headlessly; the code-behind maps it across (A-30).</summary>
public enum DropEffect
{
    /// <summary>No drop here — the drag-over shows the "no-drop" cursor and nothing lands.</summary>
    None,
    /// <summary>The drop re-parents / reorders the dragged node (US-054/US-055).</summary>
    Move,
    /// <summary>The drop links the dragged node to the target (US-022/US-023) or builds a program from it (US-028).</summary>
    Link,
}

/// <summary>The specific route a legal drop takes — resolved ONCE by <see cref="TreeDragDropController.CanDropOn"/> so
/// the drop performs it without re-evaluating the legality (W3-9). Both <see cref="PinLink"/> and
/// <see cref="ProgramBuild"/> present as <see cref="DropEffect.Link"/>; both <see cref="Reorder"/> and
/// <see cref="Reparent"/> present as <see cref="DropEffect.Move"/> — the route is what tells them apart.</summary>
public enum DropRoute
{
    None,
    /// <summary>Link two pins (US-022/US-023).</summary>
    PinLink,
    /// <summary>Arm the Use-in-program method popup for a variable dropped on an events/commands container (US-028).</summary>
    ProgramBuild,
    /// <summary>Reorder among same-tag siblings (US-055).</summary>
    Reorder,
    /// <summary>Re-parent a product into another locality (US-054).</summary>
    Reparent,
}

/// <summary>
/// The outcome of asking whether a dragged node may be dropped on a target — the value
/// <see cref="TreeDragDropController.CanDropOn"/> returns and the drop dispatcher routes on (A-30). Carries the
/// <see cref="Effect"/> the drag-over presents, the resolved <see cref="Route"/> the drop takes, and — when refused —
/// a human <see cref="Reason"/> for the status bar.
/// </summary>
public readonly record struct DropVerdict(bool Ok, DropEffect Effect, DropRoute Route = DropRoute.None, string? Reason = null)
{
    /// <summary>Not a drop target at all — refused silently (no highlight, nothing to say).</summary>
    public static readonly DropVerdict None = new(false, DropEffect.None);

    /// <summary>Refused with a reason to show the user (e.g. a node dropped onto itself).</summary>
    public static DropVerdict Refused(string reason) => new(false, DropEffect.None, DropRoute.None, reason);

    /// <summary>A legal pin-link drop (US-022/US-023).</summary>
    public static DropVerdict PinLink() => new(true, DropEffect.Link, DropRoute.PinLink);

    /// <summary>A legal program-build drop — a variable armed onto an events/commands container (US-028).</summary>
    public static DropVerdict ProgramBuild() => new(true, DropEffect.Link, DropRoute.ProgramBuild);

    /// <summary>A legal reorder drop among same-tag siblings (US-055).</summary>
    public static DropVerdict Reorder() => new(true, DropEffect.Move, DropRoute.Reorder);

    /// <summary>A legal re-parent drop into another locality (US-054).</summary>
    public static DropVerdict Reparent() => new(true, DropEffect.Move, DropRoute.Reparent);
}
