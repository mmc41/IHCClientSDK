using Ihc.Vis.Editing;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// The base for every project mutation the document session applies (proposal §3.4). A command knows how to
    /// describe itself (for the undo/status label), check its own legality against a pre-edit context, and execute
    /// against a <see cref="ProjectEditor"/>. The three mechanics are <c>internal</c> — only the session drives
    /// them; the GUI creates concrete commands and hands them to <c>session.Apply</c>. Concrete command families
    /// land in W2-5..W2-11.
    /// </summary>
    public abstract record ProjectCommand
    {
        /// <summary>A human label for the edit (e.g. "Insert product 'X'"), resolved against the <b>pre-edit</b>
        /// project (D10) so a rename/delete label reads the old state.</summary>
        internal abstract string Describe(Project project);

        /// <summary>Checks the command's legality against the pre-edit context; a negative verdict short-circuits
        /// the apply to <see cref="EditStatus.Refused"/>.</summary>
        internal abstract EditVerdict Evaluate(EditContext context);

        /// <summary>Mutates the project through the editor. May throw <see cref="EditRefusedException"/> from a deep
        /// guard (→ Refused); any other exception is a Failed.</summary>
        internal abstract void Execute(ProjectEditor editor);
    }

    /// <summary>A <see cref="ProjectCommand"/> that produces a typed result (e.g. a new element's id). The result is
    /// surfaced through <see cref="EditOutcome{T}"/>; <see cref="Execute"/> is sealed to forward to
    /// <see cref="ExecuteCore"/> so a subclass supplies only the value-returning body.</summary>
    public abstract record ProjectCommand<TResult> : ProjectCommand
    {
        /// <summary>Mutates the project and returns the produced result.</summary>
        internal abstract TResult ExecuteCore(ProjectEditor editor);

        internal sealed override void Execute(ProjectEditor editor) => ExecuteCore(editor);
    }
}
