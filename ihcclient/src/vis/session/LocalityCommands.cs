#nullable enable
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>Inserts a new locality (US-008) named <paramref name="Name"/>, producing its allocated id.</summary>
    public sealed record AddLocality(string Name) : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Indsæt lokalitet";

        internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;

        internal override ElementId ExecuteCore(ProjectEditor editor) => editor.AddGroup(Name).Id;
    }

    /// <summary>The generic Name/Note edit by id (US-007/US-019/US-026/US-027): sets an element's <c>name</c> and
    /// <c>note</c>. Used for a locality, a function block, and (T015) an ordinary FB resource variable — the shape is
    /// identical, so one command serves all three. The label is resolved against the pre-edit project (shows the old
    /// name).</summary>
    public sealed record RenameLocality(ElementId Id, string Name, string Note) : ProjectCommand
    {
        internal override string Describe(Project project) =>
            "Omdøb " + ((project.FindById(Id) is { } element ? project.View(element).Name : null) ?? "element");

        internal override EditVerdict Evaluate(EditContext context) =>
            context.RequireExists(Id, "Elementet")
                .And(context.RequireUnlockedTarget(Id, inclusive: false));  // A locked block's instance Name/Note remain editable; only its descendants are protected.

        internal override void Execute(ProjectEditor editor)
        {
            ElementRef handle = editor.Resolve(Id, "element");
            handle.SetAttribute("name", Name);
            handle.SetAttribute("note", Note);
        }
    }

    /// <summary>Deletes a locality by id (US-009), cascading through the references to its contents. The confirm for
    /// a non-empty locality stays in the GUI (unbundled in W2-13); the label shows the pre-edit name.</summary>
    public sealed record DeleteLocality(ElementId Id) : ProjectCommand
    {
        internal override string Describe(Project project) =>
            "Slet " + ((project.FindById(Id) is { } element ? project.View(element).Name : null) ?? "lokalitet");

        internal override EditVerdict Evaluate(EditContext context) => context.RequireExists(Id, "Lokaliteten");

        internal override void Execute(ProjectEditor editor) =>
            editor.DeleteById(Id, DeleteReferencePolicy.CascadeReferences);
    }
}
