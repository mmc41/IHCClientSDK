#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Ihc.Projects
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

        /// <summary>The <c>version_major.version_minor</c> of the project format (always 4.0 for v1).</summary>
        public string Version =>
            $"{Root.GetAttribute("version_major") ?? "4"}.{Root.GetAttribute("version_minor") ?? "0"}";

        /// <summary>The project creation stamp <c>id1</c> (constant for the project's life).</summary>
        public string? Id1 => Root.GetAttribute("id1");

        /// <summary>The current-save stamp <c>id2</c> (re-stamped every save).</summary>
        public string? Id2 => Root.GetAttribute("id2");

        /// <summary>The persistent high-water-mark id <c>last_unique_id</c>.</summary>
        public string? LastUniqueId => Root.GetAttribute("last_unique_id");

        /// <summary>The programmer (<c>project_info/@programmer</c>); the write-path counterpart is <see cref="ProjectDetails.Programmer"/>.</summary>
        public string? Programmer => Child("project_info")?.GetAttribute("programmer");

        /// <summary>The project number (<c>project_info/@number</c>).</summary>
        public string? ProjectNumber => Child("project_info")?.GetAttribute("number");

        /// <summary>The installer name (<c>installer_info/@name</c>); the write-path counterpart is <see cref="ProjectDetails.InstallerName"/>.</summary>
        public string? InstallerName => Child("installer_info")?.GetAttribute("name");

        /// <summary>The installer country (<c>installer_info/@country</c>); the write-path counterpart is <see cref="ProjectDetails.InstallerCountry"/>.</summary>
        public string? InstallerCountry => Child("installer_info")?.GetAttribute("country");

        /// <summary>The customer name (<c>customer_info/@name</c>).</summary>
        public string? CustomerName => Child("customer_info")?.GetAttribute("name");

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
                    ? new DateTimeOffset(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local))
                    : null;
            }
        }

        /// <summary>The seven fixed root children, in document order.</summary>
        public IReadOnlyList<ProjectElement> Children =>
            Root.Children.IsDefaultOrEmpty ? ImmutableArray<ProjectElement>.Empty : Root.Children;

        /// <summary>Returns the named fixed child element (e.g. <c>groups</c>), or <c>null</c> when absent.</summary>
        public ProjectElement? Child(string tag) => Root.FindChild(tag);

        /// <summary>The <c>group</c> localities declared under <c>groups</c>.</summary>
        public IReadOnlyList<ProjectElement> Groups =>
            Child("groups") is { } groups && !groups.Children.IsDefaultOrEmpty
                ? groups.Children.Where(c => c.Tag == "group").ToImmutableArray()
                : ImmutableArray<ProjectElement>.Empty;

        public override string ToString() =>
            $"Project(Version={Version}, Id1={Id1}, Id2={Id2}, Children=ProjectElement[{Children.Count}])";
    }
}
