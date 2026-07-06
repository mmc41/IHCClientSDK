#nullable enable
namespace Ihc.Vis.Model
{
    /// <summary>
    /// A decoded, read-only view of one resource child of a product or function-block definition body — its
    /// element <see cref="Tag"/>, display <see cref="Name"/> and placeholder <see cref="Id"/>. Lets a GUI bind
    /// a preview/summary of an authored or catalog-discovered definition (e.g. "2 inputs, 1 output, 1 timer")
    /// without walking the raw <see cref="ProjectElement"/> tree or decoding id tokens itself — the projection
    /// counterpart to the by-handle authoring surface that keeps those tokens off the caller on the way in.
    /// </summary>
    /// <param name="Tag">The resource element's wire tag (e.g. <c>dataline_input</c>, <c>resource_timer</c>).</param>
    /// <param name="Name">The resource's display name, or the empty string when it carries none.</param>
    /// <param name="Id">The resource's placeholder id (re-minted on insert), or <c>null</c> when it has none.</param>
    public sealed record ResourceSummary(string Tag, string Name, ElementId? Id);
}
