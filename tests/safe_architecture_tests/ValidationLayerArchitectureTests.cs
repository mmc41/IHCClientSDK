using System;
using System.Collections.Generic;
using System.Linq;

using ArchUnitNET.Domain;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;

using static Ihc.Tests.ArchRuleHelpers;

namespace Ihc.Tests
{
    /// <summary>
    /// T064 — the layer model stated BEFORE Phase 2 built anything, enforced. Each rule below is one of
    /// L1–L5 in <c>ARCHITECTURE.md</c>'s challenge 5; L6 (the code-family ownership partition) is already held by
    /// <see cref="ProblemOwnershipArchitectureTests"/>, so it is not restated here.
    ///
    /// <para><b>Why these are fitness tests and not symbol bans.</b> ADR-004's fidelity test decides the mechanism,
    /// and it was applied per rule: all six namespaces live in ONE project, and the ban mechanism is
    /// project-scoped, so a ban cannot express "Session must not reach Validation" without also banning it from the
    /// validation layer itself. The remaining rules name a ROSTER of our own evolving API (the executor ports, the
    /// value types a dialog may bind), which ADR-004 keeps out of symbol bans because a roster in a ban file states
    /// the rule wrongly in both directions. No <c>BannedSymbols.txt</c> entry is added by this task, and that is the
    /// ADR's own conclusion rather than a shortcut.</para>
    ///
    /// <para><b>Every rule is demonstrated ARMED.</b> The dependency-direction rules use the helpers' vacuity guard
    /// plus a seeded known-true violation, and the two roster rules are name-based scans — the shape that stays
    /// armed even while the forbidden types are legitimately absent from the subject's own model.</para>
    /// </summary>
    public class ValidationLayerArchitectureTests
    {
        private static readonly string Problems = typeof(ProblemCode).Namespace!;          // Ihc.Vis.Problems
        private static readonly string Validation = typeof(ProblemCatalog).Namespace!;     // Ihc.Vis.Validation
        private static readonly string Session = typeof(EditVerdict).Namespace!;           // Ihc.Vis.Session
        private static readonly string Model = typeof(ProjectElement).Namespace!;          // Ihc.Vis.Model
        private const string VisRoot = "Ihc.Vis";

        /// <summary>
        /// The definition layer: builders that REPORT findings and must never run an executor. Anchored to a public
        /// type per namespace so a rename breaks the compile rather than the rule.
        /// </summary>
        private static IEnumerable<string> DefinitionNamespaces() =>
        [
            typeof(Ihc.Vis.Products.ProductDefinitionBuilder).Namespace!,
            typeof(Ihc.Vis.FunctionBlocks.FunctionBlockDefinitionBuilder).Namespace!,
            typeof(Ihc.Vis.Catalog.CatalogReader).Namespace!,
        ];

        /// <summary>
        /// The executor PORTS, by full name. Reflected from the SDK so a rename cannot silently empty the set, and
        /// compared as names because a subject that correctly does NOT reference them has no edge for a fluent
        /// target set to match.
        /// <para>
        /// ONE DIVERGENCE FROM THE ADR, recorded rather than retrofitted: it names three ports, the third being
        /// <c>IValidationSurface</c> for the dialog-metadata face. No such interface was built — the face landed as
        /// the pure function <see cref="FieldMetadataFace"/> over a rule set, because describing a field needs no
        /// port to inject. The ADR's roster is corrected to what shipped; the RULE is unchanged, since a static
        /// entry point is exactly as forbidden to a definition builder as an interface would have been.
        /// </para>
        /// </summary>
        private static IReadOnlyCollection<string> ExecutorPortNames() =>
        [
            typeof(IWholeProjectValidator).FullName!,
            typeof(FieldMetadataFace).FullName!,
        ];

        /// <summary>
        /// The engine's rule REGISTRY: nothing outside the SDK has business assembling or running a rule set.
        /// <para>
        /// NOTE WHAT IS NOT HERE, and it is the finding this rule's first draft produced: <c>ProblemCatalog</c> and
        /// <c>ProblemCatalogEntry</c> are NOT forbidden to the GUI. The app declares its own reserved
        /// <c>app.openvisual.*</c> family through those very types (T041), and R17's whole point is that a reserved
        /// family buys a host its own code space, not an exemption from governance — the same schema, the same
        /// checks. What L5 forbids is READING THE SDK'S catalogue to re-derive text the SDK owns, which is
        /// the <c>Current</c> access point below, not the schema types.
        /// </para>
        /// </summary>
        private static IReadOnlyCollection<string> RuleRegistryNames() =>
        [
            typeof(ProjectRules).FullName!,
            typeof(RuleSet).FullName!,
        ];

        /// <summary>The SDK catalogue's access point — the members that hand out the SDK's own rows.</summary>
        private static IReadOnlyCollection<string> SdkCatalogueAccessors() => ["get_Current"];

        // ── L1: the problem namespace is neutral ────────────────────────────────────────────────────

        /// <summary>
        /// L1 — <c>Ihc.Vis.Problems</c> depends on <c>Ihc.Vis.Model</c> and nothing else in <c>Ihc.Vis.*</c>. This
        /// is what lets an <c>io.*</c> refusal in the reader, an <c>import.*</c> outcome or a <c>bridge.*</c> upload
        /// failure carry a coded problem without dragging the engine in behind it.
        /// <para>Asserted over the EDGES rather than as a list of forbidden namespaces: a new
        /// <c>Ihc.Vis.Something</c> the problem layer starts reaching for fails without anyone remembering to add
        /// it to a list.</para>
        /// </summary>
        [Test]
        public void L1_TheProblemNamespace_DependsOnTheModelAndNothingElseInTheEngine()
        {
            string[] reached =
            [
                .. DependencyEdges(ArchitectureModels.Sdk, Problems)
                    .Select(edge => OutermostTypeName(edge.Target))
                    .Where(target => target.StartsWith(VisRoot + ".", StringComparison.Ordinal))
                    .Select(ParentNamespace)
                    .Where(ns => ns != Problems && ns != Model)
                    .Distinct()
                    .OrderBy(ns => ns, StringComparer.Ordinal),
            ];

            Assert.Multiple(() =>
            {
                Assert.That(DependencyEdges(ArchitectureModels.Sdk, Problems), Is.Not.Empty,
                    "the problem namespace produced no dependency edges at all — the rule would pass vacuously; "
                    + "fix the anchor, not the assert");
                Assert.That(reached, Is.Empty,
                    "Ihc.Vis.Problems may reach Ihc.Vis.Model and nothing else in the engine, so that any layer "
                    + "can carry a coded problem without depending on the validation engine. Reached: "
                    + string.Join(", ", reached));
            });
        }

        // ── L2 and L3: nothing below the engine reaches up into it ──────────────────────────────────

        /// <summary>
        /// L2 — the session layer does not depend on the validation engine. The direction is Validation → Session:
        /// the command-legality face reads a command, and <c>ProjectAppService</c> composes the two. This is the
        /// rule that forces the session's refusal sentences to live beside their codes, which
        /// <c>RefusalLabelDriftTests</c> then keeps in step with the catalogue.
        /// </summary>
        [Test]
        public void L2_TheSessionLayer_DoesNotDependOnTheValidationEngine() =>
            AssertNoDependency(ArchitectureModels.Sdk, Subtree(Session), Validation,
                "the direction is Validation -> Session; ProjectAppService composes the two, and a session that "
                + "could read the engine would be free to look its own refusal sentences up");

        /// <summary>
        /// L3 — the IO layer does not depend on the validation engine, so the open/save coded refusals reach for
        /// <c>Ihc.Vis.Problems</c> instead. The existing direction Validation → Io stays.
        /// </summary>
        [Test]
        public void L3_TheIoLayer_DoesNotDependOnTheValidationEngine() =>
            AssertNoDependency(ArchitectureModels.Sdk, Subtree(typeof(Ihc.Vis.Io.ProjectSerializer).Namespace!),
                Validation,
                "a refusing reader or writer carries a coded problem from Ihc.Vis.Problems; reaching the engine "
                + "would invert the layering and let the IO layer read the catalogue");

        // ── L4: a definition builder reports, it does not run the engine ────────────────────────────

        /// <summary>
        /// L4 — the definition builders emit findings and keep doing so, but none of them may touch an executor
        /// port. Name-based, so the rule is armed even though the correct code has no such edge at all.
        /// </summary>
        [TestCaseSource(nameof(DefinitionNamespaces))]
        public void L4_ADefinitionBuilder_DoesNotRunAnExecutor(string definitionNamespace) =>
            AssertNoDependencyOnTypeNames(ArchitectureModels.Sdk, definitionNamespace, ExecutorPortNames(),
                "the executor ports",
                "a definition builder REPORTS findings (its Build() returns them); running a whole-project "
                + "validator or reading the field-metadata face from a builder would put the engine underneath "
                + "the catalog");

        // ── L5: the GUI goes through the facade ─────────────────────────────────────────────────────

        /// <summary>
        /// L5 — the GUI renders problems and binds dialogs to validation VALUE types, but must not construct or run
        /// an executor, and must not read the catalogue. Both halves are name-based scans over the app assembly.
        /// </summary>
        [Test]
        public void L5_TheGui_DoesNotRunAnExecutorAndDoesNotReadTheCatalogue()
        {
            const string appRoot = "ihc_openvisual";

            Assert.Multiple(() =>
            {
                AssertNoDependencyOnTypeNames(ArchitectureModels.Gui, appRoot, ExecutorPortNames(),
                    "the executor ports",
                    "the GUI reaches the engine through ProjectAppService and IProjectDocument; constructing an "
                    + "executor in a view-model would put a second composition root in the shell");
                AssertNoDependencyOnTypeNames(ArchitectureModels.Gui, appRoot, RuleRegistryNames(),
                    "the rule registry",
                    "assembling or running a rule set is the SDK's job; a shell that could do it would be a second "
                    + "composition root for the engine");
                AssertDoesNotCallMembers(ArchitectureModels.Gui, appRoot, typeof(ProblemCatalog).FullName,
                    SdkCatalogueAccessors(), "the SDK catalogue's access point",
                    "the GUI renders the Danish sentence a finding already carries; reading the SDK's catalogue "
                    + "would let it re-derive user-facing text the SDK owns. Declaring its OWN app.* family through "
                    + "the same schema types is sanctioned (R17) and deliberately not forbidden here");
            });
        }

        // ── the arming pass ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The rules are demonstrated ARMED with known-true violations, which is what separates a fitness function
        /// from a decoration: the validation engine DOES depend on the session layer and on the IO layer (that is
        /// the sanctioned direction), so pointing L2's and L3's mechanism the other way must report a violation.
        /// </summary>
        [Test]
        public void TheDirectionRulesCanFail()
        {
            Assert.Multiple(() =>
            {
                // BY NAME, because the one carrier left is internal. After the command-evaluator face retired,
                // the ONLY Ihc.Vis.Validation -> Ihc.Vis.Session edge in the tree is
                // ProblemCatalogEntries.EditRefusals.cs, which builds each edit entry from the code MEMBER its
                // refusal site uses. That type is internal, safe_architecture_tests is not in ihcclient's
                // InternalsVisibleTo list, and the typeof overload therefore cannot reach it — so this assertion
                // is anchored on its full name rather than dropped. Dropping it is the one unacceptable option:
                // it would leave L2 armed by nothing on the direction it actually governs.
                AssertDependencyIsDetected(ArchitectureModels.Sdk, Validation + ".ProblemCatalogEntries", Session,
                    "the edit-refusal catalogue entries really do depend on the session layer — if this comes out "
                    + "clean, the dependency mechanism is broken and L2 is passing vacuously");
                AssertDependencyIsDetected(ArchitectureModels.Sdk, typeof(ProjectRules), Validation,
                    "and a type inside the engine depends on the engine, which is the other end of the same "
                    + "mechanism");
            });
        }
    }
}
