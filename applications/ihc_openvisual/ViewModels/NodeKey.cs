using Ihc.Vis.Model;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// fablerefac W3-2: the stable identity of a tree row for the reconciler (W3-4). A <c>readonly record struct</c>
/// so value equality + hashing come for free and it is an alloc-free dictionary key (the engine <see cref="ElementId"/>
/// mould). It has two forms, never mixed:
/// <list type="bullet">
/// <item><b>element-backed</b> — <see cref="ForElement"/> wraps the row's own <see cref="ElementId"/> (a locality,
/// product, function block, pin, program leaf, …);</item>
/// <item><b>synthetic</b> — <see cref="ForStructural"/> keys a structural row that owns no element of its own by
/// <c>(owner, role[, refId])</c>: the owning element's id, a role string that separates the different rows under
/// one owner (variable sections, "Programs", Events/Commands containers, link rows, scene members), and an
/// optional refId that separates rows sharing an <c>(owner, role)</c> (e.g. two link rows under one pin, keyed by
/// the partner element).</item>
/// </list>
/// The two forms never collide: an element-backed key stores its id in <see cref="Element"/> while a synthetic
/// key stores the owning id in <see cref="Owner"/>, so a row standing for element X is not the container of X.
/// </summary>
public readonly record struct NodeKey
{
    private NodeKey(ElementId? element, ElementId? owner, string? role, ElementId? refId)
    {
        Element = element;
        Owner = owner;
        Role = role;
        RefId = refId;
    }

    /// <summary>The row's own element id, for an element-backed key; null for a synthetic key.</summary>
    public ElementId? Element { get; }

    /// <summary>The owning element's id, for a synthetic key; null for an element-backed key.</summary>
    public ElementId? Owner { get; }

    /// <summary>The role that separates the different structural rows under one <see cref="Owner"/>
    /// (e.g. <c>events</c> vs <c>commands</c>); null for an element-backed key.</summary>
    public string? Role { get; }

    /// <summary>An optional discriminator for structural rows that share an <c>(owner, role)</c> — the partner
    /// element of a link/scene row; null when the <c>(owner, role)</c> pair is already unique.</summary>
    public ElementId? RefId { get; }

    /// <summary>Whether this key stands for a real element (as opposed to a synthetic structural row).</summary>
    public bool IsElementBacked => Element is not null;

    /// <summary>A key for a row that stands for the element <paramref name="id"/>.</summary>
    public static NodeKey ForElement(ElementId id) => new(id, null, null, null);

    /// <summary>A key for a structural row owned by <paramref name="owner"/>, separated from its siblings by
    /// <paramref name="role"/> and, when needed, <paramref name="refId"/>.</summary>
    public static NodeKey ForStructural(ElementId owner, string role, ElementId? refId = null) =>
        new(null, owner, role, refId);
}
