#nullable enable

using Ihc.Vis.Model;
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// One follow-link row owned by a resource — a <c>link_from_resource</c> or <c>link_to_resource</c> child.
    /// Carries the row's own id (pass to <see cref="ProjectEditor.ResolveLinkOpposite"/> for the F4 jump), its
    /// <see cref="Tag"/> (the direction: <c>link_from_resource</c> = this resource is the source, <c>link_to_resource</c>
    /// = the sink), and the <see cref="PartnerLinkId"/> the row's <c>link</c> IDREF points at (the reciprocal row).
    /// </summary>
    public sealed record LinkInfo(ElementId LinkRowId, string Tag, ElementId PartnerLinkId);
}
