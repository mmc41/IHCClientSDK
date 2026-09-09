using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Schema
{
    /// <summary>The role a function-block variable type plays in the §6.3.1 section↔type model (ADR-002): an
    /// input pin, an output pin, or a value variable accepted by any block container. SDK-authoritative — the UI
    /// palette projects display labels over it.</summary>
    public enum VariableRole
    {
        /// <summary>An input pin (<c>resource_input</c>), bound to the block's <c>inputs</c> container.</summary>
        Input,

        /// <summary>An output pin (<c>resource_output</c>), bound to the block's <c>outputs</c> container.</summary>
        Output,

        /// <summary>A value variable accepted by any function-block value container (inputs/outputs/settings/internalsettings).</summary>
        Value,
    }

    /// <summary>An SDK-supported, user-authorable function-block variable type: its resource <see cref="Tag"/> and
    /// its <see cref="Role"/> classification (ADR-002).</summary>
    internal readonly record struct VariableTypeInfo(string Tag, VariableRole Role);

    /// <summary>
    /// The single authoritative registry of the function-block variable types a user can author into a block's
    /// sections (US-027, ADR-002): the input and output pin types and the value-variable resource types the
    /// engine accepts, each classified by <see cref="VariableRole"/>. <see cref="PlacementRules"/> admits value
    /// insertion by this set, and the OpenVisual "Insert variable" palette projects display labels over it — so the
    /// set the engine accepts and the set the UI offers cannot drift apart. Display labels stay app-side
    /// (presentation); a UI completeness test guarantees every entry here is either presented or on an explicit
    /// suppression list. Scene links (<c>resource_scene</c>) are a separate flow (US-024), deliberately not a
    /// variable type.
    /// </summary>
    internal static class VariableTypeRegistry
    {
        /// <summary>Every authorable variable type with its role, in palette order (input, output, then the value
        /// variables in the order they are offered under a block container).</summary>
        public static ImmutableArray<VariableTypeInfo> All { get; } =
        [
            new("resource_input", VariableRole.Input),
            new("resource_output", VariableRole.Output),
            new("resource_flag", VariableRole.Value),
            new("resource_integer", VariableRole.Value),
            new("resource_floating_point", VariableRole.Value),
            new("resource_counter", VariableRole.Value),
            new("resource_date", VariableRole.Value),
            new("resource_time", VariableRole.Value),
            new("resource_timer", VariableRole.Value),
            new("resource_timertime", VariableRole.Value),
            new("resource_weekday", VariableRole.Value),
            new("resource_holiday", VariableRole.Value),
            new("resource_enum", VariableRole.Value),
            new("resource_light", VariableRole.Value),
            new("resource_light_level", VariableRole.Value),
            new("resource_temperature", VariableRole.Value),
            new("resource_humidity_level", VariableRole.Value),
            new("kW", VariableRole.Value),
            new("kWh", VariableRole.Value),
            new("W", VariableRole.Value),
            new("Wh", VariableRole.Value),
        ];

        /// <summary>The value-variable resource tags (<see cref="VariableRole.Value"/>), in order — the exact set
        /// <see cref="PlacementRules"/> admits into any function-block value container (§6.3.1).</summary>
        public static ImmutableArray<string> ValueTypeTags { get; } =
            [.. All.Where(t => t.Role == VariableRole.Value).Select(t => t.Tag)];

        private static readonly FrozenSet<string> AllTags =
            All.Select(t => t.Tag).ToFrozenSet(StringComparer.Ordinal);

        private static readonly FrozenDictionary<string, VariableRole> RolesByTag =
            All.ToFrozenDictionary(t => t.Tag, t => t.Role, StringComparer.Ordinal);

        /// <summary>
        /// The role <paramref name="tag"/> plays in the section/type model, for a tag this registry declares. The
        /// role is DECLARED per row above, so a reader asks for it here rather than inferring it from membership of
        /// <see cref="ValueTypeTags"/> — which cannot tell an input from an output.
        /// </summary>
        /// <exception cref="KeyNotFoundException"><paramref name="tag"/> is not an authorable variable type; test
        /// with <see cref="IsVariableType"/> first.</exception>
        public static VariableRole RoleOf(string tag) => RolesByTag[tag];

        /// <summary>Whether <paramref name="tag"/> is an authorable variable type — any role, so both signal pins and
        /// the value variables. The membership test callers use to keep non-variables (notably <c>resource_scene</c>,
        /// which US-024 owns) out of a variable palette without hard-coding that exception themselves.</summary>
        public static bool IsVariableType(string tag) => AllTags.Contains(tag);
    }
}
