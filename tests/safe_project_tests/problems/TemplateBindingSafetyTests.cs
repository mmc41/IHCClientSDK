using System;
using System.Collections.Generic;
using System.Linq;

using Ihc.Vis.Problems;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RF downgraded item 2: binding a template cannot rewrite an AUTHORED VALUE.
    ///
    /// <para>The binder applied each argument as a <c>StringBuilder.Replace</c> over the whole buffer, in
    /// sequence. So a value inserted by an early argument became part of the text the LATER arguments searched:
    /// a value containing the characters <c>{tag}</c> was substituted a second time, as though it had been a
    /// placeholder in the template. The data a user's file supplied was then rewritten by the engine's own
    /// binding.</para>
    ///
    /// <para>Reachable rather than theoretical: <see cref="ProblemArgumentType.AttributeValue"/> slots carry raw
    /// attribute text straight out of a <c>.vis</c> file, and the same rows declare a <c>{tag}</c> slot beside
    /// them. A hand-edited or foreign file is all it takes.</para>
    ///
    /// <para>This is now the SDK's ONE binder — the catalogue's message and diagnostic and every refusing site's
    /// own copy all go through it — so the property is asserted once here.</para>
    /// </summary>
    [TestFixture]
    public sealed class TemplateBindingSafetyTests
    {
        private static string Bind(string template, params (string Name, object Value)[] arguments) =>
            ProblemTemplate.Bind(template, arguments.Select(a => new ProblemArgument(a.Name, a.Value)));

        [Test]
        public void AValueContainingAPlaceholderTokenSurvivesBindingUnchanged()
        {
            string bound = Bind(
                "Ukendt attribut '{attribute}' på <{tag}>.",
                ("attribute", "{tag}"),
                ("tag", "group"));

            Assert.That(bound, Is.EqualTo("Ukendt attribut '{tag}' på <group>."),
                "the authored value is data, not template — a later argument may not reach into it");
        }

        /// <summary>
        /// The same defect with the arguments the other way round, so the fix cannot be an ordering trick: what
        /// matters is that substitution happens against the ORIGINAL template, never against inserted text.
        /// </summary>
        [Test]
        public void TheOrderOfArgumentsDoesNotChangeTheResult()
        {
            const string template = "Ugyldig værdi '{value}' i attributten '{attribute}'.";

            Assert.That(
                Bind(template, ("value", "{attribute}"), ("attribute", "locked")),
                Is.EqualTo(Bind(template, ("attribute", "locked"), ("value", "{attribute}"))),
                "binding is a substitution over the template, so it cannot depend on argument order");
        }

        [Test]
        public void OrdinaryBindingIsUnchanged()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Bind("Feltet '{field}' skal være mindst {minimum}.", ("field", "Pin"), ("minimum", 3)),
                    Is.EqualTo("Feltet 'Pin' skal være mindst 3."));
                Assert.That(Bind("Ingen pladsholdere her."), Is.EqualTo("Ingen pladsholdere her."));
                Assert.That(Bind(string.Empty), Is.Empty);
            });
        }

        /// <summary>
        /// An unsupplied slot stays as its own placeholder — the documented policy: "a visible gap is a defect a
        /// reader reports, where a silent blank reads as intended text".
        /// </summary>
        [Test]
        public void AnUnsuppliedSlotIsLeftAsItsPlaceholder()
        {
            Assert.That(Bind("Feltet '{field}' skal være mindst {minimum}.", ("field", "Pin")),
                Is.EqualTo("Feltet 'Pin' skal være mindst {minimum}."));
        }

        /// <summary>
        /// A repeated slot binds at every occurrence, and a repeated ARGUMENT keeps the first value — which is
        /// what the sequential replace did, so the behaviour is preserved rather than quietly changed.
        /// </summary>
        [Test]
        public void ARepeatedSlotBindsEverywhereAndARepeatedArgumentKeepsTheFirstValue()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Bind("{id} og {id}", ("id", "_0x1")), Is.EqualTo("_0x1 og _0x1"));
                Assert.That(Bind("{id}", ("id", "først"), ("id", "sidst")), Is.EqualTo("først"));
            });
        }

        /// <summary>Text that merely contains a brace is not a placeholder and is copied through.</summary>
        [Test]
        public void UnbalancedOrUndeclaredBracesAreCopiedThrough()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Bind("Et { uden slut", ("x", "y")), Is.EqualTo("Et { uden slut"));
                Assert.That(Bind("{ukendt} slot", ("x", "y")), Is.EqualTo("{ukendt} slot"));
            });
        }
    }
}
