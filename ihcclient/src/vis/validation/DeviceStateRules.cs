using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The remaining DEVICE rows: what a program does to a variable it may not write, what a product leaves
    /// uncommissioned, what survives a power failure and how much of it, and what an initial value is worth —
    /// whether a program overwrites it at every start, and whether it is inside the range its own field declares.
    ///
    /// <para><b>Two of them are scoped by MEASUREMENT rather than by their own wording, and the backlog says so
    /// up front.</b> <c>dev-backup-missing</c> is about BLOCK VARIABLES alone — an output terminal ships
    /// <c>backup="yes"</c> and an input terminal declares no such attribute, so a walk over every backup-capable
    /// element would report most of a project. And <c>dev-setting-default</c> needs to know what a factory default
    /// IS; this predicate answers that without a single default value, because the vendor writes a setting's value
    /// only when the installer changes it.</para>
    ///
    /// <para><b>The pattern both of those land on is "the author has shown intent".</b> An unmarked variable in a
    /// block where another variable IS marked, and an untouched setting on a product whose other settings were
    /// configured: in both cases the surrounding evidence is what turns a default into an omission. Without it the
    /// rule reports the ordinary state of every project, which is the failure mode this whole phase keeps meeting.
    /// </para>
    /// </summary>
    public static class DeviceStateRules
    {
        /// <summary>
        /// The block-variable kinds that HOLD STATE across a restart, taken from the four that declare a
        /// <c>backup</c> attribute defaulting to <c>no</c>. A timer or a clock reading is not state an installation
        /// reasons about after an outage.
        /// </summary>
        private static readonly ImmutableHashSet<string> StateVariableTags =
            ["resource_flag", "resource_counter", "resource_integer", "resource_enum"];

        /// <summary>The attribute marking a variable as surviving a power failure, and the value that marks it.</summary>
        private const string BackupAttribute = "backup";

        private const string BackupMarked = "yes";

        /// <summary>The accessibility attribute, and the value a program may not assign to.</summary>
        private const string AccessAttribute = "access";

        private const string ReadOnlyAccess = "readonly";

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "dev-write-to-read-only", WriteToReadOnly),
                Rule(catalog, "dev-setting-default", SettingAtDefault(catalog)),
                Rule(catalog, "dev-backup-missing", BackupMissing),
                Rule(catalog, "dev-inivalue-overwritten", InitialValueOverwritten),
                Rule(catalog, "dev-inivalue-out-of-range", InitialValueOutOfRange(catalog)),
                Rule(catalog, "backup-retained-count", RetainedCount));
        }

        /// <summary>
        /// How many resource values the project asks the controller to keep across a power failure — a number
        /// the controller rations at upload.
        /// <para>
        /// EVERY <c>resource_*</c> KIND, WHERE <see cref="BackupMissing"/> TAKES ONLY FOUR — the contrast is the
        /// point: that row asks which BLOCK VARIABLES an author forgot to mark, this one how large a budget the
        /// project asks for. One attribute, two questions.
        /// </para>
        /// <para>
        /// A TERMINAL IS NOT A RESOURCE ELEMENT and is not counted, even though it too ships
        /// <c>backup="yes"</c>. Whether its retained value draws on the same ration is unestablished, and the
        /// count is scoped exactly as the source scopes it rather than on an inference.
        /// </para>
        /// <para>NO CEILING IS COMPARED: the row states the count and stops. See the entry for why no threshold
        /// and no controller context are declared.</para>
        /// </summary>
        private static void RetainedCount(IProjectInspection inspection)
        {
            int retained = inspection.Analyses.Elements.Count(e =>
                e.Tag.StartsWith("resource_", StringComparison.Ordinal)
                && e.GetAttribute(BackupAttribute) == BackupMarked);
            if (retained > 0)
            {
                inspection.Report(null, Arguments(("count", retained)));
            }
        }

        /// <summary>
        /// The resource kinds whose value unit is a PERCENTAGE, and the only kinds
        /// <see cref="InitialValueOutOfRange"/> is scoped to.
        /// <para>
        /// <c>resource_light</c> IS DELIBERATELY ABSENT and the near-miss is the point: it is a LUX value on a
        /// 0–60,000 range, so checking it against 0–100 would report every well-formed project carrying one. The
        /// two kinds here are those whose 0–100 range the format specification records.
        /// </para>
        /// </summary>
        private static readonly ImmutableHashSet<string> PercentResourceTags =
            ["resource_humidity_level", "resource_light_level"];

        /// <summary>The attribute holding a resource's initial value.</summary>
        private const string InitialValueAttribute = "inivalue";

        /// <summary>
        /// A percent-unit resource whose initial value no physical unit can reach: nothing in the vendor tool
        /// checks it, so it reaches the controller unexamined.
        /// <para>SUBJECT: <see cref="PercentResourceTags"/> alone — see that field for why the lux-valued sibling
        /// is not among them. BOUNDS: declared on the entry, both INCLUSIVE. EXCLUSION: a value arithmetic cannot
        /// read, and a resource carrying no <c>inivalue</c> at all.</para>
        /// <para>THE RAW STRING IS BOUND, not the parsed number: the slot is <c>AttributeValue</c> so the
        /// sentence prints exactly what the file carries, decimals included. Parsing happens only to decide
        /// WHETHER to report.</para>
        /// </summary>
        /// <param name="catalog">The catalogue the entry, and both declared bounds, are declared in.</param>
        private static ProjectInspection InitialValueOutOfRange(ProblemCatalog catalog)
        {
            double minimum = Threshold(catalog, "dev-inivalue-out-of-range", "PercentMinimum");
            double maximum = Threshold(catalog, "dev-inivalue-out-of-range", "PercentMaximum");
            return inspection =>
            {
                foreach (ProjectElement resource in inspection.Analyses.Elements)
                {
                    if (!PercentResourceTags.Contains(resource.Tag)
                        || resource.GetAttribute(InitialValueAttribute) is not { Length: > 0 } raw
                        || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    {
                        continue;
                    }

                    if (value < minimum || value > maximum)
                    {
                        inspection.Report(resource, Arguments(
                            ("value", raw), ("variable", Name(resource)),
                            ("minimum", (int)minimum), ("maximum", (int)maximum)));
                    }
                }
            };
        }

        /// <summary>
        /// A program command assigning a variable declared read-only: the assignment is refused or ignored at
        /// runtime.
        /// <para>SUBJECT: every <c>action</c> whose operand resolves to a resource whose <c>access</c> is
        /// <c>readonly</c>. EXCLUSION: <c>writeonly</c> and <c>readwrite</c> — writing to a write-only variable is
        /// exactly what it is for. LOCATION: the action, which is the thing to change.</para>
        /// </summary>
        private static void WriteToReadOnly(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement action in inspection.Analyses.WithTag("action"))
            {
                if (topology.ByToken(action.GetAttribute("link1")) is { } target
                    && target.GetAttribute(AccessAttribute) == ReadOnlyAccess)
                {
                    inspection.Report(action, Arguments(
                        ("action", Name(action)), ("variable", Name(target))));
                }
            }
        }

        /// <summary>
        /// A device setting still at its factory default on a product whose other settings WERE configured: the
        /// device may not have been commissioned at all.
        /// <para>
        /// SUBJECT: every product holding a settings group. WHAT "at its factory default" MEANS, and why this
        /// predicate holds no default value at all: the vendor writes a setting's <c>value</c> only when the
        /// installer changes it — the catalog ships these elements with an id and nothing else — so a setting that
        /// stores no value IS at its factory default, whatever that default happens to be per family. The backlog
        /// asked that the defaults not be literals here; they are not present at all.
        /// </para>
        /// <para>
        /// THE THRESHOLD is what "otherwise configured" needs: the product must carry at least
        /// <c>MinimumConfiguredSettings</c> settings that DO store a value. Without it the rule reports every
        /// freshly placed product, where nothing is configured and nothing is therefore forgotten.
        /// </para>
        /// </summary>
        private static ProjectInspection SettingAtDefault(ProblemCatalog catalog)
        {
            double minimumConfigured = Threshold(catalog, "dev-setting-default", "MinimumConfiguredSettings");
            return inspection =>
            {
                foreach (ProjectElement product in AllProducts(inspection.Analyses))
                {
                    ImmutableArray<ProjectElement> settings = [.. Settings(product)];
                    if (settings.Length == 0)
                    {
                        continue;
                    }

                    ImmutableArray<ProjectElement> untouched =
                        [.. settings.Where(s => s.GetAttribute("value") is null)];
                    if (untouched.Length == 0 || settings.Length - untouched.Length < minimumConfigured)
                    {
                        continue;
                    }

                    inspection.ReportGroup(product, untouched, Arguments(
                        ("product", Name(product)),
                        ("untouched", untouched.Length),
                        ("settings", settings.Length)));
                }
            };
        }

        /// <summary>
        /// A block state variable not marked to survive a power failure, in a block where another variable IS
        /// marked: the installation returns to its initial state after an outage.
        /// <para>
        /// SUBJECT: BLOCK VARIABLES ALONE, measured: the same <i>Gem aktuel værdi</i> control
        /// appears on terminals too, but every <c>dataline_output</c> and <c>airlink_relay</c> ships
        /// <c>backup="yes"</c> and an input terminal declares no such attribute — so a walk over every
        /// backup-capable element would report most of a project. STATE variables only: the four kinds that
        /// declare a <c>backup</c> attribute defaulting to <c>no</c>.
        /// </para>
        /// <para>
        /// QUALIFIER: at least one OTHER variable of the same block is marked. Block variables default to
        /// unmarked, so an unmarked one is only informative where the author has demonstrably used the feature —
        /// which is the contrast the vendor fixture carries on purpose.
        /// </para>
        /// </summary>
        private static void BackupMissing(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                ImmutableArray<ProjectElement> variables =
                    [.. BlockVariables(block).Where(v => StateVariableTags.Contains(v.Tag))];
                if (!variables.Any(IsBackedUp))
                {
                    continue;   // the author never used the feature in this block: unmarked is the ordinary state
                }

                foreach (ProjectElement variable in variables.Where(v => !IsBackedUp(v)))
                {
                    inspection.Report(variable, Arguments(
                        ("variable", Name(variable)), ("block", Name(block))));
                }
            }
        }

        /// <summary>
        /// A variable whose stored initial value a power-up program assigns again at every start: the initial value
        /// is meaningless.
        /// <para>
        /// SUBJECT: every action inside a program triggered by <c>event_power</c>. CONDITION: its operand stores an
        /// <c>inivalue</c>. STORED means NON-DEFAULT here, and that is the canonicalizer's own rule rather than an
        /// assumption: a value equal to the DTD default is elided on save, so an <c>inivalue</c> present in the
        /// file is one the author chose — which is exactly what makes overwriting it at every start worth
        /// reporting.
        /// </para>
        /// <para>LOCATION: the variable, which is where the reader decides whether the initial value or the
        /// program is the redundant one.</para>
        /// </summary>
        private static void InitialValueOverwritten(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement program in inspection.Analyses.Elements
                .Where(e => e.Tag is "program_simple" or "program_sub"))
            {
                if (program.FindChild("events") is not { } events
                    || !events.Children.Any(e => e.Tag == "event_power"))
                {
                    continue;
                }

                foreach (ProjectElement action in program.Descendants().Where(e => e.Tag == "action"))
                {
                    if (topology.ByToken(action.GetAttribute("link1")) is { } target
                        && target.GetAttribute("inivalue") is { Length: > 0 } initial)
                    {
                        inspection.Report(target, Arguments(
                            ("variable", Name(target)), ("value", initial)));
                    }
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        private static bool IsBackedUp(ProjectElement variable) =>
            variable.GetAttribute(BackupAttribute) == BackupMarked;

        /// <summary>Every variable in a block's five containers — its own declarations, not a product's pins.</summary>
        private static IEnumerable<ProjectElement> BlockVariables(ProjectElement block) =>
            FunctionBlockSections.All
                .Select(section => block.FindChild(section.Container))
                .OfType<ProjectElement>()
                .SelectMany(container => container.Children);

        /// <summary>Every setting element of a product's settings groups — the commissioning surface.</summary>
        private static IEnumerable<ProjectElement> Settings(ProjectElement product) =>
            product.Descendants()
                .Where(e => e.Tag.EndsWith("_settings", StringComparison.Ordinal))
                .SelectMany(group => group.Children);
    }
}
