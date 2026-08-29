using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Two DECISIONS about what the <c>doc-*</c> rows declare, held against the day someone changes them by
    /// accident.
    ///
    /// <para>Neither is a transcription check. The one-time map that held the pre-migration attribute literals
    /// against the post-migration declarations was deleted once the emission-consistency sweep stood in its
    /// place (T059) — a sweep that judges every emission is a stronger statement than a table of expected
    /// strings, and keeping both would have meant maintaining the weaker one for ever. What survives here is
    /// what that sweep does NOT say: an absence, and a null tag.</para>
    /// </summary>
    public sealed class DocumentationTargetDeclarationTests
    {
        /// <summary>
        /// <c>doc-not-linked</c> stays element-level, and that is a decision rather than an omission: it tests for
        /// the absence of link CHILDREN, so there is no attribute it could ever name.
        /// </summary>
        [Test]
        public void TheLinkRuleDeclaresNoAttributeBecauseItIsAboutChildrenNotAField()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("doc-not-linked"),
                out ProblemCatalogEntry entry), Is.True);
            Assert.That(entry.Target.Attribute, Is.Null);
        }

        /// <summary>
        /// The two terminal rows report on <c>dataline_input</c> AND <c>dataline_output</c>, so they declare the
        /// wildcard — a null tag with an attribute. Pinned because declaring a single tag would have looked
        /// correct and quietly excluded half their sites from the field face.
        /// </summary>
        [Test]
        public void TheTerminalRowsDeclareTheWildcardRatherThanOneOfTheirTwoTags()
        {
            Assert.Multiple(() =>
            {
                foreach (string code in new[] { "doc-cable-colour", "doc-address" })
                {
                    Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(code),
                        out ProblemCatalogEntry entry), Is.True);
                    Assert.That(entry.Target.Tag, Is.Null, code);
                    Assert.That(entry.Target.IsWholeProject, Is.False,
                        $"'{code}' names an attribute, so it is a wildcard and not a whole-project row");
                }
            });
        }
    }
}
