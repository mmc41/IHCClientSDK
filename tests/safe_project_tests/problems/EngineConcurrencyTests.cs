using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Safe concurrent reuse, and the rule-throws policy.
    ///
    /// <para><b>What is claimed, and what deliberately is not.</b> The catalogue, the rule set and the executor
    /// are built once and hold no per-run state, so ONE of each may be shared for a process lifetime while a
    /// background validation and a foreground command evaluation both use it. Nothing here makes an EDIT SESSION
    /// concurrent — serializing edits remains the document's job, exactly as before.</para>
    ///
    /// <para><b>Why immutability is asserted structurally as well as behaviourally.</b> A passing concurrency
    /// test proves today's code is safe; the absence of a mutator proves it stays safe. A single <c>Add</c> or
    /// public setter appearing later would make the guarantee false without any test noticing, because the race
    /// would need a specific interleaving to show itself.</para>
    ///
    /// <para><b>A rule that throws has a policy, not an accident.</b> By default the failing rule costs its own
    /// result and the run continues; the alternative is a project with a novel shape silently ceasing to be
    /// validated, handing the user a clean bill of health produced by a crash. Under concurrency that matters
    /// more, not less: a thrown rule on one thread must not corrupt what another thread is collecting.</para>
    /// </summary>
    [TestFixture]
    public sealed class EngineConcurrencyTests
    {
        private const int Threads = 16;

        private static ProblemCatalogEntry Entry(string code) =>
            new(new ProblemCode(code), ProblemCatalogSection.ProjectFindings, ValidationCategory.Addressing,
                CatalogDisposition.Warning, RuleKind.UserContentRule, RuleFaces.WholeProject, default,
                FindingShape.OnePerOccurrence, default, "Label");

        private static (Project Project, WholeProjectValidator Validator) Fixture(bool includeBrokenRule)
        {
            ProjectElement first = Tree.Node("dataline_input", "_0x10", []);
            ProjectElement second = Tree.Node("dataline_input", "_0x20", []);
            Project project = new(Tree.Node("utcs_project", null, [], first, second));

            ProblemCatalogEntry healthy = Entry("addr-unassigned");
            ProblemCatalogEntry broken = Entry("aaa-broken");
            ProblemCatalogEntry[] entries = includeBrokenRule ? [healthy, broken] : [healthy];

            ProblemCatalog catalog = ProblemCatalog.From(entries.ToImmutableArray());
            RuleDefinition[] rules = includeBrokenRule
                ?
                [
                    new RuleBuilder(healthy).Inspect(i => { i.Report(first, default); i.Report(second, default); }).Build(),
                    new RuleBuilder(broken).Inspect(_ => throw new InvalidOperationException("rule bug")).Build(),
                ]
                :
                [
                    new RuleBuilder(healthy).Inspect(i => { i.Report(first, default); i.Report(second, default); }).Build(),
                ];

            return (project, new WholeProjectValidator(RuleSet.Create(catalog, rules)));
        }

        [Test]
        public async Task ManyThreadsValidatingOverOneExecutorAllGetTheSameAnswer()
        {
            (Project project, WholeProjectValidator validator) = Fixture(includeBrokenRule: false);
            ConcurrentBag<string> results = [];

            await Task.WhenAll(Enumerable.Range(0, Threads).Select(_ => Task.Run(() =>
                results.Add(string.Join(
                    ",",
                    validator.Validate(project, ValidationProfile.ProjectOnly)
                        .Select(f => $"{f.Code.Value}@{f.Primary?.Locator}"))))));

            Assert.Multiple(() =>
            {
                Assert.That(results, Has.Count.EqualTo(Threads));
                Assert.That(results.Distinct(), Has.Exactly(1).Items,
                    "one shared executor, one answer — and the same ORDER, not merely the same set");
                Assert.That(results.First(), Is.EqualTo("addr-unassigned@_0x10,addr-unassigned@_0x20"));
            });
        }

        /// <summary>
        /// The interesting concurrent case: one rule throwing on every thread at once must not disturb what the
        /// other rule contributes, and every thread must still see the full picture.
        /// </summary>
        [Test]
        public async Task AThrowingRuleUnderConcurrencyCostsOnlyItsOwnResultOnEveryThread()
        {
            (Project project, WholeProjectValidator validator) = Fixture(includeBrokenRule: true);
            ConcurrentBag<string> results = [];

            await Task.WhenAll(Enumerable.Range(0, Threads).Select(_ => Task.Run(() =>
                results.Add(string.Join(",",
                    validator.Validate(project, ValidationProfile.ProjectOnly).Select(f => f.Code.Value))))));

            Assert.Multiple(() =>
            {
                Assert.That(results.Distinct(), Has.Exactly(1).Items, "every thread saw the same run");
                Assert.That(results.First().Split(',').Count(c => c == "addr-unassigned"), Is.EqualTo(2),
                    "the healthy rule reported both of its sites, on every thread");
                Assert.That(results.First(), Does.Contain("internal.unexpected"),
                    "and the broken rule contributed exactly its own failure");
            });
        }

        [Test]
        public async Task TheSharedCatalogueAnswersLookupsFromManyThreadsAtOnce()
        {
            ProblemCatalog catalog = ProblemCatalog.Current;
            ProblemCode[] codes = [.. catalog.Entries.Select(e => e.Code).Take(50)];
            ConcurrentBag<int> found = [];

            await Task.WhenAll(Enumerable.Range(0, Threads).Select(_ => Task.Run(() =>
                found.Add(codes.Count(c => catalog.TryGet(c, out ProblemCatalogEntry _))))));

            Assert.That(found.Distinct(), Is.EqualTo(new[] { codes.Length }).AsCollection,
                "every thread resolved every code — the lookup is a read over frozen state");
        }

        /// <summary>
        /// The structural half. Behaviour proves today; the absence of a mutator is what keeps it true.
        /// </summary>
        [Test]
        public void NothingInTheEngineOffersAWayToMutateItAfterConstruction()
        {
            Type[] shared =
            [
                typeof(ProblemCatalog), typeof(ProblemCatalogEntry), typeof(RuleSet), typeof(RuleDefinition),
                typeof(ValidationProfile), typeof(ControllerCapabilityLimits), typeof(ConstraintSequence),
                typeof(ControllerFirmwareVersion), typeof(DeclaredFirmwareBound),
            ];

            Assert.Multiple(() =>
            {
                foreach (Type type in shared)
                {
                    foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        // `init` accessors are allowed: they run during construction and cannot be called on a
                        // shared instance afterwards. A plain `set` can.
                        MethodInfo? setter = property.GetSetMethod();
                        bool initOnly = setter?.ReturnParameter.GetRequiredCustomModifiers()
                            .Any(m => m.Name == "IsExternalInit") ?? false;
                        Assert.That(setter is null || initOnly, Is.True,
                            $"{type.Name}.{property.Name} has a settable property on a type shared across threads");
                    }

                    foreach (string mutator in new[] { "Add", "Remove", "Clear", "Insert", "Sort" })
                    {
                        Assert.That(type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(m => m.Name),
                            Has.None.EqualTo(mutator), $"{type.Name}.{mutator}");
                    }
                }
            });
        }

        /// <summary>
        /// The policy is DECLARED rather than implicit, which is what makes it reviewable — and it is on the
        /// profile rather than fixed in the executor precisely so a diagnostic run can choose the other one.
        /// </summary>
        [Test]
        public void TheRuleThrowsPolicyIsDeclaredAndDefaultsToContinuing()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Enum.GetNames<RuleFailurePolicy>(), Is.EquivalentTo(new[]
                {
                    nameof(RuleFailurePolicy.ReportAndContinue), nameof(RuleFailurePolicy.Rethrow),
                }));
                Assert.That(ValidationProfile.ProjectOnly.FailurePolicy,
                    Is.EqualTo(RuleFailurePolicy.ReportAndContinue),
                    "a broken rule costs its own result, not the run");
                Assert.That(ValidationProfile.Categorized.FailurePolicy,
                    Is.EqualTo(RuleFailurePolicy.ReportAndContinue));
            });
        }
    }
}
