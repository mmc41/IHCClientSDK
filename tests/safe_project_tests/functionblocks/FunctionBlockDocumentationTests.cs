using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Exercises the programmatic-lookup-only <see cref="DefinitionDocumentation"/> help metadata carried on a
    /// <see cref="FunctionBlockDefinition"/>: the block's overview text plus a per-resource text keyed by display name
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

        // Synthetic, self-authored help text  — only its shape matches one.
        private static DefinitionDocumentation Documented(string summary) =>
            new DefinitionDocumentation(
                summary,
                ImmutableDictionary<string, string>.Empty
                    .Add("Kip", "Opdigtet hjælpetekst: denne indgang skifter udgangens tilstand i eksemplet.")
                    .Add("Udgang", "Opdigtet hjælpetekst: eksemplets udgangssignal."));

        [Test]
        public void Empty_HasNoBlockTextAndNoResourceText()
        {
            DefinitionDocumentation empty = DefinitionDocumentation.Empty;

            Assert.Multiple(() =>
            {
                Assert.That(empty.IsEmpty, Is.True);
                Assert.That(empty.Summary, Is.Null);
                Assert.That(empty.ForResource("Kip"), Is.Null);
            });
        }

        [Test]
        public void ForResource_ReturnsText_ForDocumentedName_AndNull_ForUndocumented()
        {
            DefinitionDocumentation doc = Documented("Block help.");

            Assert.Multiple(() =>
            {
                Assert.That(doc.IsEmpty, Is.False);
                Assert.That(doc.ForResource("Kip"),
                    Is.EqualTo("Opdigtet hjælpetekst: denne indgang skifter udgangens tilstand i eksemplet."));
                Assert.That(doc.ForResource("Sluk"), Is.Null, "an undocumented resource has no text");
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
        public void Definition_CarriesDocumentation_LookedUpByProjectionName()
        {
            // The intended GUI flow: iterate the Inputs/Outputs projections, then look the help text up by the same
            // display name — no placeholder id tokens on the caller.
            FunctionBlockDefinition def = ToggleDefinition() with { Documentation = Documented("Block help.") };

            Assert.Multiple(() =>
            {
                Assert.That(def.Documentation.Summary, Is.EqualTo("Block help."));
                Assert.That(def.Inputs.Select(i => i.Name), Does.Contain("Kip"));
                Assert.That(def.Documentation.ForResource("Kip"),
                    Is.EqualTo("Opdigtet hjælpetekst: denne indgang skifter udgangens tilstand i eksemplet."));
                Assert.That(def.Documentation.ForResource(def.Outputs[0].Name),
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
                        .Add("Kip", "KIP-HELP-SENTINEL")
                        .Add("Udgang", "UDGANG-HELP-SENTINEL")),
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
