using System.Linq;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.NUnit;
using Ihc.Vis.Catalog;
using Ihc.Vis.Io;
using Ihc.Vis.Products;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ihc.Tests
{
    /// <summary>
    /// The byte-fidelity guarantee, enforced structurally rather than promised in a comment.
    ///
    /// <para>The dialog-metadata layer describes a DIALOG, not a project. It rides on
    /// <see cref="ProductDefinition.Dialog"/> the way <c>Documentation</c> does, and nothing that produces bytes may
    /// read it. That is what turns "adding a preset cannot change a saved file" into a fact about the code rather
    /// than a claim about intentions — and it is worth pinning mechanically, because both writers legitimately
    /// depend on <see cref="ProductDefinition"/> itself, so the isolation is one field access away from being lost
    /// by an ordinary, well-meaning edit.</para>
    ///
    /// <para>A namespace rule cannot express this: the dialog types live in <c>Ihc.Vis.Products</c> alongside
    /// <see cref="ProductDefinition"/>, which <see cref="CatalogFileWriter"/> must depend on. So the rule names the
    /// dialog TYPES, anchored with <c>typeof</c> so a rename breaks this file's compile rather than silently
    /// emptying the forbidden set.</para>
    /// </summary>
    [TestFixture]
    public class DialogMetadataIsolationArchitectureTests
    {
        private static readonly Architecture Sdk = ArchitectureModels.Sdk;

        // This test assembly as a second small model, so the seeded violator below is scannable — the same device
        // OpenVisualArchitectureTests uses for its own positive controls.
        private static readonly System.Lazy<Architecture> OwnTestAssembly = ArchitectureModels.ArchitectureTests;

        // Anchored by typeof, never by string: an emptied forbidden set is the classic way a rule like this stays
        // green while checking nothing, and TheRuleHasSomethingToCheck turns that into a failure.
        private static readonly System.Type[] DialogMetadataTypes =
        [
            typeof(ProductDialogModel),
            typeof(DialogGroupModel),
            typeof(DialogPartModel),
            typeof(DialogFieldModel),
            typeof(DialogRepeatModel),
            typeof(DialogWidgetModel),
            typeof(DialogBinding),
            typeof(DialogValueRule),
            typeof(DialogControlKind),
            typeof(DialogWidgetKind),
            typeof(ProductDialogPresets),
        ];

        // includeReferenced: in the test-assembly model the dialog types are referenced rather than owned, and a
        // target set that cannot see them would make the positive control unable to fail.
        private static IObjectProvider<IType> DialogMetadata() =>
            Types(includeReferenced: true).That().Are(DialogMetadataTypes).As("the dialog-metadata types");

        /// <summary>The rule under test, as a reusable shape, so the positive control exercises the SAME mechanism
        /// rather than a hand-rolled lookalike that could pass for different reasons.</summary>
        private static IArchRule NoDialogMetadataDependency(params System.Type[] writers) =>
            Types().That().Are(writers).Should().NotDependOnAny(DialogMetadata());

        /// <summary>
        /// Guards the guard: were the forbidden set or either subject to match nothing, every rule below would pass
        /// without checking anything.
        /// </summary>
        [Test]
        public void TheRuleHasSomethingToCheck()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DialogMetadata().GetObjects(Sdk).Count(), Is.EqualTo(DialogMetadataTypes.Length),
                    "every named dialog type must resolve in the SDK model, or the forbidden set is incomplete");
                Assert.That(Types().That().Are(typeof(CatalogFileWriter)).GetObjects(Sdk).Any(), Is.True);
                Assert.That(Types().That().Are(typeof(ProjectSerializer)).GetObjects(Sdk).Any(), Is.True);
            });
        }

        /// <summary>
        /// The catalog writer emits <c>.def</c> bytes: a <c>.def</c>'s content is fixed by its catalog grammar and
        /// body, and a dialog preset must not be able to influence it.
        /// </summary>
        [Test]
        public void CatalogFileWriter_DependsOnNoDialogMetadata() =>
            NoDialogMetadataDependency(typeof(CatalogFileWriter)).Check(Sdk);

        /// <summary>
        /// The project serializer emits <c>.vis</c> bytes — the subject of the whole byte-fidelity oracle corpus.
        /// Nothing about how a dialog is described may reach them.
        /// </summary>
        [Test]
        public void ProjectSerializer_DependsOnNoDialogMetadata() =>
            NoDialogMetadataDependency(typeof(ProjectSerializer)).Check(Sdk);

        /// <summary>
        /// The positive control. The SAME rule, pointed at a type that DOES touch the dialog layer, must report a
        /// violation — otherwise the two green rules above prove only that the mechanism is silent.
        /// </summary>
        [Test]
        public void TheRuleIsArmed_ASeededWriterThatTouchesTheDialogLayerIsCaught()
        {
            IArchRule rule = NoDialogMetadataDependency(typeof(SeededDialogAwareWriter));

            Assert.That(rule.HasNoViolations(OwnTestAssembly.Value), Is.False,
                "the seeded violator must be reported — if it is not, the rules above are passing vacuously");
        }

        /// <summary>And the control is not silent for a trivial reason: the seeded type must be in its model.</summary>
        [Test]
        public void TheSeededViolatorIsInTheModel()
            => Assert.That(Types().That().Are(typeof(SeededDialogAwareWriter)).GetObjects(OwnTestAssembly.Value).Any(),
                Is.True);

        // The synthetic violator: the two shapes a real regression would take — holding a dialog model, and calling
        // into the preset lookup. Deliberately a WRITER-shaped class (it "serializes"), because that is the kind of
        // type the rule protects.
        private static class SeededDialogAwareWriter
        {
            private static readonly ProductDialogModel Cached = ProductDialogPresets.Dataline;

            public static string Write(ProductDefinition definition) =>
                definition.Dialog.Groups.Length + "/" + Cached.Groups.Length
                + ProductDialogPresets.ForRootTag(definition.Body.Tag).IsEmpty;
        }
    }
}
