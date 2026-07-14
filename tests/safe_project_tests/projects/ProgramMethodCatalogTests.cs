using System.Linq;

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
                Assert.That(One(ProgramMethodCatalog.Arithmetic, "_0x64").NameTemplate, Is.EqualTo("%P = %P − %S"));
                Assert.That(One(ProgramMethodCatalog.Arithmetic, "_0x64").OperatorSymbol, Is.EqualTo("−"));
                Assert.That(ProgramMethodCatalog.Arithmetic.Select(m => m.OperandCount), Is.All.EqualTo(2));
            });
        }

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
