#nullable enable
using System.Linq;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// Bundles several <see cref="ProjectCommand"/>s into one gesture (proposal §3.4): the session applies them as a
    /// single unit, so one <c>Apply</c> is one history entry and a single Undo reverses the whole bundle. The
    /// gesture is all-or-nothing — <see cref="Evaluate"/> passes only when every part passes against the pre-edit
    /// context, so a composite must be built from parts whose preconditions all hold before the first part runs.
    /// <para><b>Only INDEPENDENT parts belong in one bundle.</b> Every part is evaluated against the PRE-EDIT
    /// project, never against what the parts before it produced, so a part whose precondition depends on an earlier
    /// part having run is not merely unchecked — it is checked against the wrong project and passes. Bundling
    /// <c>[Delete(x), Rename(x)]</c> is the shape of the mistake: both parts evaluate clean, and the miss surfaces
    /// only when <see cref="Execute"/> reaches it, as a refusal from the engine's require-or-throw resolver
    /// (<see cref="Ihc.Vis.Editing.ProjectEditor.Resolve"/>) that discards the whole bundle. Applied one at a time
    /// the same two commands would commit the delete and refuse only the rename, so the two routes agree on the
    /// resulting project exactly when the parts are independent — pinned over randomized sequences by
    /// <c>CompositeCommandMetamorphicTests</c>.</para>
    /// </summary>
    public sealed record CompositeCommand(string Label, EquatableArray<ProjectCommand> Parts) : ProjectCommand
    {
        // The former `params ReadOnlySpan<ProjectCommand>` convenience ctor is gone: EquatableArray<T> carries a
        // collection builder, so `new CompositeCommand("x", [a, b])` reaches the primary constructor directly.
        // Keeping both made every collection-expression call site ambiguous (CS0121) — the overload hazard the
        // wrapper design set out to avoid, so the overload goes rather than the collection expression.

        internal override string Describe(Project project) => Label;

        internal override EditVerdict Evaluate(EditContext context) =>
            Parts.Select(part => part.Evaluate(context)).FirstOrDefault(verdict => !verdict.Ok, EditVerdict.Allow);

        internal override void Execute(ProjectEditor editor)
        {
            foreach (ProjectCommand part in Parts)
            {
                part.Execute(editor);
            }
        }
    }
}
