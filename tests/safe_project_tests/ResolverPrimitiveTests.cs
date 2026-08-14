using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Review theme 2: the ProjectEditor throwing-resolver primitives <c>Resolve(id, noun)</c> and
    /// <c>RequireTagged(id, ...tags)</c> — the single require-or-throw guards the id-addressed editing entry points
    /// (T011) route through. Pins the resolve/throw and tag-check contract these later tasks depend on.
    /// </summary>
    public class ResolverPrimitiveTests
    {
        private static async Task<ProjectEditor> Editor() =>
            (await new ProjectAppService(TestSetup.Settings).Load("testdata/projects/project3-KompleksWired.vis")).Edit();

        private static ElementId Absent
        {
            get { ElementId.TryParse("_0xdead01", out ElementId id); return id; }
        }

        // A stale id is an expected condition, not an engine fault, so the miss is a REFUSAL carrying the caller's
        // Danish noun — the same sentence EditContext.RequireExists composes on the Evaluate side, so a bundled
        // gesture and a one-at-a-time sequence answer a stale id in one voice (see RefusalLanguageTests).
        [Test]
        public async Task Resolve_LiveId_ReturnsHandle_AbsentId_RefusesWithNoun()
        {
            ProjectEditor editor = await Editor();
            ElementId group = editor.ToProject().Groups.First().Id!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(editor.Resolve(group, "Lokaliteten").Tag, Is.EqualTo("group"), "a live id resolves to its handle");
                var ex = Assert.Throws<Ihc.Vis.Session.EditRefusedException>(() => editor.Resolve(Absent, "Dimsen"));
                Assert.That(ex!.Message, Is.EqualTo("Dimsen findes ikke længere."),
                    "the refusal splices the caller's noun into the shared Danish sentence");
            });
        }

        // T011: the id-addressed entry points now route their tag guard through RequireTagged; the wrong-tag throw
        // must still surface (name the mismatch) so a caller/GUI sees why the operation was refused.
        [Test]
        public async Task Group_OnNonGroupId_StillSurfacesWrongTagMessage()
        {
            ProjectEditor editor = await Editor();
            ElementId product = editor.ToProject().Root.Descendants()
                .First(e => ProductClassifier.IsProduct(e.Tag) && e.Id is not null).Id!.Value;

            var ex = Assert.Throws<System.InvalidOperationException>(() => editor.Group(product));
            Assert.That(ex!.Message, Does.Contain("group").And.Contain("product"),
                "the routed entry point still surfaces the actual-vs-expected tag mismatch");
        }

        [Test]
        public async Task RequireTagged_MatchingTag_Returns_WrongTag_ThrowsNamingBoth()
        {
            ProjectEditor editor = await Editor();
            ElementId group = editor.ToProject().Groups.First().Id!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(editor.RequireTagged(group, "group").Tag, Is.EqualTo("group"), "a matching tag returns the element");
                Assert.That(editor.RequireTagged(group, "functionblock", "group").Tag, Is.EqualTo("group"),
                    "one of several accepted tags matches");
                var ex = Assert.Throws<System.InvalidOperationException>(() => editor.RequireTagged(group, "functionblock"));
                Assert.That(ex!.Message, Does.Contain("group").And.Contain("functionblock"),
                    "the wrong-tag throw names the actual and the expected tag");
            });
        }
    }
}
