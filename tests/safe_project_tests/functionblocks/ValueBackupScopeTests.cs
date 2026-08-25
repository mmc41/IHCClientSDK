using Ihc.Vis.Schema;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Alignment F-27: the power-loss
    /// <i>Gem aktuel værdi</i> setting belongs to every variable the format admits it on — not to outputs alone.
    ///
    /// <para>Measured 2026-08-11: the reference application's <c>Rediger Flag egenskaber</c> — the properties dialog
    /// of an ordinary internal <c>resource_flag</c> — carries a <c>Ved strømsvigt</c> group with
    /// <c>Gem aktuel værdi</c>. <c>SetOutputBackup</c> admitted only <c>resource_output</c>,
    /// <c>dataline_output</c> and <c>airlink_relay</c>, so the very edit the vendor offers on a flag was refused.</para>
    ///
    /// <para>The format agrees with the vendor, which is what makes this a defect rather than a guess: the DTD
    /// declares <c>backup (yes | no) "no"</c> on <b>every</b> <c>resource_*</c> variable type except
    /// <c>resource_scene</c> — exactly the set <see cref="VariableTypeRegistry.IsVariableType"/> already models —
    /// plus <c>dataline_output</c> and <c>airlink_relay</c>. The built-in catalog itself materializes
    /// <c>backup="yes"</c> on internal flags, so such attributes are already read and written; only the EDIT was
    /// withheld.</para>
    /// </summary>
    public class ValueBackupScopeTests
    {
        private static IhcSettings Settings => TestSetup.Settings;
        private static ProjectAppService App => new(Settings);

        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        /// <summary>An UNLOCKED block, plus a freshly authored variable of <paramref name="tag"/> in its settings
        /// container — the shape the vendor's dialog was read on. Authored rather than fished out of a fixture so
        /// the case is exercised rather than skipped.</summary>
        private static (ProjectDocumentSession session, ElementId variable) BlockWithVariable(Project project, string tag)
        {
            ProjectDocumentSession session = Session(project);
            ElementId block = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("locked") != "yes").Id!.Value;
            session.Apply(new AddVariable(block, "settings", tag, "Probe"));
            ElementId variable = session.Current!.FindById(block)!.Descendants()
                .First(e => e.Tag == tag && e.GetAttribute("name") == "Probe").Id!.Value;
            return (session, variable);
        }

        [TestCase("resource_flag")]
        [TestCase("resource_integer")]
        public async Task SetBackup_OnAnInternalVariable_IsAllowedAndWritesTheAttribute(string tag)
        {
            Project project = await Load("project3-KompleksWired.vis");
            (ProjectDocumentSession session, ElementId variable) = BlockWithVariable(project, tag);

            EditOutcome outcome = session.Apply(new SetOutputBackup(variable, Save: true));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed),
                    "the vendor offers this edit on an ordinary variable, and the DTD admits the attribute there");
                Assert.That(session.Current!.FindById(variable)!.GetAttribute("backup"), Is.EqualTo("yes"));
            });
        }

        /// <summary>The one variable-shaped element the DTD does NOT declare <c>backup</c> on. Asserted so the
        /// widening stays bounded by the format rather than becoming "anything with an id".</summary>
        [Test]
        public async Task SetBackup_OnAScene_IsStillRefused()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement? scene = project.Root.Descendants().FirstOrDefault(e => e.Tag == "resource_scene");
            if (scene?.Id is null)
            {
                Assert.Ignore("no resource_scene in this fixture");
                return;
            }
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new SetOutputBackup(scene.Id.Value, Save: true));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                "a scene is not a stored value; the DTD declares no backup attribute on it");
        }
    }
}
