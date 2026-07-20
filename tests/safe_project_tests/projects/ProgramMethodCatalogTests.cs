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
                    Is.EqualTo(new ProgramMethod(ProgramMethodCategory.Event, "_0xa", "%P -> ON", "Start program when %P changes to ON", 1, null)));
                Assert.That(One(ProgramMethodCatalog.Events, "_0x96").NameTemplate, Is.EqualTo("%P changes state"));
                Assert.That(One(ProgramMethodCatalog.Events, "_0x9b").NameTemplate, Is.EqualTo("%P is assigned"));
                Assert.That(ProgramMethodCatalog.Events.Select(m => m.OperandCount), Is.All.EqualTo(1));
            });
        }

        [Test]
        public void Commands_CarryVendorTemplates()
        {
            Assert.Multiple(() =>
            {
                Assert.That(One(ProgramMethodCatalog.Commands, "_0xa"),
                    Is.EqualTo(new ProgramMethod(ProgramMethodCategory.Command, "_0xa", "%P = ON", "Sets %P to ON", 1, null)));
                Assert.That(One(ProgramMethodCatalog.Commands, "_0x14").NameTemplate, Is.EqualTo("%P = OFF"));
                Assert.That(One(ProgramMethodCatalog.Commands, "_0x23").NameTemplate, Is.EqualTo("Toggle %P"));
            });
        }

        [Test]
        public void Conditions_CarryVendorTemplatesIncludingNotVariant()
        {
            Assert.Multiple(() =>
            {
                Assert.That(One(ProgramMethodCatalog.Conditions, "_0xa").NameTemplate, Is.EqualTo("%P = ON"));
                Assert.That(One(ProgramMethodCatalog.Conditions, "_0x14").NameTemplate, Is.EqualTo("%P = OFF"));
                Assert.That(One(ProgramMethodCatalog.Conditions, "_0x28").NameTemplate, Is.EqualTo("%P <> ON"), "the NOT variant");
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
                .Concat(ProgramMethodCatalog.Arithmetic);
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
    }
}
