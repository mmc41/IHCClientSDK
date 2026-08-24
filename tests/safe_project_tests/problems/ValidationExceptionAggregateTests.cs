using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The validation exception carries its findings as an AGGREGATE, and specifically not as a chain.
    ///
    /// <para><b>Why the distinction is the whole test.</b> A chain is one failure restated more precisely, so a
    /// renderer walks it to the most specific level and shows that. An aggregate is N different failures about N
    /// different things. Apply the chain rule to an aggregate and the user is shown one finding while N−1 are
    /// discarded silently — the worst possible outcome for a dialog whose entire job is "here is everything
    /// wrong with your project". The types are separate so that mistake cannot be written, and the assertions
    /// below are what keep the exception on the right side of the line.</para>
    ///
    /// <para><b>Warnings are not items.</b> An aggregate explains why an operation STOPPED, and warnings never
    /// stop one. Listing them would hand a reader repairs that change nothing about the refusal.</para>
    /// </summary>
    [TestFixture]
    public sealed class ValidationExceptionAggregateTests
    {
        private static ProjectValidationResult ResultWith(params (ValidationSeverity Severity, string Rule, string Message)[] findings) =>
            ProjectValidationResult.FromFindings(
                [.. findings.Select(f => new ProjectValidationFinding(f.Severity, f.Rule, "_0x2a", f.Message))]);

        [Test]
        public void TheAggregateRendersWholeWithOneItemPerError()
        {
            ProjectValidationException thrown = new(new ProblemCode("io.save"), ResultWith(
                (ValidationSeverity.Error, "id-duplicate-token", "Dobbelt id"),
                (ValidationSeverity.Error, "attr-required", "Mangler påkrævet attribut"),
                (ValidationSeverity.Error, "attr-latin1", "Tegn kan ikke gemmes")));

            Assert.Multiple(() =>
            {
                Assert.That(thrown.Problems.Items, Has.Length.EqualTo(3), "every error is an item; none is elided");
                Assert.That(thrown.Problems.Items.Select(p => p.Code.Value), Is.EqualTo(new[]
                {
                    "id-duplicate-token", "attr-required", "attr-latin1",
                }).AsCollection, "and each keeps its OWN catalogue id");
                Assert.That(thrown.Problems.Head.Code, Is.EqualTo(new ProblemCode("io.save")));
                Assert.That(thrown.Operation.Value, Is.EqualTo("io.save"));
            });
        }

        /// <summary>
        /// The head names the operation and counts the items; it does NOT carry one of their ids. A singular rule
        /// id on an aggregate head is the shape that invites "just show the head", which is the discard.
        /// </summary>
        [Test]
        public void TheHeadNamesTheOperationAndNeverASingleFindingsId()
        {
            ProjectValidationException thrown = new(new ProblemCode("io.save"), ResultWith(
                (ValidationSeverity.Error, "id-duplicate-token", "Dobbelt id"),
                (ValidationSeverity.Error, "attr-required", "Mangler påkrævet attribut")));

            Assert.Multiple(() =>
            {
                Assert.That(thrown.Problems.Head.Code.Family, Is.EqualTo(ProblemFamily.Io));
                Assert.That(thrown.Problems.Items.Select(i => i.Code), Does.Not.Contain(thrown.Problems.Head.Code));
                Assert.That(thrown.Problems.Head.Arguments.Single().Value, Is.EqualTo(2),
                    "the count travels as a declared datum, so the Danish head can say how many without "
                    + "assembling a sentence");
            });
        }

        /// <summary>
        /// TRAVERSAL DOES NOT APPLY. There is no most-specific member to reach for, because the type has none —
        /// which is what makes the discard unwritable rather than merely discouraged.
        /// </summary>
        [Test]
        public void TraversalDoesNotApplyToTheAggregate()
        {
            string[] members = [.. typeof(ProblemAggregate)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name)];

            Assert.Multiple(() =>
            {
                foreach (string reducer in new[] { "MostSpecific", "Innermost", "Cause", "Flatten", "Single" })
                {
                    Assert.That(members, Does.Not.Contain(reducer), reducer);
                }

                Assert.That(typeof(ProjectValidationException)
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(p => p.PropertyType),
                    Has.None.EqualTo(typeof(ProblemChain)),
                    "the exception carries an aggregate and never a chain");
            });
        }

        [Test]
        public void WarningsAreNotItemsBecauseTheyStopNothing()
        {
            ProjectValidationException thrown = new(new ProblemCode("io.save"), ResultWith(
                (ValidationSeverity.Error, "attr-required", "Mangler påkrævet attribut"),
                (ValidationSeverity.Warning, "root-children", "Uventet rækkefølge i roden")));

            Assert.Multiple(() =>
            {
                Assert.That(thrown.Problems.Items.Select(i => i.Code.Value),
                    Is.EqualTo(new[] { "attr-required" }).AsCollection);
                Assert.That(thrown.Result.Findings, Has.Length.EqualTo(2),
                    "the full result still carries both — the aggregate is beside it, not instead of it");
            });
        }

        /// <summary>
        /// Both operation-level codes are governed entries, and their Danish heads come from the catalogue rather
        /// than from the throw site. A code minted with nothing behind it is the defect the catalogue exists for.
        /// </summary>
        [Test]
        public void BothOperationCodesAreGovernedAndSupplyTheirOwnDanishHead()
        {
            Assert.Multiple(() =>
            {
                foreach (string code in new[] { "io.save", "import.definition-invalid" })
                {
                    Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry),
                        Is.True, code);
                    Assert.That(entry.Section, Is.EqualTo(ProblemCatalogSection.OperationOutcomes), code);
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Refusal), code);
                    Assert.That(entry.MessageTemplate, Is.Not.Empty, code);
                }

                ProjectValidationException thrown = new(new ProblemCode("io.save"),
                    ResultWith((ValidationSeverity.Error, "attr-required", "Mangler påkrævet attribut")));
                // The row's template BOUND with the head's own count — the head is the row, not a prefix of it.
                Assert.That(thrown.Problems.Head.Message,
                    Is.EqualTo("Projektet kunne ikke gemmes: 1 fejl skal rettes først."),
                    "the head's Danish comes from the catalogue entry, not from the throwing code");
            });
        }
    }
}
