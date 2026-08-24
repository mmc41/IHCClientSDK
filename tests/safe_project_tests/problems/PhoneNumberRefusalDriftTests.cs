using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Ihc.Vis.Problems;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The telephone sentence has THREE copies, and this is what holds them together.
    ///
    /// <para>One copy is the operative rule's, <c>DialogValueRule.PhoneNumber.Refusal</c> in
    /// <c>Ihc.Vis.Products</c>, which the dialog shows inline as the installer types. One is the refusing site's,
    /// written beside its code in <c>ProductDialogCommands</c>, because <c>Ihc.Vis.Session</c> may not read the
    /// catalogue. The third is the catalogue's own template on
    /// <c>edit.field-phonenumber-malformed</c> — the entry that governs the code that site raises.</para>
    ///
    /// <para><b>Why this is a new fixture rather than a row in <see cref="RefusalLabelDriftTests"/>.</b> That gate
    /// reflects literal <c>RefusalIdentity</c> members and pairs each one's <c>CauseLabel</c> with its own code's
    /// template. Neither of these copies is a <c>RefusalIdentity</c>: one is a value on a product-layer rule and
    /// one is an interpolated string at a command site, so nothing reflective reaches either and the existing gate
    /// cannot merely be extended.</para>
    ///
    /// <para><b>Slot-elided, because the copies differ by exactly the slot.</b> The entry binds the offending value
    /// (<c>'{value}'</c>); the rule's sentence is the same guidance with no value to name, because it is shown
    /// while the field is still being typed. Comparing them means removing the slot and requiring the rest to be
    /// identical — and the elision is itself asserted to remove something, so the comparison cannot go vacuous if
    /// the template ever loses its slot.</para>
    /// </summary>
    [TestFixture]
    public sealed class PhoneNumberRefusalDriftTests
    {
        /// <summary>The refusal code the commit site raises, whose entry governs the sentence it shows.</summary>
        private static readonly ProblemCode RefusalCode = new("edit.field-phonenumber-malformed");

        /// <summary>The project-finding row that reports the same condition on a file that already carries it.</summary>
        private static readonly ProblemCode FindingCode = new("addr-modem-phonenumber-malformed");

        private static string TemplateOf(ProblemCode code)
        {
            Assert.That(ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry), Is.True,
                $"{code.Value} needs a catalogue entry before its sentence can be governed");
            return entry.MessageTemplate;
        }

        /// <summary>
        /// The gate's one comparison, as a predicate so the armed control can NEGATE it rather than restate it:
        /// the template with its quoted value slot removed must be the rule's sentence, word for word.
        /// </summary>
        private static bool Agrees(string template, string refusal) =>
            string.Equals(Elide(template), refusal, StringComparison.Ordinal);

        /// <summary>Removes the quoted <c>{value}</c> slot and the space that separates it from the next word.</summary>
        private static string Elide(string template) => template.Replace("'{value}' ", string.Empty);

        [Test]
        public void TheDialogRulesSentenceIsTheRefusalEntrysTemplate()
        {
            string template = TemplateOf(RefusalCode);

            Assert.Multiple(() =>
            {
                Assert.That(Elide(template), Is.Not.EqualTo(template),
                    "the elision must actually remove a slot — otherwise this comparison would pass on a "
                    + "template that had quietly lost its {value} binding, which is the drift it exists to catch");
                Assert.That(Agrees(template, DialogValueRule.PhoneNumber.Refusal), Is.True,
                    "the rule's sentence and the entry's template must be the same words; the rule's layer "
                    + "cannot read the catalogue, so nothing but this test keeps them in step");
            });
        }

        /// <summary>
        /// The whole-project row states the same guidance as the refusal, and deliberately so: both read ONE
        /// predicate. Pinning them to each other is what stops the file finding and the commit refusal from
        /// telling an installer two different things about one number.
        /// </summary>
        [Test]
        public void TheProjectFindingSaysTheSameAsTheRefusal()
        {
            Assert.That(TemplateOf(FindingCode), Is.EqualTo(TemplateOf(RefusalCode)));
        }

        /// <summary>
        /// The site's own copy, measured through the door rather than read off the source: the sentence a refused
        /// commit actually carries must be the entry's template with the offending value bound into its slot.
        /// </summary>
        [Test]
        public async Task TheCommitSitesSentenceIsThatTemplateWithTheValueBound()
        {
            const string offending = "12";
            var app = new ProjectAppService(TestSetup.Settings);
            Project project = await app.Load("testdata/projects/project3-KompleksWired.vis");
            ProductDefinition modem = app.GetAvailableProducts().First(p => p.ProductIdentifier == "_0x3103");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId id = session.Apply(new AddProduct(project.Groups.First().Id!.Value, modem)).Value;
            DialogDescriptorField slot = app.GetProductDialog(session.Current!, id)
                .Groups.Single(g => g.Id == "telefonnumre").Fields[0];

            EditOutcome outcome = session.Apply(new ApplyProductDialog(id,
                ImmutableArray.Create(new ProductDialogEdit(slot.Target, "phonenumber", offending))));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Is.EqualTo(TemplateOf(RefusalCode).Replace("{value}", offending)),
                    "the refusing site carries its own copy of the entry's template — this is the only thing "
                    + "keeping that copy honest");
            });
        }

        /// <summary>
        /// The armed control. A gate that only ever runs against agreeing copies is a gate nobody knows can fail,
        /// so both sides are drifted by one character and the SAME predicate the gate asserts through must come
        /// out false for each.
        /// </summary>
        [Test]
        public void TheGateIsArmed()
        {
            string template = TemplateOf(RefusalCode);
            string refusal = DialogValueRule.PhoneNumber.Refusal;

            Assert.Multiple(() =>
            {
                Assert.That(Agrees(template, refusal), Is.True,
                    "the undrifted pair must agree, or the control is measuring the wrong thing");
                Assert.That(Agrees(template + " x", refusal), Is.False, "a drifted template must fail");
                Assert.That(Agrees(template, refusal + " x"), Is.False, "a drifted refusal must fail too");
            });
        }
    }
}
