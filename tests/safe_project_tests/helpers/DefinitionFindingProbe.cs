using System;
using System.Collections.Immutable;

using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Every finding a `.def`/`.ifb` raiser can produce, PROVOKED — each code raised by driving a builder or the
    /// grammar advisor into the state that raises it.
    ///
    /// <para><b>Why provoking beats scanning.</b> These findings never reach the project corpus: they are about
    /// catalog definition files, which the corpus does not validate, so the recording that governs every project
    /// rule cannot cover them. The alternative that grew up in their absence was a regular expression over the
    /// SDK's source text, and it has two holes a scan cannot close — it passes on a raiser that spells everything
    /// correctly in a branch nothing reaches, and it is blind to any raiser that does not literally match the
    /// pattern. <c>CatalogGrammarAdvisor</c> is the second case in the flesh: its six codes funnel through one
    /// shared helper, so the scan sees a single raise and extending the advisor buys no cover at all.</para>
    ///
    /// <para><b>Shared, not duplicated.</b> Two gates read these findings — one holds each Danish sentence equal
    /// to its entry's template, the other holds each raised severity equal to its entry's disposition. They are
    /// different questions about the same population, and a second copy of these provocations would be a second
    /// thing to keep current.</para>
    /// </summary>
    internal static class DefinitionFindingProbe
    {
        /// <summary>
        /// The provoked findings, each with the raiser that produced it so a failure names the culprit.
        /// Provoked ONCE: the gates that read this ask different questions of one population, and a builder
        /// driven into the same state twice can only answer the same way.
        /// </summary>
        public static ImmutableArray<(string Raiser, ProjectValidationFinding Finding)> Provoked() =>
            LazyProvoked.Value;

        private static readonly Lazy<ImmutableArray<(string Raiser, ProjectValidationFinding Finding)>>
            LazyProvoked = new(Provoke);

        private static ImmutableArray<(string Raiser, ProjectValidationFinding Finding)> Provoke()
        {
            var found = ImmutableArray.CreateBuilder<(string, ProjectValidationFinding)>();

            void Collect(string raiser, ProjectValidationResult result)
            {
                foreach (ProjectValidationFinding finding in result.Findings)
                {
                    found.Add((raiser, finding));
                }
            }

            // identity-missing, product side: no identifier, no display name, no known family root tag.
            Collect(nameof(ProductDefinitionBuilder),
                ProductDefinitionBuilder.Dataline(string.Empty, string.Empty).Validate());

            // scenes-without-output: AddScenes with no preceding resource to bind to.
            Collect(nameof(ProductDefinitionBuilder), Dataline().AddScenes("Scener").Validate());

            // resource-enum-unwired: a resource_enum with no typedef wired to an enum_definition.
            Collect(nameof(ProductDefinitionBuilder), Dataline()
                .RawChild(new ProjectElement("resource_enum", new ElementId(0x90, 0x08),
                    ImmutableArray.Create(("id", "_0x9008"), ("name", "E")),
                    ImmutableArray<ProjectElement>.Empty))
                .Validate());

            // block-identity-missing: a block that is not an empty template and carries no master name. Its own
            // code since the split — this provocation once raised `identity-missing`, the PRODUCT row above.
            Collect(nameof(FunctionBlockDefinitionBuilder),
                FunctionBlockDefinitionBuilder.Create("1.1.01", "e", string.Empty).Validate());

            // program-empty: a program opened and left carrying no events.
            FunctionBlockDefinitionBuilder block = FunctionBlockDefinitionBuilder.Create("1.1.01", "e", ProbeName);
            block.Program("Tom");
            Collect(nameof(FunctionBlockDefinitionBuilder), block.Validate());

            // The six grammar advisories, each through a product body the effective grammar rejects.
            foreach ((_, Func<ProductDefinitionBuilder> body) in GrammarAdvisories)
            {
                Collect(nameof(CatalogGrammarAdvisor), body().Validate());
            }

            return found.ToImmutable();
        }

        /// <summary>
        /// The six product bodies the effective grammar objects to, one per advisory code — the provocations
        /// themselves, held apart from what is asked of them.
        ///
        /// <para>Read from here by <c>BuilderGrammarSurfaceTests</c> as well, which asks whether each one
        /// warns without blocking Build or Write, where this class asks what sentence and severity it raises
        /// with. The bodies are grammar-sensitive to the byte — a preset that stopped objecting to one would
        /// leave its test passing while testing nothing — so a second copy would go quietly vacuous on one
        /// side while the other stayed honest.</para>
        /// </summary>
        public static ImmutableArray<(string Code, Func<ProductDefinitionBuilder> Body)> GrammarAdvisories
        { get; } =
        [
            ("grammar-undeclared-type", () => Dataline().RawChild(
                new ProjectElement("resource_mystery", new ElementId(0x90, 0x06),
                    ImmutableArray.Create(("id", "_0x9006"), ("name", "?")),
                    ImmutableArray<ProjectElement>.Empty))),

            ("grammar-undeclared-attribute",
                () => Dataline().AddInput("Tryk", i => i.Attribute("mystery_attr", "x"))),

            // The airlink relay declares address_channel #REQUIRED; splice one without it.
            ("grammar-missing-required", () => ProductDefinitionBuilder
                .Airlink("_0x9fe3", ProbeName)
                .Attribute("device_type", "_0x0804")
                .RawChild(new ProjectElement("airlink_relay", new ElementId(0x90, 0x07),
                    ImmutableArray.Create(("id", "_0x9007"), ("name", "Relay")),
                    ImmutableArray<ProjectElement>.Empty))),

            // The authentic S0 kWh vendor bug: accessibility="readwrite" is outside
            // (read | write | read-write).
            ("grammar-enum-value", () => ProductDefinitionBuilder
                .S0Device("_0x9fe4", ProbeName)
                .AddResource("kWh", "Energi", r => r.Attribute("accessibility", "readwrite"))),

            ("grammar-duplicate-id", () => Dataline()
                .RawChild(new ProjectElement("dataline_input", new ElementId(0x9, 0x11),
                    ImmutableArray.Create(("id", "_0x911"), ("name", "A")),
                    ImmutableArray<ProjectElement>.Empty))
                .RawChild(new ProjectElement("dataline_input", new ElementId(0x9, 0x11),
                    ImmutableArray.Create(("id", "_0x911"), ("name", "B")),
                    ImmutableArray<ProjectElement>.Empty))),

            ("grammar-dangling-idref", () => Dataline()
                .AddOutput("Udgang")
                .RawChild(new ProjectElement("scenes", new ElementId(0x9, 0x27),
                    ImmutableArray.Create(("id", "_0x927"), ("name", "S"), ("scene_resource", "_0xdead")),
                    ImmutableArray<ProjectElement>.Empty))),
        ];

        /// <summary>The display name every provoked definition carries, so a dump names its origin.</summary>
        private const string ProbeName = "Drift probe";

        private static ProductDefinitionBuilder Dataline() =>
            ProductDefinitionBuilder.Dataline("_0x9fe2", ProbeName);
    }
}
