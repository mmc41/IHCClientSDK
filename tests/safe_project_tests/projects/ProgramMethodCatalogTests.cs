using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// US-028/029/031/032: the SDK program-method catalog — the promoted source of the vendor tokens, name/note
    /// templates and semantics. Asserts each <c>(category, token)</c> carries the byte-fidelity name/note/arity/
    /// operator, including the deliberate token reuse across categories (a token alone is not a unique key).
    /// </summary>
    public class ProgramMethodCatalogTests
    {
        private static ProgramMethod One(System.Collections.Immutable.ImmutableArray<ProgramMethod> list, string token) =>
            list.Single(m => m.Token == token);

        [Test]
        public void Events_CarryVendorTemplatesAndUnaryArity()
        {
            Assert.Multiple(() =>
            {
                Assert.That(One(ProgramMethodCatalog.Events, "_0xa"),
                    Is.EqualTo(new ProgramMethod(ProgramMethodCategory.Event, "_0xa", "%P -> ON", "Start program når %P skifter til ON", 1, null)));
                Assert.That(One(ProgramMethodCatalog.Events, "_0x14"),
                    Is.EqualTo(new ProgramMethod(ProgramMethodCategory.Event, "_0x14", "%P -> OFF", "Start program når %P skifter til OFF", 1, null)));
                Assert.That(One(ProgramMethodCatalog.Events, "_0x96"),
                    Is.EqualTo(new ProgramMethod(ProgramMethodCategory.Event, "_0x96", "%P bliver ændret", "Start program når %P skifter værdi", 1, null)));
                Assert.That(One(ProgramMethodCatalog.Events, "_0x9b"),
                    Is.EqualTo(new ProgramMethod(ProgramMethodCategory.Event, "_0x9b", "%P bliver tilskrevet", "Start program når %P bliver tilskrevet", 1, null)));
                // T008: the direct/state triggers are unary; the "-> <pin>" / "NOT -> <pin>" transitions are two-operand.
                Assert.That(One(ProgramMethodCatalog.Events, "_0xa").OperandCount, Is.EqualTo(1));
                Assert.That(One(ProgramMethodCatalog.Events, "_0x1e"),
                    Is.EqualTo(new ProgramMethod(ProgramMethodCategory.Event, "_0x1e", "%P -> %S", "Start program når %P skifter til %S", 2, null)));
                Assert.That(One(ProgramMethodCatalog.Events, "_0x28"),
                    Is.EqualTo(new ProgramMethod(ProgramMethodCategory.Event, "_0x28", "%P NOT -> %S", "Start program når %P skifter til NOT %S", 2, null)));
                Assert.That(One(ProgramMethodCatalog.Events, "_0x28").OperandCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void Commands_ToggleCarriesVendorPayload()
        {
            Assert.Multiple(() =>
            {
                Assert.That(One(ProgramMethodCatalog.Commands, "_0xa"),
                    Is.EqualTo(new ProgramMethod(ProgramMethodCategory.Command, "_0xa", "%P = ON", "Sets %P to ON", 1, null)));
                Assert.That(One(ProgramMethodCatalog.Commands, "_0x14").NameTemplate, Is.EqualTo("%P = OFF"));
                Assert.That(One(ProgramMethodCatalog.Commands, "_0x23"),
                    Is.EqualTo(new ProgramMethod(ProgramMethodCategory.Command, "_0x23", "Kip %P",
                        "Sætter %P til modsat værdi af aktuel værdi", 1, null)));
            });
        }

        [Test]
        public void Conditions_CarryVendorTemplatesIncludingNotVariant()
        {
            Assert.Multiple(() =>
            {
                Assert.That(One(ProgramMethodCatalog.Conditions, "_0xa").NameTemplate, Is.EqualTo("%P = ON"));
                Assert.That(One(ProgramMethodCatalog.Conditions, "_0x14").NameTemplate, Is.EqualTo("%P = OFF"));
                Assert.That(One(ProgramMethodCatalog.Conditions, "_0x1e").NameTemplate, Is.EqualTo("%P = %S"), "the two-operand equals");
                // T008: the NOT variant is the pinned two-operand "%P <> %S", not the old unary "%P <> ON".
                Assert.That(One(ProgramMethodCatalog.Conditions, "_0x28").NameTemplate, Is.EqualTo("%P <> %S"));
                Assert.That(One(ProgramMethodCatalog.Conditions, "_0x28").OperandCount, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task Conditions_NotesMatchTheAuthenticOracleRows()
        {
            Project oracle = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project2-CustomBlock.vis");
            var oracleNotesByName = oracle.Root.Descendants()
                .Where(e => e.Tag == "condition")
                .ToDictionary(e => e.GetAttribute("name")!, e => e.GetAttribute("note")!);

            Assert.Multiple(() =>
            {
                foreach (ProgramMethod method in ProgramMethodCatalog.Conditions)
                    Assert.That(method.Note, Is.EqualTo(oracleNotesByName[method.NameTemplate]),
                        $"condition {method.Token} stores the vendor note verbatim");
            });
        }

        [Test]
        public void Arithmetic_IsBinary_WithOperatorSymbols()
        {
            Assert.Multiple(() =>
            {
                Assert.That(One(ProgramMethodCatalog.Arithmetic, "_0x5a").NameTemplate, Is.EqualTo("%P = %P + %S"));
                Assert.That(One(ProgramMethodCatalog.Arithmetic, "_0x5a").OperatorSymbol, Is.EqualTo("+"));
                Assert.That(One(ProgramMethodCatalog.Arithmetic, "_0x64").NameTemplate, Is.EqualTo("%P = %P - %S"));
                Assert.That(One(ProgramMethodCatalog.Arithmetic, "_0x64").OperatorSymbol, Is.EqualTo("-"));
                Assert.That(ProgramMethodCatalog.Arithmetic.Select(m => m.OperandCount), Is.All.EqualTo(2));
            });
        }

        // H1: every persisted template/note/operator MUST live in the ISO-8859-1 repertoire — the .vis format is
        // Latin-1 with no BOM, so a character outside U+0000..U+00FF cannot be encoded and Save throws. The subtract
        // entry historically carried U+2212 (MINUS SIGN), which is NOT Latin-1; this guards the whole catalog.
        [Test]
        public void AllProgramMethods_UseOnlyLatin1EncodableText()
        {
            var all = ProgramMethodCatalog.Events
                .Concat(ProgramMethodCatalog.Commands)
                .Concat(ProgramMethodCatalog.Conditions)
                .Concat(ProgramMethodCatalog.Arithmetic)
                .Concat(ProgramMethodCatalog.CounterSteps)
                .Concat(ProgramMethodCatalog.TimerCommands);
            Assert.Multiple(() =>
            {
                foreach (ProgramMethod m in all)
                {
                    Assert.That(IsLatin1(m.NameTemplate), Is.True,
                        $"NameTemplate outside Latin-1: {m.Category}/{m.Token} '{m.NameTemplate}'");
                    Assert.That(IsLatin1(m.Note), Is.True,
                        $"Note outside Latin-1: {m.Category}/{m.Token}");
                    if (m.OperatorSymbol is { } sym)
                    {
                        Assert.That(IsLatin1(sym), Is.True,
                            $"OperatorSymbol outside Latin-1: {m.Category}/{m.Token} '{sym}'");
                    }
                }
            });
        }

        // H1 end-to-end: authoring a subtract command from the catalog template and saving must not throw, and the
        // command name must survive the save→reload round-trip (before the fix, Save throws EncoderFallbackException).
        [Test]
        public async Task AddSubtractCommand_FromCatalogTemplate_SavesAndRoundTrips()
        {
            ProgramMethod subtract = One(ProgramMethodCatalog.Arithmetic, "_0x64");
            Project original = await ReplayOracle.LoadProject("project2-CustomBlock.vis");

            ProjectEditor editor = original.Edit();
            FunctionBlockRef custom = editor.Group("Stue").FunctionBlock("Custom blok");
            SubProgramRef sub = custom.Program().AddSubProgram();
            sub.WhenTrue.AddAction(subtract.NameTemplate, custom.Output("Udgang"), subtract.Token,
                custom.Setting("NyTypeForThisProject"));
            Project after = editor.ToProject();

            using var ms = new MemoryStream();
            await new ProjectAppService(TestSetup.Settings).Save(after, ms);

            Project reloaded = ProjectReader.Read(ms.ToArray());
            ProjectElement action = reloaded.Root.Descendants()
                .Single(e => e.Tag == "action" && e.GetAttribute("method") == subtract.Token);
            Assert.That(action.GetAttribute("name"), Is.EqualTo(subtract.NameTemplate),
                "the subtract command name survives the ISO-8859-1 save/reload round-trip");
        }

        // T020: the case-switch eligibility set (US-031) is a public SDK fact — the single source both the session's
        // AddCase Evaluate guard and the OpenVisual case menu read; the app keeps no private copy.
        [Test]
        public void EligibleCaseVariableTags_AreTheFiveSwitchableTypes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProgramMethodCatalog.EligibleCaseVariableTags, Is.EquivalentTo(new[]
                {
                    "resource_counter", "resource_enum", "resource_weekday", "resource_integer", "resource_date",
                }));
                Assert.That(ProgramMethodCatalog.EligibleCaseVariableTags.Contains("resource_flag"), Is.False,
                    "a boolean flag is not a switchable case variable");
            });
        }

        private static bool IsLatin1(string value) => value.All(c => c <= 'ÿ');

        // The same token means different things per category — the (category, token) key is required.
        [Test]
        public void Token0xa_ReusedAcrossThreeCategories_WithDistinctNotes()
        {
            ProgramMethod ev = One(ProgramMethodCatalog.Events, "_0xa");
            ProgramMethod cmd = One(ProgramMethodCatalog.Commands, "_0xa");
            ProgramMethod cond = One(ProgramMethodCatalog.Conditions, "_0xa");
            Assert.Multiple(() =>
            {
                Assert.That(ev.Category, Is.EqualTo(ProgramMethodCategory.Event));
                Assert.That(cmd.Category, Is.EqualTo(ProgramMethodCategory.Command));
                Assert.That(cond.Category, Is.EqualTo(ProgramMethodCategory.Condition));
                // Command and Condition even share the same name — only the category distinguishes them.
                Assert.That(cmd.NameTemplate, Is.EqualTo(cond.NameTemplate).And.EqualTo("%P = ON"));
                Assert.That(cmd.Note, Is.Not.EqualTo(cond.Note));
            });
        }

        // T007/PG-1b: a pin's tag maps to exactly one operator family — timer/weekday/analog leave the bool default.
        [Test]
        public void ClassifyPin_MapsTagsToTheirOperatorFamily()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProgramMethodCatalog.ClassifyPin("resource_timer"), Is.EqualTo(ProgramPinType.Timer));
                Assert.That(ProgramMethodCatalog.ClassifyPin("resource_weekday"), Is.EqualTo(ProgramPinType.Weekday));
                Assert.That(ProgramMethodCatalog.ClassifyPin("resource_humidity_level"), Is.EqualTo(ProgramPinType.Analog));
                Assert.That(ProgramMethodCatalog.ClassifyPin("resource_floating_point"), Is.EqualTo(ProgramPinType.Analog));
                Assert.That(ProgramMethodCatalog.ClassifyPin("resource_input"), Is.EqualTo(ProgramPinType.Bool));
                Assert.That(ProgramMethodCatalog.ClassifyPin("resource_output"), Is.EqualTo(ProgramPinType.Bool));
                Assert.That(ProgramMethodCatalog.ClassifyPin("resource_enum"), Is.EqualTo(ProgramPinType.Enum),
                    "an enumerator has its own measured operator family and must not inherit Boolean commands");
            });
        }

        private static string[] Tokens(System.Collections.Immutable.ImmutableArray<ProgramMethod> list) =>
            list.Select(m => m.Token).ToArray();

        // T007/PG-1b: each pin type offers EXACTLY its listed operators per container (asserted by token).
        [Test]
        public void PinTypeKeyedLists_OfferExactlyTheirPinnedOperators()
        {
            Assert.Multiple(() =>
            {
                // Bool is unchanged — the existing category lists.
                Assert.That(ProgramMethodCatalog.EventsFor(ProgramPinType.Bool), Is.EqualTo(ProgramMethodCatalog.Events));
                Assert.That(ProgramMethodCatalog.CommandsFor(ProgramPinType.Bool), Is.EqualTo(ProgramMethodCatalog.Commands));
                Assert.That(ProgramMethodCatalog.ConditionsFor(ProgramPinType.Bool), Is.EqualTo(ProgramMethodCatalog.Conditions));

                // Enum: assignment/comparison against another value of the same type plus state-change/write events.
                Assert.That(Tokens(ProgramMethodCatalog.EventsFor(ProgramPinType.Enum)),
                    Is.EqualTo(new[] { "_0x1e", "_0x96", "_0x9b" }));
                Assert.That(Tokens(ProgramMethodCatalog.CommandsFor(ProgramPinType.Enum)),
                    Is.EqualTo(new[] { "_0x1e" }));
                Assert.That(Tokens(ProgramMethodCatalog.ConditionsFor(ProgramPinType.Enum)),
                    Is.EqualTo(new[] { "_0x1e", "_0x28" }));

                // Analog: only the two "is changed"/"is written" triggers; no commands or conditions of its own.
                Assert.That(Tokens(ProgramMethodCatalog.EventsFor(ProgramPinType.Analog)), Is.EqualTo(new[] { "_0x96", "_0x9b" }));
                Assert.That(ProgramMethodCatalog.CommandsFor(ProgramPinType.Analog), Is.Empty);
                Assert.That(ProgramMethodCatalog.ConditionsFor(ProgramPinType.Analog), Is.Empty);

                // Weekday: System-weekday assign (_0x5) + the two shared triggers; no commands/conditions.
                Assert.That(Tokens(ProgramMethodCatalog.EventsFor(ProgramPinType.Weekday)), Is.EqualTo(new[] { "_0x5", "_0x96", "_0x9b" }));
                Assert.That(ProgramMethodCatalog.CommandsFor(ProgramPinType.Weekday), Is.Empty);

                // Timer: full nine commands, two events, seven conditions (pinned in dedicated Timer* tests).
                Assert.That(ProgramMethodCatalog.CommandsFor(ProgramPinType.Timer), Has.Length.EqualTo(9));
                Assert.That(ProgramMethodCatalog.EventsFor(ProgramPinType.Timer), Has.Length.EqualTo(2));
                Assert.That(ProgramMethodCatalog.ConditionsFor(ProgramPinType.Timer), Has.Length.EqualTo(7));

                // A shared token carries a type-specific template: a timer's _0xa is "= 0", not the bool "= ON".
                Assert.That(One(ProgramMethodCatalog.CommandsFor(ProgramPinType.Timer), "_0xa").NameTemplate, Is.EqualTo("%P = 0"));
            });
        }

        // T008: the bool operator lists are complete, including the two-operand transitions/comparisons.
        [Test]
        public void BoolLists_AreCompleteWithTwoOperandOperators()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Tokens(ProgramMethodCatalog.Events), Is.EqualTo(new[] { "_0xa", "_0x14", "_0x1e", "_0x28", "_0x96", "_0x9b" }));
                Assert.That(Tokens(ProgramMethodCatalog.Commands), Is.EqualTo(new[] { "_0xa", "_0x14", "_0x1e", "_0x28", "_0x23" }));
                Assert.That(Tokens(ProgramMethodCatalog.Conditions), Is.EqualTo(new[] { "_0xa", "_0x14", "_0x1e", "_0x28" }));
                // The two-operand operators carry arity 2 with the %P/%S templates.
                Assert.That(One(ProgramMethodCatalog.Commands, "_0x1e").NameTemplate, Is.EqualTo("%P = %S"));
                Assert.That(One(ProgramMethodCatalog.Commands, "_0x28").NameTemplate, Is.EqualTo("%P <> %S"));
                Assert.That(One(ProgramMethodCatalog.Commands, "_0x1e").OperandCount, Is.EqualTo(2));
            });
        }

        // T036: the full vendor set of nine timer commands, with the byte-fidelity templates and arities.
        [Test]
        public void TimerCommands_AreTheFullNine_WithVendorTemplatesAndArities()
        {
            var timer = ProgramMethodCatalog.CommandsFor(ProgramPinType.Timer);
            Assert.Multiple(() =>
            {
                Assert.That(Tokens(timer), Is.EqualTo(new[] { "_0xa", "_0x19", "_0x1e", "_0x5a", "_0x64", "_0xbe", "_0xc8", "_0xd2", "_0xdc" }));
                Assert.That(timer.Where(m => m.OperandCount == 2).Select(m => m.Token),
                    Is.EquivalentTo(new[] { "_0x1e", "_0x5a", "_0x64" }), "= <pin> / = Timer +/- are two-operand");
                Assert.That(One(timer, "_0x19").NameTemplate, Is.EqualTo("%P = Initialværdi"));
                Assert.That(One(timer, "_0x5a").NameTemplate, Is.EqualTo("%P = %P + %S"));
                Assert.That(One(timer, "_0xc8").NameTemplate, Is.EqualTo("Aktiver optælling på %P"));
                Assert.That(One(timer, "_0xdc").NameTemplate, Is.EqualTo("Stands tælling på %P"));
            });
        }

        // T037: the full arithmetic opcode grid (F-108) + commit-legality matrix (F-109) — generic vs mixed (+0x5),
        // and which (op, target-class, operand-class) cells are authorable (a dead cell returns null, never invented).
        [Test]
        public void ArithmeticGrid_EncodesOpcodesAndLegalityMatrix()
        {
            const string INT = "resource_integer", FLT = "resource_floating_point", CTR = "resource_counter";
            Assert.Multiple(() =>
            {
                // + : every pair except float+float; generic _0x5a, mixed _0x5f.
                Assert.That(ProgramMethodCatalog.ArithmeticToken("+", INT, INT), Is.EqualTo("_0x5a"));
                Assert.That(ProgramMethodCatalog.ArithmeticToken("+", INT, FLT), Is.EqualTo("_0x5f"), "mixed add");
                Assert.That(ProgramMethodCatalog.ArithmeticToken("+", FLT, INT), Is.EqualTo("_0x5f"), "float←int add (F-097)");
                Assert.That(ProgramMethodCatalog.ArithmeticToken("+", FLT, FLT), Is.Null, "float+float is a dead cell");
                Assert.That(ProgramMethodCatalog.ArithmeticToken("+", CTR, CTR), Is.EqualTo("_0x5a"), "counter is generic-class");
                // − : float target only (float−float _0x64, float−int _0x69); int/counter targets dead.
                Assert.That(ProgramMethodCatalog.ArithmeticToken("-", FLT, FLT), Is.EqualTo("_0x64"));
                Assert.That(ProgramMethodCatalog.ArithmeticToken("-", FLT, INT), Is.EqualTo("_0x69"), "float−int mixed");
                Assert.That(ProgramMethodCatalog.ArithmeticToken("-", INT, INT), Is.Null, "int−int is dead");
                Assert.That(ProgramMethodCatalog.ArithmeticToken("-", CTR, INT), Is.Null, "counter−int is dead");
                // ÷ : int target only (int÷int _0x6e, int÷float _0x73); float-target dead (F-107).
                Assert.That(ProgramMethodCatalog.ArithmeticToken("/", INT, INT), Is.EqualTo("_0x6e"));
                Assert.That(ProgramMethodCatalog.ArithmeticToken("/", INT, FLT), Is.EqualTo("_0x73"), "int÷float mixed");
                Assert.That(ProgramMethodCatalog.ArithmeticToken("/", FLT, FLT), Is.Null, "float-target ÷ is dead");
                // × : float×float _0x78, mixed _0x7d; int×int dead, counter dead.
                Assert.That(ProgramMethodCatalog.ArithmeticToken("*", FLT, FLT), Is.EqualTo("_0x78"));
                Assert.That(ProgramMethodCatalog.ArithmeticToken("*", FLT, INT), Is.EqualTo("_0x7d"), "mixed multiply");
                Assert.That(ProgramMethodCatalog.ArithmeticToken("*", INT, INT), Is.Null, "int×int is dead");
                Assert.That(ProgramMethodCatalog.ArithmeticToken("*", CTR, FLT), Is.Null, "counter × is dead");
                // the 1-op counter steps.
                Assert.That(Tokens(ProgramMethodCatalog.CounterSteps), Is.EqualTo(new[] { "_0x54", "_0x57" }));
            });
        }

        // T036: the authoring byte-invariance pin — each catalog timer template matches the authentic vendor row
        // stored in project4-PrgTokens.vis (a template diverging from what the vendor persists fails here).
        [Test]
        public async Task TimerCommands_TemplatesMatchTheAuthenticOracleRows()
        {
            Project oracle = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project4-PrgTokens.vis");
            var oracleNameByMethod = oracle.Root.Descendants()
                .Where(e => e.Tag == "action" && e.GetAttribute("method") is not null)
                .GroupBy(e => e.GetAttribute("method")!)
                .ToDictionary(g => g.Key, g => g.First().GetAttribute("name")!);

            Assert.Multiple(() =>
            {
                foreach (ProgramMethod m in ProgramMethodCatalog.CommandsFor(ProgramPinType.Timer))
                    if (oracleNameByMethod.TryGetValue(m.Token, out string? stored))
                        Assert.That(m.NameTemplate, Is.EqualTo(stored), $"timer {m.Token} stores the vendor row verbatim");
            });
        }

        // T039: the timer event + condition operator lists, the withheld dead entries, and the (code, family) scoping.
        [Test]
        public void TimerEventsAndConditions_AreThePinnedLists_WithFamilyScopedSemantics()
        {
            Assert.Multiple(() =>
            {
                // Timer events: -> 0 and is written; the dead two-operand "Timer ->" is never modelled (all are 1-op).
                Assert.That(Tokens(ProgramMethodCatalog.EventsFor(ProgramPinType.Timer)), Is.EqualTo(new[] { "_0xa", "_0x9b" }));
                Assert.That(ProgramMethodCatalog.TimerEvents.Select(m => m.OperandCount), Is.All.EqualTo(1), "no dead two-operand Timer-> event");
                // Timer conditions: = 0, >, >=, <=, counting up/down/stopped; the dead "<" is never modelled.
                Assert.That(Tokens(ProgramMethodCatalog.ConditionsFor(ProgramPinType.Timer)),
                    Is.EqualTo(new[] { "_0xa", "_0x32", "_0x46", "_0x50", "_0xc8", "_0xd2", "_0xdc" }));
                // The comparisons are two-operand, the count-state predicates are 1-op.
                Assert.That(One(ProgramMethodCatalog.TimerConditions, "_0x32").OperandCount, Is.EqualTo(2));
                Assert.That(One(ProgramMethodCatalog.TimerConditions, "_0xdc").OperandCount, Is.EqualTo(1));
                // (code, family) scoping (F-105): _0xc8 is "activate count-up" as a COMMAND but "counting up" as a CONDITION.
                Assert.That(One(ProgramMethodCatalog.CommandsFor(ProgramPinType.Timer), "_0xc8").NameTemplate, Is.EqualTo("Aktiver optælling på %P"));
                Assert.That(One(ProgramMethodCatalog.TimerConditions, "_0xc8").NameTemplate, Is.EqualTo("%P tæller op"));
            });
        }

        // T039: the timer event/condition templates match the authentic vendor rows in project4-PrgTokens-round2.vis.
        [Test]
        public async Task TimerEventsAndConditions_TemplatesMatchTheRound2OracleRows()
        {
            Project oracle = await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project4-PrgTokens-round2.vis");
            string? Name(string tag, string method) => oracle.Root.Descendants()
                .FirstOrDefault(e => e.Tag == tag && e.GetAttribute("method") == method)?.GetAttribute("name");

            Assert.Multiple(() =>
            {
                foreach (ProgramMethod m in ProgramMethodCatalog.TimerEvents)
                    Assert.That(m.NameTemplate, Is.EqualTo(Name("event", m.Token)), $"timer event {m.Token}");
                foreach (ProgramMethod m in ProgramMethodCatalog.TimerConditions)
                    Assert.That(m.NameTemplate, Is.EqualTo(Name("condition", m.Token)), $"timer condition {m.Token}");
            });
        }
    }
}
