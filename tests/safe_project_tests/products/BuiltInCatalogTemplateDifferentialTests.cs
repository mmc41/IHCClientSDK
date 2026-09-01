using System.IO;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Reference-catalog differential (plan Phase C, C1–C3): the three hand-authored <see cref="BuiltInCatalog"/>
    /// File→New templates must be <b>structurally identical</b> to what
    /// <see cref="CatalogDiscovery.FromInstallDir"/> loads from the reference catalog's <c>NewDoc.idf</c>,
    /// <c>EnumeratorDefinitions.def</c> and <c>fb.def</c> (i.e. their POST-parse, DTD-defaulted shape). The reference
    /// directory comes only from <see cref="IhcSettings.IhcVisualInstallDir"/>; the tests skip when it is unset.
    /// </summary>
    /// <remarks>
    /// The empty-FB template's <c>InlineDtdBlocks</c> are deliberately excluded from the comparison: the
    /// code-authored template leaves them empty because every <c>fb.def</c> tag is registry-declared and never
    /// merged (see <see cref="BuiltInCatalog"/>.Templates remarks). Body + identity are the functional surface.
    /// </remarks>
    public class BuiltInCatalogTemplateDifferentialTests
    {
        private static ICatalog Reference() =>
            ReferenceCatalog.OpenOrIgnore("template differential");

        [Test]
        public void NewProjectSkeleton_MatchesReferenceCatalog()
        {
            ICatalog reference = Reference();
            ICatalog built = new BuiltInCatalog();
            AssertStructural(reference.NewProjectSkeleton, built.NewProjectSkeleton);
        }

        [Test]
        public void BuiltInEnumerators_MatchReferenceCatalog()
        {
            ICatalog reference = Reference();
            ICatalog built = new BuiltInCatalog();
            AssertStructural(reference.BuiltInEnumerators, built.BuiltInEnumerators);
        }

        [Test]
        public void EmptyFunctionBlockTemplate_BodyAndIdentityMatchReferenceCatalog()
        {
            ICatalog reference = Reference();
            ICatalog built = new BuiltInCatalog();
            var expected = reference.EmptyFunctionBlockTemplate;
            var actual = built.EmptyFunctionBlockTemplate;
            Assert.Multiple(() =>
            {
                AssertStructural(expected.Body, actual.Body);
                Assert.That(actual.IsEmptyTemplate, Is.True, "template flag");
                Assert.That(actual.MasterName, Is.EqualTo(expected.MasterName), nameof(actual.MasterName));
                Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName), nameof(actual.DisplayName));
                Assert.That(actual.MasterType, Is.EqualTo(expected.MasterType), nameof(actual.MasterType));
                Assert.That(actual.MasterVersion, Is.EqualTo(expected.MasterVersion), nameof(actual.MasterVersion));
                Assert.That(actual.CategoryPath, Is.EqualTo(expected.CategoryPath), nameof(actual.CategoryPath));
            });
        }

        private static void AssertStructural(ProjectElement expected, ProjectElement actual) =>
            ReferenceCatalog.AssertStructural(
                "Structural mismatch between the code-authored template and the reference-catalog file.", expected, actual);
    }
}
