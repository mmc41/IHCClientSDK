#nullable enable
using System;
using System.Collections.Generic;

using Ihc.Vis.Editing;
namespace Ihc.Vis.Schema
{
    /// <summary>
    /// The context-sensitive containment model behind <see cref="ProjectEditor.CanInsert"/>/
    /// <see cref="ProjectEditor.GetInsertableAt"/>: which child element types are legal directly under each
    /// structural parent. Authored from the spec (ch. 03 locality tree, ch. 04 products, §6.3.1 function-block
    /// section↔type matrix, §8.2 scene gating) and validated against the authentic <c>.vis</c> oracle corpus —
    /// every parent→child pair the vendor files contain is admitted, and the named illegal placements rejected.
    /// A parent the model does not cover is <b>permissive</b> (returns legal / no suggestions) so the query never
    /// blocks a legitimate insert it simply does not model yet.
    /// </summary>
    internal static class PlacementRules
    {
        private const string CatLocality = "Locality";
        private const string CatProduct = "Product";
        private const string CatFunctionBlock = "Function block";
        private const string CatPin = "Pin";
        private const string CatScene = "Scene";
        private const string CatVariable = "Variable";
        private const string CatProgram = "Program";

        // Value-variable resource types (spec §6.3.1) — accepted by any function-block container; the full set the
        // authentic oracles place across inputs/outputs/settings/internalsettings.
        private static readonly string[] ValueTypes =
        {
            "resource_flag", "resource_integer", "resource_floating_point", "resource_counter",
            "resource_date", "resource_time", "resource_timer", "resource_timertime", "resource_weekday",
            "resource_holiday", "resource_enum", "resource_light", "resource_light_level",
            "resource_temperature", "resource_humidity_level", "kW", "kWh", "W", "Wh",
        };

        private static readonly HashSet<string> ValueTypeSet = new(ValueTypes, StringComparer.Ordinal);

        // The four value/pin containers share their tag with product-level containers of the same name (e.g. a
        // product's own `settings` holds dataline_input config, not function-block variables). The §6.3.1 matrix
        // therefore applies only when the container sits inside a functionblock — the vendor .ihccmd rules key on
        // the grandparent code (PP) for exactly this reason. Elsewhere the container is unmodeled (permissive).
        private static bool InFunctionBlock(string? grandParentTag) => grandParentTag == "functionblock";

        /// <summary>
        /// Whether <paramref name="childTag"/> may be inserted directly under a <paramref name="parentTag"/> whose
        /// own parent is <paramref name="grandParentTag"/> (the context that disambiguates a function-block
        /// container from a like-named product container).
        /// </summary>
        public static bool CanInsert(string parentTag, string childTag, string? grandParentTag) => parentTag switch
        {
            "groups" => childTag == "group",
            // A locality holds products/devices (dataline, airlink, rs485 dimmer, s0 meter, …) and function
            // blocks — but never nests. The device families vary by install, so admit any product_* plus the
            // known non-product device roots; GetInsertableAt surfaces the primary families for the menu.
            "group" => childTag is "functionblock" or "s0_device"
                       || childTag.StartsWith("product_", StringComparison.Ordinal),
            "inputs" => !InFunctionBlock(grandParentTag) || childTag == "resource_input" || ValueTypeSet.Contains(childTag),
            "outputs" => !InFunctionBlock(grandParentTag) || childTag is "resource_output" or "resource_scene" || ValueTypeSet.Contains(childTag),
            "settings" or "internalsettings" => !InFunctionBlock(grandParentTag) || ValueTypeSet.Contains(childTag),
            "programs" => childTag == "program_simple",
            _ => true,   // unmodeled parent → permissive: never block an insert the model does not know about
        };

        /// <summary>The insert options offered under the parent in its context; empty for an unmodeled parent.</summary>
        public static IReadOnlyList<InsertOption> OptionsFor(string parentTag, string? grandParentTag)
        {
            var options = new List<InsertOption>();
            bool inFb = InFunctionBlock(grandParentTag);
            switch (parentTag)
            {
                case "groups":
                    options.Add(new InsertOption("group", CatLocality));
                    break;
                case "group":
                    options.Add(new InsertOption("product_dataline", CatProduct));
                    options.Add(new InsertOption("product_airlink", CatProduct));
                    options.Add(new InsertOption("functionblock", CatFunctionBlock));
                    break;
                case "inputs" when inFb:
                    options.Add(new InsertOption("resource_input", CatPin));
                    AddValueOptions(options);
                    break;
                case "outputs" when inFb:
                    options.Add(new InsertOption("resource_output", CatPin));
                    options.Add(new InsertOption("resource_scene", CatScene));
                    AddValueOptions(options);
                    break;
                case "settings" when inFb:
                case "internalsettings" when inFb:
                    AddValueOptions(options);
                    break;
                case "programs":
                    options.Add(new InsertOption("program_simple", CatProgram));
                    break;
            }
            return options;
        }

        private static void AddValueOptions(List<InsertOption> options)
        {
            foreach (string tag in ValueTypes)
            {
                options.Add(new InsertOption(tag, CatVariable));
            }
        }
    }
}
