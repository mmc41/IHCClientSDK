#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Io
{
    /// <summary>
    /// The serializer's own write self-check (M2, extracted from <c>ProjectAppService.Save</c>): after
    /// <see cref="ProjectSerializer.Serialize"/>, re-parse the bytes and confirm they reproduce the in-memory
    /// project — so a model that holds state the <c>.vis</c> format cannot represent throws BEFORE the file is
    /// handed back, instead of silently writing a lossy file. Schema-coupled statics with no instance state.
    /// </summary>
    internal static class ProjectRoundTripVerifier
    {
        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> when <paramref name="bytes"/> (the serialized form of
        /// <paramref name="toWrite"/>) do not re-parse back to <paramref name="toWrite"/>. Tolerant comparison: the
        /// serializer omits a Defaulted attribute whose value equals its DTD default (<see cref="AttrSchema.OmitsOnWrite"/>)
        /// and the reader never re-materializes it, so a model that explicitly carried such an attribute is a FAITHFUL
        /// write even though a naive re-parse equality would differ. Drop exactly those on both sides before comparing
        /// — Project equality is Root-only, so the stripped roots compare directly — and a foreign file with an
        /// explicit default-equal attribute round-trips while any genuine loss (a changed/absent non-default value, a
        /// dropped subtree) still diverges and throws. Both schema views are memoized (the reader warms the reparsed
        /// one eagerly).
        /// </summary>
        public static void Verify(Project toWrite, byte[] bytes)
        {
            Project reparsed = ProjectReader.Read(new MemoryStream(bytes));
            ProjectElement expected = StripDefaultEqualAttrs(toWrite.Root, toWrite.SchemaView);
            ProjectElement actual = StripDefaultEqualAttrs(reparsed.Root, reparsed.SchemaView);
            if (!actual.Equals(expected))
            {
                throw new InvalidOperationException(
                    "Serialize/re-parse mismatch: the written bytes do not reproduce the in-memory project" +
                    FirstDivergence(expected, actual, path: "utcs_project") +
                    " — the model holds state the .vis format cannot represent.");
            }
        }

        private static string FirstDivergence(ProjectElement expected, ProjectElement actual, string path)
        {
            if (expected.Tag != actual.Tag)
            {
                return $" (first divergence at {path}: element <{expected.Tag}> re-read as <{actual.Tag}>)";
            }
            var actualAttrs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string name, string value) in actual.Attrs)
            {
                actualAttrs[name] = value;
            }
            foreach ((string name, string value) in expected.Attrs)
            {
                if (!actualAttrs.Remove(name, out string? reread))
                {
                    return $" (first divergence at {path}/<{expected.Tag}>: attribute '{name}'='{value}' is absent after re-parse)";
                }
                if (reread != value)
                {
                    return $" (first divergence at {path}/<{expected.Tag}>: attribute '{name}' expected '{value}', re-read '{reread}')";
                }
            }
            if (actualAttrs.Count > 0)
            {
                string extra = actualAttrs.Keys.First();
                return $" (first divergence at {path}/<{expected.Tag}>: attribute '{extra}' appears only after re-parse)";
            }
            int expectedCount = expected.Children.Length;
            int actualCount = actual.Children.Length;
            if (expectedCount != actualCount)
            {
                return $" (first divergence at {path}/<{expected.Tag}>: {expectedCount} children re-read as {actualCount})";
            }
            for (int i = 0; i < expectedCount; i++)
            {
                if (!expected.Children[i].Equals(actual.Children[i]))
                {
                    return FirstDivergence(expected.Children[i], actual.Children[i], $"{path}/<{expected.Tag}>[{i}]");
                }
            }
            return string.Empty;
        }

        // Drops every attribute the serializer omits on write (AttrSchema.OmitsOnWrite — the serializer's own omit
        // rule), recursively, so the round-trip verification compares only the state that actually reaches the
        // file: the benign omit-if-default asymmetry is normalized away on both sides, genuine differences are not.
        // Copy-on-write: an element with nothing to strip anywhere below it — the overwhelmingly common case, and
        // by construction always true for the reparsed side — is returned as-is, so the walk allocates nothing.
        private static ProjectElement StripDefaultEqualAttrs(ProjectElement element, ProjectSchemaView view)
        {
            ElementSchema? schema = view.TryGet(element.Tag);
            ImmutableArray<(string Name, string Value)> attrs = element.Attrs.AsImmutableArray();
            ImmutableArray<(string, string)>.Builder? keptAttrs = null;   // created on the first dropped attribute
            for (int i = 0; i < attrs.Length; i++)
            {
                if (schema?.FindAttr(attrs[i].Name) is { } attr && attr.OmitsOnWrite(attrs[i].Value))
                {
                    if (keptAttrs is null)
                    {
                        keptAttrs = ImmutableArray.CreateBuilder<(string, string)>(attrs.Length);
                        for (int j = 0; j < i; j++) { keptAttrs.Add(attrs[j]); }
                    }
                    continue;
                }
                keptAttrs?.Add(attrs[i]);
            }
            ImmutableArray<ProjectElement> children = element.Children.AsImmutableArray();
            ImmutableArray<ProjectElement>.Builder? keptChildren = null;   // created on the first changed child
            for (int i = 0; i < children.Length; i++)
            {
                ProjectElement stripped = StripDefaultEqualAttrs(children[i], view);
                if (keptChildren is null && !ReferenceEquals(stripped, children[i]))
                {
                    keptChildren = ImmutableArray.CreateBuilder<ProjectElement>(children.Length);
                    for (int j = 0; j < i; j++) { keptChildren.Add(children[j]); }
                }
                keptChildren?.Add(stripped);
            }
            return keptAttrs is null && keptChildren is null
                ? element
                : new ProjectElement(element.Tag, element.Id,
                    keptAttrs?.ToImmutable() ?? attrs,
                    keptChildren?.ToImmutable() ?? children);
        }
    }
}
