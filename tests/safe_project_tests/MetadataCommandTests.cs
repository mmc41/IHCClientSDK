using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-10: the metadata / variables / enums / text / dimmer command family. Project information
    /// round-trips through the read projection and burns no ids (the id-less metadata path); an enumerator append
    /// with nothing new is a NoChange that leaves history untouched (pins the removal of the old hand-rolled
    /// "nothing to append" bypass); a real append matches the engine's own <see cref="ProjectEditor.AddEnumValues"/>
    /// (byte-verified against the vendor oracle in <c>EnumAppendReplayByteFidelityTests</c>); and a dimmer-settings
    /// edit writes the six <c>dimmer_setting_*</c> values.
    /// </summary>
    public class MetadataCommandTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        // The pre-existing empty, editable (non-catalog) enumerator in project3 — the vendor's "TestEnum".
        private static string EmptyEditableEnumName(Project project) =>
            project.Child("enum_definitions")!.ChildrenOrEmpty()
                .First(c => c.Tag == "enum_definition"
                    && (project.View(c).Effective("typeid") ?? ElementId.NullToken) == ElementId.NullToken
                    && !c.ChildrenOrEmpty().Any(v => v.Tag == "enum_value"))
                .GetAttribute("name")!;

        [Test]
        public async Task UpdateProjectInfo_RoundTripsThroughProjection_AndBurnsNoIds()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            var data = new ProjectInfoData("desc-A1", "num-A1", "prog-A1",
                new ContactInfo("kName", "kAddr", "kCity", "kZip", "kCountry", "kPhone", "kMob", "kEmail"),
                new ContactInfo("iName", "iAddr", "iCity", "iZip", "iCountry", "iPhone", "iMob", "iEmail"));

            EditOutcome outcome = session.Apply(new UpdateProjectInfo(data));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.GetProjectInfo(), Is.EqualTo(data), "project info reads back as written");
                Assert.That(session.Current!.LastUniqueId, Is.EqualTo(project.LastUniqueId),
                    "metadata edits are id-less (ENG-A1: last_unique_id unchanged)");
            });
        }

        [Test]
        public async Task UpdateEnumStates_WithNoNewValues_IsNoChange_HistoryUntouched()
        {
            Project project = await Load("project3-KompleksWired.vis");
            string name = EmptyEditableEnumName(project);
            ProjectDocumentSession session = Session(project);
            Project before = session.Current!;

            EditOutcome outcome = session.Apply(new UpdateEnumStates(name, []));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.NoChange), "an empty append changes nothing");
                Assert.That(session.CanUndo, Is.False, "a no-op leaves the undo history untouched");
                Assert.That(ReferenceEquals(session.Current, before), Is.True, "the snapshot is unchanged");
            });
        }

        [Test]
        public async Task UpdateEnumStates_AppendsNewValues_MatchesEngine()
        {
            Project project = await Load("project3-KompleksWired.vis");
            string name = EmptyEditableEnumName(project);
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new UpdateEnumStates(name, ["AppendA", "AppendB", "AppendC"]));

            ProjectEditor editor = project.Edit();
            editor.AddEnumValues(editor.EnumDefinition(name), "AppendA", "AppendB", "AppendC");
            Project viaEngine = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.Equals(viaEngine), Is.True, "matches the engine's own AddEnumValues");
            });
        }

        // T013: a Relabels entry changes an existing USER value's label in place (no append) and matches the engine.
        [Test]
        public async Task UpdateEnumStates_RelabelsExistingValue_MatchesEngine()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement def = project.Root.Descendants().First(e => e.Tag == "enum_definition"
                && e.GetAttribute("typeid") is null && e.ChildrenOrEmpty().Any(v => v.IsEnumValue));
            string name = project.View(def).Name!;
            ElementId valueId = def.ChildrenOrEmpty().First(v => v.IsEnumValue).Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new UpdateEnumStates(name, []) { Relabels = [(valueId, "Relabeled")] });

            ProjectEditor editor = project.Edit();
            editor.RelabelEnumValue(editor.EnumDefinition(name), valueId, "Relabeled");
            Project viaEngine = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindById(valueId)!.GetAttribute("name"), Is.EqualTo("Relabeled"), "the label changed in place");
                Assert.That(session.Current!.Equals(viaEngine), Is.True, "matches the engine's own RelabelEnumValue");
            });
        }

        [Test]
        public async Task UpdateDimmerSettings_WritesTheSixSettingValues()
        {
            Project project = await Load("project3-KompleksWired.vis");
            // The smallest id-bearing subtree that still owns the dimmer settings is the dimmer product itself
            // (a locality's subtree is larger), so this targets one product's own dimmer_settings.
            ProjectElement product = project.Root.Descendants()
                .Where(e => e.Id is not null && e.Descendants().Any(d => d.Tag == "dimmer_setting_load_mode"))
                .OrderBy(e => e.DescendantsAndSelf().Count())
                .First();
            ElementId id = product.Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(
                new UpdateDimmerSettings(id, new AdvancedDimmerResult(111, 222, 33, 5, 95, "rl")));

            ProjectElement after = session.Current!.FindById(id)!;
            string Val(string tag) => after.DescendantsAndSelf().First(e => e.Tag == tag).GetAttribute("value") ?? "";
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(Val("dimmer_setting_fade_rate_up"), Is.EqualTo("111"));
                Assert.That(Val("dimmer_setting_fade_rate_down"), Is.EqualTo("222"));
                Assert.That(Val("dimmer_setting_dimming_rate"), Is.EqualTo("33"));
                Assert.That(Val("dimmer_setting_minimum_value"), Is.EqualTo("5"));
                Assert.That(Val("dimmer_setting_maximum_value"), Is.EqualTo("95"));
                Assert.That(Val("dimmer_setting_load_mode"), Is.EqualTo("rl"));
            });
        }
    }
}
