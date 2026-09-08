using System;
using System.Collections.Generic;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace ihc_openvisual.ViewModels;

/// <summary>Which tree pane an arrow endpoint's row is drawn in (US-089).</summary>
public enum LinkArrowPane
{
    /// <summary>The <i>Installation</i> pane — localities, products, their pins and scene containers.</summary>
    Installation,

    /// <summary>The <i>Funktioner</i> pane — localities, function blocks, their variable sections and pins.</summary>
    Functions,
}

/// <summary>Which family of wire an arrow stands for (US-089).</summary>
public enum LinkArrowKind
{
    /// <summary>A follow-link between two pins (US-022/US-023).</summary>
    Follow,

    /// <summary>A scenario link from a block's scene output to a product's <c>scenes</c> container (US-024).</summary>
    Scene,
}

/// <summary>
/// One end of an arrow: the row it anchors on, and the ancestor chain that leads to it.
/// <para>The chain is what lets the overlay stay honest when the row itself is not realized — a collapsed
/// locality, a block that was never opened — by anchoring on the nearest ancestor that IS on screen instead of
/// dropping the arrow or, worse, drawing it to a coordinate nothing occupies.</para>
/// </summary>
public sealed class LinkArrowEndpoint
{
    internal LinkArrowEndpoint(IReadOnlyList<ElementId> path, LinkArrowPane pane)
    {
        Path = path;
        Pane = pane;
    }

    /// <summary>The chain from the outermost ancestor down to <see cref="Element"/>, which is its last entry.</summary>
    public IReadOnlyList<ElementId> Path { get; }

    /// <summary>The pane whose tree draws this end. A locality is drawn in BOTH panes, so an ancestor fallback
    /// cannot be told apart by its id alone — the pane is decided here, from whether the chain passes through a
    /// function block, exactly as the projector splits the two panes.</summary>
    public LinkArrowPane Pane { get; }

    /// <summary>The row the arrow points at: a pin, or a product's <c>scenes</c> container.</summary>
    public ElementId Element => Path[^1];
}

/// <summary>An arrow to draw: one wire, rendered once, pointing from its producer to what it drives.</summary>
public sealed class LinkArrowEdge
{
    internal LinkArrowEdge(LinkArrowEndpoint from, LinkArrowEndpoint to, LinkArrowKind kind, ElementId half)
    {
        From = from;
        To = to;
        Kind = kind;
        Half = half;
    }

    public LinkArrowEndpoint From { get; }

    public LinkArrowEndpoint To { get; }

    public LinkArrowKind Kind { get; }

    /// <summary>
    /// The link ROW this arrow stands in for — the half the plan was built from, which still exists in the
    /// document and is simply not on screen. It is both the arrow's identity across re-plans and what
    /// <i>Slet</i> removes, so deleting an arrow is literally the US-057 row delete on the row the mode hid.
    /// </summary>
    /// <remarks>Which of the two reciprocal halves it is does not matter to either use: the plan walks the
    /// document in order so the same one is chosen every time, and removing either takes the pair.</remarks>
    public ElementId Half { get; }
}

/// <summary>
/// Turns a project's link halves into the arrows <i>Vis ▸ Links ▸ Pile</i> draws (US-089) — Avalonia-free, so the
/// decision is testable without a window and the view-models stay clear of drawing types.
///
/// <para>The projector and this planner ask the SAME question through <see cref="DrawsAsArrow"/>: a half either
/// keeps its label row or becomes part of an arrow, never both and never neither. That is the whole reason the
/// predicate is public — two renderings of one fact drift the moment they are decided in two places.</para>
///
/// <para>A wire is written as a reciprocal PAIR of halves (US-022), so both halves resolve to the same ordered
/// pair of rows and the second is dropped: one wire, one arrow. Direction comes from the file's own orientation —
/// the producer owns the <c>link_from_resource</c> half — never from which pane a row happens to sit in.</para>
/// </summary>
public static class LinkArrowPlanner
{
    /// <summary>
    /// Whether this link half is rendered as an arrow rather than as a row: both ends must resolve, and both must
    /// be rows the tree actually draws. A half pointing at nothing, or into an element with no row, keeps its
    /// label — an arrow there would have nothing to point at, and the fact would be lost from both renderings.
    /// </summary>
    public static bool DrawsAsArrow(Project project, ProjectElement half)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(half);

        return half.IsLinkHalf && Ends(project, half) is not null;
    }

    /// <summary>Every arrow the two panes should carry, in document order of the halves that produced them.</summary>
    public static IReadOnlyList<LinkArrowEdge> Plan(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var edges = new List<LinkArrowEdge>();
        var drawn = new HashSet<(ElementId From, ElementId To)>();
        foreach (ProjectElement element in project.Root.DescendantsAndSelf())
        {
            if (!element.IsLinkHalf || Ends(project, element) is not { } ends)
            {
                continue;
            }
            if (drawn.Add((ends.From.Element, ends.To.Element)))
            {
                edges.Add(new LinkArrowEdge(ends.From, ends.To,
                    element.IsSceneLink ? LinkArrowKind.Scene : LinkArrowKind.Follow, element.Id!.Value));
            }
        }
        return edges;
    }

    // The wire this half is part of, oriented producer → driven, or null when it cannot be drawn as an arrow.
    private static (LinkArrowEndpoint From, LinkArrowEndpoint To)? Ends(Project project, ProjectElement half)
    {
        if (half.Id is not { } halfId || project.FindParent(halfId) is not { } owner)
        {
            return null;
        }
        // The partner walk is the label's own (TreeLabelFormatter), so an arrow and the row it replaces always
        // name the same far end: the opposite half's OWNER — the pin, or the scenes container a scene member sits in.
        if (Endpoint(project, TreeLabelFormatter.LinkPartnerChain(project, half)) is not { } partner
            || Endpoint(project, TreeLabelFormatter.AncestorChain(project, owner)) is not { } near)
        {
            return null;
        }
        // US-022 "which half is which": the PRODUCER owns the from-half and the consumer the to-half — the element
        // names read backwards from the roles, so the orientation is taken from the tag, never from the words.
        // A scene link is owned by the block output that drives the scene, so it reads as a from-half too.
        return half.IsLinkFromEnd || half.IsSceneLink ? (near, partner) : (partner, near);
    }

    // The endpoint for an ancestor chain running innermost-first, or null when its innermost element has no row.
    private static LinkArrowEndpoint? Endpoint(Project project, IReadOnlyList<ProjectElement> chain)
    {
        if (chain.Count == 0 || !ProjectTreeProjector.HasRow(project, chain[0]))
        {
            return null;
        }
        var path = new List<ElementId>();
        bool functions = false;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            functions |= chain[i].Kind == ElementKind.FunctionBlock;
            if (chain[i].Id is { } id)
            {
                path.Add(id);
            }
        }
        return path.Count == 0
            ? null
            : new LinkArrowEndpoint(path, functions ? LinkArrowPane.Functions : LinkArrowPane.Installation);
    }
}
