using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The case-structure authoring byte-fidelity gate (G3, US-031) for <see cref="CaseRef"/>/<c>AddCase</c> against
    /// the authentic vendor oracle <c>project2-CustomBlock-case.vis</c> (IHC Visual 03.04.72.03 after one recorded
    /// program-authoring sequence on <c>project2-CustomBlock.vis</c>, single save — the corpus's first
    /// <c>program_case</c>). The SDK loads the original, reproduces Action 0
    /// (<see cref="ProjectEditor.NormalizeCatalogEnums"/>), replays the sequence in allocation order — sub-program
    /// skeleton (<c>_0x105</c>–<c>_0x108</c>), <c>program_case</c> on the Tæller input <b>plus its eagerly-allocated
    /// Else branch</b> (<c>_0x109</c>/<c>_0x10a</c>, §18 gate A: allocated together at case-insert, Else serialized
    /// last), two case values 100/1000 (each a vendor "Rediger konstant" id burn — reproduced test-side, the V4
    /// idiom — then <c>case_action</c> + bare embedded <c>&lt;resource_counter inivalue=…&gt;</c> operand), then the
    /// three branch actions — restamps to the oracle's clock and asserts byte-identity. Per the oracle bytes the
    /// third action (<c>Kip %P</c>) sits in the sub's false branch, not the Else container (the capture's drop
    /// landed there; the Else stays empty/self-closed). Catalog-free.
    /// </summary>
    public class CaseReplayByteFidelityTests
    {
        private const string Original = "project2-CustomBlock.vis";
        private const string CaseOracle = "project2-CustomBlock-case.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        // The vendor "Rediger konstant" dialog burns one id per case value before committing the
        // case_action + operand pair (ENG2-B2 census gaps 267/270) — a UI artifact, not engine semantics, so it is
        // reproduced test-side via the established add-then-delete idiom (the V4 replay's Burn()).
        private static void Burn(ProjectEditor editor, FunctionBlockRef custom) =>
            editor.DeleteById(custom.AddSetting("resource_flag", "burn").Id!.Value);

        // ---- Full replay: Action 0 → skeleton → case+Else → burns+values → actions → byte-identity ----

        [Test]
        public async Task AuthorCounterCase_ReplaysCustomBlockCaseOracle_ByteIdentical() =>
            // id2 _0xc0e2c02 decodes to day 12 / hour 14 / min 44 / sec 2; <modified> is minute-precision (14:44).
            await ReplayOracle.AssertReplaysByteIdentical(Original, CaseOracle,
                new DateTimeOffset(2026, 7, 12, 14, 44, 2, TimeSpan.Zero),
                editor =>
                {
                    // Row names/notes are the vendor method-vocabulary strings, transcribed verbatim from the
                    // oracle bytes.
                    FunctionBlockRef custom = editor.Group("Stue").FunctionBlock("Custom blok");
                    SubProgramRef sub = custom.Program().AddSubProgram();            // C1: _0x105.._0x108
                    CaseRef kase = sub.WhenTrue.AddCase("Case (%LT)", custom.Input("Tæller"),
                        "Udfører case når %P er lig case værdien");                  // C2: _0x109 + Else _0x10a
                    Burn(editor, custom);                                            // _0x10b: Rediger-konstant burn
                    BranchRef eq100 = kase.Case("Case", "resource_counter",
                        op => op.SetAttribute("inivalue", "100"),
                        "Udfører case når %P er lig case værdien");                  // C3: _0x10c + operand _0x10d
                    Burn(editor, custom);                                            // _0x10e: second burn
                    BranchRef eq1000 = kase.Case("Case", "resource_counter",
                        op => op.SetAttribute("inivalue", "1000"),
                        "Udfører case når %P er lig case værdien");                  // C4: _0x10f + operand _0x110
                    eq100.AddAction("%P = ON", custom.Output("Udgang"), "_0xa",
                        note: "Sætter %P til ON");                                   // C5: _0x111
                    sub.WhenFalse.AddAction("Kip %P", custom.Output("Udgang"), "_0x23",
                        note: "Sætter %P til modsat værdi af aktuel værdi");         // C6: _0x112 (false branch!)
                    eq1000.AddAction("%P = OFF", custom.Output("Udgang"), "_0x14",
                        note: "Sætter %P til OFF");                                  // C7: _0x113
                });

        // ---- Composition isolation: eager Else, doc-last default, operand-first, pinned shapes, no burn ----

        [Test]
        public async Task CaseAuthoring_EagerElseDocLast_OperandFirst_PinnedShapes()
        {
            Project original = await ReplayOracle.LoadProject(Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();
            FunctionBlockRef custom = editor.Group("Stue").FunctionBlock("Custom blok");
            ResourceRef taeller = custom.Input("Tæller");
            SubProgramRef sub = custom.Program().AddSubProgram();                    // _0x105.._0x108
            CaseRef kase = sub.WhenTrue.AddCase("Case (%LT)", taeller, "note");      // _0x109 + _0x10a
            BranchRef eq100 = kase.Case("Case", "resource_counter",
                op => op.SetAttribute("inivalue", "100"));                           // _0x10b + _0x10c
            BranchRef eq1000 = kase.Case("Case", "resource_counter",
                op => op.SetAttribute("inivalue", "1000"));                          // _0x10d + _0x10e
            kase.Default().AddAction("Kip %P", custom.Output("Udgang"), "_0x23");    // _0x10f into the Else
            Project after = editor.ToProject();

            ProjectElement caseElement = after.Root.Descendants().Single(e => e.Tag == "program_case");
            Assert.Multiple(() =>
            {
                Assert.That(after.LastUniqueId, Is.EqualTo("_0x10f"),
                    "case+Else together, then 2 ids per value, then the action — the SDK burns nothing");
                Assert.That(caseElement.Id!.Value.ToToken(), Is.EqualTo("_0x10921"), "program_case id");
                Assert.That(caseElement.Id, Is.EqualTo(kase.Id), "returned handle addresses the case");
                Assert.That(caseElement.GetAttribute("name"), Is.EqualTo("Case (%LT)"), "%LT template name");
                Assert.That(caseElement.GetAttribute("icon"), Is.EqualTo("_0x7"), "program-case icon");
                Assert.That(caseElement.GetAttribute("link"), Is.EqualTo(taeller.Id!.Value.ToToken()),
                    "link = the switch criterion (always written, Fb-builder precedent)");

                Assert.That(caseElement.Children, Has.Length.EqualTo(3), "two values + one default");
                ProjectElement ca1 = caseElement.Children[0];
                ProjectElement ca2 = caseElement.Children[1];
                ProjectElement dflt = caseElement.Children[2];

                Assert.That(dflt.Tag, Is.EqualTo("actions"), "default container present exactly once, trailing");
                Assert.That(dflt.Id!.Value.ToToken(), Is.EqualTo("_0x10a66"),
                    "Else allocated 2nd (right after the case) though serialized last — §18 gate A");
                Assert.That(dflt.GetAttribute("name"), Is.EqualTo("Udføres når ingen case er lig case værdien"));
                Assert.That(dflt.GetAttribute("note"), Is.EqualTo("Udføres når ingen case er lig case værdien"));
                Assert.That(dflt.GetAttribute("type"), Is.EqualTo("_0x1"), "default branch type");
                Assert.That(dflt.Children.Single().GetAttribute("name"), Is.EqualTo("Kip %P"),
                    "Default() hands out the Else branch for actions");

                Assert.That(ca1.Tag, Is.EqualTo("case_action"));
                Assert.That(ca1.Id!.Value.ToToken(), Is.EqualTo("_0x10b66"), "first value after the Else pair");
                Assert.That(ca1.GetAttribute("name"), Is.EqualTo("Case"), "case_action display name");
                Assert.That(ca1.GetAttribute("icon"), Is.EqualTo("_0x8"), "case-action icon");
                Assert.That(ca1.GetAttribute("variable"), Is.EqualTo(taeller.Id!.Value.ToToken()),
                    "variable = the criterion (== program_case@link)");
                Assert.That(ca1.GetAttribute("value"), Is.EqualTo("_0x10c0c"), "value = the embedded operand");

                ProjectElement operand1 = ca1.Children[0];
                Assert.That(operand1.Tag, Is.EqualTo("resource_counter"), "counter criterion → counter operand");
                Assert.That(operand1.Id!.Value.ToToken(), Is.EqualTo("_0x10c0c"), "operand allocated right after");
                Assert.That(operand1.GetAttribute("inivalue"), Is.EqualTo("100"), "the case value string");
                Assert.That(operand1.GetAttribute("name"), Is.Null, "bare operand — no name/icon (fb08 shape)");

                Assert.That(ca2.Id!.Value.ToToken(), Is.EqualTo("_0x10d66"), "second value before the Else");
                Assert.That(ca2.Children[0].GetAttribute("inivalue"), Is.EqualTo("1000"));
            });

            // Actions added through the returned branch handles land after the operand (operand stays first child).
            eq100.AddAction("%P = ON", custom.Output("Udgang"), "_0xa");
            eq1000.AddAction("%P = OFF", custom.Output("Udgang"), "_0x14");
            ProjectElement recheck = editor.ToProject().Root.Descendants().Single(e => e.Tag == "program_case");
            Assert.Multiple(() =>
            {
                Assert.That(recheck.Children[0].Children[0].Tag, Is.EqualTo("resource_counter"), "operand-first");
                Assert.That(recheck.Children[0].Children[1].Tag, Is.EqualTo("action"), "actions follow the operand");
                Assert.That(recheck.Children[1].Children[1].GetAttribute("method"), Is.EqualTo("_0x14"));
            });
        }

        [TestCase("Tæller")]
        [TestCase("NyTypeForThisProject")]
        public async Task AddCase_CommandPersistsVendorTemplateAndNote(string switchName)
        {
            var app = new ProjectAppService(Settings);
            Project project = await ReplayOracle.LoadProject(Original);
            ProjectElement custom = project.Root.Descendants()
                .Single(e => e.Tag == "functionblock" && project.View(e).Name == "Custom blok");
            ElementId commandsId = custom.Descendants().First(e => e.Tag == "actions").Id!.Value;
            ElementId switchId = custom.Descendants()
                .First(e => project.View(e).Name == switchName).Id!.Value;
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(app.Commands.AddCase(session.Current!, commandsId, switchId));

            using var stream = new System.IO.MemoryStream();
            await app.Save(session.Current!, stream);
            Project reloaded = ProjectReader.Read(stream.ToArray());
            ProjectElement authoredCase = reloaded.Root.Descendants()
                .Single(e => e.Tag == "program_case" && e.GetAttribute("link") == switchId.ToToken());
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(authoredCase.GetAttribute("name"), Is.EqualTo("Case (%LT)"),
                    "the vendor template remains live so a switch rename re-renders the row");
                Assert.That(authoredCase.GetAttribute("note"),
                    Is.EqualTo("Udfører case når %P er lig case værdien"),
                    "the vendor note is part of the persisted program_case payload");
            });
        }

        // ---- Validator interplay: counter and enum case content validates clean ----

        // case_action@variable must be FB-local (fb-local-ref) and @value must point at the embedded literal
        // operand (inline-constant) — both variants (bare counter, fb08-shaped enum) satisfy the rules.
        [Test]
        public async Task CounterAndEnumCases_ValidateClean()
        {
            var app = new ProjectAppService(Settings);
            Project original = await ReplayOracle.LoadProject(Original);

            ProjectEditor editor = original.Edit();
            FunctionBlockRef custom = editor.Group("Stue").FunctionBlock("Custom blok");
            SubProgramRef sub = custom.Program().AddSubProgram();
            CaseRef counterCase = sub.WhenTrue.AddCase("Case (%LT)", custom.Input("Tæller"));
            counterCase.Case("Case", "resource_counter", op => op.SetAttribute("inivalue", "100"))
                .AddAction("%P = ON", custom.Output("Udgang"), "_0xa");
            CaseRef enumCase = sub.WhenFalse.AddCase("Case (%LT)", custom.Input("NyTypeForThisProject"));
            enumCase.Case("Case", editor.EnumDefinition("NyTypeForThisProject"), "Værdi2")
                .AddAction("%P = OFF", custom.Output("Udgang"), "_0x14");

            ProjectValidationResult validation = app.Validate(editor.ToProject());

            Assert.That(validation.IsValid, Is.True,
                "authored counter + enum cases validate clean; errors: " + string.Join(" | ", validation.Errors));
        }
    }
}
