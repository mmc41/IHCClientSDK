#nullable enable
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>Inserts a new locality (US-008) named <paramref name="Name"/>, producing its allocated id.</summary>
    public sealed record AddLocality(string Name) : ProjectCommand<ElementId>
    {
        internal override string Describe(Project project) => "Insert locality";

        internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;

        internal override ElementId ExecuteCore(ProjectEditor editor) => editor.AddGroup(Name).Id;
    }

    /// <summary>Renames a locality (or function block) by id (US-007/US-019): sets its name and note. The label is
    /// resolved against the pre-edit project, so it shows the old name.</summary>
    public sealed record RenameLocality(ElementId Id, string Name, string Note) : ProjectCommand
    {
        internal override string Describe(Project project) =>
            "Rename " + (project.FindById(Id)?.GetAttribute("name") ?? "locality");

        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(Id) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse("The element no longer exists.");

        internal override void Execute(ProjectEditor editor)
        {
            if (!editor.TryResolve(Id, out ElementRef? handle))
            {
                throw new EditRefusedException("The element no longer exists.");
            }
            handle.SetAttribute("name", Name);
            handle.SetAttribute("note", Note);
        }
    }

    /// <summary>Deletes a locality by id (US-009), cascading through the references to its contents. The confirm for
    /// a non-empty locality stays in the GUI (unbundled in W2-13); the label shows the pre-edit name.</summary>
    public sealed record DeleteLocality(ElementId Id) : ProjectCommand
    {
        internal override string Describe(Project project) =>
            "Delete " + (project.FindById(Id)?.GetAttribute("name") ?? "locality");

        internal override EditVerdict Evaluate(EditContext context) =>
            context.Index.FindById(Id) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse("The locality no longer exists.");

        internal override void Execute(ProjectEditor editor) =>
            editor.DeleteById(Id, DeleteReferencePolicy.CascadeReferences);
    }
}
