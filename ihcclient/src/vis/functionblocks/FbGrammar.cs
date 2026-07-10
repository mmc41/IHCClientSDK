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
    /// This is the single transcription of the shared vendor constants: <c>Ihc.Vis.Editing.ProgramGrammar</c> (the
    /// layer above — <c>Editing</c> already depends on <c>FunctionBlocks</c> via <see cref="FbResourceHandle"/>)
    /// aliases the icons/names/branch-type it shares with this table, keeping only its genuinely divergent container
    /// notes local: those were transcribed from a project-embedded custom block, whereas the notes here match the
    /// authentic <c>FunctionBlocks\*.ifb</c> convention (the synthetic oracle set). Only the <c>inputs</c>/<c>outputs</c>
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

        // program_case / case_action: a switch on a variable (program_case@link) with per-value case_action branches
        // (each embedding a bare resource_enum operand named by case_action@value) plus a trailing default actions
        // container. Icons match the program_sub / actions glyphs; the default-branch name/note are the vendor strings.
        public const string ProgramCaseIcon = "_0x7";
        public const string CaseActionIcon = "_0x8";
        public const string DefaultCaseName = "Udføres når ingen case er lig case værdien";
        public const string DefaultCaseNote = "Udføres når ingen case er lig case værdien";
        public const string DefaultCaseType = "_0x1";

        // The display name IHC Visual composes from a block's master identity: bare name for a keyless user block,
        // "{type}. {name}" for a versionless stock block, else "{type}.{version}. {name}". The builder stamps this by
        // default and the decompiler recomputes it to decide whether a .DisplayName(..) override is needed, so both must
        // read from this one formula.
        public static string ComposeDisplayName(string? masterType, string? masterVersion, string masterName) =>
            string.IsNullOrEmpty(masterType) ? masterName
            : string.IsNullOrEmpty(masterVersion) ? $"{masterType}. {masterName}"
            : $"{masterType}.{masterVersion}. {masterName}";

        private static readonly ProjectElement[] NoChildren = Array.Empty<ProjectElement>();

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
