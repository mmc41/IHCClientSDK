using System.Linq;
using System.Threading.Tasks;

using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RF-3: <see cref="EditContext.RequireTag"/> has TWO failure conditions and they are not the same refusal.
    ///
    /// <para>An id that no longer resolves is a MISSING target — the user deleted it, or an undo took it away —
    /// and an id that resolves to an element of the wrong tag is a target of the WRONG KIND. The guard used to
    /// answer both with <c>edit.target-wrong-kind</c> and the sentence <i>Målet er ikke …</i>, which tells a user
    /// whose target was deleted something that is not true about it: the element is not the wrong kind, it is not
    /// there. The two codes exist precisely so a caller can tell the conditions apart, so collapsing them
    /// throws away the distinction the catalogue publishes.</para>
    ///
    /// <para>The sentences are taken from <see cref="EditRefusalProblems"/> rather than inlined here or at the
    /// guard: the session layer may not read the catalogue, so one owner per sentence is what keeps the copy the
    /// guard raises equal to the template the entry declares.</para>
    /// </summary>
    public class EditContextGuardTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static async Task<EditContext> Context()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            return new EditContext(project, ProjectIndex.Build(project));
        }

        /// <summary>An id no element carries. Well outside the oracle's allocated range.</summary>
        private static ElementId Absent => new(0x7FFFFF, 0x99);

        /// <summary>An id that IS in the project, on an element whose tag is not the one asked for.</summary>
        private static ElementId PresentButNotAFunctionBlock(Project project) =>
            project.Root.DescendantsAndSelf()
                .First(e => e.Id is not null && e.Tag != "functionblock").Id!.Value;

        [Test]
        public async Task RequireTag_OnAnIdThatDoesNotResolve_RefusesAsAMissingTarget()
        {
            EditContext context = await Context();

            EditVerdict verdict = context.RequireTag(Absent, "en funktionsblok", "functionblock");

            Assert.Multiple(() =>
            {
                Assert.That(verdict.Ok, Is.False, "an absent target is refused");
                Assert.That(verdict.Code, Is.EqualTo(EditRefusalCodes.TargetMissing),
                    "a deleted target is MISSING, not of the wrong kind");
                Assert.That(verdict.Reason, Is.EqualTo("Målet findes ikke længere."),
                    "and it says so, in the words the target-missing entry declares");
            });
        }

        [Test]
        public async Task RequireTag_OnAnIdOfAnotherTag_RefusesAsTheWrongKind()
        {
            Project project = await App.Load("testdata/projects/project3-KompleksWired.vis");
            EditContext context = new(project, ProjectIndex.Build(project));

            EditVerdict verdict = context.RequireTag(
                PresentButNotAFunctionBlock(project), "en funktionsblok", "functionblock");

            Assert.Multiple(() =>
            {
                Assert.That(verdict.Ok, Is.False, "a target of another kind is refused");
                Assert.That(verdict.Code, Is.EqualTo(EditRefusalCodes.TargetWrongKind));
                Assert.That(verdict.Reason, Is.EqualTo("Målet er ikke en funktionsblok."),
                    "the noun the command named is spliced into the wrong-kind sentence");
            });
        }

        /// <summary>
        /// The two guards agree about what a missing target is. <see cref="EditContext.RequireExists"/> already
        /// answered this condition correctly, which is why the fix is that <see cref="EditContext.RequireTag"/>
        /// joins it rather than that either invents a third answer.
        /// </summary>
        [Test]
        public async Task BothGuardsGiveAMissingTargetTheSameCode()
        {
            EditContext context = await Context();

            Assert.That(context.RequireTag(Absent, "en funktionsblok", "functionblock").Code,
                Is.EqualTo(context.RequireExists(Absent, "Funktionsblokken").Code),
                "one condition, one code, whichever guard happened to notice it");
        }
    }
}
