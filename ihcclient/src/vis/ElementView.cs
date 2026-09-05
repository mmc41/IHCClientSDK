using Ihc.Vis.Model;
using Ihc.Vis.Products;
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

        // API-A universal read properties — each the effective value of one attribute (the attribute-name string
        // literals live only here, SDK-side). The write-side Ref handles model these as strings, so the read side
        // mirrors that shape; the (yes | no) flags decode to bool.

        /// <summary>The element's effective <c>name</c> label (US-006), or its DTD default.</summary>
        public string? Name => Effective("name");

        /// <summary>The element's effective documentation <c>note</c> (US-047), or its DTD default.</summary>
        public string? Note => Effective("note");

        /// <summary>
        /// The element's effective SECOND documentation field, <c>note-2</c> (US-027/W5) — the installer-facing help
        /// text, distinct from <see cref="Note"/>'s function documentation. The reference application's properties
        /// dialog offers both (<i>Tekst til funktionsdokumentation</i> and <i>Noter for hjælpetekst</i>).
        /// Its DTD default is the empty string, so a variable that never had one reads blank and writes nothing.
        /// </summary>
        public string? HelpNote => Effective("note-2");

        /// <summary>The element's effective <c>position</c> token, or its DTD default.</summary>
        public string? Position => Effective("position");

        /// <summary>The element's effective <c>icon</c> token, or its DTD default.</summary>
        public string? Icon => Effective("icon");

        /// <summary>The element's effective <c>value</c> (e.g. a dimmer setting or enum value), or its DTD default.</summary>
        public string? Value => Effective("value");

        /// <summary>
        /// The bounds this element DECLARES on its own <c>value</c> — its <c>minimum</c>/<c>maximum</c> attributes,
        /// resolved through the schema like any other attribute. Null where the catalog declares none: the engine
        /// does not invent a limit.
        /// <para>
        /// It lives here because two callers need the same answer and used to be one: the product-dialog composer
        /// derives a field's range from it, and the dialog-metadata face advertises it to a hand-written window.
        /// A second reader would be the staleness the composer's own "derived, never declared" note warns about.
        /// </para>
        /// <para>
        /// THREE STATES, NOT TWO — see <see cref="DeclaredNumericBounds.Unreadable"/>. A pair of nullable numbers
        /// could not tell "the catalog declares no bound" from "the catalog declares one this engine cannot read",
        /// and both callers therefore saw an unconstrained field for a limit the catalog states.
        /// </para>
        /// </summary>
        public DeclaredNumericBounds DeclaredBounds
        {
            get
            {
                string? minimum = Effective("minimum");
                string? maximum = Effective("maximum");
                return new DeclaredNumericBounds(
                    ParseBound(minimum),
                    ParseBound(maximum),
                    IsUnreadable(minimum) || IsUnreadable(maximum));
            }
        }

        private static int? ParseBound(string? raw) =>
            int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : null;

        /// <summary>A bound the element DECLARES — non-blank — that does not parse as a whole number.</summary>
        private static bool IsUnreadable(string? raw) =>
            !string.IsNullOrWhiteSpace(raw) && ParseBound(raw) is null;

        /// <summary>The element's effective initial value (<c>inivalue</c>, e.g. an output's on/off power-up state),
        /// or its DTD default.</summary>
        public string? InitialValue => Effective("inivalue");

        /// <summary>A wireless product's effective <c>serialnumber</c> (blank/null until commissioned, US-014).</summary>
        public string? SerialNumber => Effective("serialnumber");

        /// <summary>Whether the element is locked (US-020) — the effective <c>locked</c> flag is <c>"yes"</c>.</summary>
        public bool Locked => Flag("locked");

        /// <summary>Whether an output's value is persisted across a power loss (US-033) — effective <c>backup="yes"</c>.</summary>
        public bool Backup => Flag("backup");

        /// <summary>Whether a product is included in the end-user report — effective <c>enduser_report="yes"</c>.</summary>
        public bool EnduserReport => Flag("enduser_report");

        /// <summary>Whether this is a wireless product not yet linked to the controller (US-014). Reuses
        /// <see cref="ProductClassifier"/> over the element tag and effective <see cref="SerialNumber"/>.</summary>
        public bool IsUnlinkedWireless => ProductClassifier.IsUnlinkedWireless(Element.Tag, SerialNumber);

        /// <summary>Decodes an effective <c>(yes | no)</c> flag to a bool — absent reads as the DTD default
        /// (e.g. <c>"no"</c> → <c>false</c>).</summary>
        private bool Flag(string attr) => Effective(attr) == "yes";
    }

    /// <summary>
    /// What an element's <c>minimum</c>/<c>maximum</c> attributes say about its <c>value</c>.
    /// </summary>
    /// <param name="Minimum">The declared lower bound, when one is declared AND readable.</param>
    /// <param name="Maximum">The declared upper bound, when one is declared AND readable.</param>
    /// <param name="Unreadable">
    /// Whether the element declares a bound whose text is not a whole number. It is the third state a pair of
    /// nullable numbers cannot express: a null bound otherwise means "the catalog declares none", so an
    /// unreadable declaration silently became "no limit" — on a path that writes to a <c>.vis</c>. A reader that
    /// finds this set must not treat the field as unbounded; the defect is in the DEFINITION file, and
    /// <c>catalog-bound-unreadable</c> is the row that reports it.
    /// </param>
    /// <remarks>
    /// The struct's <see langword="default"/> is "no bound declared", so an element with neither attribute reads
    /// as the loosest answer — the same property <c>FieldConstraintMetadata.Unconstrained</c> has.
    /// </remarks>
    public readonly record struct DeclaredNumericBounds(int? Minimum, int? Maximum, bool Unreadable);

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
