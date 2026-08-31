using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// THE DURABLE SWEEP behind every declared target: whenever a rule whose entry names an attribute actually
    /// emits, the element it reported must be one whose tag DECLARES that attribute.
    ///
    /// <para>Without it a declaration is unfalsifiable. Registration checks that the attribute exists somewhere in
    /// the schema, and nothing has ever checked that the rule reports it where it exists — so a row could declare
    /// <c>cabletype</c> and report a terminal, and every consumer that trusts the declaration to reach a FIELD
    /// would be pointed at an element that has no such field.</para>
    ///
    /// <para><b>A row nothing emitted is reported as NOT CHECKED, never as passed.</b> The corpus does not witness
    /// every rule — the capacity rows need controller limits the default profile does not supply, so they cannot
    /// fire here at all — and counting an unreachable row as a pass is how a sweep comes to certify rules it never
    /// ran. The two populations are printed separately for that reason.</para>
    /// </summary>
    [TestFixture]
    public sealed class EmissionConsistencyTests
    {
        /// <summary>One emitted finding, reduced to what this sweep judges.</summary>
        private sealed record Emitted(string Case, string Code, string Tag, string Locator);

        /// <summary>
        /// The sweep itself, as a PURE function over (emission, declared attribute) so the armed test below can
        /// run it against a declaration that is deliberately wrong. A checker that could only be invoked with the
        /// real catalogue could not be shown to have teeth.
        /// </summary>
        /// <param name="emitted">What the corpus produced.</param>
        /// <param name="declaredAttribute">The attribute a code's entry declares, or null for none.</param>
        /// <param name="declares">Whether a tag declares an attribute.</param>
        private static IReadOnlyList<string> Violations(
            IEnumerable<Emitted> emitted,
            Func<string, string?> declaredAttribute,
            Func<string, string, bool> declares)
        {
            List<string> violations = [];
            foreach (Emitted finding in emitted)
            {
                if (declaredAttribute(finding.Code) is not { } attribute)
                {
                    continue;   // element-level by declaration: there is nothing to be inconsistent with
                }
                if (!declares(finding.Tag, attribute))
                {
                    violations.Add(
                        $"{finding.Code} declares '{attribute}' but reported <{finding.Tag}> "
                        + $"({finding.Locator}) in {finding.Case}, which does not declare it");
                }
            }
            return violations;
        }

        /// <summary>Everything the characterization corpus emits, with each finding's reported tag resolved.</summary>
        private static (List<Emitted> Emitted, Func<string, string, bool> Declares) RunCorpus()
        {
            List<Emitted> emitted = [];
            Dictionary<(string Tag, string Attribute), bool> declares = [];

            foreach ((string caseName, Func<Project> build) in ValidationCharacterizationTests.Corpus)
            {
                Project project = build();
                var app = new ProjectAppService(TestSetup.Settings);
                foreach (ValidationFinding finding in app.ValidateStructured(project).Findings)
                {
                    // A finding that names no element cannot contradict a tag — a whole-project row, or one whose
                    // locator did not parse. It is not a violation and not a witness either.
                    if (finding.Primary?.Element is not { } id || project.FindById(id) is not { } element)
                    {
                        continue;
                    }
                    emitted.Add(new Emitted(
                        caseName, finding.Code.Value, element.Tag, finding.Primary.Locator ?? string.Empty));

                    // Resolved through the PROJECT's own schema view — its captured inline DTD first, registry
                    // fallback — so a file that declares its own grammar is judged by that grammar.
                    (string, string) key = (element.Tag, finding.TargetAttribute ?? string.Empty);
                    declares.TryAdd(key,
                        project.SchemaView.TryGet(element.Tag)?.FindAttr(key.Item2) is not null);
                }
            }

            return (emitted, (tag, attribute) =>
                declares.TryGetValue((tag, attribute), out bool known)
                    ? known
                    : ProjectSchemaRegistry.TryGet(tag)?.FindAttr(attribute) is not null);
        }

        private static string? DeclaredAttribute(string code) =>
            ProblemCatalog.Current.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry)
                ? entry.Target.Attribute
                : null;

        [Test]
        public void EveryEmittedFindingReportsAnElementThatDeclaresItsEntrysAttribute()
        {
            (List<Emitted> emitted, Func<string, string, bool> declares) = RunCorpus();

            IReadOnlyList<string> violations = Violations(emitted, DeclaredAttribute, declares);

            // WHAT WAS ACTUALLY CHECKED, printed rather than implied. A green sweep over an empty witness set is
            // the failure mode this reporting exists to make visible.
            var witnessed = emitted
                .Where(e => DeclaredAttribute(e.Code) is not null)
                .Select(e => e.Code)
                .ToHashSet(StringComparer.Ordinal);
            var declaring = ProblemCatalog.Current.Entries
                .Where(e => e.Section == ProblemCatalogSection.ProjectFindings
                    && e.Status == ProblemCodeStatus.Active
                    && e.Target.Attribute is not null)
                .ToList();
            var unreachable = declaring
                .Where(e => !witnessed.Contains(e.Code.Value))
                .ToList();

            TestContext.Out.WriteLine("CHECKED (emitted at least once in the corpus):");
            foreach (string code in witnessed.OrderBy(c => c, StringComparer.Ordinal))
            {
                TestContext.Out.WriteLine($"  {code}");
            }
            TestContext.Out.WriteLine("NOT CHECKED (declares an attribute, emitted nothing here) — NOT a pass:");
            foreach (ProblemCatalogEntry entry in unreachable.OrderBy(e => e.Code.Value, StringComparer.Ordinal))
            {
                string why = entry.RequiresControllerLimits ? "needs controller limits"
                    : entry.RequiresLibrary ? "needs a library"
                    : "no corpus case witnesses it";
                TestContext.Out.WriteLine($"  {entry.Code.Value} — {why}");
            }

            Assert.Multiple(() =>
            {
                Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
                Assert.That(witnessed, Is.Not.Empty,
                    "the sweep must actually have judged something — a green run over no witnesses proves nothing");
            });
        }

        /// <summary>
        /// EVERY FAMILY, not merely whichever rows the corpus happens to exercise. The declarations arrived
        /// family by family — naming, addressing, documentation, device settings, scenes, logic — and a sweep
        /// that quietly stopped witnessing one of them would stay green while that family emitted anything it
        /// liked.
        ///
        /// <para>A category that declares an attribute must therefore be WITNESSED: at least one of its rows
        /// really emitted here. A category none of whose rows can fire in this corpus is named in
        /// <see cref="UnwitnessableCategories"/> with its reason, so the exception is a decision rather than a
        /// silence.</para>
        /// </summary>
        [Test]
        public void EveryCategoryThatDeclaresAnAttributeIsWitnessed()
        {
            (List<Emitted> emitted, _) = RunCorpus();
            var witnessed = emitted.Select(e => e.Code).ToHashSet(StringComparer.Ordinal);

            var byCategory = ProblemCatalog.Current.Entries
                .Where(e => e.Section == ProblemCatalogSection.ProjectFindings
                    && e.Status == ProblemCodeStatus.Active
                    && e.Target.Attribute is not null
                    && e.Category is not null)
                .GroupBy(e => e.Category!.Value)
                .ToList();

            List<string> silent = [];
            foreach (var group in byCategory.OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
            {
                int seen = group.Count(e => witnessed.Contains(e.Code.Value));
                TestContext.Out.WriteLine($"{group.Key}: {group.Count()} declaring, {seen} witnessed");
                if (seen == 0 && !UnwitnessableCategories.ContainsKey(group.Key))
                {
                    silent.Add(
                        $"{group.Key} declares an attribute on {group.Count()} row(s) and the corpus witnessed "
                        + "none of them — widen the corpus or record why the family cannot fire here");
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(byCategory, Has.Count.GreaterThan(1),
                    "sanity: the declarations span more than one family, or this test asserts nothing");
                Assert.That(silent, Is.Empty, string.Join(Environment.NewLine, silent));
            });
        }

        /// <summary>Categories whose declaring rows cannot fire in this corpus at all, and why.</summary>
        private static readonly Dictionary<ValidationCategory, string> UnwitnessableCategories = [];

        /// <summary>
        /// THE ARMING CHECK. The sweep above passes; this proves it would not have passed a wrong declaration, by
        /// running the same predicate over a catalogue that lies about one row.
        /// </summary>
        [Test]
        public void TheSweepReportsADeclarationTheEmissionContradicts()
        {
            (List<Emitted> emitted, Func<string, string, bool> declares) = RunCorpus();
            Assert.That(emitted.Any(e => e.Code == "doc-cable-colour"), Is.True,
                "precondition: the corpus emits the row this arming check lies about");

            // Same rule, same emissions — only the DECLARATION is wrong: cable_colour reported on a terminal is
            // consistent, product_identifier reported on a terminal is not.
            IReadOnlyList<string> violations = Violations(
                emitted,
                code => code == "doc-cable-colour" ? "product_identifier" : DeclaredAttribute(code),
                declares);

            Assert.That(violations, Is.Not.Empty,
                "a declaration the emission contradicts must be reported, or the sweep above is decorative");
        }
    }
}
