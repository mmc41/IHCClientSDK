using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-7 — function-block resource authoring + the §6.3.1 section↔type matrix.
    /// <see cref="FunctionBlockRef.AddInput"/>/<see cref="FunctionBlockRef.AddOutput"/> add pins to the fixed
    /// <c>inputs</c>/<c>outputs</c> containers; <see cref="FunctionBlockRef.AddSetting"/>/
    /// <see cref="FunctionBlockRef.AddInternalVariable"/> add value variables to <c>settings</c>/
    /// <c>internalsettings</c>, rejecting pin types (which are container-bound). <c>project2-CustomBlock.vis</c>
    /// (a five-container <c>Custom blok</c>) is the fixture, so these run without an install dir.
    /// </summary>
    public class FunctionBlockResourceTests
    {
        private const string Oracle = "project2-CustomBlock.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/" + Oracle);

        private static FunctionBlockRef CustomBlok(ProjectEditor editor, Project project)
        {
            ProjectElement fb = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Custom blok");
            string groupName = project.FindParent(fb.Id!.Value)!.GetAttribute("name")!;
            return editor.Group(groupName).FunctionBlock("Custom blok");
        }

        private static ProjectElement Container(Project built, string container) =>
            built.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Custom blok")
                .FindChild(container)!;

        [Test]
        public async Task AddInput_PlacesResourceInputUnderInputs()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();

            CustomBlok(editor, project).AddInput("NyIndgang");
            Project built = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(Container(built, "inputs").Children.Any(c => c.Tag == "resource_input" && c.GetAttribute("name") == "NyIndgang"),
                    Is.True, "the new pin is a resource_input under inputs");
                Assert.That(app.Validate(built).IsValid, Is.True);
            });
        }

        [Test]
        public async Task AddOutput_PlacesResourceOutputUnderOutputs()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();

            CustomBlok(editor, project).AddOutput("NyUdgang");
            Project built = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(Container(built, "outputs").Children.Any(c => c.Tag == "resource_output" && c.GetAttribute("name") == "NyUdgang"),
                    Is.True);
                Assert.That(app.Validate(built).IsValid, Is.True);
            });
        }

        [Test]
        public async Task AddSetting_And_AddInternalVariable_PlaceValueTypesInTheirContainers()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();

            FunctionBlockRef fb = CustomBlok(editor, project);
            fb.AddSetting("resource_flag", "IndstillingsFlag");
            fb.AddInternalVariable("resource_flag", "InterntFlag");
            Project built = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(Container(built, "settings").Children.Any(c => c.Tag == "resource_flag" && c.GetAttribute("name") == "IndstillingsFlag"),
                    Is.True, "a value type goes in settings");
                Assert.That(Container(built, "internalsettings").Children.Any(c => c.Tag == "resource_flag" && c.GetAttribute("name") == "InterntFlag"),
                    Is.True, "and in internalsettings");
                Assert.That(app.Validate(built).IsValid, Is.True);
            });
        }

        [Test]
        public async Task AddSetting_RejectsPinTypes_PerSectionTypeMatrix()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            FunctionBlockRef fb = CustomBlok(editor, project);

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => fb.AddSetting("resource_input", "x"), "settings excludes input pins");
                Assert.Throws<ArgumentException>(() => fb.AddSetting("resource_output", "x"), "settings excludes output pins");
                Assert.Throws<ArgumentException>(() => fb.AddSetting("resource_scene", "x"), "settings excludes scene pins");
                Assert.Throws<ArgumentException>(() => fb.AddInternalVariable("resource_input", "x"), "internalsettings excludes pins too");
            });
        }

        [Test]
        public async Task AddSetting_ConfigureCallback_SetsTypeSpecificRequiredAttributes()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();

            CustomBlok(editor, project).AddSetting("resource_timertime", "NyTimertid",
                h => h.SetAttribute("hour", "0").SetAttribute("minute", "5")
                      .SetAttribute("second", "0").SetAttribute("millisecond", "0"));
            Project built = editor.ToProject();

            ProjectElement added = Container(built, "settings").Children
                .First(c => c.Tag == "resource_timertime" && c.GetAttribute("name") == "NyTimertid");

            Assert.Multiple(() =>
            {
                Assert.That(added.GetAttribute("minute"), Is.EqualTo("5"), "the configure callback set the required attrs");
                Assert.That(app.Validate(built).IsValid, Is.True, "all #REQUIRED attrs present: " + string.Join(" | ", app.Validate(built).Errors));
            });
        }

        [Test]
        public async Task AddInput_ReturnsLinkableHandle()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();
            FunctionBlockRef fb = CustomBlok(editor, project);

            ResourceRef pin = fb.AddInput("LinkbarIndgang");
            ResourceRef sink = fb.AddOutput("LinkbarUdgang");
            editor.Link(sink, pin);   // a newly added pin carries a real id and can be linked
            Project built = editor.ToProject();

            Assert.That(app.Validate(built).IsValid, Is.True, "the new pins wire into a valid reciprocal link");
        }
    }
}
