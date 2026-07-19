#nullable enable
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis
{
    /// <summary>
    /// API-C (fablerefac Wave 1): a project-scoped read view over one <see cref="ProjectElement"/>. It pairs the
    /// element with its owning <see cref="Project"/> so <see cref="Effective"/> resolves attribute defaults against
    /// the project's own inline-DTD (with the SDK registry as fallback) — those defaults live on the project, not
    /// the element (<see cref="ProjectElement"/> is deliberately context-free), so a bare element reader could not
    /// see per-project defaults. Obtained via <c>project.View(element)</c>; W1-4 hangs the universal read
    /// properties (Name/Note/Locked/…) off this same handle.
    /// </summary>
    public readonly record struct ElementView(Project Project, ProjectElement Element)
    {
        /// <summary>
        /// The effective value of <paramref name="attr"/>: the element's own value when present (an empty string
        /// stays empty), else the attribute's DTD default when the element type declares one, else <c>null</c>.
        /// The default is resolved through the project's schema view (inline DTD first, SDK registry fallback), so a
        /// <c>(yes | no) "no"</c> / <c>(auto | rc | rl) "auto"</c> attribute reads its declared default instead of
        /// the GUI re-encoding it. A declared-but-non-defaulted attribute (<c>#IMPLIED</c>/<c>#REQUIRED</c>) has no
        /// default, so an absent one is <c>null</c> — never the empty <see cref="AttrSchema.Default"/> placeholder.
        /// </summary>
        public string? Effective(string attr) =>
            Element.GetAttribute(attr)
            ?? (Project.SchemaView.TryGet(Element.Tag)?.FindAttr(attr) is { Kind: AttrKind.Defaulted } declared
                ? declared.Default
                : null);
    }

    /// <summary>Project-scoped read-surface entry points (API-C/D, fablerefac Wave 1).</summary>
    public static class ProjectReadView
    {
        extension(Project project)
        {
            /// <summary>A project-scoped read <see cref="ElementView"/> over <paramref name="element"/> — the handle
            /// the effective-value reader (and, from W1-4, the universal read properties) resolve through.</summary>
            public ElementView View(ProjectElement element) => new(project, element);
        }
    }
}
