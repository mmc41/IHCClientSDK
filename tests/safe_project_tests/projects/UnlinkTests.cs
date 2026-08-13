using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// <see cref="ProjectEditor.Unlink"/> must remove exactly the reciprocal pair between the two resources and
    /// fail loudly when that pair does not exist — never touching other links. Guards the multi-link topologies
    /// where the previous first-child-of-tag pairing corrupted the bijection: an owner with several outgoing
    /// links, a shared sink, a repeated unlink, and a never-linked pair.
    /// </summary>
    public class UnlinkTests
    {
        private const string Oracle = "Project1-SimpelWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        private static int LinkChildCount(Project project, ElementId id, string tag)
        {
            ProjectElement element = project.FindById(id)!;
            return element.Children.IsEmpty ? 0 : element.Children.Count(c => c.Tag == tag);
        }

        [Test]
        public async Task Unlink_MultiLinkOwner_RemovesExactlyTheRequestedPair()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();
            GroupRef stue = editor.Group("Stue");
            GroupRef entre = editor.Group("Entré");

            // A button feeding two block inputs: the only multi-link-owner shape IHC Visual can actually
            // produce (a product pin never links to another product pin — LinkLegalityTests).
            ResourceRef a = stue.Product("LK FUGA Tryk 2 tast").Input("Tryk (venstre)");   // wired to Kip in the oracle
            ResourceRef b = stue.FunctionBlock("1.1.01.e. Kip tænd sluk").Input("Sluk");   // wired from Tryk (højre) in the oracle
            ResourceRef c = entre.FunctionBlock("1.4.02.a. PIR styring ").Input("PIR");    // wired from the PIR product in the oracle

            editor.Link(a, b).Link(a, c);   // a now owns three from-halves (oracle + b + c)
            editor.Unlink(a, b);
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(LinkChildCount(after, a.Id!.Value, "link_from_resource"), Is.EqualTo(2),
                    "a keeps its oracle link and the authored link to c");
                Assert.That(LinkChildCount(after, b.Id!.Value, "link_to_resource"), Is.EqualTo(1),
                    "b keeps only its oracle link");
                Assert.That(LinkChildCount(after, c.Id!.Value, "link_to_resource"), Is.EqualTo(2),
                    "the unrelated a–c pair is untouched");
                ProjectValidationResult v = app.Validate(after);
                Assert.That(v.IsValid, Is.True, "bijection intact: " + string.Join(" | ", v.Errors));
            });
        }

        [Test]
        public async Task Unlink_NotLinkedPair_Throws_AndMutatesNothing()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ResourceRef a = editor.Group("Stue").Product("LK FUGA Tryk 2 tast").Input("Tryk (venstre)");
            ResourceRef b = editor.Group("Entré").Product("Stikkontakt").Output("Udgang");
            Project before = editor.ToProject();

            Assert.That(() => editor.Unlink(a, b), Throws.InvalidOperationException,
                "unlinking a never-linked pair is an error, not a silent (or destructive) no-op");
            Assert.That(editor.ToProject(), Is.EqualTo(before), "a failed unlink mutates nothing");
        }

        [Test]
        public async Task Unlink_Twice_SecondCallThrows_AndOtherLinksSurvive()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();
            ResourceRef a = editor.Group("Stue").Product("LK FUGA Tryk 2 tast").Input("Tryk (venstre)");
            ResourceRef b = editor.Group("Stue").FunctionBlock("1.1.01.e. Kip tænd sluk").Input("Sluk");

            editor.Link(a, b);
            editor.Unlink(a, b);
            Project between = editor.ToProject();

            Assert.That(() => editor.Unlink(a, b), Throws.InvalidOperationException, "the pair is already gone");
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(after, Is.EqualTo(between), "the failed second unlink mutates nothing");
                Assert.That(LinkChildCount(after, a.Id!.Value, "link_from_resource"), Is.EqualTo(1),
                    "a's oracle link survives both calls");
                Assert.That(LinkChildCount(after, b.Id!.Value, "link_to_resource"), Is.EqualTo(1),
                    "b's oracle link survives both calls");
                ProjectValidationResult v = app.Validate(after);
                Assert.That(v.IsValid, Is.True, "bijection intact: " + string.Join(" | ", v.Errors));
            });
        }

        [Test]
        public void Unlink_NonCanonicalLinkTokens_ResolvesThePair()
        {
            // Finding 13: a foreign file whose reciprocal halves are spelled with leading zeros ("_0x0302" for the
            // id "_0x302") — ElementId-equal but not string-equal. Unlink must resolve the pair by parsed id, not
            // throw "not follow-linked in this orientation".
            const string follow = "Følg Link";
            Project project = new Project(Tree.Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x6000") },
                Tree.Node("groups", "_0x2031", new[] { ("name", "L") },
                    Tree.Node("group", "_0x2132", new[] { ("name", "Stue") },
                        Tree.Node("product_dataline", "_0x5153", new[] { ("product_identifier", "_0x2202"), ("name", "PA") },
                            Tree.Node("dataline_input", "_0x201", new[] { ("name", "InA") },
                                Tree.Node("link_from_resource", "_0x301", new[] { ("name", follow), ("icon", "_0x47"), ("link", "_0x0302") }))),
                        Tree.Node("product_dataline", "_0x5154", new[] { ("product_identifier", "_0x2202"), ("name", "PB") },
                            Tree.Node("dataline_output", "_0x202", new[] { ("name", "OutB") },
                                Tree.Node("link_to_resource", "_0x302", new[] { ("name", follow), ("icon", "_0x4a"), ("link", "_0x0301") })))))));

            ProjectEditor editor = project.Edit();
            ResourceRef inA = editor.Group("Stue").Product("PA").Input("InA");
            ResourceRef outB = editor.Group("Stue").Product("PB").Output("OutB");

            Assert.That(() => editor.Unlink(inA, outB), Throws.Nothing,
                "a reciprocal pair spelled with leading-zero tokens must resolve, not throw");

            Assert.Multiple(() =>
            {
                Assert.That(editor.GetLinks(inA.Id!.Value), Is.Empty, "the from-half is removed");
                Assert.That(editor.GetLinks(outB.Id!.Value), Is.Empty, "the to-half is removed");
            });
        }

        [Test]
        public async Task Unlink_WrongOrientation_Throws()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ResourceRef a = editor.Group("Stue").Product("LK FUGA Tryk 2 tast").Input("Tryk (venstre)");
            ResourceRef b = editor.Group("Stue").FunctionBlock("1.1.01.e. Kip tænd sluk").Input("Sluk");
            editor.Link(a, b);

            Assert.That(() => editor.Unlink(b, a), Throws.InvalidOperationException,
                "Unlink mirrors Link's from/to orientation");
            editor.Unlink(a, b);   // correct orientation still works afterwards
        }
    }
}
