#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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
        // authentic oracles place across inputs/outputs/settings/internalsettings. The single SDK-authoritative
        // source is VariableTypeRegistry (ADR-002/D07): the engine admits value insertion by exactly this set and
        // the UI variable palette projects labels over the same registry, so the two can never drift apart.
        private static readonly HashSet<string> ValueTypeSet = new(VariableTypeRegistry.ValueTypeTags, StringComparer.Ordinal);

        // The four value/pin containers share their tag with product-level containers of the same name (e.g. a
        // product's own `settings` holds dataline_input config, not function-block variables). The §6.3.1 matrix
        // therefore applies only when the container sits inside a functionblock — the vendor .ihccmd rules key on
        // the grandparent code (PP) for exactly this reason. Elsewhere the container is unmodeled (permissive).
        private static bool InFunctionBlock(string? grandParentTag) => grandParentTag == "functionblock";

        /// <summary>
        /// A product/device family root placeable directly in a room: any <c>product_*</c> family plus the known
        /// non-product device roots (<c>s0_device</c>). The one classification the <c>group</c> containment rule,
        /// <c>GroupRef.Product</c> lookup and <c>ProjectEditor.GetFullPath</c> rendering all share.
        /// </summary>
        public static bool IsDeviceRoot(string tag) =>
            tag == "s0_device" || tag.StartsWith("product_", StringComparison.Ordinal);

        /// <summary>
        /// The one function-block container a pin/scene type is bound to (§6.3.1) — <c>inputs</c> for
        /// <c>resource_input</c>, <c>outputs</c> for <c>resource_output</c>/<c>resource_scene</c> — or <c>null</c>
        /// for a type legal in any container. The single encoding of the pin-binding fact, shared by
        /// <see cref="CanInsert"/>, the <c>FunctionBlockRef</c> mutation guards and the validator's
        /// pin-container check.
        /// </summary>
        public static string? PinContainerFor(string tag) => tag switch
        {
            "resource_input" => "inputs",
            "resource_output" or "resource_scene" => "outputs",
            _ => null,
        };

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
            "group" => childTag == "functionblock" || IsDeviceRoot(childTag),
            "inputs" or "outputs" => !InFunctionBlock(grandParentTag)
                       || PinContainerFor(childTag) == parentTag || ValueTypeSet.Contains(childTag),
            "settings" or "internalsettings" => !InFunctionBlock(grandParentTag) || ValueTypeSet.Contains(childTag),
            "programs" => childTag == "program_simple",
            _ => true,   // unmodeled parent → permissive: never block an insert the model does not know about
        };

        // The six option lists are compile-time constants of the containment model — the same objects every call,
        // authored order preserved. Built once as statics rather than re-allocated per call: OptionsFor sits on the
        // drag-over path (CanContain asks it per pointer move), where the per-call form allocated a List plus up to
        // 21 InsertOption records only to be read and dropped.
        private static readonly ImmutableArray<InsertOption> NoOptions = ImmutableArray<InsertOption>.Empty;

        private static readonly ImmutableArray<InsertOption> ValueOptions =
            VariableTypeRegistry.ValueTypeTags.Select(tag => new InsertOption(tag, CatVariable)).ToImmutableArray();

        private static readonly ImmutableArray<InsertOption> GroupsOptions =
            [new InsertOption("group", CatLocality)];

        private static readonly ImmutableArray<InsertOption> GroupOptions =
        [
            new InsertOption("product_dataline", CatProduct),
            new InsertOption("product_airlink", CatProduct),
            new InsertOption("functionblock", CatFunctionBlock),
        ];

        private static readonly ImmutableArray<InsertOption> InputsOptions =
            [new InsertOption("resource_input", CatPin), .. ValueOptions];

        private static readonly ImmutableArray<InsertOption> OutputsOptions =
            [new InsertOption("resource_output", CatPin), new InsertOption("resource_scene", CatScene), .. ValueOptions];

        private static readonly ImmutableArray<InsertOption> ProgramsOptions =
            [new InsertOption("program_simple", CatProgram)];

        /// <summary>The insert options offered under the parent in its context; empty for an unmodeled parent.</summary>
        public static IReadOnlyList<InsertOption> OptionsFor(string parentTag, string? grandParentTag)
        {
            bool inFb = InFunctionBlock(grandParentTag);
            return parentTag switch
            {
                "groups" => GroupsOptions,
                "group" => GroupOptions,
                "inputs" when inFb => InputsOptions,
                "outputs" when inFb => OutputsOptions,
                "settings" or "internalsettings" when inFb => ValueOptions,
                "programs" => ProgramsOptions,
                _ => NoOptions,
            };
        }
    }
}
