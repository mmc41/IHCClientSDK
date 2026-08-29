using System;
using System.Collections.Generic;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The hand-built fluent authoring surface, and the three finding shapes an author must choose between.
    ///
    /// <para><b>The surface is deliberately small.</b> It offers a body and nothing else, because everything else
    /// a rule needs to say — kind, category, disposition, shape, target, whether it needs controller limits — is
    /// already on the catalogue entry. Offering them again at the rule site would be a third copy of each fact
    /// and a way for two copies to disagree, which is the duplication this design exists to avoid.</para>
    ///
    /// <para><b>THE REPAIR TEST, as an executable decision rather than per-author taste.</b> Ask what the user
    /// must DO to clear the finding:</para>
    /// <list type="bullet">
    /// <item><description>One repair clears every instance → <see cref="FindingShape.OneFinding"/>. The
    /// project-wide counts are these: raising a capacity limit once clears it.</description></item>
    /// <item><description>Each occurrence needs its own repair → <see cref="FindingShape.OnePerOccurrence"/>.
    /// The default for content rows: every blank cable type is filled in separately.</description></item>
    /// <item><description>One repair, but the reader must SEE every site to make it →
    /// <see cref="FindingShape.PrimaryWithRelated"/>. The collisions: two elements sharing an id is ONE fault,
    /// and reporting it twice tells the user twice that two things collide.</description></item>
    /// </list>
    ///
    /// <para>Ordering and aggregation are absent from the surface on purpose, and their absence is asserted
    /// below: a rule that could sort would make the executor's determinism unprovable.</para>
    /// </summary>
    [TestFixture]
    public sealed class RuleAuthoringTests
    {
        private static ProblemCatalogEntry Entry(string code, FindingShape shape, RuleFaces faces = RuleFaces.WholeProject) =>
            new(new ProblemCode(code), ProblemCatalogSection.ProjectFindings, ValidationCategory.Addressing,
                CatalogDisposition.Warning, RuleKind.UserContentRule, faces, default, shape,
                default, "Label");

        /// <summary>A recording inspection, standing in for the executor that does not exist yet.</summary>
        private sealed class Recorder : IProjectInspection
        {
            private readonly List<(ProjectElement? Primary, int RelatedCount, int ArgumentCount)> reported = [];

            public Recorder(Project project) => Project = project;

            public Project Project { get; }

            public ControllerCapabilityLimits? Controller => null;

            // D27's second declared context. Null here for the same reason Controller is: this recorder exists to
            // watch a rule's SHAPE, and a rule that needs a library is not run without one.
            public ILibraryBlockSource? Library => null;

            public IProjectAnalyses Analyses =>
                throw new NotSupportedException("this recorder stands in for the executor and computes no analyses");

            public IReadOnlyList<(ProjectElement? Primary, int RelatedCount, int ArgumentCount)> Reported => reported;

            public void Report(ProjectElement? element, EquatableArray<ProblemArgument> arguments) =>
                reported.Add((element, 0, arguments.Length));

            // T054's overload. Recorded identically: what this fixture judges is the ARGUMENT BINDING, and a
            // fix location changes none of it.
            public void Report(
                ProjectElement? element, EquatableArray<ProblemArgument> arguments, FixLocation? fix) =>
                Report(element, arguments);

            public void ReportGroup(
                ProjectElement primary,
                EquatableArray<ProjectElement> related,
                EquatableArray<ProblemArgument> arguments) =>
                reported.Add((primary, related.Length, arguments.Length));
        }

        private sealed class NeverSatisfied : IValueConstraint
        {
            public ProblemCode Code => new("addr-unassigned");

            public ValueConstraintVerdict Check(string? rawValue) =>
                ValueConstraintVerdict.Failed(EquatableArray.Create<ProblemArgument>(
                    [new ProblemArgument("value", rawValue ?? string.Empty)]));

            public FieldConstraintMetadata Describe() => FieldConstraintMetadata.Unconstrained with { Required = true };
        }

        [Test]
        public void ARuleNamesItsTargetDirectlyAsATagAndAttribute()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned", FindingShape.OnePerOccurrence) with
            {
                Target = new RuleTarget("dataline_input", "address_dataline"),
            };

            RuleDefinition rule = new RuleBuilder(entry).Inspect(_ => { }).Build();

            Assert.Multiple(() =>
            {
                Assert.That(rule.Entry.Target.Tag, Is.EqualTo("dataline_input"));
                Assert.That(rule.Entry.Target.Attribute, Is.EqualTo("address_dataline"));
                Assert.That(rule.Entry.Target.IsAttributeTarget, Is.True);
                Assert.That(rule.Entry.Target.IsWholeProject, Is.False);
            });
        }

        [Test]
        public void TheSurfaceOffersOnlyABody_NotAClassificationTheEntryAlreadyCarries()
        {
            string[] members = [.. typeof(RuleBuilder)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .Distinct()];

            Assert.Multiple(() =>
            {
                Assert.That(members, Is.EquivalentTo(new[] { nameof(RuleBuilder.Constrain), nameof(RuleBuilder.Inspect), nameof(RuleBuilder.Build) }));

                foreach (string restated in new[] { "OfKind", "Shaped", "Requiring", "AboutProject", "WithSeverity", "WithMessage" })
                {
                    Assert.That(members, Does.Not.Contain(restated),
                        $"'{restated}' would re-state on the rule what the catalogue entry already declares");
                }

                foreach (string ordering in new[] { "OrderBy", "RunAfter", "Priority", "Aggregate" })
                {
                    Assert.That(members, Does.Not.Contain(ordering),
                        $"'{ordering}' would move ordering out of the executor and make determinism unprovable");
                }
            });
        }

        [Test]
        public void ARuleHasExactlyOneBody_DeclarativeOrTraversal()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned", FindingShape.OnePerOccurrence);

            RuleDefinition declarative = new RuleBuilder(entry).Constrain(new NeverSatisfied()).Build();
            RuleDefinition traversal = new RuleBuilder(entry).Inspect(_ => { }).Build();

            Assert.Multiple(() =>
            {
                Assert.That(declarative.Constraints, Is.Not.Null);
                Assert.That(declarative.Inspection, Is.Null);
                Assert.That(traversal.Inspection, Is.Not.Null);
                Assert.That(traversal.Constraints, Is.Null);

                // Neither, and both, fail where the rule is WRITTEN rather than where it is collected.
                Assert.That(() => new RuleBuilder(entry).Build(),
                    Throws.TypeOf<RuleRegistrationException>()
                        .With.Property(nameof(RuleRegistrationException.Fault)).EqualTo(RuleRegistrationFault.BodyCount));
                Assert.That(() => new RuleBuilder(entry).Constrain(new NeverSatisfied()).Inspect(_ => { }).Build(),
                    Throws.TypeOf<RuleRegistrationException>()
                        .With.Property(nameof(RuleRegistrationException.Fault)).EqualTo(RuleRegistrationFault.BodyCount));
            });
        }

        [Test]
        public void TheSingleConstraintOverloadIsTheSequenceOfOne()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned", FindingShape.OnePerOccurrence);
            IValueConstraint constraint = new NeverSatisfied();

            RuleDefinition rule = new RuleBuilder(entry).Constrain(constraint).Build();

            Assert.That(rule.Constraints!.Ordered, Is.EqualTo(new[] { constraint }).AsCollection);
        }

        /// <summary>
        /// All three shapes, authored and exercised. The executor does not exist yet, so a recording inspection
        /// stands in for it — what is proven here is that the authoring surface can express each shape and that
        /// the two report forms differ in the way the shapes require.
        /// </summary>
        [Test]
        public void AllThreeFindingShapesAreAuthorableAndReportDifferently()
        {
            Project project = new(Tree.Node("utcs_project", null, []));
            ProjectElement first = Tree.Node("dataline_input", "_0x1", []);
            ProjectElement second = Tree.Node("dataline_input", "_0x2", []);

            (FindingShape Shape, ProjectInspection Body, int Expected, int Related)[] cases =
            [
                (FindingShape.OneFinding,
                    i => i.Report(null, EquatableArray<ProblemArgument>.Empty), 1, 0),
                (FindingShape.OnePerOccurrence,
                    i => { i.Report(first, default); i.Report(second, default); }, 2, 0),
                (FindingShape.PrimaryWithRelated,
                    i => i.ReportGroup(first, EquatableArray.Create<ProjectElement>([second]), default), 1, 1),
            ];

            Assert.Multiple(() =>
            {
                foreach ((FindingShape shape, ProjectInspection body, int expected, int related) in cases)
                {
                    RuleDefinition rule = new RuleBuilder(Entry($"shape-{shape}".ToLowerInvariant(), shape))
                        .Inspect(body).Build();
                    Recorder recorder = new(project);
                    rule.Inspection!(recorder);

                    Assert.That(rule.Entry.Shape, Is.EqualTo(shape));
                    Assert.That(recorder.Reported, Has.Count.EqualTo(expected), shape.ToString());
                    Assert.That(recorder.Reported[0].RelatedCount, Is.EqualTo(related), shape.ToString());
                }
            });
        }

        /// <summary>
        /// The repair test applied to the shipped catalogue, on the population where the answer is not a matter of
        /// opinion: a collision is ONE fault at N sites, so it is never one-per-occurrence.
        /// </summary>
        [Test]
        public void CollisionRowsCarryThePrimaryWithRelatedShape()
        {
            string[] collisions =
            [
                "id-duplicate-token", "id-duplicate-counter", "dataline-address-duplicate",
                "enum-def-duplicate-name", "enum-def-duplicate-index", "logic-case-duplicate-value",
                "scene-duplicate-target", "name-duplicate-siblings",
            ];

            Assert.Multiple(() =>
            {
                foreach (string code in collisions)
                {
                    Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry),
                        Is.True, code);
                    Assert.That(entry.Shape, Is.EqualTo(FindingShape.PrimaryWithRelated), code);
                }

                // And a plainly per-occurrence row is not swept into the same shape.
                Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("doc-cabletype"), out ProblemCatalogEntry doc), Is.True);
                Assert.That(doc.Shape, Is.EqualTo(FindingShape.OnePerOccurrence));
            });
        }

        [Test]
        public void ARuleAuthoredThroughTheBuilderRegistersAgainstTheCatalogue()
        {
            ProblemCatalogEntry entry = Entry("addr-unassigned", FindingShape.OnePerOccurrence);
            RuleDefinition rule = new RuleBuilder(entry).Inspect(_ => { }).Build();

            RuleSet rules = RuleSet.Create(
                ProblemCatalog.From(EquatableArray.Create<ProblemCatalogEntry>([entry])), [rule]);

            Assert.That(rules.TryGet(entry.Code, out _), Is.True);
        }
    }
}
