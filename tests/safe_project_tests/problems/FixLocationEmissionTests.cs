using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T054 — a rule saying where THIS occurrence is repaired, and that answer reaching the finding.
    ///
    /// <para>The engine half of the carried-fact pattern one level down. The entry's target holds for the whole
    /// row; a rule that knows better per occurrence states it on the emission, and the finding carries it out to
    /// a host that may not read the catalogue at all.</para>
    ///
    /// <para>Both halves are asserted, because the second is what makes the first safe: every rule that says
    /// nothing produces findings with no fix location, exactly as before.</para>
    /// </summary>
    public class FixLocationEmissionTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static async Task<Project> Corpus() =>
            await App.Load("testdata/projects/project3-KompleksWired.vis");

        /// <summary>
        /// THE FAMILY THAT OPTED IN. Every <c>attr-*</c> finding names the attribute THIS occurrence is about,
        /// and it is the same attribute the finding's own sentence binds — so the route and the message cannot
        /// disagree about which attribute the reader is being sent to.
        ///
        /// <para>Checked over the whole corpus rather than one case, and it asserts that something was judged:
        /// a green run over a corpus that emitted no <c>attr-*</c> finding would prove nothing at all.</para>
        /// </summary>
        [Test]
        public void EveryAttrFindingCarriesTheAttributeItsSentenceNames()
        {
            List<ValidationFinding> attrFindings = [];
            foreach ((string _, Func<Project> build) in ValidationCharacterizationTests.Corpus)
            {
                attrFindings.AddRange(App.ValidateStructured(build())
                    .Where(f => f.Problem.Code.Value.StartsWith("attr-", StringComparison.Ordinal)));
            }

            Assert.Multiple(() =>
            {
                Assert.That(attrFindings, Is.Not.Empty,
                    "precondition: the corpus emits this family, or the test judges nothing");
                foreach (ValidationFinding finding in attrFindings)
                {
                    string code = finding.Problem.Code.Value;
                    string? bound = finding.Problem.Arguments
                        .FirstOrDefault(a => a.Name == "attribute").Value?.ToString();

                    Assert.That(finding.Fix, Is.Not.Null,
                        $"{code} knows which attribute it is about — it binds one into its own sentence");
                    Assert.That(finding.Fix?.Attribute, Is.EqualTo(bound),
                        $"{code}: the fix location and the sentence must name the SAME attribute");
                    Assert.That(finding.Fix?.Element.ToToken(), Is.EqualTo(finding.Primary?.Locator),
                        $"{code}: and the same element the finding is anchored to");
                }
            });
        }

        /// <summary>
        /// And ONLY that family. A rule that says nothing still produces findings with no fix location, so the
        /// declaration keeps speaking for every row that has not opted in — which is what made the mechanism
        /// safe to add to a shipped engine.
        /// </summary>
        [Test]
        public async Task ARuleThatSaysNothingStillCarriesNoFixLocation()
        {
            Project project = await Corpus();

            EquatableArray<ValidationFinding> findings = App.ValidateStructured(project);
            var opted = findings.Where(f => f.Fix is not null)
                .Select(f => f.Problem.Code.Value).Distinct().ToList();

            Assert.Multiple(() =>
            {
                Assert.That(findings, Is.Not.Empty, "precondition: the corpus produced something to judge");
                Assert.That(opted.Where(c => !c.StartsWith("attr-", StringComparison.Ordinal)), Is.Empty,
                    "only the family that opted in carries one — " + string.Join(", ", opted));
            });
        }

        /// <summary>
        /// And a finding CAN carry one: the value survives being put on a finding and read back, which is the
        /// whole contract the host relies on. Asserted on the type rather than by inventing a rule, because a
        /// rule invented here would be a rule the registration gate never saw.
        /// </summary>
        [Test]
        public async Task AFindingCarriesAFixLocationThroughToItsReader()
        {
            Project project = await Corpus();
            ValidationFinding first = App.ValidateStructured(project).First();
            ElementId elsewhere = project.Root.Descendants().First(e => e.Id is not null).Id!.Value;

            ValidationFinding located = first with { Fix = new FixLocation(elsewhere, "inivalue") };

            Assert.Multiple(() =>
            {
                Assert.That(located.Fix?.Element, Is.EqualTo(elsewhere));
                Assert.That(located.Fix?.Attribute, Is.EqualTo("inivalue"));
                Assert.That(located.Problem.Code, Is.EqualTo(first.Problem.Code),
                    "and nothing else about the finding moved");
                Assert.That(first.Fix, Is.Null, "the original is untouched — findings are values");
            });
        }
    }
}
