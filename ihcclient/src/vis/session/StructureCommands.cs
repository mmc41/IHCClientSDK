#nullable enable
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>Moves a node to another container with its identity preserved (US-054): a product or function block
    /// under a locality. Refuses an illegal container, a no-op move into the current parent, or a self/descendant
    /// target (the engine move-contract, <see cref="ProjectEditor.CanMoveSubtree"/>).</summary>
    public sealed record MoveNode(ElementId SourceId, ElementId TargetParentId) : ProjectCommand
    {
        internal override string Describe(Project project) => "Move";
        internal override EditVerdict Evaluate(EditContext context) =>
            (context.Index.FindById(SourceId) is { } source
            && context.Index.FindById(TargetParentId) is { } target
            && StructurePlacement.CanContain(source.Tag, target.Tag)
            && context.Index.FindParent(SourceId)?.Id != TargetParentId
            && context.Project.Edit().CanMoveSubtree(SourceId, TargetParentId)
                ? EditVerdict.Allow
                : EditVerdict.Refuse("That move is not allowed."))
            .And(context.RequireUnlockedTarget(TargetParentId, inclusive: true));   // T003: no move INTO a locked block
        internal override void Execute(ProjectEditor editor) => editor.MoveSubtree(SourceId, TargetParentId);
    }

    /// <summary>Reorders a node to a target position among its same-tag siblings (US-055), identity-preserving. The
    /// caller computes the same-tag index (from a delta or a drop target).</summary>
    public sealed record ReorderNode(ElementId Id, int SameTagIndex) : ProjectCommand
    {
        internal override string Describe(Project project) => "Reorder";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireExists(Id, "node")
                .And(context.RequireUnlockedTarget(Id, inclusive: false));   // T003: no reorder INSIDE a locked block
        internal override void Execute(ProjectEditor editor) => editor.ReorderSubtree(Id, SameTagIndex);
    }

    /// <summary>Copies a node and pastes it as an independent duplicate under a container (US-056): fresh ids,
    /// external link halves dropped. Returns the new node's id.</summary>
    public sealed record CopyNode(ElementId SourceId, ElementId TargetParentId) : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Paste";
        internal override EditVerdict Evaluate(EditContext context) =>
            (context.Index.FindById(SourceId) is { } source
            && context.Index.FindById(TargetParentId) is { } target
            && StructurePlacement.CanContain(source.Tag, target.Tag)
                ? EditVerdict.Allow
                : EditVerdict.Refuse("That container cannot hold this node."))
            .And(context.RequireUnlockedTarget(TargetParentId, inclusive: true));   // T003: no copy INTO a locked block
        internal override ElementId ExecuteCore(ProjectEditor editor) => editor.CopySubtree(SourceId, TargetParentId);
    }

    /// <summary>Deletes a node by id (US-009/US-057). <paramref name="Cascade"/> (decided by the GUI after its
    /// reference-check + confirm) chooses cascade-references over strict.</summary>
    public sealed record DeleteNode(ElementId Id, bool Cascade) : ProjectCommand
    {
        internal override string Describe(Project project) => "Delete";
        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireExists(Id, "node") is { Ok: false } missing
                ? missing
                : ProjectEditor.DeletionRefusalReason(context.Project.Root, Id) is { } reason
                    ? EditVerdict.Refuse(reason)                    // catalog pin / locked-block node (review3 H1)
                    : EditVerdict.Allow;
        internal override void Execute(ProjectEditor editor) =>
            editor.DeleteById(Id, Cascade ? DeleteReferencePolicy.CascadeReferences : DeleteReferencePolicy.Strict);
    }

    internal static class StructurePlacement
    {
        // A product or function block belongs under a locality (group) — US-054/US-056.
        public static bool CanContain(string sourceTag, string targetTag) =>
            (ProductClassifier.IsProduct(sourceTag) || sourceTag == "functionblock") && targetTag == "group";
    }
}
