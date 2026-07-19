#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;
using Ihc.Vis.Editing;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// Bundles several <see cref="ProjectCommand"/>s into one gesture (proposal §3.4): the session applies them as a
    /// single unit, so one <c>Apply</c> is one history entry and a single Undo reverses the whole bundle. The
    /// gesture is all-or-nothing — <see cref="Evaluate"/> passes only when every part passes against the pre-edit
    /// context, so a composite must be built from parts whose preconditions all hold before the first part runs.
    /// </summary>
    public sealed record CompositeCommand(string Label, ImmutableArray<ProjectCommand> Parts) : ProjectCommand
    {
        /// <summary>Bundles the given parts under a single label (C# 13 params-span convenience over the array ctor).</summary>
        public CompositeCommand(string label, params ReadOnlySpan<ProjectCommand> parts)
            : this(label, ImmutableArray.Create(parts)) { }

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
