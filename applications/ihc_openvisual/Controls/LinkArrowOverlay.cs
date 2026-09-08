using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;

namespace ihc_openvisual.Controls;

/// <summary>
/// One arrow, resolved to coordinates: a cubic bezier from the producer's row to what it drives, plus the family
/// it belongs to so the pen can tell a follow-link from a scenario link.
/// </summary>
/// <remarks>Public as the overlay's test seam. Drawing is the one part of US-089 that only realized controls can
/// answer for — a row's rectangle, a pane's viewport — so the geometry is built here and asserted from
/// <c>safe_visual_tests</c> over a shown window, rather than inferred from a screenshot.</remarks>
public readonly record struct LinkArrowShape(
    Point Start, Point Control1, Point Control2, Point End, LinkArrowEdge Edge, bool CrossesPanes, bool IsSelected)
{
    /// <summary>Which family of wire this is — the pen's only input besides selection.</summary>
    public LinkArrowKind Kind => Edge.Kind;

    /// <summary>The width this arrow's line is drawn at: the SELECTED wire is drawn at double the ordinary
    /// width, which is the one selection cue that survives the high-contrast palette and reads over whatever
    /// the line happens to cross (US-089).</summary>
    public double StrokeWidth =>
        IsSelected ? LinkArrowOverlay.SelectedLineThickness : LinkArrowOverlay.LineThickness;
}

/// <summary>
/// Draws the wires of <i>Vis ▸ Links ▸ Pile</i> (US-089) over the two tree panes: for each planned
/// <see cref="LinkArrowEdge"/>, one directed line from the producing pin's row to the row it drives — within one
/// pane or across both.
///
/// <para>It owns no state about the project. The edges come from the view-model's plan, and every coordinate is
/// read from the LIVE controls at render time, so scrolling, expanding, resizing and a splitter drag all move the
/// arrows with the rows they connect without anything having to be invalidated by hand.</para>
///
/// <para>A row whose subtree is closed is not realized, so the arrow anchors on the nearest ancestor that IS
/// realized (the endpoint's chain is walked from the row outwards) and an anchor scrolled out of its pane is
/// clamped to that pane's edge. Both are deliberate: an arrow that ends at a closed locality still says where the
/// wire goes, where a dropped arrow says nothing and an unclamped one is drawn over the neighbouring panel.</para>
///
/// <para>It is pointer-transparent everywhere EXCEPT along a line, which it achieves by answering
/// <see cref="ICustomHitTest"/> from the same geometry it draws. That is what makes the pointer over a wire belong
/// to the wire: without it the row underneath still lit its hover highlight, so aiming at a line read as picking a
/// row — the confusion, and the source of a mis-click.</para>
///
/// <para>A wire claims the pointer only where the SHELL underneath has nothing of its own there, though: an arrow
/// drawn across a row must not cost the installer the twisty that opens it, nor the row's own click, nor — where
/// every cross-pane wire has to run — the splitter between the panes. What is underneath is asked
/// (<see cref="WiresClaimingPointer"/>) rather than second-guessed, so the rule holds whatever the item template
/// grows. It costs the wires nothing they need — these lines run through free space, and one always leaves its pin
/// past the label — so every wire keeps a stretch that can be grabbed. <see cref="PassThroughPointer"/> hands the
/// whole band back while a drag is in flight, so a drop that lands on a line still reaches the pane beneath.</para>
///
/// <para><see cref="LinkDisplay.Labels"/> nevertheless stays the DEFAULT: a link row is announced by a screen
/// reader and reachable by keyboard, and an arrow is neither. The overlay is focusable only so a selected wire has
/// a keyboard-focus owner to deliver <c>Delete</c> to; it is never a tab stop.</para>
/// </summary>
public sealed class LinkArrowOverlay : Control, ICustomHitTest
{
    /// <summary>The wires to draw — <c>MainWindowViewModel.LinkArrows</c>, empty in labels mode.</summary>
    public static readonly StyledProperty<IReadOnlyList<LinkArrowEdge>?> EdgesProperty =
        AvaloniaProperty.Register<LinkArrowOverlay, IReadOnlyList<LinkArrowEdge>?>(nameof(Edges));

    /// <summary>The <i>Installation</i> pane's tree; endpoints on that side are resolved against it.</summary>
    public static readonly StyledProperty<TreeView?> InstallationTreeProperty =
        AvaloniaProperty.Register<LinkArrowOverlay, TreeView?>(nameof(InstallationTree));

    /// <summary>The <i>Funktioner</i> pane's tree.</summary>
    public static readonly StyledProperty<TreeView?> FunctionsTreeProperty =
        AvaloniaProperty.Register<LinkArrowOverlay, TreeView?>(nameof(FunctionsTree));

    /// <summary>The arrow ink. A token rather than a literal, so the high-contrast palette can replace it.</summary>
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<LinkArrowOverlay, IBrush?>(nameof(Stroke));

    /// <summary>The casing drawn under the ink, in the workspace's own background colour, so a line crossing a
    /// tree label stays readable and the label stays readable through it.</summary>
    public static readonly StyledProperty<IBrush?> HaloProperty =
        AvaloniaProperty.Register<LinkArrowOverlay, IBrush?>(nameof(Halo));

    /// <summary>The link row of the selected wire (US-089) — the one arrow drawn at double width.</summary>
    public static readonly StyledProperty<ElementId?> SelectedArrowProperty =
        AvaloniaProperty.Register<LinkArrowOverlay, ElementId?>(nameof(SelectedArrow));

    public IReadOnlyList<LinkArrowEdge>? Edges
    {
        get => GetValue(EdgesProperty);
        set => SetValue(EdgesProperty, value);
    }

    public TreeView? InstallationTree
    {
        get => GetValue(InstallationTreeProperty);
        set => SetValue(InstallationTreeProperty, value);
    }

    public TreeView? FunctionsTree
    {
        get => GetValue(FunctionsTreeProperty);
        set => SetValue(FunctionsTreeProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? Halo
    {
        get => GetValue(HaloProperty);
        set => SetValue(HaloProperty, value);
    }

    public ElementId? SelectedArrow
    {
        get => GetValue(SelectedArrowProperty);
        set => SetValue(SelectedArrowProperty, value);
    }

    // Clearance between a row's own content and where its arrow attaches, so a line never touches the text.
    private const double Gap = 5;
    // How close to a pane's border an anchor may be pushed once it is clamped.
    private const double Edge = 4;
    // The shortest horizontal reach of a bezier's control points; below it a curve reads as a kink.
    private const double MinReach = 26;
    /// <summary>The ordinary width of an arrow's line.</summary>
    public const double LineThickness = 1.7;

    /// <summary>The width the SELECTED arrow is drawn at — double, and stated as such rather than as a second
    /// literal, so the two can never drift apart.</summary>
    public const double SelectedLineThickness = LineThickness * 2;
    // How far the casing stands out beyond the ink on each side. Added to whatever width the line is drawn
    // at, so a SELECTED wire keeps the same margin rather than a proportionally thinner one.
    private const double HaloMargin = 2.8;
    private const double HeadLength = 9;
    private const double HeadHalfWidth = 4.2;
    private const double TailRadius = 2.6;
    // How near a click has to fall to count as aimed at a wire. Wide enough that a 1.7px line is not a test of
    // aim, narrow enough that two wires being within it of one pointer really does mean they overlap there.
    private const double HitTolerance = 6;
    // The chord count the curve is measured by. At the curvatures these arrows draw, a 24-chord polyline is
    // sub-pixel from the true bezier, so a sampled distance and the real one agree well inside the tolerance.
    private const int HitSamples = 24;

    static LinkArrowOverlay()
    {
        AffectsRender<LinkArrowOverlay>(EdgesProperty, StrokeProperty, HaloProperty,
            InstallationTreeProperty, FunctionsTreeProperty, SelectedArrowProperty);
    }

    /// <summary>
    /// While set, the overlay answers NO to every hit test, handing its band back to the panes underneath.
    /// <para>Set around a drag: hit testing cannot tell a click from a drop, so without this a drop that landed on
    /// the few pixels of a line would be swallowed by a control that is not a drop target — a failure with nothing
    /// on screen to explain it.</para>
    /// </summary>
    public bool PassThroughPointer { get; set; }

    /// <summary>
    /// The splitter between the two panes, whose strip the wires never claim (<see cref="WiresClaimingPointer"/>).
    /// <para>A plain property rather than a styled one, and handed over by the view like the two panes are: it is
    /// something the overlay must not cover, never something it draws from.</para>
    /// </summary>
    public Control? PaneSplitter { get; set; }

    public LinkArrowOverlay()
    {
        // A pointer over a line is over the WIRE, which is what the hand says. Everywhere else the cursor never
        // changes, because everywhere else the pointer is not over this control at all.
        Cursor = new Cursor(StandardCursorType.Hand);
        // Focusable, but never a TAB stop. A selected wire needs a keyboard-focus OWNER, because Avalonia
        // delivers a key press to the focused element and to nothing else — measured on the running application:
        // click a line straight after using the menu (which is how the mode gets turned on, so focus is not in a
        // pane), and Delete reached no handler at all while Rediger ▸ Slet sat there enabled. Focus arrives only
        // by a click ON a line, so nothing else about the shell's keyboard order changes.
        Focusable = true;
        IsTabStop = false;
        // Every coordinate is read at render time, so the overlay only has to learn that SOMETHING moved. A layout
        // pass covers expanding, resizing and the splitter; scrolling is a routed ScrollChanged the panes bubble.
        LayoutUpdated += (_, _) => Redraw();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == InstallationTreeProperty || change.Property == FunctionsTreeProperty)
        {
            (change.OldValue as TreeView)?.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnPaneScrolled);
            (change.NewValue as TreeView)?.AddHandler(ScrollViewer.ScrollChangedEvent, OnPaneScrolled);
        }
    }

    private void OnPaneScrolled(object? sender, ScrollChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        if (Edges is { Count: > 0 })
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        base.Render(context);

        if (Stroke is not { } ink)
        {
            return;
        }
        foreach (LinkArrowShape arrow in BuildArrows())
        {
            double width = arrow.StrokeWidth;
            // A scenario link is dashed: it drives a whole scene rather than one terminal, and the two families
            // are otherwise indistinguishable once the labels are gone.
            var pen = arrow.Kind == LinkArrowKind.Scene
                ? new Pen(ink, width, new DashStyle([4, 3], 0), PenLineCap.Round)
                : new Pen(ink, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            StreamGeometry curve = Curve(arrow);
            StreamGeometry head = Head(arrow);
            if (Halo is { } halo)
            {
                context.DrawGeometry(null, new Pen(halo, width + HaloMargin, lineCap: PenLineCap.Round), curve);
                context.DrawGeometry(null, new Pen(halo, HaloMargin), head);
                context.DrawEllipse(halo, null, arrow.Start, TailRadius + 1.4, TailRadius + 1.4);
            }
            context.DrawGeometry(null, pen, curve);
            // The tail dot says which end the wire leaves from even where the head is off-screen.
            context.DrawEllipse(ink, null, arrow.Start, TailRadius, TailRadius);
            context.DrawGeometry(ink, null, head);
        }
    }

    /// <summary>
    /// The arrows as this frame would draw them. Public so the geometry can be asserted directly: what makes an
    /// arrow right is where its ends landed, and a rendered surface cannot be asked that.
    /// </summary>
    public IReadOnlyList<LinkArrowShape> BuildArrows()
    {
        var arrows = new List<LinkArrowShape>();
        if (Edges is not { Count: > 0 } edges
            || PaneOf(InstallationTree) is not { } installation
            || PaneOf(FunctionsTree) is not { } functions)
        {
            return arrows;
        }
        Dictionary<ElementId, Rect> installationRows = Rows(InstallationTree!);
        Dictionary<ElementId, Rect> functionsRows = Rows(FunctionsTree!);

        foreach (LinkArrowEdge edge in edges)
        {
            if (Anchor(edge.From) is not { } from || Anchor(edge.To) is not { } to)
            {
                continue;
            }
            // Both ends folded onto the SAME row — a wire entirely inside one closed subtree. There is nothing to
            // point from or at, and the arrow would be a loop leaving and re-entering one row, which reads as a
            // wire that goes nowhere rather than as one the installer has closed away.
            if (from.Side == to.Side && from.Row == to.Row)
            {
                continue;
            }
            arrows.Add(Shape(from, to, edge, edge.Half == SelectedArrow));
        }
        return arrows;

        Anchored? Anchor(LinkArrowEndpoint end)
        {
            bool left = end.Pane == LinkArrowPane.Installation;
            Dictionary<ElementId, Rect> rows = left ? installationRows : functionsRows;
            Rect pane = left ? installation : functions;
            // Deepest first: the row itself when it is on screen, else the closest ancestor that is.
            for (int i = end.Path.Count - 1; i >= 0; i--)
            {
                if (rows.TryGetValue(end.Path[i], out Rect row))
                {
                    return new Anchored(row, pane, end.Pane);
                }
            }
            return null;
        }
    }

    // A resolved end: the row's rectangle, the viewport it must stay inside, and which pane that is.
    private readonly record struct Anchored(Rect Row, Rect Pane, LinkArrowPane Side);

    private static LinkArrowShape Shape(Anchored from, Anchored to, LinkArrowEdge edge, bool selected)
    {
        double y0 = Math.Clamp(from.Row.Center.Y, from.Pane.Top + Edge, from.Pane.Bottom - Edge);
        double y1 = Math.Clamp(to.Row.Center.Y, to.Pane.Top + Edge, to.Pane.Bottom - Edge);

        if (from.Side == to.Side)
        {
            // Both ends in one pane: attach on the RIGHT of each row and bow out into the empty space beside the
            // labels, which is the only part of a tree pane reliably free of text.
            double x0 = Math.Min(from.Row.Right + Gap, from.Pane.Right - Edge);
            double x1 = Math.Min(to.Row.Right + Gap, to.Pane.Right - Edge);
            double reach = Math.Max(MinReach, Math.Abs(y1 - y0) * 0.35);
            double bow = Math.Min(Math.Max(x0, x1) + reach, from.Pane.Right - Edge);
            return new LinkArrowShape(new Point(x0, y0), new Point(bow, y0), new Point(bow, y1), new Point(x1, y1),
                edge, CrossesPanes: false, selected);
        }

        // Across the panes: leave the source on the side facing the target and arrive on the target's facing side,
        // so the line crosses the splitter once instead of doubling back around a pane.
        bool rightwards = from.Side == LinkArrowPane.Installation;
        double startX = rightwards
            ? Math.Min(from.Row.Right + Gap, from.Pane.Right - Edge)
            : Math.Max(from.Row.Left - Gap, from.Pane.Left + Edge);
        double endX = rightwards
            ? Math.Max(to.Row.Left - Gap, to.Pane.Left + Edge)
            : Math.Min(to.Row.Right + Gap, to.Pane.Right - Edge);
        double span = Math.Max(MinReach, Math.Abs(endX - startX) * 0.35);
        return new LinkArrowShape(
            new Point(startX, y0),
            new Point(rightwards ? startX + span : startX - span, y0),
            new Point(rightwards ? endX - span : endX + span, y1),
            new Point(endX, y1),
            edge, CrossesPanes: true, selected);
    }

    private static StreamGeometry Curve(LinkArrowShape arrow)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(arrow.Start, isFilled: false);
            context.CubicBezierTo(arrow.Control1, arrow.Control2, arrow.End);
            context.EndFigure(isClosed: false);
        }
        return geometry;
    }

    // The head, oriented along the curve's END tangent — which for a cubic bezier is the direction from its last
    // control point to its end, so the head always continues the line rather than pointing at the straight chord.
    private static StreamGeometry Head(LinkArrowShape arrow)
    {
        Vector along = arrow.End - arrow.Control2;
        along = along.Length < 0.001 ? new Vector(1, 0) : along.Normalize();
        var across = new Vector(-along.Y, along.X);
        Point back = arrow.End - along * HeadLength;

        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(arrow.End, isFilled: true);
            context.LineTo(back + across * HeadHalfWidth);
            context.LineTo(back - across * HeadHalfWidth);
            context.EndFigure(isClosed: true);
        }
        return geometry;
    }

    /// <summary>Whether the pointer is over a wire this overlay may claim — the hit test the input system asks,
    /// answered from the same geometry the frame draws, so the clickable band and the visible line cannot drift
    /// apart.</summary>
    bool ICustomHitTest.HitTest(Point point) => WiresClaimingPointer(point).Count > 0;

    /// <summary>
    /// The wires a press at <paramref name="point"/> would act on — <see cref="WiresAt"/>, minus everything the
    /// panes have a prior claim to.
    /// <para>Empty where a pane owns the point (a row's expand/collapse twisty, or its icon and label) and over
    /// the splitter between them, because the shell's own controls win: a line drawn across a parent row must not
    /// be what stops it opening, nor the strip every cross-pane wire crosses what stops the panes resizing. Empty
    /// too while <see cref="PassThroughPointer"/> is set, and in labels mode, where there is nothing drawn.</para>
    /// <para>The ONE answer both the hit test and the window's press handler use — the two deciding this
    /// separately is how a click gets refused by one and consumed by the other.</para>
    /// </summary>
    public IReadOnlyList<LinkArrowEdge> WiresClaimingPointer(Point point) =>
        PassThroughPointer || Edges is not { Count: > 0 } || ShellOwnsPointer(point)
            ? Array.Empty<LinkArrowEdge>()
            : WiresAt(point);

    // Whether the shell underneath has something of its OWN under this point: a pane's control, or the splitter
    // between them. Asked of the pane through the framework's own hit test rather than re-derived from rectangles
    // here, so it keeps answering correctly however the row template changes; the walk up stops at the row
    // container, since anything above it is the pane itself.
    private bool ShellOwnsPointer(Point point) =>
        SplitterOwns(point) || PaneOwns(InstallationTree, point) || PaneOwns(FunctionsTree, point);

    // The splitter keeps its whole strip. It is the one control under this overlay operated by DRAGGING, and a
    // press cannot be told from the start of a drag on the way in — so a wire claiming the strip would leave the
    // panes unresizable along the band it crosses, and every cross-pane wire crosses it. Its bounds are the whole
    // answer because a drag it never receives the press for is a drag it cannot start, wherever inside its
    // template the pointer landed.
    private bool SplitterOwns(Point point) =>
        PaneSplitter is { IsEffectivelyVisible: true } splitter
        && this.TranslatePoint(point, splitter) is { } local
        && new Rect(splitter.Bounds.Size).Contains(local);

    private bool PaneOwns(TreeView? pane, Point point)
    {
        if (pane is null || this.TranslatePoint(point, pane) is not { } local
            || pane.InputHitTest(local) is not Visual hit)
        {
            return false;
        }
        for (Visual? part = hit; part is not null and not TreeViewItem; part = part.GetVisualParent())
        {
            if (part is ToggleButton)
            {
                return true;   // the expand/collapse twisty
            }
            if (part is StackPanel { Orientation: Avalonia.Layout.Orientation.Horizontal, DataContext: TreeNodeViewModel } row)
            {
                // The row's content panel STRETCHES the full width of the pane, so being inside it says nothing.
                // Only as far as its content reaches does the row own the pointer — the free margin past the
                // label is nobody's, and is exactly where every arrow attaches.
                return this.TranslatePoint(point, row) is { } inRow && inRow.X <= ContentWidth(row);
            }
        }
        return false;
    }

    /// <summary>
    /// Every wire drawn within <see cref="HitTolerance"/> of <paramref name="point"/>, as GEOMETRY — what is
    /// drawn there, before any question of who may claim the pointer (<see cref="WiresClaimingPointer"/>).
    /// <para>Deliberately a LIST rather than a nearest match: two lines that overlap where the pointer is are not
    /// one the installer can be said to have aimed at, so the caller refuses rather than guessing (US-089).</para>
    /// </summary>
    public IReadOnlyList<LinkArrowEdge> WiresAt(Point point)
    {
        var hits = new List<LinkArrowEdge>();
        foreach (LinkArrowShape arrow in BuildArrows())
        {
            if (DistanceTo(arrow, point) <= HitTolerance)
            {
                hits.Add(arrow.Edge);
            }
        }
        return hits;
    }

    // The distance from a point to the curve, measured along the chords the curve is sampled into.
    private static double DistanceTo(LinkArrowShape arrow, Point point)
    {
        double nearest = double.MaxValue;
        Point previous = arrow.Start;
        for (int i = 1; i <= HitSamples; i++)
        {
            Point next = OnCurve(arrow, (double)i / HitSamples);
            nearest = Math.Min(nearest, DistanceToSegment(point, previous, next));
            previous = next;
        }
        return nearest;
    }

    private static Point OnCurve(LinkArrowShape arrow, double t)
    {
        double u = 1 - t;
        return new Point(
            (u * u * u * arrow.Start.X) + (3 * u * u * t * arrow.Control1.X)
                + (3 * u * t * t * arrow.Control2.X) + (t * t * t * arrow.End.X),
            (u * u * u * arrow.Start.Y) + (3 * u * u * t * arrow.Control1.Y)
                + (3 * u * t * t * arrow.Control2.Y) + (t * t * t * arrow.End.Y));
    }

    private static double DistanceToSegment(Point point, Point from, Point to)
    {
        Vector along = to - from;
        Vector offset = point - from;
        double lengthSquared = (along.X * along.X) + (along.Y * along.Y);
        double t = lengthSquared < double.Epsilon
            ? 0                                                                                  // a degenerate chord is its own start
            : Math.Clamp(((offset.X * along.X) + (offset.Y * along.Y)) / lengthSquared, 0, 1);
        Point nearest = from + (along * t);
        return Math.Sqrt(((point.X - nearest.X) * (point.X - nearest.X))
                         + ((point.Y - nearest.Y) * (point.Y - nearest.Y)));
    }

    // A pane's viewport in this overlay's coordinates; null when the tree is absent or not laid out yet.
    private Rect? PaneOf(TreeView? tree) =>
        tree is { Bounds: { Width: > 0, Height: > 0 } bounds } && tree.TranslatePoint(default, this) is { } origin
            ? new Rect(origin, bounds.Size)
            : null;

    // Every realized row of one pane, by element id, as rectangles in this overlay's coordinates. Realized is the
    // operative word: a collapsed or virtualized-away row is simply absent, which is what makes the endpoint
    // chains' ancestor fallback the answer rather than a special case.
    private Dictionary<ElementId, Rect> Rows(Control tree)
    {
        var rows = new Dictionary<ElementId, Rect>();
        Collect(tree);
        return rows;

        void Collect(Visual visual)
        {
            foreach (Visual child in visual.GetVisualChildren())
            {
                if (child is TreeViewItem { DataContext: TreeNodeViewModel { ElementId: { } id } } item
                    && RowRect(item) is { } rect)
                {
                    rows.TryAdd(id, rect);
                }
                Collect(child);
            }
        }
    }

    // A row's rectangle: its own header content, NOT the TreeViewItem, whose bounds span the whole subtree — and
    // not the header presenter either, which stretches the full pane width and would put every arrow's tail on the
    // same vertical line instead of just past each label.
    private Rect? RowRect(TreeViewItem item)
    {
        // EFFECTIVELY visible, not IsVisible: closing a TreeViewItem hides its items presenter but leaves the
        // containers under it realized, still carrying the rectangles they had when open. Read as visible, those
        // stale rows would anchor arrows on coordinates nothing occupies — which is exactly the case the ancestor
        // fallback exists to handle, and it never gets a turn if a closed row still answers.
        if (RowContent(item) is not { IsEffectivelyVisible: true } row
            || row.TranslatePoint(default, this) is not { } origin)
        {
            return null;
        }
        double width = ContentWidth(row);
        return width > 0 ? new Rect(origin, new Size(width, row.Bounds.Height)) : null;
    }

    // The item template's own row panel: the first horizontal StackPanel under this item that is bound to a tree
    // node. The descent stops at a nested TreeViewItem, which owns its own row and must not answer for its parent.
    private static StackPanel? RowContent(TreeViewItem item)
    {
        return Find(item);

        static StackPanel? Find(Visual visual)
        {
            foreach (Visual child in visual.GetVisualChildren())
            {
                if (child is TreeViewItem)
                {
                    continue;
                }
                if (child is StackPanel { Orientation: Avalonia.Layout.Orientation.Horizontal, DataContext: TreeNodeViewModel } row)
                {
                    return row;
                }
                if (Find(child) is { } found)
                {
                    return found;
                }
            }
            return null;
        }
    }

    // How far the row's content actually reaches. The panel itself stretches, so its own width says nothing; the
    // last visible child's right edge is where the label ends and the free space beside it begins.
    private static double ContentWidth(Panel row)
    {
        double right = 0;
        foreach (Control child in row.Children)
        {
            if (child.IsVisible)
            {
                right = Math.Max(right, child.Bounds.Right);
            }
        }
        return right > 0 ? right : row.Bounds.Width;
    }
}
