#nullable enable
using System.Collections.Immutable;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Builds in-memory <see cref="ProjectElement"/> trees with raw id tokens, for tests that must reproduce
    /// foreign / hand-authored files carrying non-canonical or unparseable id spellings the loader would never
    /// emit from its own writer. An unparseable token yields a null <see cref="ProjectElement.Id"/> while the raw
    /// <c>id</c> attribute is preserved verbatim — exactly the loader's own behaviour.
    /// </summary>
    internal static class Tree
    {
        public static ProjectElement Node(string tag, string? id, (string, string)[] attrs, params ProjectElement[] children)
        {
            ElementId? parsed = id is not null && ElementId.TryParse(id, out ElementId p) ? p : null;
            var bag = ImmutableArray.CreateBuilder<(string, string)>();
            if (id is not null)
            {
                bag.Add(("id", id));
            }
            bag.AddRange(attrs);
            return new ProjectElement(tag, parsed, bag.ToImmutable(), children.ToImmutableArray());
        }

        /// <summary>
        /// A project whose root is a well-formed <c>utcs_project</c> carrying the given children — the shape a rule
        /// test needs when the root itself is not what it is testing. Version 4.0 and a high-water mark above every
        /// counter these trees use, so no id or version rule fires alongside the one under test.
        /// </summary>
        /// <param name="children">The root's children, in document order.</param>
        public static Project WithRoot(params ProjectElement[] children) =>
            new(Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"),
                 ("last_unique_id", "_0xffff")],
                children));

        /// <summary>
        /// The smallest project the SERIALIZER accepts: a bare, childless root, so nothing but the gate under
        /// test can refuse a write of it.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="WithRoot"/>, which exists for rule tests and seeds a high-water
        /// <c>last_unique_id</c> so no counter rule fires beside the one being exercised. This one is for the
        /// SAVE and EDIT paths, where the document is a backdrop rather than the subject.
        /// <para>
        /// Metadata is written VERBATIM by the fixtures that use it: a vendor-like re-stamp needs a
        /// <c>&lt;modified&gt;</c> element, and adding one would put a second reason to refuse beside the one
        /// under test.
        /// </para>
        /// </remarks>
        /// <param name="extraAttrs">Root attributes to add, for a test that needs one.</param>
        public static Project MinimalProject(params (string Name, string Value)[] extraAttrs) =>
            new(Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"),
                 ("last_unique_id", "_0x3"), .. extraAttrs]));
    }
}
