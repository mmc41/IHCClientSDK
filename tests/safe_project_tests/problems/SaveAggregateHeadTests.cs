using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

using Ihc.Vis.Problems;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RF-13: the head of the save-validation aggregate is BUILT from its catalogue row, count and all.
    ///
    /// <para>The head already carried a <c>count</c> argument and already read the row's template — but it took
    /// the template RAW and never bound it, so the row declared no slot, the argument rendered nowhere, and an
    /// installer whose project had seven errors read "Projektet kunne ikke gemmes" with no number in it. The
    /// count was computed, attached, and thrown away one line later.</para>
    ///
    /// <para>The head's code was also a <c>new ProblemCode("io.save")</c> LITERAL at the throwing site, which is
    /// the one thing the operation-head registry exists to prevent: two spellings of one operation with nothing
    /// keeping them equal.</para>
    /// </summary>
    [TestFixture]
    public sealed class SaveAggregateHeadTests
    {
        private static ProjectValidationResult ResultWith(int errors) =>
            ProjectValidationResult.FromFindings(
            [
                .. Enumerable.Range(0, errors).Select(i => new ProjectValidationFinding(
                    ValidationSeverity.Error, "attr-required", $"_0x{i:x}", "Mangler påkrævet attribut")),
            ]);

        [Test]
        public void TheHeadRendersTheErrorCountItWasGiven()
        {
            ProjectValidationException thrown = new(OperationCodes.Save, ResultWith(7));

            Assert.Multiple(() =>
            {
                Assert.That(thrown.Problems.Head.Message, Does.Contain("7"),
                    "the count reaches the sentence an installer reads");
                Assert.That(thrown.Problems.Head.Message, Does.Not.Contain("{"),
                    "and every declared slot of the row binds");
                Assert.That(thrown.Problems.Items, Has.Length.EqualTo(7), "the items are still the errors");
            });
        }

        /// <summary>
        /// The head IS the row: same code, same words. Asserted against the catalogue rather than against a
        /// literal, so a wording change to the row moves the head with it.
        /// </summary>
        [Test]
        public void TheHeadIsItsCatalogueRowBound()
        {
            ProjectValidationException thrown = new(OperationCodes.Save, ResultWith(3));

            Assert.That(ProblemCatalog.Current.TryGet(OperationCodes.Save, out ProblemCatalogEntry entry), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(thrown.Problems.Head.Code, Is.EqualTo(OperationCodes.Save));
                Assert.That(thrown.Problems.Head.Message, Is.EqualTo(entry.BindTemplate(thrown.Problems.Head)),
                    "the head's Danish is the row's template bound with the head's own arguments");
                Assert.That(entry.Slots.Select(s => s.Name), Does.Contain("count"),
                    "and the row DECLARES the slot, so the argument is not carried in vain");
            });
        }

        /// <summary>
        /// No site outside the catalogue spells the operation as a literal. The registry exists so one operation
        /// has one spelling; a literal beside it is a second one nothing keeps in step.
        /// </summary>
        [Test]
        public void NoSiteOutsideTheCatalogueSpellsTheOperationAsALiteral()
        {
            string root = TestRepository.RequireRoot();
            string[] scanned =
            [
                "ihcclient/src/app/services/ProjectAppService.cs",
                "ihcclient/src/vis/validation/ProjectValidationException.cs",
            ];

            string[] literals =
            [
                .. scanned.Where(relative =>
                    File.ReadAllText(
                        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8)
                        .Contains("new ProblemCode(\"io.save\")", StringComparison.Ordinal))
            ];

            Assert.That(literals, Is.Empty,
                "these sites spell io.save as a literal instead of naming OperationCodes.Save: "
                + string.Join(", ", literals));
        }
    }
}
