using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T041: writing a product's configurable CONSTANT — the value behind a row of the sensors' <i>Indstillinger</i>
    /// grid, which the vendor edits through <i>Rediger konstant</i>.
    ///
    /// <para>Measured on build 3.4.72.3: the editor writes <c>inivalue</c> on the flagged element;
    /// an absent attribute becomes <c>"2.50"</c>, negatives are accepted (<c>"-1.50"</c>), and returning the value
    /// to its default <b>removes the attribute</b>, so the file round-trips content-identical.</para>
    ///
    /// <para>The last of those needs no code of its own: <c>inivalue</c> is declared with a default, and the
    /// serializer omits a defaulted attribute carrying exactly its default
    /// (<c>AttrSchema.OmitsOnWrite</c> — the one omit rule the writer and the round-trip verifier share). The
    /// tests below prove that this is what actually happens rather than what ought to, because "the bytes come
    /// back" is the whole claim.</para>
    /// </summary>
    public class ProductSettingCommandTests
    {
        private const string TemperatureSensor = "_0x2124";

        private static ProjectAppService App => new(TestSetup.Settings);

        private static async Task<byte[]> Bytes(Project project)
        {
            using var ms = new MemoryStream();
            await App.Save(project, ms);
            return ms.ToArray();
        }

        /// <summary>A project holding one temperature sensor, whose calibration settings are untouched.</summary>
        private static async Task<(ProjectDocumentSession Session, ElementId Product, ElementId Setting)> Placed()
        {
            Project project = await App.Load("testdata/projects/Project1-SimpelWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId locality = project.Groups.First().Id!.Value;
            ElementId product = session.Apply(new AddProduct(locality,
                App.GetAvailableProducts().First(p => p.ProductIdentifier == TemperatureSensor))).Value;
            ElementId setting = new ProductView(session.Current!, session.Current!.FindById(product)!)
                .SettingElements.First().Id!.Value;
            return (session, product, setting);
        }

        private static ApplyProductDialog Visit(ElementId product, ElementId setting, ResourceInitialValue value) =>
            new(product, EquatableArray<ProductDialogEdit>.Empty)
            {
                SettingEdits = EquatableArray.Create([new ProductDialogSettingEdit(setting, value)]),
            };

        /// <summary>The value reaches the file, in the format the resource type stores.</summary>
        [Test]
        public async Task EditingAConstant_WritesInivalueOnTheFlaggedElement()
        {
            (ProjectDocumentSession session, ElementId product, ElementId setting) = await Placed();

            EditOutcome result = session.Apply(Visit(product, setting, ResourceInitialValue.OfDecimal(2.5)));
            Project reloaded = ProjectReader.Read(await Bytes(session.Current!));

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(reloaded.FindById(setting)!.GetAttribute("inivalue"), Is.EqualTo("2.50"),
                    "written as the measured vendor edit wrote it, and still there after a save and a reload");
            });
        }

        /// <summary>A negative offset is an ordinary calibration, not an error.</summary>
        [Test]
        public async Task ANegativeConstant_IsAccepted()
        {
            (ProjectDocumentSession session, ElementId product, ElementId setting) = await Placed();

            session.Apply(Visit(product, setting, ResourceInitialValue.OfDecimal(-1.5)));
            Project reloaded = ProjectReader.Read(await Bytes(session.Current!));

            Assert.That(reloaded.FindById(setting)!.GetAttribute("inivalue"), Is.EqualTo("-1.50"));
        }

        /// <summary>
        /// THE ROUND TRIP. Setting a constant and putting it back leaves the file exactly as it was — not merely
        /// reading back as zero, but byte-for-byte the same, because the attribute is gone again.
        /// </summary>
        [Test]
        public async Task ReturningAConstantToItsDefault_LeavesTheFileByteIdentical()
        {
            (ProjectDocumentSession session, ElementId product, ElementId setting) = await Placed();
            byte[] before = await Bytes(session.Current!);

            session.Apply(Visit(product, setting, ResourceInitialValue.OfDecimal(2.5)));
            byte[] edited = await Bytes(session.Current!);

            session.Apply(Visit(product, setting, ResourceInitialValue.OfDecimal(0)));
            byte[] restored = await Bytes(session.Current!);

            Assert.Multiple(() =>
            {
                Assert.That(edited, Is.Not.EqualTo(before),
                    "precondition: the edit really did change the file, so the restore has something to undo");
                Assert.That(session.Current!.FindById(setting)!.GetAttribute("inivalue"), Is.Null.Or.EqualTo("0.00"),
                    "the in-memory value is back at the default, however the model spells it");
                Assert.That(restored, Is.EqualTo(before),
                    "and the SAVED BYTES are the original's: omit-if-default removed the attribute again");
            });
        }

        /// <summary>
        /// The visit is ONE commit, settings included: <i>Fortryd</i> after the dialog's OK takes back the
        /// constant along with everything else.
        /// </summary>
        [Test]
        public async Task TheConstantIsPartOfTheVisitsSingleUndoStep()
        {
            (ProjectDocumentSession session, ElementId product, ElementId setting) = await Placed();

            session.Apply(Visit(product, setting, ResourceInitialValue.OfDecimal(2.5)));
            session.Undo();

            Assert.That(session.Current!.FindById(setting)!.GetAttribute("inivalue"), Is.Null.Or.EqualTo("0.00"),
                "one act, one undo entry");
        }

        /// <summary>
        /// An element that is not a flagged setting is REFUSED. Every product is full of resources that are not
        /// settings, so without this the command would be a way to write <c>inivalue</c> on any of them through a
        /// dialog that never offered it.
        /// </summary>
        [Test]
        public async Task AnElementThatIsNotAFlaggedSetting_IsRefused()
        {
            (ProjectDocumentSession session, ElementId product, ElementId setting) = await Placed();
            ElementId notASetting = session.Current!.FindById(product)!.DescendantsAndSelf()
                .First(e => e.Id is not null && e.Id != setting
                    && e.GetAttribute("setting") != "yes" && e.Kind == ElementKind.DatalinePin).Id!.Value;

            EditOutcome result = session.Apply(Visit(product, notASetting, ResourceInitialValue.OfDecimal(2.5)));

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(result.Code.Value, Is.EqualTo("edit.target-wrong-kind"));
                Assert.That(result.Reason, Is.EqualTo("Målet er ikke en indstilling."));
            });
        }

        /// <summary>And one belonging to a DIFFERENT product, for the same reason the field edits are checked.</summary>
        [Test]
        public async Task ASettingOutsideTheProduct_IsRefused()
        {
            (ProjectDocumentSession session, ElementId product, _) = await Placed();
            ElementId locality = session.Current!.Groups.First().Id!.Value;
            ElementId other = session.Apply(new AddProduct(locality,
                App.GetAvailableProducts().First(p => p.ProductIdentifier == TemperatureSensor))).Value;
            ElementId elsewhere = new ProductView(session.Current!, session.Current!.FindById(other)!)
                .SettingElements.First().Id!.Value;

            EditOutcome result = session.Apply(Visit(product, elsewhere, ResourceInitialValue.OfDecimal(2.5)));

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(result.Code.Value, Is.EqualTo("edit.field-outside-product"));
            });
        }
    }
}
