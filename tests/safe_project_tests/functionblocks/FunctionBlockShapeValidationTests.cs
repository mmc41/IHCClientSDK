using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-11 — validator rule for the function-block five-container invariant (spec ch. 06 §6.3): every
    /// <c>functionblock</c> must contain exactly one of each of <c>inputs, outputs, settings, internalsettings,
    /// programs</c> in that fixed order; <c>programs</c> may hold only <c>program_simple</c>; and pin types
    /// (<c>resource_input</c>/<c>_output</c>/<c>_scene</c>) are bound to their container. Authentic oracles pass;
    /// a mutated block with a dropped/reordered container, a foreign <c>programs</c> child, or a misplaced pin fails.
    /// </summary>
    public class FunctionBlockShapeValidationTests
    {
        private static IhcSettings Settings => TestSetup.Settings;
        private static ProjectAppService App => new(Settings);

        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectElement TomBlok(Project project) =>
            project.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Tom blok");

        [Test]
        public async Task Validate_AuthenticProject_AllFunctionBlocksPassTheShapeRule()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectValidationResult result = App.Validate(project);
            Assert.That(result.IsValid, Is.True, "vendor function blocks are well-formed: " + string.Join(" | ", result.Errors));
        }

        // T015: the validator's five-container invariant must stay derived from FunctionBlockSections.All
        // (the shared FB-section source of truth) plus the "programs" container — never an independent literal
        // that can silently drift from it. An authentic function block therefore presents exactly that sequence,
        // and validates clean against it.
        [Test]
        public async Task Validate_FunctionBlockShapeRule_IsDerivedFromFunctionBlockSections()
        {
            Project project = await Load("project3-KompleksWired.vis");
            string[] expected = FunctionBlockSections.All.Select(s => s.Container).Append("programs").ToArray();

            string[] actual = TomBlok(project).Children.Select(c => c.Tag).ToArray();
            Assert.That(actual, Is.EqualTo(expected),
                "the authentic FB containers must equal FunctionBlockSections.All + 'programs'");

            ProjectValidationResult result = App.Validate(project);
            Assert.That(result.IsValid, Is.True, "errors: " + string.Join(" | ", result.Errors));
        }

        [Test]
        public async Task Validate_FunctionBlockMissingAContainer_Fails()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectEditor editor = project.Edit();
            editor.DeleteById(TomBlok(project).FindChild("settings")!.Id!.Value);
            ProjectValidationResult result = App.Validate(editor.ToProject());

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.Findings.Any(f => f.RuleId == "fb-shape"), Is.True,
                    "findings: " + string.Join(" | ", result.Findings.Select(f => f.RuleId)));
            });
        }

        [Test]
        public async Task Validate_FunctionBlockContainersReordered_Fails()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement fb = TomBlok(project);
            ProjectEditor editor = project.Edit();
            editor.MoveSubtree(fb.FindChild("inputs")!.Id!.Value, fb.Id!.Value, index: 4);   // inputs to the end
            ProjectValidationResult result = App.Validate(editor.ToProject());

            Assert.That(result.Findings.Any(f => f.RuleId == "fb-shape"), Is.True,
                "a reordered container sequence is rejected: " + string.Join(" | ", result.Findings.Select(f => f.RuleId)));
        }

        [Test]
        public async Task Validate_ProgramsWithNonProgramSimpleChild_Fails()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement fb = TomBlok(project);
            ElementId eventsId = fb.FindChild("programs")!.FindChild("program_simple")!.FindChild("events")!.Id!.Value;
            ProjectEditor editor = project.Edit();
            editor.CopySubtree(eventsId, fb.FindChild("programs")!.Id!.Value);   // an events under programs
            ProjectValidationResult result = App.Validate(editor.ToProject());

            Assert.That(result.Findings.Any(f => f.RuleId == "fb-programs"), Is.True,
                "programs may hold only program_simple: " + string.Join(" | ", result.Errors));
        }

        [Test]
        public async Task Validate_PinInWrongContainer_Fails()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement fb = TomBlok(project);
            ElementId resourceInputId = project.Root.Descendants().First(e => e.Tag == "resource_input").Id!.Value;
            ProjectEditor editor = project.Edit();
            editor.CopySubtree(resourceInputId, fb.FindChild("settings")!.Id!.Value);   // resource_input under settings
            ProjectValidationResult result = App.Validate(editor.ToProject());

            Assert.That(result.Findings.Any(f => f.RuleId == "fb-pin-container"), Is.True,
                "a pin type is bound to its container: " + string.Join(" | ", result.Errors));
        }
    }
}
