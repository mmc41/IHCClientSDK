using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Exercises the programmatic-lookup-only <see cref="DefinitionDocumentation"/> help metadata carried on a
    /// <see cref="FunctionBlockDefinition"/>: the block's overview text plus one text per resource, read back off the
    /// pin through <see cref="ResourceSummary.Documentation"/>
    /// (a shape modeled on, but not copied from, a vendor <c>FunctionBlocks\*.md</c> help file — the sample strings here
    /// are synthetic), and — the load-bearing guarantee — that attaching it
    /// leaves the serialized <see cref="FunctionBlockDefinition.Body"/> untouched, so it never reaches a project
    /// <c>.vis</c> or a function-block description <c>.ifb</c>. These run against a hand-built definition body (no
    /// builder, no catalog, no install dir), so they are real runnable tests independent of the Stage-1 builder stubs.
    /// </summary>
    public class FunctionBlockDocumentationTests
    {
        private static ProjectElement El(string tag, string name, params ProjectElement[] children) =>
            new ProjectElement(
                tag,
                null,
                ImmutableArray.Create<(string, string)>(("name", name)),
                children.Length == 0 ? ImmutableArray<ProjectElement>.Empty : ImmutableArray.Create(children));

        // The "Kip tænd sluk" toggle block shape from DefinitionProjectionTests — two inputs, one output, one timer —
        // so documentation lookup can be tied to the same names a GUI reads off Inputs/Outputs.
        private static ProjectElement ToggleBody() =>
            El("functionblock", "1.1.01.e. Kip tænd sluk",
                El("inputs", "inputs", El("resource_input", "Kip"), El("resource_input", "Sluk")),
                El("outputs", "outputs", El("resource_output", "Udgang")),
                El("settings", "settings", El("resource_timer", "Timer")),
                El("internalsettings", "internalsettings"),
                El("programs", "programs"));

        private static FunctionBlockDefinition ToggleDefinition(ProjectElement? body = null) =>
            new FunctionBlockDefinition(
                "1.1.01", "e", "Kip tænd sluk", "1.1.01.e. Kip tænd sluk", "00. Foretrukne", body ?? ToggleBody());

        // Synthetic, self-authored help text  — only its shape matches one. Keyed by POSITION (the first input, the
        // first output) through the same minter the projection reads with: a display name identifies no single pin.
        private static DefinitionDocumentation Documented(string summary) =>
            new DefinitionDocumentation(
                summary,
                ImmutableDictionary<string, string>.Empty
                    .Add(ResourceDocKey.ForBlock("inputs", 0),
                         "Opdigtet hjælpetekst: denne indgang skifter udgangens tilstand i eksemplet.")
                    .Add(ResourceDocKey.ForBlock("outputs", 0),
                         "Opdigtet hjælpetekst: eksemplets udgangssignal."));

        [Test]
        public void Empty_HasNoBlockTextAndNoResourceText()
        {
            DefinitionDocumentation empty = DefinitionDocumentation.Empty;

            Assert.Multiple(() =>
            {
                Assert.That(empty.IsEmpty, Is.True);
                Assert.That(empty.Summary, Is.Null);
                Assert.That(empty.Resources, Is.Empty);
            });
        }

        [Test]
        public void EachResourceGetsItsOwnText_AndAnUndocumentedOneGetsNone()
        {
            FunctionBlockDefinition def = ToggleDefinition() with { Documentation = Documented("Block help.") };

            Assert.Multiple(() =>
            {
                Assert.That(def.Documentation.IsEmpty, Is.False);
                Assert.That(def.Inputs[0].Documentation,
                    Is.EqualTo("Opdigtet hjælpetekst: denne indgang skifter udgangens tilstand i eksemplet."));
                Assert.That(def.Inputs[1].Documentation, Is.Null, "an undocumented resource has no text");
                Assert.That(def.Settings[0].Documentation, Is.Null, "nor does one in another container");
            });
        }

        [Test]
        public void Definition_DefaultsToEmptyDocumentation()
        {
            // Catalog discovery constructs the 6-arg definition and never sets documentation (an .ifb has no help
            // text), so the default must be the empty sentinel, not null.
            FunctionBlockDefinition def = ToggleDefinition();

            Assert.Multiple(() =>
            {
                Assert.That(def.Documentation, Is.SameAs(DefinitionDocumentation.Empty));
                Assert.That(def.Documentation.IsEmpty, Is.True);
            });
        }

        [Test]
        public void Definition_CarriesDocumentation_ReadOffTheProjection()
        {
            // The intended GUI flow: iterate the Inputs/Outputs projections and render each pin's own help text — the
            // caller handles neither a name key nor a placeholder id token.
            FunctionBlockDefinition def = ToggleDefinition() with { Documentation = Documented("Block help.") };

            Assert.Multiple(() =>
            {
                Assert.That(def.Documentation.Summary, Is.EqualTo("Block help."));
                Assert.That(def.Inputs.Select(i => i.Name), Does.Contain("Kip"));
                Assert.That(def.Inputs.Single(i => i.Name == "Kip").Documentation,
                    Is.EqualTo("Opdigtet hjælpetekst: denne indgang skifter udgangens tilstand i eksemplet."));
                Assert.That(def.Outputs[0].Documentation,
                    Is.EqualTo("Opdigtet hjælpetekst: eksemplets udgangssignal."));
            });
        }

        [Test]
        public void AttachingDocumentation_LeavesBodyUntouched_SoItIsNeverSerialized()
        {
            // The load-bearing guarantee. GroupRef.AddFunctionBlock (and any serializer) consume ONLY Body +
            // InlineDtdBlocks, so proving the help text lives entirely outside Body proves it can never be written into
            // a .vis or .ifb. Distinctive sentinels make an accidental leak into any attribute (e.g. a 'note') visible.
            FunctionBlockDefinition bare = ToggleDefinition();
            FunctionBlockDefinition documented = bare with
            {
                Documentation = new DefinitionDocumentation(
                    "BLOCK-HELP-SENTINEL",
                    ImmutableDictionary<string, string>.Empty
                        .Add(ResourceDocKey.ForBlock("inputs", 0), "KIP-HELP-SENTINEL")
                        .Add(ResourceDocKey.ForBlock("outputs", 0), "UDGANG-HELP-SENTINEL")),
            };

            var attributeValuesInBody =
                new[] { documented.Body }.Concat(documented.Body.Descendants())
                    .SelectMany(e => e.Attrs.Select(a => a.Value));

            Assert.Multiple(() =>
            {
                Assert.That(documented.Body, Is.SameAs(bare.Body),
                    "attaching documentation copies the same Body reference the serializer consumes");
                Assert.That(attributeValuesInBody, Has.None.Contains("HELP-SENTINEL"),
                    "no help text is smuggled into any Body attribute");
            });
        }

        [Test]
        public void Documentation_ParticipatesInDefinitionEquality()
        {
            // Same Body instance, so the only thing that can differ is the documentation — pinning that it is part of a
            // definition's identity (a caller keying definitions by value sees a documented and an undocumented block
            // as distinct), while an undocumented clone stays equal.
            ProjectElement body = ToggleBody();
            FunctionBlockDefinition bare = ToggleDefinition(body);
            FunctionBlockDefinition documented = bare with { Documentation = Documented("Block help.") };

            Assert.Multiple(() =>
            {
                Assert.That(bare with { }, Is.EqualTo(bare), "an undocumented clone stays equal");
                Assert.That(documented, Is.Not.EqualTo(bare), "documentation participates in value equality");
            });
        }

        // T023 (S3): the FB and product documentation records were identical and are now the ONE shared
        // DefinitionDocumentation — both definition families carry that single type, and one instance is assignable
        // to either (deliberately coupling the FB/Product namespaces under D01).
        [Test]
        public void BothDefinitionFamilies_ShareTheOneDocumentationType()
        {
            System.Type fbDocType = typeof(FunctionBlockDefinition).GetProperty(nameof(FunctionBlockDefinition.Documentation))!.PropertyType;
            System.Type productDocType = typeof(ProductDefinition).GetProperty(nameof(ProductDefinition.Documentation))!.PropertyType;
            var shared = new DefinitionDocumentation("shared", ImmutableDictionary<string, string>.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(fbDocType, Is.EqualTo(typeof(DefinitionDocumentation)));
                Assert.That(productDocType, Is.EqualTo(typeof(DefinitionDocumentation)));
                Assert.That((ToggleDefinition() with { Documentation = shared }).Documentation, Is.SameAs(shared),
                    "the one record assigns to a FunctionBlockDefinition");
            });
        }
    }
}
