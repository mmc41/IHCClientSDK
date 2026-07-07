using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The E2E authoring byte-fidelity track (E13) — reconstruct each authentic oracle from scratch through the
    /// public builder API and assert byte-identity via the <see cref="BuildFidelity"/> harness (BL-E0). BL-E1
    /// (Project0-Tomt via template <see cref="ProjectAppService.CreateNew"/>) is the reference pattern that
    /// establishes the harness; the content builds (BL-E2…E4) follow. Install-free (SDK-embedded catalog).
    /// </summary>
    public class AuthoringByteFidelityTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        // BL-E2: reconstructs Project1-SimpelWired.vis from scratch through the public builders and drives the
        // harness to byte-identity (88,321 = 88,321). The former "6-byte / enum-hoist id-allocation-order" gap was
        // reverse-engineered (tmp/experiments/out/A1..A3) to be neither an enum-hoist nor an InsertTransform issue,
        // but pure USER-ACTION ORDER: IHC Visual allocates ids at action time, and the vendor wired the three Kip
        // links (6 ids) right after the Kip block — before starting the Entré room — whereas the original test
        // created all links last, shifting every later element by −6. Placing the Kip links immediately after the
        // Kip block (output link first, matching the vendor's 193–198 order) closes the gap with zero engine changes.
        // Install-free (SDK-embedded catalog via RequireCatalog).
        [Test]
        public async Task BL_E2_ReproducesProject1SimpelWired_FromFluentBuilders()
        {
            ICatalog cat = BuildFidelity.RequireCatalog();
            // Creation 27th 14:58:31 → id1 = _0x1b0e3a1f; the default save re-stamps at 15:05:27 → id2/modified.
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 14, 58, 31, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, cat, clock);

            await BuildFidelity.AssertByteIdentical(app, "Project1-SimpelWired.vis",
                build: () =>
                {
                    ProjectEditor editor = app
                        .CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"))
                        .Edit();

                    GroupRef stue = editor.Group("Stue");
                    ProductRef fuga = stue.AddProduct(cat.Product("_0x2101"))
                        .Name("LK FUGA Tryk 2 tast").Locked().EnduserReport()
                        .Note("Tryk med 2 SL").Position("Ved dør")
                        .CableType("3x1,5mm2 NOIKJ").CableNumber("1")
                        .DocumentationTag("test1").PowerGroup("grupp1");
                    ResourceRef trykLeft = fuga.AddInput("Tryk (venstre)", i => i.Address("_0x1").CableColour("Rød").Note("note1"));
                    ResourceRef trykRight = fuga.AddInput("Tryk (højre)", i => i.Address("_0x2").CableColour("Grå").Note("note2"));

                    ProductRef lamp = stue.AddProduct(cat.Product("_0x2202"))
                        .Name("Lampeudtag").Locked().Note("note3").Position("I loft")
                        .CableType("3G1,5mm2 PVIKJ").CableNumber("2")
                        .DocumentationTag("test2").PowerGroup("gruppe2");
                    ResourceRef lampOut = lamp.AddOutput("Udgang", o => o.Address("_0x1").Backup());
                    lamp.AddScenes();

                    FunctionBlockRef kip = stue.AddFunctionBlock(cat.FunctionBlock("1.1.01")).Locked();
                    kip.Setting("Timer", t => t.Minutes(3));
                    // Vendor wired the Kip block before starting the Entré room — allocation order 193–198
                    // (output link first, then the two input links). See tmp/experiments/out/A1/findings.md.
                    editor.Link(kip.Output("Udgang"), lampOut);   // pair ① Kip.Udgang → Lamp.Udgang (193/194)
                    editor.Link(trykLeft, kip.Input("Kip"));      // pair ② Tryk (venstre) → Kip.Kip (195/196)
                    editor.Link(trykRight, kip.Input("Sluk"));    // pair ③ Tryk (højre) → Kip.Sluk (197/198)

                    GroupRef entre = editor.Group("Entré");
                    ProductRef stik = entre.AddProduct(cat.Product("_0x2201"))
                        .Name("Stikkontakt").Locked().Note("note4")
                        .Position("ved hoveddør og udestuedør - 2 stk, fælles tilslutning")
                        .CableType("5x1,5mm2 NOIKJ").CableNumber("3")
                        .DocumentationTag("test3").PowerGroup("gruppe3");
                    ResourceRef stikOut = stik.AddOutput("Udgang", o => o.Address("_0x9").Backup());
                    stik.AddScenes();

                    FunctionBlockRef pirFb = entre.AddFunctionBlock(cat.FunctionBlock("1.4.02")).Locked();
                    pirFb.Setting("Efterløb", t => t.Minutes(3).Backup());

                    ProductRef pir = entre.AddProduct(cat.Product("_0x210e"))
                        .Name("PIR").Locked().Note("PIR").Position("I loft")
                        .CableType("3x1,5mm2 NOIKJ").CableNumber("4")
                        .DocumentationTag("test5").PowerGroup("gruppe5");
                    ResourceRef pirPresence = pir.AddInput("Tilstedeværelses indikering", i => i.Address("_0x21"));

                    // Vendor wired the PIR block last — allocation order 529–532.
                    editor.Link(pirPresence, pirFb.Input("PIR"));  // pair ④ PIR presence → pirFb.PIR (529/530)
                    editor.Link(pirFb.Output("Udgang"), stikOut);  // pair ⑤ pirFb.Udgang → Stik.Udgang (531/532)

                    clock.SetUtcNow(new DateTimeOffset(2026, 6, 27, 15, 5, 27, TimeSpan.Zero));
                    return editor.ToProject();
                },
                ProjectSaveOptions.Default);
        }

        // M1 / V2 — repeated PIR (1.4.02) insert. IHC Visual allocates the 2nd block's three enum_definitions
        // (+their values) in document order but DISCARDS them because each duplicates the 1st block's enum by NAME
        // (these user enums carry no typeid), rewiring the references to the 1st block's defs and leaving the
        // permanent 9-id hole 407–415 (R-enum; tmp/experiments/out/B3/findings.md Part A). Live-authored in IHC
        // Visual 03.04.72.03 (B3 step02-pir2). Header stamps decode via PackedStamp (Day<<24|Hour<<16|Min<<8|Sec):
        // id1 _0x3101b23 → creation 3rd 16:27:35; id2 _0x3101c22 → save 3rd 16:28:34 (modified 16:28);
        // last_unique_id _0x2da (730). Install-free (SDK-embedded catalog).
        [Test]
        public async Task EnumDedup_RepeatedPirInsert_MatchesLiveOracle_ByteIdentical()
        {
            ICatalog cat = BuildFidelity.RequireCatalog();
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 3, 16, 27, 35, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, cat, clock);

            await BuildFidelity.AssertByteIdentical(app, "LiveAuthored/step02-pir2.vis",
                build: () =>
                {
                    ProjectEditor editor = app
                        .CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"))
                        .Edit();

                    editor.Group("Stue").AddFunctionBlock(cat.FunctionBlock("1.4.02"));    // 1st PIR: enums 82/85/88 hoisted fresh
                    editor.Group("Entré").AddFunctionBlock(cat.FunctionBlock("1.4.02"));   // 2nd PIR: enums dedup → hole 407–415

                    clock.SetUtcNow(new DateTimeOffset(2026, 7, 3, 16, 28, 34, TimeSpan.Zero));
                    return editor.ToProject();
                },
                ProjectSaveOptions.Default);
        }

        // M1 / V3 — "med logning" temperature-sensor products (_0x2125 ×2, _0x2139 ×1). Each product embeds a
        // "Logning" enum that dedups against the seed global "Logning" (_0x4747, ctr 71) — so it is allocated then
        // discarded, leaving one 2-id hole per insert (82/83, 94/95, 106/107) and every product resource_enum
        // pointing back at _0x4747 (R-enum; B3 findings Part B). Live-authored in IHC Visual 03.04.72.03
        // (B3 step06-luxtemp). id1 _0x3101e29 → creation 3rd 16:30:41; id2 _0x3102107 → save 3rd 16:33:07
        // (modified 16:33); last_unique_id _0x73 (115). Install-free (SDK-embedded catalog).
        [Test]
        public async Task EnumDedup_LogningProducts_MatchesLiveOracle_ByteIdentical()
        {
            ICatalog cat = BuildFidelity.RequireCatalog();
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 3, 16, 30, 41, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, cat, clock);

            await BuildFidelity.AssertByteIdentical(app, "LiveAuthored/step06-luxtemp.vis",
                build: () =>
                {
                    ProjectEditor editor = app
                        .CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"))
                        .Edit();

                    editor.Group("Stue").AddProduct(cat.Product("_0x2125"));     // 24809 "Temperatur sensor med logning": hole 82/83
                    editor.Group("Entré").AddProduct(cat.Product("_0x2125"));    // 24809 again: hole 94/95
                    editor.Group("Køkken").AddProduct(cat.Product("_0x2139"));   // 24813 "Lux / Temperatur sensor med logning": hole 106/107

                    clock.SetUtcNow(new DateTimeOffset(2026, 7, 3, 16, 33, 7, TimeSpan.Zero));
                    return editor.ToProject();
                },
                ProjectSaveOptions.Default);
        }

        // M3 / V4 — project2-CustomBlock: a catalog AutoProof FB + a hand-authored "Custom blok" (full resource
        // palette across settings/internalsettings/inputs/outputs + a program with event_power and nested
        // program_sub/conditions/actions + a user enum). Built INCREMENTALLY as M3's surface lands: seed knob (3.2),
        // AutoProof name-lookup (3.3), AddEnumDefinition (3.4), ProgramBuilder (3.5), reorder/delete replay (3.6).
        // id1 _0x3071722 → creation 3rd 07:23:34; id2 _0x3072b2d → save 3rd 07:43:45 (modified 7:43);
        // last_unique_id _0xf7 (247). Install-free (SDK-embedded catalog). See tmp/e3-divergence-log.md.
        // The canonical 18-type value palette IHC Visual offers, in the fixed order it materializes them (blueprint
        // §PALETTE). Reused for the settings/internalsettings/inputs/outputs fills (each with its own head variations).
        private static readonly (string Tag, string Name)[] ValuePalette =
        {
            ("resource_date", "Dato"), ("resource_flag", "Flag"), ("resource_humidity_level", "Fugtighed"),
            ("resource_holiday", "Helligdag"), ("resource_floating_point", "Kommatal"), ("kW", "kW"), ("kWh", "kWh"),
            ("resource_light", "Lys"), ("resource_light_level", "Lys niveau"), ("resource_counter", "Tæller"),
            ("resource_integer", "Tal"), ("resource_temperature", "Temperatur"), ("resource_time", "Tidspunkt"),
            ("resource_timer", "Timer"), ("resource_timertime", "Timertid"), ("resource_weekday", "Ugedag"),
            ("W", "W"), ("Wh", "Wh"),
        };

        [Test]
        public async Task BL_E3_ReproducesProject2CustomBlock_FromFluentBuilders()
        {
            ICatalog cat = BuildFidelity.RequireCatalog();
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 3, 7, 23, 34, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, cat, clock);

            await BuildFidelity.AssertByteIdentical(app, "project2-CustomBlock.vis",
                build: () =>
                {
                    // A-1 legacy seed layout: project2 allocates modules 65–67 before enums 68–80 (SeedIdLayout.ModulesFirst).
                    ProjectEditor editor = app
                        .CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"), SeedIdLayout.ModulesFirst)
                        .Edit();
                    GroupRef stue = editor.Group("Stue");

                    // 81–102: AutoProof catalog FB (user-saved .ifb, no master_type → name-keyed lookup).
                    stue.AddFunctionBlock(cat.FunctionBlockByName("AutoProof"));

                    // 103–111: empty "Custom blok" (fb.def skeleton: 5 containers + one empty program_simple).
                    FunctionBlockRef custom = stue.AddEmptyFunctionBlock(
                        cat.EmptyFunctionBlockTemplate, new DateOnly(2026, 7, 3), "Custom blok");

                    var settings = new Dictionary<string, ResourceRef>();
                    var internals = new Dictionary<string, ResourceRef>();
                    var inputs = new Dictionary<string, ResourceRef>();
                    var outputs = new Dictionary<string, ResourceRef>();
                    void Burn() => editor.DeleteById(custom.AddSetting("resource_flag", "burn").Id!.Value);   // add-then-delete: burns 1 counter

                    // 112/113: the two named pins.
                    ResourceRef indgang = custom.AddInput("Indgang");                       // resource_input 112
                    ResourceRef udgang = custom.AddOutput("Udgang");                        // resource_output 113

                    // 114–131: settings palette (18 value types, canonical order).
                    foreach ((string tag, string name) in ValuePalette)
                    {
                        settings[name] = custom.AddSetting(tag, name);
                    }

                    internals["Dato"] = custom.AddInternalVariable("resource_date", "Dato");  // 132

                    // 133: settings enum reusing the SEED global "Persienne tilstand" (def 68 _0x4447 / value 69 _0x4548).
                    settings["Persienne tilstand"] = custom.AddSetting("resource_enum", "Persienne tilstand",
                        e => e.SetAttribute("typedef", "_0x4447").SetAttribute("inivalue", "_0x4548"));

                    // 134–137: author the user enum "NyTypeForThisProject" (def + 3 values) on first use.
                    EnumDefinitionRef nyType = editor.AddEnumDefinition("NyTypeForThisProject", "Værdi1", "Værdi2", "Værdi3");
                    void WireNyType(ElementRef e) =>
                        e.SetAttribute("typedef", nyType.Typedef).SetAttribute("inivalue", nyType.InitialValue("Værdi1"));

                    settings["NyTypeForThisProject"] = custom.AddSetting("resource_enum", "NyTypeForThisProject", WireNyType);  // 138
                    Burn();                                                                  // 139
                    internals["NyTypeForThisProject"] = custom.AddInternalVariable("resource_enum", "NyTypeForThisProject", WireNyType);  // 140
                    internals["Flag"] = custom.AddInternalVariable("resource_flag", "Flag"); // 141 (written LAST → reordered)
                    internals["Fugtighed"] = custom.AddInternalVariable("resource_humidity_level", "Fugtighed");  // 142
                    internals["Helligdag"] = custom.AddInternalVariable("resource_holiday", "Helligdag");         // 143
                    internals["Kommatal"] = custom.AddInternalVariable("resource_floating_point", "Kommatal");   // 144
                    Burn();                                                                  // 145
                    internals["kW"] = custom.AddInternalVariable("kW", "kW");                // 146
                    internals["kWh"] = custom.AddInternalVariable("kWh", "kWh");             // 147
                    internals["Lys"] = custom.AddInternalVariable("resource_light", "Lys");  // 148
                    internals["Lys niveau"] = custom.AddInternalVariable("resource_light_level", "Lys niveau");  // 149
                    Burn();                                                                  // 150
                    internals["Tæller"] = custom.AddInternalVariable("resource_counter", "Tæller");      // 151
                    internals["Tal"] = custom.AddInternalVariable("resource_integer", "Tal");            // 152
                    internals["Temperatur"] = custom.AddInternalVariable("resource_temperature", "Temperatur");  // 153
                    internals["Tidspunkt"] = custom.AddInternalVariable("resource_time", "Tidspunkt");   // 154
                    internals["Timer"] = custom.AddInternalVariable("resource_timer", "Timer");          // 155
                    internals["Timertid"] = custom.AddInternalVariable("resource_timertime", "Timertid");// 156
                    internals["Ugedag"] = custom.AddInternalVariable("resource_weekday", "Ugedag");      // 157
                    internals["W"] = custom.AddInternalVariable("W", "W");                   // 158
                    internals["Wh"] = custom.AddInternalVariable("Wh", "Wh");                // 159

                    // inputs 160–179: Dato, then (after a burn) enum + the palette tail (no Dato head).
                    inputs["Dato"] = custom.AddInput("resource_date", "Dato");              // 160
                    Burn();                                                                  // 161
                    inputs["NyTypeForThisProject"] = custom.AddInput("resource_enum", "NyTypeForThisProject", WireNyType);  // 162
                    foreach ((string tag, string name) in ValuePalette.Skip(1))             // 163–179 (Flag..Wh)
                    {
                        inputs[name] = custom.AddInput(tag, name);
                    }

                    // outputs 180–203: Dato, enum, Flag..Kommatal, Kommatal(dup), kW, [burn], kWh..Tal (+2 Scenarie), [burn], Temperatur..Wh.
                    outputs["Dato"] = custom.AddOutput("resource_date", "Dato");            // 180
                    outputs["NyTypeForThisProject"] = custom.AddOutput("resource_enum", "NyTypeForThisProject", WireNyType);  // 181
                    outputs["Flag"] = custom.AddOutput("resource_flag", "Flag");            // 182
                    outputs["Fugtighed"] = custom.AddOutput("resource_humidity_level", "Fugtighed");  // 183
                    outputs["Helligdag"] = custom.AddOutput("resource_holiday", "Helligdag");         // 184
                    outputs["Kommatal"] = custom.AddOutput("resource_floating_point", "Kommatal");   // 185
                    custom.AddOutput("resource_floating_point", "Kommatal");                // 186 (duplicate, unreferenced)
                    outputs["kW"] = custom.AddOutput("kW", "kW");                            // 187
                    Burn();                                                                  // 188
                    outputs["kWh"] = custom.AddOutput("kWh", "kWh");                         // 189
                    outputs["Lys"] = custom.AddOutput("resource_light", "Lys");             // 190
                    outputs["Lys niveau"] = custom.AddOutput("resource_light_level", "Lys niveau");   // 191
                    custom.AddOutput("resource_scene", "Scenarie");                         // 192
                    custom.AddOutput("resource_scene", "Scenarie");                         // 193 (duplicate)
                    outputs["Tæller"] = custom.AddOutput("resource_counter", "Tæller");     // 194
                    outputs["Tal"] = custom.AddOutput("resource_integer", "Tal");           // 195
                    Burn();                                                                  // 196
                    outputs["Temperatur"] = custom.AddOutput("resource_temperature", "Temperatur");   // 197
                    outputs["Tidspunkt"] = custom.AddOutput("resource_time", "Tidspunkt");  // 198
                    outputs["Timer"] = custom.AddOutput("resource_timer", "Timer");         // 199
                    outputs["Timertid"] = custom.AddOutput("resource_timertime", "Timertid");// 200
                    outputs["Ugedag"] = custom.AddOutput("resource_weekday", "Ugedag");     // 201
                    outputs["W"] = custom.AddOutput("W", "W");                               // 202
                    outputs["Wh"] = custom.AddOutput("Wh", "Wh");                            // 203

                    // 204–247: the program. event_power + 20 events + nested subs + conditions + actions + embedded enum.
                    // Leaf note strings are the vendor's method-specific labels (transcribed verbatim from the oracle,
                    // incl. the vendor's "forskelling" spelling at 242).
                    ProgramBuilder prog = custom.Program();
                    prog.AddPowerEvent("Powerup", "Start program ved Powerup");             // 204
                    SubProgramRef outer = prog.AddSubProgram();                             // 205–208 (sub/conds/sande/falske)
                    prog.AddEvent("%P -> ON", indgang, "_0xa", note: "Start program når %P skifter til ON");                    // 209
                    prog.AddEvent("System dato -> %P", inputs["Dato"], "_0xa", note: "Start program når System dato skifter til %P");  // 210
                    prog.AddEvent("%P skifter tilstand", inputs["NyTypeForThisProject"], "_0x96", note: "Start program når %P skifter tilstand");  // 211
                    prog.AddEvent("%P NOT -> %S", inputs["Flag"], "_0x28", indgang, "Start program når %P skifter til NOT %S");  // 212 (→ reordered to idx 2)
                    prog.AddEvent("%P bliver ændret", inputs["Fugtighed"], "_0x96", note: "Start program når %P skifter værdi");     // 213
                    prog.AddEvent("%P bliver tilskrevet", inputs["Helligdag"], "_0x9b", note: "Start program når %P bliver tilskrevet");  // 214
                    prog.AddEvent("%P bliver tilskrevet", inputs["Kommatal"], "_0x9b", note: "Start program når %P bliver tilskrevet");   // 215
                    prog.AddEvent("%P -> %S", inputs["kW"], "_0x1e", outputs["kW"], "Start program når %P skifter til %S");       // 216
                    prog.AddEvent("%P -> %S", inputs["kWh"], "_0x1e", internals["kWh"], "Start program når %P skifter til %S");   // 217
                    prog.AddEvent("%P bliver ændret", inputs["Lys"], "_0x96", note: "Start program når %P skifter værdi");           // 218
                    prog.AddEvent("%P -> %S", inputs["Lys niveau"], "_0x1e", internals["Lys niveau"], "Start program når %P skifter til %S");  // 219
                    prog.AddEvent("%P -> 0", inputs["Tæller"], "_0xa", note: "Start program når %P skifter til 0");              // 220
                    prog.AddEvent("%P -> %S", inputs["Tal"], "_0x1e", settings["Tæller"], "Start program når %P skifter til %S");  // 221
                    prog.AddEvent("%P bliver tilskrevet", inputs["Temperatur"], "_0x9b", note: "Start program når %P bliver tilskrevet");  // 222
                    prog.AddEvent("%P -> %S", inputs["Tidspunkt"], "_0x1e", settings["Tidspunkt"], "Start program når %P skifter til %S");  // 223
                    prog.AddEvent("%P bliver tilskrevet", inputs["Timer"], "_0x9b", note: "Start program når %P bliver tilskrevet");   // 224
                    prog.AddEvent("%P bliver tilskrevet", inputs["Timertid"], "_0x9b", note: "Start program når %P bliver tilskrevet"); // 225
                    prog.AddEvent("System ugedag -> %P", inputs["Ugedag"], "_0x5", note: "Start program når System ugedag skifter til %P");  // 226
                    prog.AddEvent("%P -> %S", inputs["W"], "_0x1e", settings["W"], "Start program når %P skifter til %S");         // 227
                    prog.AddEvent("%P -> %S", inputs["Wh"], "_0x1e", outputs["Wh"], "Start program når %P skifter til %S");        // 228
                    SubProgramRef falseSub = outer.WhenFalse.AddSubProgram();               // 229–232 (created before the true sub)
                    SubProgramRef trueSub = outer.WhenTrue.AddSubProgram();                 // 233–236
                    trueSub.WhenTrue.AddAction("%P = ON", udgang, "_0xa", note: "Sætter %P til ON");                            // 237
                    Burn();                                                                  // 238
                    outer.AddCondition("%P = OFF", settings["Flag"], "_0x14", note: "Betingelse at %P er OFF");                 // 239
                    outer.AddCondition("%P = ON", settings["Helligdag"], "_0xa", note: "Betingelse at %P er ON");              // 240
                    outer.AddCondition("%P = %S", settings["Lys niveau"], "_0x1e", inputs["Lys niveau"], "Betingelse at %P er samme som %S");  // 241 (→ reordered to idx 0)
                    ConditionRef cond = outer.AddCondition("%P <> %S", settings["NyTypeForThisProject"], "_0x28", note: "Betingelse at %P er forskelling fra %S");  // 242
                    cond.AddEnumOperand("Enumerator", nyType, "Værdi2");                    // 243 (wires 242.link2)
                    outer.AddCondition("%P = 0", internals["Timer"], "_0xa", note: "Betingelse at %P er 0");                   // 244
                    falseSub.WhenFalse.AddAction("%P = OFF", udgang, "_0x14", note: "Sætter %P til OFF");                       // 245
                    falseSub.WhenTrue.AddAction("%P = %S", udgang, "_0x1e", indgang, "Sætter %P til samme værdi som %S");       // 246
                    trueSub.WhenFalse.AddAction("Kip %P", udgang, "_0x23", note: "Sætter %P til modsat værdi af aktuel værdi"); // 247

                    // Intra-container reorders (creation order ≠ document order). Peek a snapshot to resolve ids by
                    // creation position, then move on the live editor (ids stable, a move allocates nothing — R4).
                    ProjectElement fbSnap = editor.ToProject().Root.Descendants()
                        .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Custom blok");
                    ProjectElement intSet = fbSnap.FindChild("internalsettings")!;
                    ProjectElement programSimple = fbSnap.FindChild("programs")!.FindChild("program_simple")!;
                    ProjectElement events = programSimple.FindChild("events")!;
                    ProjectElement outerConds = programSimple.FindChild("actions")!
                        .Children.First(c => c.Tag == "program_sub").FindChild("conditions")!;

                    editor.MoveSubtree(internals["Flag"].Id!.Value, intSet.Id!.Value, index: null);        // Flag → end of internalsettings
                    editor.MoveSubtree(events.Children[4].Id!.Value, events.Id!.Value, index: 2);          // "%P NOT -> %S" → after event 209 (idx 2)
                    editor.MoveSubtree(outerConds.Children[2].Id!.Value, outerConds.Id!.Value, index: 0);  // "%P = %S" → idx 0

                    clock.SetUtcNow(new DateTimeOffset(2026, 7, 3, 7, 43, 45, TimeSpan.Zero));
                    return editor.ToProject();
                },
                ProjectSaveOptions.Default);
        }

        // M4 / V5 — project3-KompleksWired: the broadest oracle (1332 ids, last_unique_id _0x56c=1388, 2499 lines).
        // Replays the full A4 project3 action script in allocation (user-action) order: 13 datalinie/airlink products,
        // two "med logning" enum-dedup inserts, 6 catalog FBs (three carrying hoisted enums), a user global enum
        // "TestEnum" (0 values), three "Tom blok" empty FBs (one with 9 internal variables), 3 follow-links, and a
        // trailing new room "Lokalitet". id1 _0x1d0e2923 → creation 29th 14:41:35; id2 _0x1d143707 → save 29th
        // 20:55:07 (modified 2026-06-29 20:55); last_unique_id _0x56c (1388). Install-free (SDK-embedded catalog). See tmp/e4-divergence-log.md.
        // Drove to byte-identity (236,518 bytes) via the M4 first-divergence loop; gaps closed en route: NormalizeTokens
        // (airlink device_type "_0x080a"→"_0x80a"), the _0x4306 catalog-collision pick, InsertStamps (airlink
        // serialnumber + RS-485 channel_id null tokens "_0x0"), NormalizeEnums (s0 kWh accessibility typo
        // "readwrite"→"read-write", elided at default), and four RS-485 error-state icons.
        [Test]
        public async Task BL_E4_ReproducesProject3KompleksWired_FromFluentBuilders()
        {
            ICatalog cat = BuildFidelity.RequireCatalog();
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 29, 14, 41, 35, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, cat, clock);

            await BuildFidelity.AssertByteIdentical(app, "project3-KompleksWired.vis",
                build: () =>
                {
                    // Default EnumsFirst seed (enums 65–77 before modules 78–80) — pinned by BL-E0. Rooms 2–10 keep
                    // their seed names; room 1 "Stue" is renamed to "Stue & Køkken \"åben\"" + note (TODO 4.3, an
                    // attribute edit — allocates nothing, R3).
                    ProjectEditor editor = app
                        .CreateNew(new ProjectDetails("Morten Christensen", "Morten", "Danmark"))
                        .Edit();

                    GroupRef stue = editor.Group("Stue").Name("Stue & Køkken \"åben\"").Note("note æøå ÆØÅ ÄÖ é©®");
                    GroupRef entre = editor.Group("Entré");
                    GroupRef kokken = editor.Group("Køkken");
                    GroupRef sove = editor.Group("Soveværelse");
                    GroupRef vaerelse = editor.Group("Værelse");
                    GroupRef bad = editor.Group("Bad");
                    GroupRef bryggers = editor.Group("Bryggers");
                    GroupRef garage = editor.Group("Garage");
                    GroupRef kaelder = editor.Group("Kælder");

                    // ── Datalinie + airlink products, allocation (user-action) order ──
                    ProductRef fuga = stue.AddProduct(cat.Product("_0x2101"));  // 81–83  LK FUGA Tryk 2 tast
                    stue.AddProduct(cat.Product("_0x2202"));      // 84–86  Lampeudtag
                    stue.AddProduct(cat.Product("_0x107"));       // 87–88  Diode
                    entre.AddProduct(cat.Product("_0x210e"));     // 89–90  PIR
                    entre.AddProduct(cat.Product("_0x2201"));     // 91–93  Stikkontakt
                    kokken.AddProduct(cat.Product("_0x2125"));    // 94–105 Temperatur sensor (enum-dedup hole 95/96)
                    kokken.AddProduct(cat.Product("_0x2139"));    // 106–116 Lux/Temperatur (enum-dedup hole 107/108)
                    sove.AddProduct(cat.Product("_0x2303"));      // 117–119 Dimmer touch
                    vaerelse.AddProduct(cat.Product("_0x4101"));  // 120–122 airlink Tryk 2 tast
                    vaerelse.AddProduct(cat.Product("_0x4202"));  // 123–125 airlink Lampeudtag
                    // _0x4306 repeats across two catalog menu entries (03#Dimmer vs 06#1-10v Converter, §9.3.3);
                    // pick the plain "Dimmer Universal" descriptor the user chose (not the converter variant).
                    sove.AddProduct(cat.Products.First(
                        p => p.ProductIdentifier == "_0x4306" && p.DisplayName == "Dimmer Universal"));  // 126–138 airlink Dimmer Universal

                    // ── Catalog function blocks (1.2.04 + 1.4.02 carry hoisted enums) ──
                    stue.AddFunctionBlock(cat.FunctionBlock("1.1.01"));     // 139–244  Kip tænd sluk
                    sove.AddFunctionBlock(cat.FunctionBlock("1.2.04"));     // 245–740  Trådløs/Bus lysdæmper
                    entre.AddFunctionBlock(cat.FunctionBlock("1.4.02"));    // 741–1065 PIR styring
                    kokken.AddFunctionBlock(cat.FunctionBlock("2.1.01"));   // 1066–1135 Ur, 1 tidspunkt
                    FunctionBlockRef andBlock = vaerelse.AddFunctionBlock(cat.FunctionBlock("4.1.01")); // 1136–1208 AND (Og-blok)
                    bryggers.AddFunctionBlock(cat.FunctionBlock("4.1.04")); // 1209–1296 Driftstimetæller

                    // ── User global enum, 0 values ──
                    editor.AddEnumDefinition("TestEnum æøå äö \"x\"");      // 1297

                    // ── Tom blok #1 → Garage ──
                    garage.AddEmptyFunctionBlock(cat.EmptyFunctionBlockTemplate, new DateOnly(2026, 6, 29), "Tom blok");  // 1298–1306

                    // ── Exotic products → Kælder ──
                    kaelder.AddProduct(cat.Product("_0x2313"));   // 1307–1318 s0 device
                    kaelder.AddProduct(cat.Product("_0x4409"));   // 1319–1354 rs485 LED dimmer 2 kanaler

                    // ── Tom blok #2 → Bad ──
                    bad.AddEmptyFunctionBlock(cat.EmptyFunctionBlockTemplate, new DateOnly(2026, 6, 29), "Tom blok");     // 1355–1363

                    // 1364–1369: 3 follow-links (R2, from-half first). FUGA Tryk buttons → AND-block inputs
                    // ("Indgang 1/2/3", ctrs 1138–1140); the from-half lands under the FUGA input, the to-half
                    // under the AND input (oracle _0x5542d/_0x5552c …).
                    editor.Link(fuga.Input("Tryk (venstre)"), andBlock.Input("Indgang 1"));  // 1364/1365
                    editor.Link(fuga.Input("Tryk (venstre)"), andBlock.Input("Indgang 2"));  // 1366/1367
                    editor.Link(fuga.Input("Tryk (højre)"), andBlock.Input("Indgang 3"));    // 1368/1369

                    // ── Tom blok #3 → Bryggers, then 9 internal variables (creation order) ──
                    FunctionBlockRef tom3 = bryggers.AddEmptyFunctionBlock(
                        cat.EmptyFunctionBlockTemplate, new DateOnly(2026, 6, 29), "Tom blok");                           // 1370–1378
                    tom3.AddInternalVariable("resource_weekday", "Ugedag");          // 1379
                    tom3.AddInternalVariable("resource_integer", "Tal");             // 1380
                    tom3.AddInternalVariable("resource_flag", "Flag");               // 1381
                    tom3.AddInternalVariable("resource_counter", "Tæller");          // 1382
                    tom3.AddInternalVariable("resource_date", "Dato");               // 1383
                    tom3.AddInternalVariable("resource_timer", "Timer");             // 1384
                    tom3.AddInternalVariable("resource_floating_point", "Kommatal"); // 1385
                    tom3.AddInternalVariable("resource_humidity_level", "Fugtighed");// 1386
                    tom3.AddInternalVariable("resource_temperature", "Temperatur");  // 1387

                    // ── New room, added last ──
                    editor.Group("Lokalitet");                    // 1388

                    clock.SetUtcNow(new DateTimeOffset(2026, 6, 29, 20, 55, 7, TimeSpan.Zero));
                    return editor.ToProject();
                },
                ProjectSaveOptions.Default);
        }

        [Test]
        public async Task BL_E0_Harness_ReproducesProject0Tomt_ViaCreateNew()
        {
            ICatalog catalog = BuildFidelity.RequireCatalog();
            // Creation 27th 16:05:51 → id1; the default save advances 14s to 16:06:05 → id2/modified.
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 27, 16, 5, 51, TimeSpan.Zero));
            var app = new ProjectAppService(Settings, catalog, clock);

            await BuildFidelity.AssertByteIdentical(app, "Project0-Tomt.vis",
                build: () =>
                {
                    Project project = app.CreateNew(new ProjectDetails(
                        Programmer: "Morten Christensen", InstallerName: "Morten", InstallerCountry: "Danmark"));
                    clock.SetUtcNow(new DateTimeOffset(2026, 6, 27, 16, 6, 5, TimeSpan.Zero));
                    return project;
                },
                ProjectSaveOptions.Default);
        }
    }
}
