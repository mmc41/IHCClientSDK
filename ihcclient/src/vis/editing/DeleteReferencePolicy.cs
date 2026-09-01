
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// How <see cref="ProjectEditor.DeleteById(Model.ElementId, DeleteReferencePolicy)"/> treats program rows that
    /// still reference the deleted subtree after the reciprocal-half cascade — the US-009 "commands, conditions and
    /// other references" half. Reciprocal wiring (follow-link halves, scene rows) always cascades regardless of
    /// policy; this only governs the schema-declared IDREFs a plain delete would leave dangling.
    /// </summary>
    public enum DeleteReferencePolicy
    {
        /// <summary>
        /// Refuse the delete when any other IDREF still points into the deleted set (the session never holds a
        /// dangling reference) — the caller deletes or rewires the referring elements first. The default.
        /// </summary>
        Strict,

        /// <summary>
        /// Vendor-parity cascade (ENG2-A5, §18 M-B = row-only): every referencing <c>action</c>/<c>condition</c>/
        /// <c>event</c> row is removed <b>whole</b> on any link-slot match (<c>link1</c> or <c>link2</c>), parent
        /// groups are left intact (emptied containers survive), and nothing is allocated. References the capture
        /// does not pin (a <c>scenes</c> binding, an enum <c>typedef</c>, a <c>program_case</c> criterion) still
        /// refuse the delete.
        /// </summary>
        CascadeReferences,
    }
}
