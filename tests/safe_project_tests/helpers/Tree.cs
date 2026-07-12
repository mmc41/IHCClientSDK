#nullable enable
using System.Collections.Immutable;

using Ihc.Vis.Model;

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
    }
}
