#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Schema;
namespace Ihc.Vis.Projects
{
    /// <summary>
    /// A thin typed view over the <c>utcs_project</c> root <see cref="ProjectElement"/>: it adds authoring
    /// ergonomics (version, <c>id1</c>/<c>id2</c>/<c>last_unique_id</c>, the in-file metadata and the seven
    /// fixed children) without forking the generic-node representation. All accessors read the underlying bag.
    /// </summary>
    /// <remarks>
    /// This is the unified model for an IHC project — the same <c>utcs_project</c> v4.0 XML whether it came
    /// from a desktop <c>.vis</c> file or a controller <c>.ihc</c> download (the controller blob is just the
    /// gzip-compressed form of the identical XML). The in-file metadata accessors here are the read path;
    /// <see cref="ProjectDetails"/> is the write path supplied at creation.
    /// </remarks>
    public sealed record Project(ProjectElement Root)
    {
        /// <summary>
        /// The per-type canonical <c>&lt;!ELEMENT&gt;/&lt;!ATTLIST&gt;</c> blocks captured verbatim from this
        /// project's own inline DTD at load time (tag → verbatim block); empty for a freshly created project.
        /// This is what makes the round-trip <em>open-world</em>: the serializer re-emits each type's grammar from
        /// the file itself, so a project using element types the static registry never declared (custom
        /// products/function blocks copied in by IHC Visual) still saves byte-identically. Preserved across
        /// <c>with</c> re-stamping; <see cref="ProjectSchemaRegistry"/> is the fallback for types not in the file.
        /// </summary>
        public ImmutableDictionary<string, string> InlineDtdBlocks { get; init; } = ImmutableDictionary<string, string>.Empty;

        /// <summary>
        /// Value equality over the project <see cref="Root"/> tree only. <see cref="InlineDtdBlocks"/> is captured
        /// serialization provenance (how to re-emit the source file's grammar), not part of the project's logical
        /// identity — so a project loaded from a file (DTD captured) equals an identically-shaped one authored
        /// in-memory (DTD empty), keeping structural round-trip assertions meaningful.
        /// </summary>
        public bool Equals(Project? other) => other is not null && Root.Equals(other.Root);

        /// <inheritdoc/>
        public override int GetHashCode() => Root.GetHashCode();

        // Memoized as ONE reference (source + view together), keyed on the InlineDtdBlocks reference it was built
        // from. A single reference field means a concurrent reader can never observe a torn (new source, old view)
        // pair — it sees either the fully-built old memo or the fully-built new one (the two separate fields it
        // replaced could tear). The record copy-constructor carries this field verbatim across `with`, so the cache
        // survives a `with { Root = ... }` (blocks unchanged → reuse) yet rebuilds after a
        // `with { InlineDtdBlocks = ... }` (blocks replaced → the carried view no longer matches, which would
        // otherwise emit the wrong DTD). ProjectReader warms it eagerly so a malformed captured DTD block fails at
        // load with file context instead of at the first save.
        private sealed record SchemaViewMemo(ImmutableDictionary<string, string> Source, ProjectSchemaView View);
        private SchemaViewMemo? schemaViewMemo;

        /// <summary>The schema resolver for this project: its captured inline-DTD blocks first, registry fallback.</summary>
        internal ProjectSchemaView SchemaView
        {
            get
            {
                SchemaViewMemo? memo = schemaViewMemo;   // snapshot the single reference before comparing/using
                if (memo is null || !ReferenceEquals(memo.Source, InlineDtdBlocks))
                {
                    memo = new SchemaViewMemo(InlineDtdBlocks, ProjectSchemaView.For(InlineDtdBlocks));
                    schemaViewMemo = memo;
                }
                return memo.View;
            }
        }

        /// <summary>
        /// The <c>version_major.version_minor</c> of the project format (4.0 for every known file), or
        /// <c>null</c> when the root does not declare both attributes — never a fabricated default.
        /// </summary>
        public string? Version
        {
            get
            {
                string? major = Root.GetAttribute("version_major");
                string? minor = Root.GetAttribute("version_minor");
                return major is null || minor is null ? null : $"{major}.{minor}";
            }
        }

        /// <summary>The project creation stamp <c>id1</c> (constant for the project's life).</summary>
        public string? Id1 => Root.GetAttribute("id1");

        /// <summary>The current-save stamp <c>id2</c> (re-stamped every save).</summary>
        public string? Id2 => Root.GetAttribute("id2");

        /// <summary>The persistent high-water-mark id <c>last_unique_id</c>.</summary>
        public string? LastUniqueId => Root.GetAttribute("last_unique_id");

        // The typed metadata getters below cover every dialog-writable attribute of the three id-less root
        // metadata blocks (each block's full DTD attribute set minus udf, which no vendor dialog writes).
        // Write paths: ProjectDetails at creation, ProjectEditor.Set{Project|Customer|Installer}Info afterwards.

        /// <summary>The programmer (<c>project_info/@programmer</c>); the write-path counterpart is <see cref="ProjectDetails.Programmer"/>.</summary>
        public string? Programmer => Child("project_info")?.GetAttribute("programmer");

        /// <summary>The project number (<c>project_info/@number</c>).</summary>
        public string? ProjectNumber => Child("project_info")?.GetAttribute("number");

        /// <summary>The drawing reference (<c>project_info/@drawing</c>).</summary>
        public string? Drawing => Child("project_info")?.GetAttribute("drawing");

        /// <summary>The project type (<c>project_info/@type</c>).</summary>
        public string? ProjectType => Child("project_info")?.GetAttribute("type");

        /// <summary>The project description (<c>project_info/@description</c>).</summary>
        public string? Description => Child("project_info")?.GetAttribute("description");

        /// <summary>The installer name (<c>installer_info/@name</c>); the write-path counterpart is <see cref="ProjectDetails.InstallerName"/>.</summary>
        public string? InstallerName => Child("installer_info")?.GetAttribute("name");

        /// <summary>The installer street address (<c>installer_info/@address</c>).</summary>
        public string? InstallerAddress => Child("installer_info")?.GetAttribute("address");

        /// <summary>The installer city (<c>installer_info/@city</c>).</summary>
        public string? InstallerCity => Child("installer_info")?.GetAttribute("city");

        /// <summary>The installer postal code (<c>installer_info/@zipcode</c>).</summary>
        public string? InstallerZipCode => Child("installer_info")?.GetAttribute("zipcode");

        /// <summary>The installer country (<c>installer_info/@country</c>); the write-path counterpart is <see cref="ProjectDetails.InstallerCountry"/>.</summary>
        public string? InstallerCountry => Child("installer_info")?.GetAttribute("country");

        /// <summary>The installer phone number (<c>installer_info/@phone</c>).</summary>
        public string? InstallerPhone => Child("installer_info")?.GetAttribute("phone");

        /// <summary>The installer mobile phone number (<c>installer_info/@mobilephone</c>).</summary>
        public string? InstallerMobilePhone => Child("installer_info")?.GetAttribute("mobilephone");

        /// <summary>The installer e-mail address (<c>installer_info/@email</c>).</summary>
        public string? InstallerEmail => Child("installer_info")?.GetAttribute("email");

        /// <summary>The customer name (<c>customer_info/@name</c>).</summary>
        public string? CustomerName => Child("customer_info")?.GetAttribute("name");

        /// <summary>The customer street address (<c>customer_info/@address</c>).</summary>
        public string? CustomerAddress => Child("customer_info")?.GetAttribute("address");

        /// <summary>The customer city (<c>customer_info/@city</c>).</summary>
        public string? CustomerCity => Child("customer_info")?.GetAttribute("city");

        /// <summary>The customer postal code (<c>customer_info/@zipcode</c>).</summary>
        public string? CustomerZipCode => Child("customer_info")?.GetAttribute("zipcode");

        /// <summary>The customer country (<c>customer_info/@country</c>).</summary>
        public string? CustomerCountry => Child("customer_info")?.GetAttribute("country");

        /// <summary>The customer phone number (<c>customer_info/@phone</c>).</summary>
        public string? CustomerPhone => Child("customer_info")?.GetAttribute("phone");

        /// <summary>The customer mobile phone number (<c>customer_info/@mobilephone</c>).</summary>
        public string? CustomerMobilePhone => Child("customer_info")?.GetAttribute("mobilephone");

        /// <summary>The customer e-mail address (<c>customer_info/@email</c>).</summary>
        public string? CustomerEmail => Child("customer_info")?.GetAttribute("email");

        /// <summary>The last-modified time from the <c>modified</c> element (local time), or <c>null</c> when absent/malformed.</summary>
        public DateTimeOffset? Modified
        {
            get
            {
                ProjectElement? m = Child("modified");
                return m is not null
                    && int.TryParse(m.GetAttribute("year"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int year)
                    && int.TryParse(m.GetAttribute("month"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int month)
                    && int.TryParse(m.GetAttribute("day"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int day)
                    && int.TryParse(m.GetAttribute("hour"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hour)
                    && int.TryParse(m.GetAttribute("minute"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int minute)
                    && year is >= 1 and <= 9999 && month is >= 1 and <= 12
                    && day >= 1 && day <= DateTime.DaysInMonth(year, month)
                    && hour is >= 0 and <= 23 && minute is >= 0 and <= 59
                    ? new DateTimeOffset(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local))
                    : null;   // null on malformed — the documented contract, incl. out-of-range date parts
            }
        }

        /// <summary>The seven fixed root children, in document order.</summary>
        public IReadOnlyList<ProjectElement> Children => Root.Children;

        /// <summary>Returns the named fixed child element (e.g. <c>groups</c>), or <c>null</c> when absent.</summary>
        public ProjectElement? Child(string tag) => Root.FindChild(tag);

        /// <summary>
        /// Resolves an element by its stable <see cref="ElementId"/> anywhere in the project tree (the root and
        /// all descendants), or <c>null</c> when no element carries that id. This is the id-addressable read
        /// primitive a GUI selection model resolves against — unambiguous even where several elements share a name.
        /// </summary>
        public ProjectElement? FindById(ElementId id) => Root.FindDescendantOrSelf(e => e.Id == id);

        /// <summary>
        /// Returns the immediate parent of the element carrying the given id, or <c>null</c> when the id is the
        /// root or is absent. <see cref="ProjectElement"/> is an immutable value with no parent pointer, so parent
        /// navigation is resolved here against the tree (the read side of link navigation and far-end paths).
        /// </summary>
        public ProjectElement? FindParent(ElementId id) =>
            Root.FindDescendantOrSelf(e => e.Children.Any(c => c.Id == id));

        /// <summary>The <c>group</c> localities declared under <c>groups</c>.</summary>
        public IReadOnlyList<ProjectElement> Groups =>
            Child("groups") is { } groups && !groups.Children.IsEmpty
                ? groups.Children.Where(c => c.Tag == "group").ToImmutableArray()
                : ImmutableArray<ProjectElement>.Empty;

        public override string ToString() =>
            $"Project(Version={Version}, Id1={Id1}, Id2={Id2}, Children=ProjectElement[{Children.Count}])";
    }
}
