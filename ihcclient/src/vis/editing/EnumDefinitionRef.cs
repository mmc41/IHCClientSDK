#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// A handle to a project-global enum definition authored via <see cref="ProjectEditor.AddEnumDefinition"/>.
    /// Its <see cref="Typedef"/> token wires a <c>resource_enum</c>'s <c>typedef</c>; <see cref="InitialValue"/>
    /// wires its <c>inivalue</c> to one of the definition's values by name.
    /// </summary>
    public sealed class EnumDefinitionRef
    {
        private readonly string name;
        private readonly ImmutableArray<(string Name, ElementId Id)> values;

        internal EnumDefinitionRef(string name, ElementId id, ImmutableArray<(string Name, ElementId Id)> values)
        {
            this.name = name;
            Id = id;
            this.values = values;
        }

        internal ElementId Id { get; }

        /// <summary>The type's display name — how the installer identifies it, and therefore the only form a refusal
        /// may name it by. No vendor surface shows an <c>_0x</c> token to a user.</summary>
        public string Name => name;

        /// <summary>The token to assign to a <c>resource_enum</c>'s <c>typedef</c> attribute.</summary>
        public string Typedef => Id.ToToken();

        /// <summary>The definition's values in document order, each with its id. The enum-manager commands address a
        /// value POSITIONALLY (the dialog lists positions, not ids), so this is where a position becomes an id.</summary>
        public IReadOnlyList<(string Name, ElementId Id)> Values => values;

        /// <summary>The <c>inivalue</c> token for this definition's first value (its default initial state), or null
        /// when the type has no values — used when inserting a variable of an EXISTING enum type (US-030, PG-4).</summary>
        public string? FirstValue => values.IsDefaultOrEmpty ? null : values[0].Id.ToToken();

        /// <summary>The <c>inivalue</c> token for the value with the given name.</summary>
        public string InitialValue(string valueName)
        {
            foreach ((string Name, ElementId Id) value in values)
            {
                if (value.Name == valueName)
                {
                    return value.Id.ToToken();
                }
            }
            throw new InvalidOperationException(
                $"Enum definition '{name}' has no value named '{valueName}'; available values: " +
                $"({string.Join(" | ", values.Select(v => v.Name))}).");
        }
    }
}
