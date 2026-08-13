namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The condition-logic-groups authoring byte-fidelity gate (G2, US-029) for
    /// <see cref="ConditionsGroupRef"/> against the authentic vendor oracle
    /// <c>project2-CustomBlock-logicgroups.vis</c> (IHC Visual 03.04.72.03 after one recorded program-authoring
    /// sequence on <c>project2-CustomBlock.vis</c>, single save — the corpus's first NOT condition
    /// (<c>method="_0x28"</c>) and first nested logic group). The SDK loads the original, reproduces the load-time
    /// enum re-hoist (<see cref="ProjectEditor.NormalizeCatalogEnums"/> — Action 0, pinned by the
    /// <c>project2-control-save.vis</c> baseline), replays the sequence in allocation order — sub-program skeleton
    /// (<c>_0x105</c>–<c>_0x108</c>), plain condition <c>%P = ON</c> (<c>_0x109</c>, method <c>_0xa</c>), NOT
    /// condition <c>%P &lt;&gt; %S</c> (<c>_0x10a</c>, method <c>_0x28</c>), empty nested logic group
    /// (<c>_0x10b</c>, Betingelser decoration verbatim), action <c>Kip %P</c> (<c>_0x10c</c>, method <c>_0x23</c>)
    /// in the true branch, then the OR toggle (literal <c>conditions@type="or"</c> and nothing else) — restamps to
    /// the oracle's clock and asserts byte-identity. Pinned vendor semantics (ENG2-B1): the persisted
    /// <c>condition/action@name</c> is the <c>%P</c>/<c>%S</c> TEMPLATE (binding is <c>method</c> +
    /// <c>link1</c>/<c>link2</c>), not the popup's substituted display label. Catalog-free.
    /// </summary>
    public class LogicGroupReplayByteFidelityTests
    {
        private const string Original = "project2-CustomBlock.vis";
        private const string LogicGroupsOracle = "project2-CustomBlock-logicgroups.vis";
        private const string ControlSaveOracle = "project2-control-save.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        // ---- Control baseline: Action 0 alone reproduces the vendor's bare load→save of project2 ----

        // Pins that project2's replay baseline is the control-save bytes (catalog-enum re-hoist Persienne/Logning,
        // _0xf7 -> _0x104, gaps 68-80), NOT the base file — the shared Action 0 of the -refdelete/-logicgroups/-case
        // replays. id2 _0xc0d0037 decodes to day 12 / hour 13 / min 0 / sec 55.
        [Test]
        public async Task ActionZeroAlone_ReplaysProject2ControlSaveOracle_ByteIdentical() =>
            await ReplayOracle.AssertReplaysByteIdentical(Original, ControlSaveOracle,
                new DateTimeOffset(2026, 7, 12, 13, 0, 55, TimeSpan.Zero),
                editor => { });   // the harness's Action 0 is the entire replay

        // ---- Full replay: Action 0 → skeleton → conditions → nested group → action → OR → byte-identity ----

        [Test]
        public async Task NestedOrNotConditions_ReplaysCustomBlockLogicOracle_ByteIdentical() =>
            // id2 _0xc0e2338 decodes to day 12 / hour 14 / min 35 / sec 56; <modified> is minute-precision (14:35).
            await ReplayOracle.AssertReplaysByteIdentical(Original, LogicGroupsOracle,
                new DateTimeOffset(2026, 7, 12, 14, 35, 56, TimeSpan.Zero),
                editor => ApplyLogicGroupGestures(editor));

        // The recorded L1–L6 gesture sequence shared by the byte replay and the composition test; returns the
        // nested logic-group handle the composition test asserts on. Row notes are the vendor's method-specific
        // vocabulary strings, transcribed verbatim from the oracle (incl. the vendor's "forskelling" spelling —
        // same strings the drag popup stamps in the V4 oracle).
        private static ConditionsGroupRef ApplyLogicGroupGestures(ProjectEditor editor)
        {
            FunctionBlockRef custom = editor.Group("Stue").FunctionBlock("Custom blok");
            SubProgramRef sub = custom.Program().AddSubProgram();                    // L1: _0x105.._0x108
            sub.Conditions.AddCondition("%P = ON", custom.Input("Flag"), "_0xa",
                note: "Betingelse at %P er ON");                                     // L2: _0x109
            sub.Conditions.AddCondition("%P <> %S", custom.Input("NyTypeForThisProject"), "_0x28",
                custom.Setting("NyTypeForThisProject"),
                "Betingelse at %P er forskelling fra %S");                           // L3 (NOT): _0x10a
            ConditionsGroupRef nested = sub.Conditions.AddConditionGroup();          // L4: _0x10b, stays empty
            sub.WhenTrue.AddAction("Kip %P", custom.Output("Udgang"), "_0x23",
                note: "Sætter %P til modsat værdi af aktuel værdi");                 // L5: _0x10c
            sub.Conditions.Or();                                                     // L6: type="or" literal
            return nested;
        }

        // ---- Composition isolation: gesture-order allocation, pinned row shapes, no burn ----

        [Test]
        public async Task ConditionGroupAuthoring_AllocatesInGestureOrder_PinnedShapes()
        {
            Project original = await ReplayOracle.LoadProject(Original);

            ProjectEditor editor = original.Edit();
            editor.NormalizeCatalogEnums();
            FunctionBlockRef custom = editor.Group("Stue").FunctionBlock("Custom blok");
            Assert.Multiple(() =>
            {
                Assert.That(custom.Input("Flag").Id!.Value.ToToken(), Is.EqualTo("_0xa30a"),
                    "value-typed input pin resolves (container-scoped, not resource_input-only)");
                Assert.That(custom.Input("NyTypeForThisProject").Id!.Value.ToToken(), Is.EqualTo("_0xa20f"),
                    "enum input pin resolves");
                Assert.That(custom.Setting("NyTypeForThisProject").Id!.Value.ToToken(), Is.EqualTo("_0x8a0f"),
                    "settings variable resolves (the NOT condition's %S operand)");
                Assert.That(custom.Output("Udgang").Id!.Value.ToToken(), Is.EqualTo("_0x7112"),
                    "output pin resolves");
            });

            ConditionsGroupRef nested = ApplyLogicGroupGestures(editor);
            Project after = editor.ToProject();

            ProjectElement subElement = after.Root.Descendants().Single(e => e.GetAttribute("id") == "_0x1051f");
            ProjectElement conditions = subElement.Children[0];
            ProjectElement sande = subElement.Children[1];
            ProjectElement falske = subElement.Children[2];
            Assert.Multiple(() =>
            {
                Assert.That(after.LastUniqueId, Is.EqualTo("_0x10c"), "+8 ids in gesture order, no burn");

                Assert.That(conditions.Id!.Value.ToToken(), Is.EqualTo("_0x10665"), "skeleton: conditions second");
                Assert.That(conditions.GetAttribute("type"), Is.EqualTo("or"), "OR toggle = literal type attr");
                Assert.That(sande.Id!.Value.ToToken(), Is.EqualTo("_0x10766"), "skeleton: true branch third");
                Assert.That(sande.GetAttribute("type"), Is.EqualTo("_0x1"), "true branch keeps its vendor type");
                Assert.That(falske.Id!.Value.ToToken(), Is.EqualTo("_0x10866"), "skeleton: false branch fourth");
                Assert.That(falske.Children.IsEmpty, Is.True, "false branch stays empty");

                ProjectElement plain = conditions.Children[0];
                Assert.That(plain.Id!.Value.ToToken(), Is.EqualTo("_0x109c9"), "plain condition fifth");
                Assert.That(plain.GetAttribute("name"), Is.EqualTo("%P = ON"), "name is the %P template");
                Assert.That(plain.GetAttribute("method"), Is.EqualTo("_0xa"), "= ON method token");
                Assert.That(plain.GetAttribute("link1"), Is.EqualTo("_0xa30a"), "wired to the Flag input");
                Assert.That(plain.GetAttribute("link2"), Is.Null, "no second operand");
                Assert.That(plain.GetAttribute("note"), Is.EqualTo("Betingelse at %P er ON"),
                    "the method-specific vocabulary note");

                ProjectElement not = conditions.Children[1];
                Assert.That(not.Id!.Value.ToToken(), Is.EqualTo("_0x10ac9"), "NOT condition sixth");
                Assert.That(not.GetAttribute("name"), Is.EqualTo("%P <> %S"), "name is the %P/%S template");
                Assert.That(not.GetAttribute("method"), Is.EqualTo("_0x28"), "NOT method token");
                Assert.That(not.GetAttribute("link1"), Is.EqualTo("_0xa20f"), "wired to the enum input");
                Assert.That(not.GetAttribute("link2"), Is.EqualTo("_0x8a0f"), "%S = the settings variable");

                ProjectElement group = conditions.Children[2];
                Assert.That(group.Id!.Value.ToToken(), Is.EqualTo("_0x10b65"), "nested group seventh");
                Assert.That(group.Id, Is.EqualTo(nested.Id), "returned handle addresses the nested group");
                Assert.That(group.GetAttribute("name"), Is.EqualTo("Betingelser"),
                    "nested logic group reuses the Betingelser decoration verbatim (no 'Logikgruppe' strings)");
                Assert.That(group.GetAttribute("icon"), Is.EqualTo("_0x16"), "conditions icon");
                Assert.That(group.GetAttribute("note"), Is.EqualTo("Gruppering af betingelser til logisk test"),
                    "conditions note");
                Assert.That(group.GetAttribute("type"), Is.Null, "nested group stays AND (attr omitted)");
                Assert.That(group.Children.IsEmpty, Is.True, "nested group authored empty");

                ProjectElement action = sande.Children[0];
                Assert.That(action.Id!.Value.ToToken(), Is.EqualTo("_0x10cca"), "action eighth (last)");
                Assert.That(action.GetAttribute("name"), Is.EqualTo("Kip %P"), "action name template");
                Assert.That(action.GetAttribute("method"), Is.EqualTo("_0x23"), "Kip method token");
                Assert.That(action.GetAttribute("link1"), Is.EqualTo("_0x7112"), "wired to the Udgang output");
            });
        }

        // ---- Or/And toggle: only the type attribute moves, and And() restores the omitted default ----

        [Test]
        public async Task OrToggle_WritesOnlyTypeOr_AndRestoresOmittedDefault()
        {
            Project original = await ReplayOracle.LoadProject(Original);

            ProjectEditor editor = original.Edit();
            SubProgramRef sub = editor.Group("Stue").FunctionBlock("Custom blok").Program().AddSubProgram();
            ElementId groupId = sub.Conditions.Id;

            sub.Conditions.Or();
            ProjectElement orShape = editor.ToProject().FindById(groupId)!;
            sub.Conditions.And();
            ProjectElement andShape = editor.ToProject().FindById(groupId)!;

            Assert.Multiple(() =>
            {
                Assert.That(orShape.GetAttribute("type"), Is.EqualTo("or"), "Or() persists the literal token");
                Assert.That(orShape.GetAttribute("name"), Is.EqualTo("Betingelser"), "decoration untouched");
                Assert.That(orShape.GetAttribute("icon"), Is.EqualTo("_0x16"), "decoration untouched");
                Assert.That(andShape.GetAttribute("type"), Is.Null,
                    "And() returns to the DTD default, which the canonicalizer re-omits");
            });
        }

        // ---- Validator interplay: NOT condition + nested group + embedded enum operand → clean ----

        [Test]
        public async Task NotConditionInNestedGroupWithEnumOperand_ValidatesClean()
        {
            var app = new ProjectAppService(Settings);
            Project original = await ReplayOracle.LoadProject(Original);

            ProjectEditor editor = original.Edit();
            FunctionBlockRef custom = editor.Group("Stue").FunctionBlock("Custom blok");
            SubProgramRef sub = custom.Program().AddSubProgram();
            sub.Conditions.Or();
            ConditionsGroupRef nested = sub.Conditions.AddConditionGroup();
            ConditionRef not = nested.AddCondition("%P <> %S", custom.Input("NyTypeForThisProject"), "_0x28");
            not.AddEnumOperand("Enumerator", editor.EnumDefinition("NyTypeForThisProject"), "Værdi2");

            ProjectValidationResult validation = app.Validate(editor.ToProject());

            Assert.That(validation.IsValid, Is.True,
                "NOT-method condition inside a nested OR group with an embedded enum operand validates clean; "
                + "errors: " + string.Join(" | ", validation.Errors));
        }
    }
}
