
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// One item type legal to insert under a selected node, for a right-click "insert…" menu:
    /// the child element <see cref="ChildTag"/> and a human-facing <see cref="Category"/> grouping
    /// (e.g. <c>Product</c>, <c>Function block</c>, <c>Pin</c>, <c>Scene</c>, <c>Variable</c>, <c>Program</c>).
    /// </summary>
    public sealed record InsertOption(string ChildTag, string Category);
}
