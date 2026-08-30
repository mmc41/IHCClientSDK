using System.Collections.Immutable;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// How the schema-conformance rules — <c>attr-required</c>, <c>attr-enum-range</c>, <c>attr-undeclared</c>,
    /// <c>attr-latin1</c> and <c>element-undeclared</c> — are DECLARED: each in the catalogue with both halves of
    /// the language split, the English sentence surviving as the diagnostic where the finding used to carry it,
    /// and the save-blocking ones still holding their whole-project face.
    ///
    /// <para><b>What this fixture deliberately does not compare, and where that comparison lives.</b> What the
    /// engine actually REPORTS for these ids over the corpus — severity, locator, arguments, node path and run
    /// caveats, in production order — is pinned byte for byte by
    /// <c>ValidationCharacterizationTests.Corpus_ReproducesItsOracleByteForByte</c> against the committed per-case
    /// oracles, over the same cases and through the whole <c>ProjectAppService</c>. A second, weaker per-id
    /// comparison here would add nothing that check does not already refuse, and would be one more place for the
    /// two sides to disagree about a cell.</para>
    /// </summary>
    [TestFixture]
    public sealed class SchemaRuleParityTests
    {
        private static readonly ImmutableArray<string> MigratedIds =
            ["attr-required", "attr-enum-range", "attr-undeclared", "attr-latin1", "element-undeclared"];

        private static ProblemCatalog Catalog => ProblemCatalog.Current;

        [Test]
        public void EveryMigratedRuleIsDeclaredInTheCatalogueWithADanishLabelAndAnEnglishDiagnostic()
        {
            Assert.Multiple(() =>
            {
                foreach (string code in MigratedIds)
                {
                    Assert.That(Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry), Is.True, code);
                    Assert.That(entry.MessageTemplate, Is.Not.Empty, $"{code}: the user-facing Danish label");
                    Assert.That(entry.Diagnostic, Is.Not.Null.And.Not.Empty, $"{code}: the English engine sentence");
                    Assert.That(entry.Kind, Is.EqualTo(RuleKind.SchemaSerializationGuard), code);
                    Assert.That(entry.Category, Is.EqualTo(ValidationCategory.FileIntegrity), code);
                }
            });
        }

        /// <summary>
        /// The English sentence is not lost, only relocated. Each migrated entry's diagnostic still says what the
        /// recorded message said — same subject, same fault — so a developer reading a log learns what they used
        /// to learn from the finding itself.
        /// </summary>
        [Test]
        public void TheRecordedEnglishSentenceSurvivesAsTheDiagnostic()
        {
            (string Code, string Fragment)[] expected =
            [
                ("attr-required", "required attribute"),
                ("attr-enum-range", "is not one of"),
                ("attr-undeclared", "is not declared in the element"),
                ("attr-latin1", "non-ISO-8859-1"),
                ("element-undeclared", "is not declared in the project"),
            ];

            Assert.Multiple(() =>
            {
                foreach ((string code, string fragment) in expected)
                {
                    Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry);
                    Assert.That(entry.Diagnostic, Does.Contain(fragment), code);
                }
            });
        }

        /// <summary>
        /// Three of the five are ALSO save refusals in the catalogue's own terms, and the migration must not have
        /// quietly demoted them to findings alone. The refusal still lives at the serializer's throw site; what
        /// this pins is that the catalogue still says so.
        /// </summary>
        [Test]
        public void TheThreeSaveBlockingRulesKeepBothFaces()
        {
            Assert.Multiple(() =>
            {
                foreach (string code in new[] { "attr-undeclared", "attr-latin1", "element-undeclared" })
                {
                    Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry);
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Error),
                        $"{code}: it reports a finding at validate");
                    Assert.That(entry.Faces & RuleFaces.WholeProject, Is.Not.EqualTo(RuleFaces.None),
                        $"{code}: and the whole-project face is what reports it");
                }
            });
        }
    }
}
