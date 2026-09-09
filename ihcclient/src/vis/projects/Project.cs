using System;
using System.Collections.Frozen;
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
        /// <para>
        /// INTERNAL, on both halves and for different reasons. The WRITE would let a caller decide how the file
        /// re-emits its own grammar -- a commitment to the serializer's representation that no supported operation
        /// asks for, and the one thing that could make a byte-identical round trip stop being byte-identical. The
        /// READ carries no domain fact: it is provenance about the source file, deliberately excluded from this
        /// record's own equality for exactly that reason, and nothing outside the engine acts on it. What a caller
        /// relies on is the round trip itself, which is the engine's promise rather than this dictionary's.
        /// </para>
        /// </summary>
        internal ImmutableDictionary<string, string> InlineDtdBlocks { get; init; } = ImmutableDictionary<string, string>.Empty;

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

        // Memoized on exactly the same terms as the schema view above, and for the same tearing reason: ONE
        // reference pairing the source with what was built from it, keyed here on the Root the maps index. A `with
        // { Root = ... }` carries this field verbatim, so the reference check is what rebuilds the maps for the new
        // tree instead of answering from the old one. Built lazily — a project nobody looks anything up in (a
        // serialize-only path) never pays for it.
        //
        // Held WEAKLY, unlike the schema view: the editing session retains every snapshot it has passed through,
        // and each of those WAS queried while it was current, so a strong memo would cost one index per history
        // entry rather than one per open document. Whoever needs the lookups to stay O(1) between calls holds the
        // instance it was handed — the session pins the current snapshot's, a report pins one for its own loop —
        // and a caller that holds nothing pays a rebuild, never a wrong answer.
        private sealed record ElementIndexMemo(ProjectElement Source, WeakReference<ProjectIndex> Index);
        private ElementIndexMemo? elementIndexMemo;

        /// <summary>This tree's id→element / id→parent lookup maps. Built on first use and reused for as long as the
        /// caller keeps what it was handed — see the lifetime contract on <see cref="ProjectIndex"/>.</summary>
        internal ProjectIndex ElementIndex
        {
            get
            {
                ElementIndexMemo? memo = elementIndexMemo;   // snapshot the single reference before comparing/using
                if (memo is not null && ReferenceEquals(memo.Source, Root) && memo.Index.TryGetTarget(out ProjectIndex? index))
                {
                    return index;
                }
                index = ProjectIndex.Build(Root);
                elementIndexMemo = new ElementIndexMemo(Root, new WeakReference<ProjectIndex>(index));
                return index;
            }
        }

        // Memoized on exactly the same terms as the two above: ONE reference pairing the tree that was walked
        // with what the walk produced, keyed on Root, so an edit — which always yields a new tree — rebuilds
        // instead of answering for the previous file.
        //
        // The NAMES are part of the memo because the walk only collects the attributes it was asked for. A
        // request naming one the memo does not hold rebuilds over the UNION of the two, so callers wanting
        // different attributes settle into a single walk rather than evicting each other's.
        //
        // Held STRONGLY, unlike the element index: the values are the very string instances the tree already
        // holds, so what this adds is a few hundred references rather than a copy of anything.
        private sealed record AttributeValueMemo(
            ProjectElement Source,
            FrozenSet<string> Names,
            FrozenDictionary<string, EquatableArray<string>> Values);
        private AttributeValueMemo? attributeValueMemo;

        /// <summary>
        /// The distinct, non-blank values each of <paramref name="names"/> is given ANYWHERE in this tree, in
        /// ordinal order, with an empty entry for a name no element carries.
        /// </summary>
        /// <remarks>
        /// One walk of the whole tree, memoized on it, because the caller is a per-element composition that asks
        /// the same question of the same snapshot once per element — so a walk per ask is a walk of the project
        /// per product.
        /// <para>A SET, and the parameter type says so rather than leaving it to a caller's discretion: the memo
        /// is compared by subset and the result is keyed by name, so a repeated name would both defeat the
        /// count comparison below and collide when the result is keyed.</para>
        /// </remarks>
        internal FrozenDictionary<string, EquatableArray<string>> DistinctAttributeValues(
            IReadOnlySet<string> names)
        {
            AttributeValueMemo? memo = attributeValueMemo;   // snapshot the single reference before comparing/using
            bool sameTree = memo is not null && ReferenceEquals(memo.Source, Root);
            if (sameTree && memo!.Names.IsSupersetOf(names))
            {
                return Only(memo.Values, names);
            }

            var collected = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                collected[name] = new SortedSet<string>(StringComparer.Ordinal);
            }
            if (sameTree)
            {
                foreach (string name in memo!.Names)
                {
                    collected.TryAdd(name, new SortedSet<string>(StringComparer.Ordinal));
                }
            }

            // Attribute-bag-outer, wanted-inner: the bag is a linear scan, so asking it once per wanted attribute
            // re-scans each element's bag once per name. One pass per element regardless of how many are wanted.
            foreach (ProjectElement element in Root.DescendantsAndSelf())
            {
                foreach ((string name, string value) in element.Attrs)
                {
                    if (collected.TryGetValue(name, out SortedSet<string>? values)
                        && !string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }
            }

            FrozenDictionary<string, EquatableArray<string>> built = collected.ToFrozenDictionary(
                entry => entry.Key,
                entry => (EquatableArray<string>)ImmutableArray.CreateRange(entry.Value),
                StringComparer.Ordinal);
            attributeValueMemo =
                new AttributeValueMemo(Root, collected.Keys.ToFrozenSet(StringComparer.Ordinal), built);
            return Only(built, names);
        }

        /// <summary>The asked-for names alone, so no caller can read an attribute another caller's walk added.
        /// Equal counts mean equal sets here, because <paramref name="all"/> is always a superset of
        /// <paramref name="names"/> and neither repeats a name.</summary>
        private static FrozenDictionary<string, EquatableArray<string>> Only(
            FrozenDictionary<string, EquatableArray<string>> all, IReadOnlySet<string> names) =>
            all.Count == names.Count
                ? all
                : names.ToFrozenDictionary(name => name, name => all[name], StringComparer.Ordinal);

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
        public ProjectElement? FindById(ElementId id) => ElementIndex.FindById(id);

        /// <summary>
        /// Returns the immediate parent of the element carrying the given id, or <c>null</c> when the id is the
        /// root or is absent. <see cref="ProjectElement"/> is an immutable value with no parent pointer, so parent
        /// navigation is resolved here against the tree (the read side of link navigation and far-end paths).
        /// </summary>
        public ProjectElement? FindParent(ElementId id) => ElementIndex.FindParent(id);

        /// <summary>The <c>group</c> localities declared under <c>groups</c>.</summary>
        public IReadOnlyList<ProjectElement> Groups =>
            Child("groups") is { } groups
                ? groups.Children.AsImmutableArray().Where(c => c.Tag == "group").ToImmutableArray()
                : ImmutableArray<ProjectElement>.Empty;

        public override string ToString() =>
            $"Project(Version={Version}, Id1={Id1}, Id2={Id2}, Children=ProjectElement[{Children.Count}])";
    }
}
