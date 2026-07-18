namespace ihc_openvisual.ViewModels;

/// <summary>The effect a drag-over yields, kept independent of Avalonia's <c>DragDropEffects</c> so the drop
/// legality stays in the (Avalonia-free) view-model and testable headlessly; the code-behind maps it across (A-30).</summary>
public enum DropEffect
{
    /// <summary>No drop here — the drag-over shows the "no-drop" cursor and nothing lands.</summary>
    None,
    /// <summary>The drop re-parents / reorders the dragged node (US-054/US-055).</summary>
    Move,
    /// <summary>The drop links the dragged node to the target (US-022/US-023) or builds a program from it (US-028).</summary>
    Link,
}

/// <summary>
/// The outcome of asking whether a dragged node may be dropped on a target — the value
/// <see cref="MainWindowViewModel.CanDropOn"/> returns and the Wave-9 drop dispatcher routes on (A-30). Carries the
/// <see cref="Effect"/> the drag-over should present and, when refused, a human <see cref="Reason"/> for the status bar.
/// </summary>
public readonly record struct DropVerdict(bool Ok, DropEffect Effect, string? Reason = null)
{
    /// <summary>Not a drop target at all — refused silently (no highlight, nothing to say).</summary>
    public static readonly DropVerdict None = new(false, DropEffect.None);

    /// <summary>Refused with a reason to show the user (e.g. a node dropped onto itself).</summary>
    public static DropVerdict Refused(string reason) => new(false, DropEffect.None, reason);

    /// <summary>A legal move/reorder drop (US-054/US-055).</summary>
    public static DropVerdict Moving() => new(true, DropEffect.Move);

    /// <summary>A legal link / program-build drop (US-022/US-023/US-028).</summary>
    public static DropVerdict Linking() => new(true, DropEffect.Link);
}
