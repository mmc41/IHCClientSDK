#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Ihc.Projects
{
    /// <summary>
    /// The per-operation schema resolver: looks up an <see cref="ElementSchema"/> for an element tag, preferring
    /// the blocks captured verbatim from a project's own inline DTD (<see cref="Project.InlineDtdBlocks"/>) and
    /// falling back to the static <see cref="ProjectSchemaRegistry"/>. This is what makes load/edit/save
    /// <em>open-world</em>: an element type the registry never declared (a custom product/function block authored
    /// in IHC Visual and copied into the file) is resolved from the file's own grammar, so it round-trips
    /// byte-identically; the registry remains the source for newly created/inserted types the file does not yet
    /// contain. File-captured blocks win, so the round-trip stays byte-exact even if the registry ever drifts from
    /// a given IHC Visual version's block.
    /// </summary>
    internal sealed class ProjectSchemaView
    {
        /// <summary>A view with no captured blocks — resolves purely against the static registry (create path).</summary>
        public static readonly ProjectSchemaView RegistryOnly = new(ImmutableDictionary<string, ElementSchema>.Empty);

        private readonly ImmutableDictionary<string, ElementSchema> captured;

        private ProjectSchemaView(ImmutableDictionary<string, ElementSchema> captured) => this.captured = captured;

        /// <summary>Builds a view over a project's captured inline-DTD blocks, with the static registry as fallback.</summary>
        public static ProjectSchemaView For(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return For(project.InlineDtdBlocks);
        }

        /// <summary>Builds a view over captured inline-DTD blocks (tag → verbatim block); registry fallback.</summary>
        public static ProjectSchemaView For(ImmutableDictionary<string, string>? blocks)
        {
            if (blocks is null || blocks.IsEmpty)
            {
                return RegistryOnly;
            }
            var builder = ImmutableDictionary.CreateBuilder<string, ElementSchema>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in blocks)
            {
                builder[entry.Key] = ProjectSchemaRegistry.ParseBlock(entry.Value);
            }
            return new ProjectSchemaView(builder.ToImmutable());
        }

        /// <summary>The schema for the tag — captured block first, then the static registry — or <c>null</c>.</summary>
        public ElementSchema? TryGet(string tag) =>
            captured.TryGetValue(tag, out ElementSchema? schema) ? schema : ProjectSchemaRegistry.TryGet(tag);

        /// <summary>The schema for the tag; throws when neither the file's inline DTD nor the registry declares it.</summary>
        public ElementSchema Get(string tag) =>
            TryGet(tag) ?? throw new InvalidOperationException(
                $"No schema for .vis element type '{tag}' in the project's own inline DTD or the schema registry. " +
                $"A project may only contain element types declared by its inline DTD or the SDK registry.");
    }
}
