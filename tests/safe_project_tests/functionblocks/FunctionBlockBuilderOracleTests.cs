using System;
using System.Collections.Immutable;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Component tests for <see cref="FunctionBlockDefinitionBuilder"/> and <see cref="FbProgramBuilder"/>: author each
    /// block from code and assert the result reduces to the same canonical block the matching synthetic
    /// <c>FunctionBlocks\*.ifb</c> oracle yields (<see cref="SyntheticOracle.AssertMatchesOracle"/>). Catalog-free /
    /// install-dir-free — the design contract "a builder is a code-authored CatalogReader", proven directly.
    /// </summary>
    public class FunctionBlockBuilderOracleTests
    {
        private const string Dir = "functionblocks/synthetic/";

        [Test]
        public void Author_fb01_Toggle_MatchesOracle()
        {
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("9.1.01", "a", "Toggle lamp")
                .VendorMaster()
                .MasterProgrammer("Morten Christensen")
                .MasterDate(new DateOnly(2026, 2, 1))
                .Locked()
                .Attribute("icon", "_0xe")
                .Note("9.1.01.a. Toggle lamp\r\n\r\nSyntetisk demoblok til afprøvning. Tryk på %P for at tænde " +
                      "eller slukke udgangen.\r\nMarkér linjen og tryk \"F1\" for at se en beskrivelse.")
                .InputsNote("Variablene i denne gruppering er indgange til blokken")
                .OutputsNote("Variablene i denne gruppering er udgange fra blokken");

            FbResourceHandle push = builder.AddInput("resource_input", "Tryk",
                i => i.Note("Aktivér for at skifte udgangen.\r\n(Udfyldes af montøren)"));
            FbResourceHandle forceOff = builder.AddInput("resource_input", "Tvangssluk", i => i.Note("Tvinger udgangen slukket."));
            FbResourceHandle lamp = builder.AddOutput("resource_output", "Lampe", o => o.Note("Tilsluttes en lampe eller stikkontakt."));
            FbResourceHandle onPulse = builder.AddOutput("resource_output", "ON puls", o => o.Note("Kort puls når udgangen tændes."));
            FbResourceHandle autoOff = builder.AddSetting("resource_timer", "Sluktimer", r => r.TimerHms(0, 5, 0));
            builder.AddInternalVariable("resource_timer", "Afvisningstid", r => r.TimerHms(0, 0, 0, 200));

            FbProgramBuilder skift = builder.Program("Skift");
            skift.AddEvent("%P -> ON", push, "_0xa", note: "Start når %P skifter til ON");
            FbSubProgramRef sub = skift.AddSubProgram();
            sub.AddCondition("%P = OFF", lamp, "_0x14", note: "Betingelse: %P er slukket");
            sub.WhenTrue.AddAction("%P = ON", lamp, "_0xa", note: "Tænder udgangen");
            sub.WhenTrue.AddAction("%P = ON", onPulse, "_0xa", note: "Afgiver ON-puls");
            sub.WhenFalse.AddAction("%P = OFF", lamp, "_0x14", note: "Slukker udgangen");
            sub.WhenFalse.AddAction("%P = %S", lamp, "_0x1e", link2: onPulse, note: "Sætter %P lig med %S");
            skift.AddAction("Aktivér nedtælling på %P", autoOff, "_0xbe", note: "Starter sluktimeren");

            FbProgramBuilder force = builder.Program("Tvangssluk");
            force.AddEvent("%P -> ON", forceOff, "_0xa", note: "Start når %P skifter til ON");
            force.AddAction("%P = OFF", lamp, "_0x14", note: "Slukker udgangen");

            builder.Grammar(CatalogGrammar.Create(new[]
            {
                OracleGrammars.FbRoot, OracleGrammars.Container("inputs"), OracleGrammars.FbPin("resource_input"),
                OracleGrammars.Container("outputs"), OracleGrammars.FbPin("resource_output"),
                OracleGrammars.Container("settings"), OracleGrammars.ResourceTimerFb,
                OracleGrammars.Container("internalsettings"), OracleGrammars.Container("programs"),
                OracleGrammars.Container("program_simple"), OracleGrammars.Container("events"),
                OracleGrammars.ProgramLeaf("event"), OracleGrammars.FbActions,
                OracleGrammars.Container("program_sub"), OracleGrammars.FbConditions,
                OracleGrammars.ProgramLeaf("condition"), OracleGrammars.ProgramLeaf("action"),
            }));
            FunctionBlockDefinition block = builder.Build();
            SyntheticOracle.AssertMatchesOracle(block.Body, Dir + "synthetic_fb01_toggle.ifb");
            SyntheticOracle.AssertWritesOracleBytes(block, Dir + "synthetic_fb01_toggle.ifb",
                "_0x5128", "_0x5223", "_0x5311", "_0x5411", "_0x5524", "_0x5612", "_0x5712", "_0x5825", "_0x5910",
                "_0x5a29", "_0x5b10", "_0x5c26", "_0x5d1e", "_0x5e64", "_0x5fc8", "_0x6066", "_0x611f", "_0x6265",
                "_0x63c9", "_0x6466", "_0x65ca", "_0x66ca", "_0x6766", "_0x68ca", "_0x69ca", "_0x6aca", "_0x6b1e",
                "_0x6c64", "_0x6dc8", "_0x6e66", "_0x6fca");
            Assert.Multiple(() =>
            {
                Assert.That(block.MasterType, Is.EqualTo("9.1.01"));
                Assert.That(block.DisplayName, Is.EqualTo("9.1.01.a. Toggle lamp"));
                Assert.That(block.Body.Tag, Is.EqualTo("functionblock"));
            });
        }

        [Test]
        public void Author_fb02_Scene_MatchesOracle()
        {
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("9.1.02", "a", "Scene recall")
                .MasterProgrammer("Morten Christensen")
                .MasterDate(new DateOnly(2026, 3, 12))
                .Locked()
                .Attribute("icon", "_0xe")
                .Note("9.1.02.a. Scene recall\r\n\r\nFremkalder et scenarie når udløseren aktiveres.")
                .InputsNote("Indgange til scenarieblokken")
                .OutputsNote("Udgange og scenarier fra blokken");

            FbResourceHandle trigger = builder.AddInput("resource_input", "Udløser", i => i.Note("Aktiverer scenariefremkaldelsen."));
            FbResourceHandle relay = builder.AddOutput("resource_output", "Relæ", o => o.Note("Slutter eller bryder belastningen."));
            FbResourceHandle sceneOn = builder.AddOutput("resource_scene", "Scenarie tændt",
                o => o.Note("Fremkaldes når udgangen tændes.").Attribute("hide_dialog", "yes"));
            FbResourceHandle sceneOff = builder.AddOutput("resource_scene", "Scenarie slukket",
                o => o.Note("Fremkaldes når udgangen slukkes.").Attribute("note-2", "Vises kun for avancerede brugere"));
            builder.AddSetting("resource_timer", "Holdetid", r => r.Backup().TimerHms(0, 2, 30));

            FbProgramBuilder on = builder.Program("Fremkald tændt");
            on.AddEvent("%P -> ON", trigger, "_0xa", note: "Start når %P skifter til ON");
            on.AddAction("%P = ON", relay, "_0xa", note: "Tænder relæet");
            on.AddAction("Fremkald %P", sceneOn, "_0xa", note: "Fremkalder scenariet");

            FbProgramBuilder off = builder.Program("Fremkald slukket");
            off.AddEvent("%P -> OFF", trigger, "_0x14", note: "Start når %P skifter til OFF");
            off.AddAction("%P = OFF", relay, "_0x14", note: "Slukker relæet");
            off.AddAction("Fremkald %P", sceneOff, "_0xa", note: "Fremkalder scenariet");

            builder.Grammar(CatalogGrammar.Create(new[]
            {
                OracleGrammars.FbRoot, OracleGrammars.Container("inputs"), OracleGrammars.FbPin("resource_input"),
                OracleGrammars.Container("outputs"), OracleGrammars.FbPin("resource_output"),
                OracleGrammars.ResourceSceneFb, OracleGrammars.Container("settings"), OracleGrammars.ResourceTimerFb,
                OracleGrammars.Container("internalsettings"), OracleGrammars.Container("programs"),
                OracleGrammars.Container("program_simple"), OracleGrammars.Container("events"),
                OracleGrammars.ProgramLeaf("event"), OracleGrammars.FbActions, OracleGrammars.ProgramLeaf("action"),
            }));
            FunctionBlockDefinition block = builder.Build();
            SyntheticOracle.AssertMatchesOracle(block.Body, Dir + "synthetic_fb02_scene.ifb");
            SyntheticOracle.AssertWritesOracleBytes(block, Dir + "synthetic_fb02_scene.ifb",
                "_0x5128", "_0x5223", "_0x5311", "_0x5424", "_0x5512", "_0x564a", "_0x574a", "_0x5825", "_0x5910",
                "_0x5a29", "_0x5b26", "_0x5c1e", "_0x5d64", "_0x5ec8", "_0x5f66", "_0x60ca", "_0x61ca", "_0x621e",
                "_0x6364", "_0x64c8", "_0x6566", "_0x66ca", "_0x67ca");
        }

        [Test]
        public void Author_fb03_Mode_MatchesOracle()
        {
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("9.2.01", "a", "Mode selector")
                .VendorMaster()
                .MasterProgrammer("Morten Christensen")
                .MasterDate(new DateOnly(2026, 4, 3))
                .Locked()
                .Attribute("icon", "_0xe")
                .Note("9.2.01.a. Mode selector\r\n\r\nKan fungere som enten kip- eller følgefunktion afhængigt af valgt tilstand.")
                .InputsNote("Indgange til vælgerblokken")
                .OutputsNote("Udgange fra vælgerblokken");

            // The oracle declares Automatik with an explicit index=1 first, Manuel (index 0, elided) second.
            FbEnumDefRef mode = builder.AddEnumDefinition("Funktionsvalg").AddValue("Automatik", 1).AddValue("Manuel", 0);
            FbResourceHandle indgang = builder.AddInput("resource_input", "Indgang", i => i.Note("Styreindgang til blokken."));
            FbResourceHandle udgang = builder.AddOutput("resource_output", "Udgang",
                o => o.Note("Følger eller kipper afhængigt af tilstanden."));
            FbResourceHandle sel = builder.AddSetting("resource_enum", "Funktionsvalg",
                r => r.Enum(mode, "Manuel").Backup().Note("Vælg mellem automatisk og manuel styring."));

            FbProgramBuilder program = builder.Program("Vælg funktion");
            program.AddEvent("%P bliver ændret", indgang, "_0x96", note: "Start når %P skifter værdi");
            FbSubProgramRef outer = program.AddSubProgram();
            FbConditionRef condition = outer.AddCondition("%P = %S", sel, "_0x1e", note: "Betingelse: valgt tilstand er %S");
            condition.AddEnumOperand("Enumerator", mode, "Automatik");
            FbSubProgramRef inner = outer.WhenTrue.AddSubProgram();
            inner.AddCondition("%P = ON", indgang, "_0xa", note: "Betingelse: %P er tændt");
            inner.WhenTrue.AddAction("Kip %P", udgang, "_0x23", note: "Skifter %P til modsat værdi");
            outer.WhenFalse.AddAction("%P = %S", udgang, "_0x1e", link2: indgang, note: "Sætter %P lig med %S");

            builder.Grammar(CatalogGrammar.Create(new[]
            {
                OracleGrammars.EnumDefinitionFb, OracleGrammars.EnumValueFb,
                OracleGrammars.FbRoot, OracleGrammars.Container("inputs"), OracleGrammars.FbPin("resource_input"),
                OracleGrammars.Container("outputs"), OracleGrammars.FbPin("resource_output"),
                OracleGrammars.Container("settings"), OracleGrammars.ResourceEnumFb,
                OracleGrammars.Container("internalsettings"), OracleGrammars.Container("programs"),
                OracleGrammars.Container("program_simple"), OracleGrammars.Container("events"),
                OracleGrammars.ProgramLeaf("event"), OracleGrammars.FbActions,
                OracleGrammars.Container("program_sub"), OracleGrammars.FbConditions,
                OracleGrammars.ProgramLeaf("condition"), OracleGrammars.ProgramLeaf("action"),
            }));
            FunctionBlockDefinition block = builder.Build();
            SyntheticOracle.AssertMatchesOracle(block.Body, Dir + "synthetic_fb03_mode.ifb");
            SyntheticOracle.AssertWritesOracleBytes(block, Dir + "synthetic_fb03_mode.ifb",
                "_0x5128", "_0x5247", "_0x5348", "_0x5448", "_0x5523", "_0x5611", "_0x5724", "_0x5812", "_0x5925",
                "_0x5a0f", "_0x5b29", "_0x5c26", "_0x5d1e", "_0x5e64", "_0x5fc8", "_0x6066", "_0x611f", "_0x6265",
                "_0x63c9", "_0x640f", "_0x6566", "_0x661f", "_0x6765", "_0x68c9", "_0x6966", "_0x6aca", "_0x6b66",
                "_0x6c66", "_0x6dca");
        }

        [Test]
        public void Author_fb04_Holiday_MatchesOracle()
        {
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("9.3.01", "a", "Holiday schedule")
                .MasterProgrammer("Morten Christensen")
                .MasterDate(new DateOnly(2026, 5, 20))
                .Locked()
                .Attribute("icon", "_0xe")
                .Note("9.3.01.a. Holiday schedule\r\n\r\nHolder en udgang tændt i en defineret ferieperiode.")
                .InputsNote("Indgange til ferieblokken")
                .OutputsNote("Udgange fra ferieblokken");

            FbResourceHandle aktiver = builder.AddInput("resource_input", "Aktivér", i => i.Note("Slår ferieprogrammet til."));
            FbResourceHandle aktiv = builder.AddOutput("resource_output", "Aktiv", o => o.Note("Er tændt i ferieperioden."));
            builder.AddSetting("resource_date", "Startdato", r => r.Backup().Note("Ferieperiodens første dag.").DateYmd(2026, 6, 1));
            builder.AddSetting("resource_date", "Stopdato", r => r.Backup().Note("Ferieperiodens sidste dag.").DateYmd(2026, 8, 31));
            FbResourceHandle bemyndiget = builder.AddSetting("resource_flag", "Bemyndiget",
                r => r.Note("Angiver om ferieprogrammet er tilladt.").Inivalue("on"));
            builder.AddInternalVariable("resource_date", "Dags dato", r => r.Note("Opdateres løbende af controlleren.").DateYmd(2000, 1, 1));

            FbProgramBuilder boot = builder.Program("Opstart");
            boot.AddPowerEvent("Opstart", note: "Start programmet når controlleren tændes");
            boot.AddAction("%P = OFF", aktiv, "_0x14", note: "Nulstiller udgangen ved opstart");

            FbProgramBuilder check = builder.Program("Kontrollér periode");
            check.AddEvent("%P -> ON", aktiver, "_0xa", note: "Start når %P skifter til ON");
            FbSubProgramRef sub = check.AddSubProgram();
            sub.OrConditions();
            sub.AddCondition("%P = ON", bemyndiget, "_0xa", note: "Betingelse: programmet er bemyndiget");
            sub.AddCondition("%P bliver ændret", aktiver, "_0x96", note: "Betingelse: indgangen er ændret");
            sub.WhenTrue.AddAction("%P = ON", aktiv, "_0xa", note: "Tænder udgangen");
            sub.WhenFalse.AddAction("%P = OFF", aktiv, "_0x14", note: "Slukker udgangen");

            builder.Grammar(CatalogGrammar.Create(new[]
            {
                OracleGrammars.FbRoot, OracleGrammars.Container("inputs"), OracleGrammars.FbPin("resource_input"),
                OracleGrammars.Container("outputs"), OracleGrammars.FbPin("resource_output"),
                OracleGrammars.Container("settings"), OracleGrammars.ResourceDateFb,
                OracleGrammars.ResourceFlagFb("off"), OracleGrammars.Container("internalsettings"),
                OracleGrammars.Container("programs"), OracleGrammars.Container("program_simple"),
                OracleGrammars.Container("events"), OracleGrammars.EventPower, OracleGrammars.FbActions,
                OracleGrammars.ProgramLeaf("action"), OracleGrammars.ProgramLeaf("event"),
                OracleGrammars.Container("program_sub"), OracleGrammars.FbConditions,
                OracleGrammars.ProgramLeaf("condition"),
            }));
            FunctionBlockDefinition block = builder.Build();
            SyntheticOracle.AssertMatchesOracle(block.Body, Dir + "synthetic_fb04_holiday.ifb");
            SyntheticOracle.AssertWritesOracleBytes(block, Dir + "synthetic_fb04_holiday.ifb",
                "_0x5128", "_0x5223", "_0x5311", "_0x5424", "_0x5512", "_0x5625", "_0x570e", "_0x580e", "_0x590a",
                "_0x5a29", "_0x5b0e", "_0x5c26", "_0x5d1e", "_0x5e64", "_0x5fc8", "_0x6066", "_0x61ca", "_0x621e",
                "_0x6364", "_0x64c8", "_0x6566", "_0x661f", "_0x6765", "_0x68c9", "_0x69c9", "_0x6a66", "_0x6bca",
                "_0x6c66", "_0x6dca");
        }

        [Test]
        public void Author_fb05_Empty_MatchesOracle()
        {
            FunctionBlockDefinition block = FunctionBlockDefinitionBuilder
                .Create("", "", "Tomt demoblok")
                .Grammar(CatalogGrammar.Create(new[]
                {
                    OracleGrammars.FbRoot, OracleGrammars.Container("inputs"), OracleGrammars.Container("outputs"),
                    OracleGrammars.Container("settings"), OracleGrammars.Container("internalsettings"),
                    OracleGrammars.Container("programs"), OracleGrammars.Container("program_simple"),
                    OracleGrammars.Container("events"), OracleGrammars.FbActions,
                }))
                .AsEmptyTemplate("_0xf")
                .Build();

            SyntheticOracle.AssertMatchesOracle(block.Body, Dir + "synthetic_fb05_empty.ifb");
            SyntheticOracle.AssertWritesOracleBytes(block, Dir + "synthetic_fb05_empty.ifb",
                "_0x5128", "_0x5223", "_0x5324", "_0x5425", "_0x5529", "_0x5626", "_0x571e", "_0x5864", "_0x5966");
            Assert.That(block.IsEmptyTemplate, Is.True);
        }

        [Test]
        public void Author_fb06_Sensor_MatchesOracle()
        {
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("9.4.01", "a", "Sensor panel")
                .DisplayName("Sensorpanel (udvidet)")
                .MasterProgrammer("Morten Christensen")
                .MasterDate(new DateOnly(2026, 6, 30))
                .Locked()
                .Attribute("icon", "_0xe")
                .Note("Sensorpanel (udvidet)\r\n\r\nOvervåger en temperatur og udløser en alarm når grænseværdien overskrides.")
                .InputsNote("Indgange til sensorpanelet")
                .OutputsNote("Udgange fra sensorpanelet");

            builder.AddInput("resource_input", "Nulstil", i => i.Note("Nulstiller alarmen."));
            FbResourceHandle maalt = builder.AddInput("resource_temperature", "Målt temperatur",
                r => r.Note("Aktuelt målt temperatur.").Inivalue("20.00"));
            FbResourceHandle alarm = builder.AddOutput("resource_output", "Alarm", o => o.Note("Aktiv når grænseværdien overskrides."));
            FbResourceHandle graense = builder.AddSetting("resource_temperature", "Grænseværdi",
                r => r.Note("Temperaturgrænse for alarm.").Inivalue("21.50"));
            FbResourceHandle udloes = builder.AddSetting("resource_counter", "Udløsninger",
                r => r.Note("Antal gange alarmen er udløst.").Inivalue("5"));
            builder.AddSetting("resource_weekday", "Aktiv ugedag",
                r => r.Note("Ugedag hvor blokken overvåger.").Inivalue("friday"));
            builder.AddSetting("resource_integer", "Tærskel", r => r.Note("Heltalstærskel for gentagne alarmer.").Inivalue("10"));
            builder.AddInternalVariable("resource_time", "Forsinkelse",
                r => r.Note("Forsinkelse før alarmen udløses.").Attribute("second", "30"));

            FbProgramBuilder watch = builder.Program("Overvågning");
            watch.AddEvent("%P bliver ændret", maalt, "_0x96", note: "Start når den målte temperatur ændres");
            FbSubProgramRef sub = watch.AddSubProgram();
            sub.AddCondition("%P > %S", maalt, "_0x64", link2: graense, note: "Betingelse: målt temperatur over grænseværdien");
            sub.WhenTrue.AddAction("%P = ON", alarm, "_0xa", note: "Udløser alarmen");
            sub.WhenTrue.AddAction("Tæl %P op", udloes, "_0xbf", note: "Tæller antal udløsninger");
            sub.WhenFalse.AddAction("%P = OFF", alarm, "_0x14", note: "Rydder alarmen");

            builder.Grammar(CatalogGrammar.Create(new[]
            {
                OracleGrammars.FbRoot, OracleGrammars.Container("inputs"), OracleGrammars.FbPin("resource_input"),
                OracleGrammars.ResourceTemperatureFb, OracleGrammars.Container("outputs"),
                OracleGrammars.FbPin("resource_output"), OracleGrammars.Container("settings"),
                OracleGrammars.ResourceCounterFb, OracleGrammars.ResourceWeekdayFb, OracleGrammars.ResourceIntegerFb,
                OracleGrammars.Container("internalsettings"), OracleGrammars.ResourceTimeFb,
                OracleGrammars.Container("programs"), OracleGrammars.Container("program_simple"),
                OracleGrammars.Container("events"), OracleGrammars.ProgramLeaf("event"), OracleGrammars.FbActions,
                OracleGrammars.Container("program_sub"), OracleGrammars.FbConditions,
                OracleGrammars.ProgramLeaf("condition"), OracleGrammars.ProgramLeaf("action"),
            }));
            FunctionBlockDefinition block = builder.Build();
            SyntheticOracle.AssertMatchesOracle(block.Body, Dir + "synthetic_fb06_sensor.ifb");
            SyntheticOracle.AssertWritesOracleBytes(block, Dir + "synthetic_fb06_sensor.ifb",
                "_0x5128", "_0x5223", "_0x5311", "_0x5414", "_0x5524", "_0x5612", "_0x5725", "_0x5814", "_0x590c",
                "_0x5a09", "_0x5b0b", "_0x5c29", "_0x5d0d", "_0x5e26", "_0x5f1e", "_0x6064", "_0x61c8", "_0x6266",
                "_0x631f", "_0x6465", "_0x65c9", "_0x6666", "_0x67ca", "_0x68ca", "_0x6966", "_0x6aca");
            Assert.That(block.DisplayName, Is.EqualTo("Sensorpanel (udvidet)"));
        }

        [Test]
        public void Author_fb07_IrregularGrammar_MatchesOracle()
        {
            // The grammar-envelope oracle: outputs declared before inputs and per-file inivalue defaults of "on" —
            // the definition's own grammar (not any preset) must drive both the header bytes and, later, the
            // effective values of the lean body's omitted attributes.
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("9.1.07", "a", "Grammatik blok")
                .VendorMaster()
                .MasterProgrammer("Morten Christensen")
                .MasterDate(new DateOnly(2026, 3, 1))
                .Locked()
                .Attribute("icon", "_0xe")
                .Note("9.1.07.a. Grammatik blok\r\n\r\nSyntetisk demoblok med omrokeret DTD og ændrede standardværdier.")
                .InputsNote("Variablene i denne gruppering er indgange til blokken")
                .OutputsNote("Variablene i denne gruppering er udgange fra blokken");

            FbResourceHandle start = builder.AddInput("resource_input", "Start",
                i => i.Note("Standard er tændt i denne bloks grammatik"));
            FbResourceHandle udgang = builder.AddOutput("resource_output", "Udgang", o => o.Note("Tilsluttes en lampe."));
            builder.AddSetting("resource_flag", "Husk tilstand",
                r => r.Note("Blokkens grammatik sætter standard til on"));

            FbProgramBuilder skift = builder.Program("Skift");
            skift.AddEvent("%P -> ON", start, "_0xa", note: "Start når %P skifter til ON");
            skift.AddAction("%P = ON", udgang, "_0xa", note: "Tænder udgangen");

            builder.Grammar(CatalogGrammar.Create(new[]
            {
                OracleGrammars.FbRoot, OracleGrammars.Container("outputs"), OracleGrammars.FbPin("resource_output"),
                OracleGrammars.Container("inputs"), OracleGrammars.FbPin("resource_input", "on"),
                OracleGrammars.Container("settings"), OracleGrammars.ResourceFlagFb("on"),
                OracleGrammars.Container("internalsettings"), OracleGrammars.Container("programs"),
                OracleGrammars.Container("program_simple"), OracleGrammars.Container("events"),
                OracleGrammars.ProgramLeaf("event"), OracleGrammars.FbActions, OracleGrammars.ProgramLeaf("action"),
            }));
            FunctionBlockDefinition block = builder.Build();
            SyntheticOracle.AssertMatchesOracle(block.Body, Dir + "synthetic_fb07_grammar.ifb");
            SyntheticOracle.AssertWritesOracleBytes(block, Dir + "synthetic_fb07_grammar.ifb",
                "_0x5128", "_0x5223", "_0x5311", "_0x5424", "_0x5512", "_0x5625", "_0x570a", "_0x5829", "_0x5926",
                "_0x5a1e", "_0x5b64", "_0x5cc8", "_0x5d66", "_0x5eca");
        }

        [Test]
        public void Author_fb08_FullSurface_MatchesOracle_AndPinsFunctionBlockPreset()
        {
            // Bare Create — no grammar call — so the written header IS the FunctionBlock preset's rendering, and
            // the body exercises the whole closed-emitter program surface (power-up trigger, sub-program with
            // conditions and branches, program_case with per-value case_actions and embedded resource_enum
            // operands, top-level enum stubs, default-tag pins). Ids are the builder's natural allocation — the
            // oracle was generated by this exact authoring, so no re-stamping is needed.
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder.Create("9.1.08", "a", "Fuld flade")
                .VendorMaster()
                .MasterProgrammer("Morten Christensen")
                .MasterDate(new DateOnly(2026, 3, 2))
                .Locked()
                .Attribute("icon", "_0xe")
                .Note("9.1.08.a. Fuld flade\r\n\r\nSyntetisk demoblok der dækker hele program-fladen (case, power-up, enum).")
                .InputsNote("Variablene i denne gruppering er indgange til blokken")
                .OutputsNote("Variablene i denne gruppering er udgange fra blokken");

            FbEnumDefRef mode = builder.AddEnumDefinition("Tilstand").AddValue("Fra").AddValue("Til", 1);
            FbResourceHandle start = builder.AddInput("Start");
            FbResourceHandle output = builder.AddOutput("Udgang");
            FbResourceHandle selector = builder.AddSetting("resource_enum", "Tilstandsvalg",
                r => r.Enum(mode, "Fra").Note("Vælger blokkens tilstand."));

            FbProgramBuilder p = builder.Program("Skift");
            p.AddPowerEvent("Opstart");
            p.AddEvent("%P -> ON", start, "_0xa", note: "Start når %P skifter til ON");
            FbSubProgramRef sub = p.AddSubProgram();
            sub.AddCondition("%P = OFF", output, "_0x14", note: "Betingelse: %P er slukket");
            sub.WhenTrue.AddAction("%P = ON", output, "_0xa", note: "Tænder udgangen");
            sub.WhenFalse.AddAction("%P = OFF", output, "_0x14", note: "Slukker udgangen");
            FbCaseRef sw = p.AddCase("Vælg tilstand", selector, note: "Skifter på tilstandsvalget");
            sw.Case("Tilstand Fra", mode, "Fra").AddAction("%P = OFF", output, "_0x14");
            sw.Case("Tilstand Til", mode, "Til").AddAction("%P = ON", output, "_0xa");
            sw.Default().AddAction("%P = OFF", output, "_0x14");

            FunctionBlockDefinition block = builder.Build();
            SyntheticOracle.AssertMatchesOracle(block.Body, Dir + "synthetic_fb08_full.ifb");
            SyntheticOracle.AssertWritesOracleBytes(block, Dir + "synthetic_fb08_full.ifb");
        }

        [Test]
        public void From_ReopenAndRebuild_PreservesBody()
        {
            FunctionBlockDefinitionBuilder builder = FunctionBlockDefinitionBuilder
                .Create("9.9.99", "z", "Round Trip")
                .VendorMaster().Locked().Attribute("icon", "_0xe");
            FbEnumDefRef mode = builder.AddEnumDefinition("Tilstand").AddValue("Nat").AddValue("Dag");
            FbResourceHandle input = builder.AddInput("Kip");
            FbResourceHandle setting = builder.AddSetting("resource_enum", "Tilstand", r => r.Enum(mode, "Nat"));
            FbResourceHandle output = builder.AddOutput("Udgang");
            FbProgramBuilder program = builder.Program("Skift");
            program.AddEvent("Kip", input, "_0xa");
            FbSubProgramRef sub = program.AddSubProgram();
            sub.AddCondition("Er nat", setting, "_0xbe");
            sub.WhenTrue.AddAction("Tænd", output, "_0xda");
            FunctionBlockDefinition original = builder.Build();

            FunctionBlockDefinition reopened = FunctionBlockDefinitionBuilder.From(original).Build();

            ImmutableDictionary<string, string> registry = ImmutableDictionary<string, string>.Empty;
            Assert.That(DefinitionNormalizer.Normalize(reopened.Body, registry),
                Is.EqualTo(DefinitionNormalizer.Normalize(original.Body, registry)));
        }
    }
}
