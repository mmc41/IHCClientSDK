#nullable enable
using System;
using System.Collections.Generic;

using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;
using TypeCode = Ihc.Vis.Schema.TypeCode;
namespace Ihc.Vis.FunctionBlocks
{
    /// <summary>
    /// The fixed vendor grammar a code-authored function-block body carries: the five resource containers'
    /// name/icon/note, the program skeleton's name/icon/note/type, and the program-leaf icons — plus the shared
    /// <see cref="Node"/> constructor. Factored here so <see cref="FunctionBlockDefinitionBuilder"/> and
    /// <see cref="FbProgramBuilder"/> emit identical structural decorations (D.R.Y).
    /// </summary>
    /// <remarks>
    /// These constants are transcribed locally rather than reused from <c>Ihc.Vis.Editing.ProgramGrammar</c>: the
    /// <c>Editing</c> layer already depends on <c>FunctionBlocks</c> (via <see cref="FbResourceHandle"/>'s reason for
    /// existing), so <c>FunctionBlocks</c> must not depend back on it. The strings differ from the editing peer's too
    /// — the notes here match the authentic <c>FunctionBlocks\*.ifb</c> convention (the synthetic oracle set), whereas
    /// <c>ProgramGrammar</c>'s were transcribed from a project-embedded custom block. Only the <c>inputs</c>/<c>outputs</c>
    /// container notes vary per block; the caller overrides those via
    /// <see cref="FunctionBlockDefinitionBuilder.InputsNote"/>/<see cref="FunctionBlockDefinitionBuilder.OutputsNote"/>.
    /// </remarks>
    internal static class FbGrammar
    {
        public const string InputsName = "Input";
        public const string InputsIcon = "_0x4";
        public const string InputsNoteDefault = "Indgange til funktionsblokken";

        public const string OutputsName = "Output";
        public const string OutputsIcon = "_0x14";
        public const string OutputsNoteDefault = "Udgange fra funktionsblokken";

        public const string SettingsName = "Indstillinger";
        public const string SettingsIcon = "_0xd";
        public const string SettingsNote = "Indstillinger som brugeren kan ændre";

        public const string InternalName = "Interne variable";
        public const string InternalIcon = "_0x13";
        public const string InternalNote = "Private variable til blokkens eget brug";

        public const string ProgramsName = "Programmer";
        public const string ProgramsIcon = "_0x19";
        public const string ProgramsNote = "Gruppering af blokkens programmer";

        public const string ProgramSimpleIcon = "_0x7";

        public const string EventsName = "Hændelser";
        public const string EventsIcon = "_0xb";
        public const string EventsNote = "Hændelser der udløser programmet";

        public const string RootActionsName = "Kommandoer";
        public const string ActionsIcon = "_0x8";
        public const string RootActionsNote = "Kommandoer der udføres når en hændelse indtræffer";
        public const string RootActionsEmptyNote = "Kommandoer der udføres";
        public const string RootActionsType = "_0x2";

        public const string SubProgramName = "Under program";
        public const string SubProgramIcon = "_0x7";

        public const string ConditionsName = "Betingelser";
        public const string ConditionsIcon = "_0x16";
        public const string ConditionsNote = "Betingelser der testes logisk";

        public const string TrueActionsName = "Kommandoer ved betingelser sande";
        public const string TrueActionsNote = "Kommandoer der udføres når betingelserne er opfyldt";
        public const string TrueBranchType = "_0x1";

        public const string FalseActionsName = "Kommandoer ved betingelser falske";
        public const string FalseActionsNote = "Kommandoer der udføres når betingelserne ikke er opfyldt";

        public const string EventIcon = "_0xc";
        public const string ConditionIcon = "_0x1a";
        public const string ActionIcon = "_0x9";
        public const string EnumOperandIcon = "_0x22";

        private static readonly ProjectElement[] NoChildren = Array.Empty<ProjectElement>();

        // The per-resource-type presentation attributes IHC Visual stamps on a freshly-authored resource: the canonical
        // GUI icon, plus the #REQUIRED value initials for the value types that carry them. Transcribed locally rather
        // than reused from Ihc.Vis.Editing.ResourceMaterialization (FunctionBlocks must not depend on Editing); keep in
        // sync with that table. A type absent here keeps its DTD-default icon (_0x0, elided on save).
        private static readonly Dictionary<string, string> ResourceIcons = new(StringComparer.Ordinal)
        {
            ["resource_enum"] = "_0x22",
            ["resource_input"] = "_0x36",
            ["resource_output"] = "_0x39",
            ["resource_timer"] = "_0x43",
            ["resource_flag"] = "_0x33",
            ["resource_time"] = "_0x2f",
            ["resource_date"] = "_0x29",
            ["resource_weekday"] = "_0x2c",
            ["resource_timertime"] = "_0x4d",
            ["resource_holiday"] = "_0x9b",
        };

        private static readonly Dictionary<string, (string Name, string Value)[]> ResourceRequiredValues =
            new(StringComparer.Ordinal)
            {
                ["resource_date"] = new[] { ("year", "2000"), ("month", "1"), ("day", "1") },
                ["resource_time"] = new[] { ("hour", "0"), ("minute", "0"), ("second", "0") },
                ["resource_timer"] = new[] { ("hour", "0"), ("minute", "0"), ("second", "0"), ("millisecond", "0") },
                ["resource_timertime"] = new[] { ("hour", "0"), ("minute", "0"), ("second", "0"), ("millisecond", "0") },
            };

        /// <summary>The presentation attributes a hand-authored resource of <paramref name="tag"/> carries: the
        /// canonical icon (when any) followed by the type's <c>#REQUIRED</c> value initials. Caller attributes are
        /// applied after these and win on any name collision.</summary>
        public static IReadOnlyList<(string Name, string Value)> NewResourceDefaults(string tag)
        {
            (string Name, string Value)[] required = ResourceRequiredValues.TryGetValue(tag, out (string Name, string Value)[]? values)
                ? values
                : Array.Empty<(string, string)>();
            if (!ResourceIcons.TryGetValue(tag, out string? icon))
            {
                return required;
            }
            var defaults = new List<(string Name, string Value)>(required.Length + 1) { ("icon", icon) };
            defaults.AddRange(required);
            return defaults;
        }

        /// <summary>Allocates a fresh id for <paramref name="tag"/> and builds a container node with the fixed
        /// name/icon plus <paramref name="note"/>, holding <paramref name="children"/>.</summary>
        public static ProjectElement Container(IdAllocator ids, string tag, string name, string icon, string note,
            IEnumerable<ProjectElement> children) =>
            Node(tag, ids.Allocate(TypeCode.RequireForTag(tag)),
                new[] { ("name", name), ("icon", icon), ("note", note) }, children);

        /// <summary>Builds an element node via <see cref="ProjectElement.Create"/>: the <c>id</c> token leads the raw
        /// attribute bag, then <paramref name="attrs"/>; the final canonicalize pass fixes order and omits defaults.</summary>
        public static ProjectElement Node(string tag, ElementId? id, IEnumerable<(string Name, string Value)> attrs,
            IEnumerable<ProjectElement> children) => ProjectElement.Create(tag, id, attrs, children);

        /// <summary>A leaf node with no children.</summary>
        public static ProjectElement Leaf(string tag, ElementId id, IEnumerable<(string Name, string Value)> attrs) =>
            Node(tag, id, attrs, NoChildren);
    }
}
