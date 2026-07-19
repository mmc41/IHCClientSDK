using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-7: the link/scene command family — LinkPins enforces the vendor data-flow legality (a refused
    /// pair is Refused with a reason; a valid function-block link commits and matches the engine's own Link), and
    /// the scene commands byte-round-trip against the engine (SetSceneValue / note).
    /// </summary>
    public class LinkSceneCommandTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        private static ElementId FirstPin(Project project, ElementId fbId, string section) =>
            project.FindById(fbId)!.FindChild(section)!.ChildrenOrEmpty().First(c => c.Id is not null).Id!.Value;

        [Test]
        public async Task LinkPins_SamePin_IsRefusedWithReason()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId pin = project.Root.Descendants()
                .First(e => e.Tag is "dataline_input" or "dataline_output").Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new LinkPins(pin, pin));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Is.Not.Null.And.Not.Empty);
            });
        }

        [Test]
        public async Task LinkPins_ValidFunctionBlockLink_Commits_MatchesEngine()
        {
            Project project = await Load("project3-KompleksWired.vis");
            FunctionBlocks.FunctionBlockDefinition block =
                App.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0 && f.Outputs.Count > 0);
            ElementId loc = project.Groups.First().Id!.Value;
            ProjectDocumentSession session = Session(project);
            ElementId fb1 = session.Apply(new AddFunctionBlock(loc, block)).Value;
            ElementId fb2 = session.Apply(new AddFunctionBlock(loc, block)).Value;
            ElementId output = FirstPin(session.Current!, fb1, "outputs");   // a source (produces)
            ElementId input = FirstPin(session.Current!, fb2, "inputs");     // a sink (consumes)

            Project before = session.Current!;
            EditOutcome outcome = session.Apply(new LinkPins(output, input));

            ProjectEditor editor = before.Edit();
            editor.Link(output, input);
            Project viaEngine = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.Equals(viaEngine), Is.True, "matches the engine's own Link byte-for-byte");
            });
        }

        [Test]
        public async Task RemoveLink_RemovesBothHalves()
        {
            Project project = await Load("project3-KompleksWired.vis");
            FunctionBlocks.FunctionBlockDefinition block =
                App.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0 && f.Outputs.Count > 0);
            ElementId loc = project.Groups.First().Id!.Value;
            ProjectDocumentSession session = Session(project);
            ElementId fb1 = session.Apply(new AddFunctionBlock(loc, block)).Value;
            ElementId fb2 = session.Apply(new AddFunctionBlock(loc, block)).Value;
            ElementId output = FirstPin(session.Current!, fb1, "outputs");
            ElementId input = FirstPin(session.Current!, fb2, "inputs");
            session.Apply(new LinkPins(output, input));
            ElementId linkRow = session.Current!.FindById(output)!.ChildrenOrEmpty()
                .First(c => c.Tag == "link_from_resource").Id!.Value;

            EditOutcome outcome = session.Apply(new RemoveLink(linkRow));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindById(output)!.ChildrenOrEmpty().Any(c => c.Tag == "link_from_resource"),
                    Is.False, "the from-half is gone");
                Assert.That(session.Current!.FindById(input)!.ChildrenOrEmpty().Any(c => c.Tag == "link_to_resource"),
                    Is.False, "and its reciprocal to-half cascaded");
            });
        }

        [Test]
        public async Task UpdateSceneValue_MatchesEngineSetSceneValue()
        {
            Project project = await Load("project3-KompleksWired-scenelinks.vis");
            ProjectElement member = project.Root.Descendants().First(e => e.Tag is "scene_dimmer" or "scene_relay");
            ElementId id = member.Id!.Value;
            ProjectDocumentSession session = Session(project);
            var r = new SceneValueResult(On: true, LevelPercent: 50, RampMinutes: 0, RampSeconds: 2);

            Project before = session.Current!;
            EditOutcome outcome = session.Apply(new UpdateSceneValue(id, r));

            ProjectEditor editor = before.Edit();
            SceneValue value = member.Tag == "scene_dimmer"
                ? SceneValue.Dimmer(50, TimeSpan.FromSeconds(2))
                : SceneValue.Relay(true);
            editor.SetSceneValue(id, value);
            Project viaEngine = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.AnyOf(EditStatus.Committed, EditStatus.NoChange),
                    "the value applied (or was already set)");
                Assert.That(session.Current!.Equals(viaEngine), Is.True, "matches the engine's own SetSceneValue byte-for-byte");
            });
        }

        [Test]
        public async Task UpdateSceneContainer_SetsTheNote()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId id = project.Root.Descendants().First(e => e.Tag == "scenes").Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new UpdateSceneContainer(id, "my note"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindById(id)!.GetAttribute("note"), Is.EqualTo("my note"));
            });
        }
    }
}
