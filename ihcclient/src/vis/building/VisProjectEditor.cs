#nullable enable
using System;
using Ihc.Vis.Model;

namespace Ihc.Vis.Building
{
    /// <summary>
    /// The mutable edit session over an immutable <see cref="VisProject"/> — the authoring (write) surface
    /// a GUI drives. Open it with <c>project.Edit()</c>, mutate through live handles (<see cref="Group"/> →
    /// products / function blocks / resources; <see cref="Link"/>/<see cref="Unlink"/>; the <c>Remove*</c>
    /// methods), then call <see cref="ToProject"/> to commit a fresh immutable snapshot to save. Loaded
    /// <c>_0x</c> ids are preserved; new ones are allocated for added elements.
    /// </summary>
    /// <remarks>
    /// Mutation is deliberate, not the default: a loaded project stays a frozen, byte-faithful image until
    /// <c>Edit()</c> opens a session, so an untouched load round-trips exactly. Read/browse from the generic
    /// <see cref="VisProject"/>/<c>VisElement</c> model, not these handles. Stage 1: signatures only.
    /// </remarks>
    public sealed class VisProjectEditor
    {
        internal VisProjectEditor()
        {
        }

        /// <summary>Gets the named locality (room), seeding it if necessary, and returns its live handle.</summary>
        public GroupRef Group(string name) => throw new NotImplementedException();

        /// <summary>
        /// Removes a locality (room) and everything in it (and any links to/from its resources). Retired
        /// <c>_0x</c> ids are not reused (plan §3.4); returns <c>this</c> for optional chaining.
        /// </summary>
        public VisProjectEditor RemoveGroup(GroupRef group) => throw new NotImplementedException();

        /// <summary>
        /// Wires a reciprocal follow-link between two live resources, writing both halves in a single call.
        /// Direction matters: <paramref name="from"/> is the source ("→") side and receives the
        /// <c>link_from_resource</c>; <paramref name="to"/> is the sink ("←") side and receives the
        /// <c>link_to_resource</c>. Works on freshly added resources and on ones looked up from a loaded
        /// project; returns <c>this</c> for optional chaining.
        /// </summary>
        public VisProjectEditor Link(ResourceRef from, ResourceRef to) => throw new NotImplementedException();

        /// <summary>
        /// Removes the reciprocal follow-link between two live resources — the inverse of <see cref="Link"/>,
        /// with the same <paramref name="from"/>→<paramref name="to"/> orientation — deleting both the
        /// <c>link_from_resource</c> and <c>link_to_resource</c> halves in a single call. Returns
        /// <c>this</c> for optional chaining.
        /// </summary>
        public VisProjectEditor Unlink(ResourceRef from, ResourceRef to) => throw new NotImplementedException();

        /// <summary>
        /// Produces the immutable project snapshot, validating it; existing ids are preserved and
        /// pending ids are allocated in document order.
        /// </summary>
        public VisProject ToProject() => throw new NotImplementedException();
    }

    /// <summary>
    /// Authoring entry points layered over <see cref="VisProject"/>.
    /// </summary>
    public static class VisProjectEditingExtensions
    {
        /// <summary>
        /// Opens a mutable edit session over a project (just loaded or created) — the deliberate
        /// read-to-write boundary. Usage: <c>project.Edit()</c> → mutate via handles →
        /// <see cref="VisProjectEditor.ToProject"/> → save.
        /// </summary>
        public static VisProjectEditor Edit(this VisProject project) => throw new NotImplementedException();
    }
}
