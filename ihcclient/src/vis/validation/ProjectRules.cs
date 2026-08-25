#nullable enable
using System;

using Ihc.Vis.Model;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The composition root: every project rule the SDK ships, registered against the catalogue once.
    /// <para>
    /// ONE PASS is the design commitment this type makes concrete. Every lifecycle gate — an upload, the opt-in
    /// validate-before-save, a report's appendix — reads the findings of a single run of these rules, never a
    /// second pipeline with its own rule set. That is what stops a save failing for a reason nothing reported.
    /// </para>
    /// <para>
    /// Built once and shared: the catalogue, the rule set and the executor hold no per-run state, so one of each
    /// serves the process. Rebuilding them per call would re-register 35 rules to answer one question.
    /// </para>
    /// </summary>
    public static class ProjectRules
    {
        /// <summary>The registered rules, built once against <see cref="ProblemCatalog.Current"/>.</summary>
        public static RuleSet Registered { get; } = RuleSet.Create(ProblemCatalog.Current, All(ProblemCatalog.Current));

        /// <summary>The shared whole-project executor over <see cref="Registered"/>.</summary>
        public static IWholeProjectValidator Validator { get; } = new WholeProjectValidator(Registered);

        /// <summary>Every shipped project rule, ready to register against a catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return
            [
                .. SchemaConformanceRules.All(catalog),
                .. IdentityRules.All(catalog),
                .. StructureRules.All(catalog),
                .. ReciprocityAndEnumRules.All(catalog),
                .. DatalineAddressRules.All(catalog),
                .. WiringRules.All(catalog),
                .. ScenarioRules.All(catalog),
                .. ModuleAddressRules.All(catalog),
                .. DeviceAddressRules.All(catalog),
                .. DeviceSettingRules.All(catalog),
                .. DeviceStateRules.All(catalog),
                .. EnumDefinitionRules.All(catalog),
                .. FunctionBlockShapeRules.All(catalog),
                .. ProgramShapeRules.All(catalog),
                .. VariableUsageRules.All(catalog),
                .. ProgramDataflowRules.All(catalog),
                .. CapacityRules.All(catalog),
                .. ProjectStructureRules.All(catalog),
                .. NamingRules.All(catalog),
                .. DocumentationCompletenessRules.All(catalog),
                .. DocumentationRules.All(catalog),
            ];
        }
    }
}
