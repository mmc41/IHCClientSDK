using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RF-5 / D03: the structured finding has a DOOR, and the related sites survive it.
    ///
    /// <para>The engine's <see cref="ValidationFinding"/> carries a problem, a primary site and every RELATED
    /// site. <see cref="ProjectValidationResult"/> carries one locator per finding, so a grouped rule's other
    /// sites were dropped at the boundary — and reaching them meant naming <c>IWholeProjectValidator</c>, which
    /// the architecture forbids a GUI (L5). The rich finding existed and was unreachable by the only consumer
    /// that wanted it.</para>
    ///
    /// <para><see cref="ProjectAppService.ValidateStructured"/> is that door. It is the SAME run as
    /// <see cref="ProjectAppService.ValidateCategorized"/> — asserted below, because two pipelines with their own
    /// rules is exactly what the one-pass commitment forbids.</para>
    /// </summary>
    [TestFixture]
    public sealed class StructuredFindingAccessorTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        /// <summary>
        /// An authentic project the corpus already pins, and one that witnesses rules reporting through
        /// <c>ReportGroup</c> — <c>link-fb-input-unfed</c> and <c>link-fb-output-unused</c> each name a primary
        /// pin and five related ones. Those five are exactly what the flat shape has nowhere to put.
        /// </summary>
        private static Project GroupedFindingFixture() =>
            ValidationCharacterizationTests.Corpus
                .Single(c => c.Case == "authentic/project3-KompleksWired").Build();

        [Test]
        public void TheAccessorKeepsTheRelatedSitesTheFlatShapeDrops()
        {
            EquatableArray<ValidationFinding> structured = App.ValidateStructured(GroupedFindingFixture());

            ValidationFinding[] grouped = [.. structured.Where(f => f.Related.Length > 0)];

            Assert.Multiple(() =>
            {
                Assert.That(structured, Is.Not.Empty, "the fixture must produce findings, or this gate is vacuous");
                Assert.That(grouped, Is.Not.Empty,
                    "at least one rule reports a group, and its related sites must survive the accessor");
                Assert.That(grouped.All(f => f.Primary is not null), Is.True,
                    "a grouped finding names a primary site as well as its related ones");
            });
        }

        /// <summary>
        /// ONE run, two shapes. The structured accessor and the flat one must agree finding-for-finding: same
        /// count, same codes, same order. A second pipeline would be the defect the one-pass commitment names.
        /// </summary>
        [Test]
        public void TheStructuredAndFlatDoorsAreTheSameRun()
        {
            Project project = GroupedFindingFixture();

            EquatableArray<ValidationFinding> structured = App.ValidateStructured(project);
            ProjectValidationResult flat = App.ValidateCategorized(project);

            Assert.Multiple(() =>
            {
                Assert.That(structured.Length, Is.EqualTo(flat.Findings.Length), "same count");
                Assert.That(structured.Select(f => f.Code.Value),
                    Is.EqualTo(flat.Findings.Select(f => f.RuleId)).AsCollection, "same codes, same order");
                Assert.That(structured.Select(f => f.Problem.Message),
                    Is.EqualTo(flat.Findings.Select(f => f.Message)).AsCollection, "same sentences");
            });
        }

        /// <summary>
        /// The accessor reaches the engine WITHOUT the caller naming the executor port — which is the whole point
        /// of the door, and is enforced for the GUI by <c>L5_TheGui_DoesNotRunAnExecutorAndDoesNotReadTheCatalogue</c>.
        /// Asserted here as a property of the signature: nothing in it mentions the port's type.
        /// </summary>
        [Test]
        public void TheDoorsSignatureNamesNoExecutorPort()
        {
            System.Reflection.MethodInfo accessor =
                typeof(ProjectAppService).GetMethod(nameof(ProjectAppService.ValidateStructured))!;

            Type[] named = [accessor.ReturnType, .. accessor.GetParameters().Select(p => p.ParameterType)];

            Assert.That(named.Any(t => t == typeof(IWholeProjectValidator)), Is.False,
                "a caller must be able to hold the rich finding without holding the executor");
        }
    }
}
