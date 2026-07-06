namespace Ihc.Vis.Tests
{
    /// <summary>
    /// SDK-consistency gate for authoring a project-global enum (<see cref="ProjectEditor.AddEnumDefinition"/>) and
    /// wiring it to a <c>resource_enum</c> in a <b>mutation</b> context — a loaded project3 — the one enum shape the
    /// standalone <c>-enumvalues.vis</c> oracle never witnesses (its <c>ValueOracleEnum</c> is unreferenced). There is
    /// no vendor byte oracle for authored+wired in a mutation context, so this asserts the wiring <em>validates
    /// clean</em> and <em>survives a save/reload round-trip</em>, not vendor byte-parity. The authored enum keeps
    /// project3's low ids un-normalized (no <see cref="ProjectEditor.NormalizeCatalogEnums"/> call) — the reference
    /// only needs the fresh def/value ids to resolve, which they do off the project counter.
    /// </summary>
    public class AuthoredEnumReferenceTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        [Test]
        public async Task AddEnumDefinition_ThenWireResourceEnum_InMutationContext_ValidatesAndRoundTrips()
        {
            var app = new ProjectAppService(Settings);
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            ProjectElement tomBlok = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Tom blok");
            string room = project.FindParent(tomBlok.Id!.Value)!.GetAttribute("name")!;

            ProjectEditor editor = project.Edit();
            EnumDefinitionRef def = editor.AddEnumDefinition("WiredProbe", "Alpha", "Beta", "Gamma");
            editor.Group(room).FunctionBlock("Tom blok").AddInput("resource_enum", "ProbeInput",
                e => e.SetAttribute("typedef", def.Typedef).SetAttribute("inivalue", def.InitialValue("Beta")));
            Project after = editor.ToProject();

            ProjectValidationResult validation = app.Validate(after);
            using var ms = new MemoryStream();
            await app.Save(after, ms, ProjectSaveOptions.PreserveExistingMetadata with { VerifyRoundTrip = true });

            ms.Position = 0;
            Project reloaded = await app.Load(ms);
            ProjectElement wired = reloaded.Root.Descendants()
                .First(e => e.Tag == "resource_enum" && e.GetAttribute("name") == "ProbeInput");

            Assert.Multiple(() =>
            {
                Assert.That(validation.IsValid, Is.True,
                    "authored + wired enum validates clean: " + string.Join(" | ", validation.Errors));
                Assert.That(validation.Findings.Any(f => f.RuleId is "enum-typedef" or "enum-inivalue"), Is.False);
                Assert.That(wired.GetAttribute("typedef"), Is.EqualTo(def.Typedef), "typedef survived save/reload");
                Assert.That(wired.GetAttribute("inivalue"), Is.EqualTo(def.InitialValue("Beta")),
                    "inivalue survived save/reload");
            });
        }
    }
}
