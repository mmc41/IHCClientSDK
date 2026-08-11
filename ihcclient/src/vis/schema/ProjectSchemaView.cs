#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

using Ihc.Vis.Projects;
namespace Ihc.Vis.Schema
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
        public static readonly ProjectSchemaView RegistryOnly = new(FrozenDictionary<string, ElementSchema>.Empty);

        // Frozen, not Immutable: built once, never mutated, and never enumerated — TryGet is the only reader, and it
        // runs per element on every whole-tree pass (canonicalize on each commit, plus load/save/validate/insert).
        private readonly FrozenDictionary<string, ElementSchema> captured;

        private ProjectSchemaView(FrozenDictionary<string, ElementSchema> captured) => this.captured = captured;

        /// <summary>The project's schema view (memoized on the project; built eagerly at load).</summary>
        public static ProjectSchemaView For(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return project.SchemaView;
        }

        /// <summary>Builds a view over captured inline-DTD blocks (tag → verbatim block); registry fallback.</summary>
        public static ProjectSchemaView For(ImmutableDictionary<string, string>? blocks)
        {
            if (blocks is null || blocks.IsEmpty)
            {
                return RegistryOnly;
            }
            var builder = new Dictionary<string, ElementSchema>(blocks.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in blocks)
            {
                builder[entry.Key] = ProjectSchemaRegistry.ParseBlock(entry.Value);
            }
            return new ProjectSchemaView(builder.ToFrozenDictionary(StringComparer.Ordinal));
        }

        // Grammar → view memo. A CatalogGrammar is immutable (and the built-in catalog interns ~99 distinct grammars
        // across 173 components), but inserts and builder validations resolve a view per call — the weak table caches
        // the pure projection once per distinct grammar instance without pinning user-built grammars.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Ihc.Vis.Model.CatalogGrammar, ProjectSchemaView> byGrammar = new();

        /// <summary>Builds a view over a catalog component's structured grammar (registry fallback) — each
        /// declaration projected directly via <see cref="ElementSchema.FromDeclaration"/>, no text round trip,
        /// memoized per grammar instance. On a lenient-fallback grammar the declarations are the best-effort
        /// projection, so exotic user files keep their insert semantics.</summary>
        public static ProjectSchemaView For(Ihc.Vis.Model.CatalogGrammar? grammar)
        {
            if (grammar is null || grammar.Declarations.IsEmpty)
            {
                return RegistryOnly;
            }
            return byGrammar.GetValue(grammar, static g =>
            {
                var builder = new Dictionary<string, ElementSchema>(g.Declarations.Length, StringComparer.Ordinal);
                foreach (Ihc.Vis.Model.GrammarDeclaration declaration in g.Declarations)
                {
                    builder[declaration.Tag] = ElementSchema.FromDeclaration(declaration);
                }
                return new ProjectSchemaView(builder.ToFrozenDictionary(StringComparer.Ordinal));
            });
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
