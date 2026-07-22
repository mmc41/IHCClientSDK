using System.Linq;
using System.Threading.Tasks;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// A follow-link may only be wired the way IHC Visual wires one. The rule is <b>data flow</b>, not
    /// kind matching: the source end must produce a signal, the sink end must consume one, and at least one
    /// end must be a function-block pin — a product pin never links straight to another product pin, because
    /// every product-to-product path in IHC runs through a function block.
    /// <para>
    /// The oracle is a 15-cell matrix driven against IHC Visual itself (`tmp\comptest\out\C2\census2.md` §C5,
    /// findings F-058/F-059/F-060), corroborated by every follow-link in the vendor-authored `.vis` corpus
    /// (397 links over 21 projects): <c>dataline_input</c> and <c>airlink_input</c> hold a
    /// <c>link_from_resource</c> half 160/160 times and a <c>link_to_resource</c> half never;
    /// <c>resource_input</c> holds a to-half 314/314 and a from-half never; <c>resource_output</c> holds a
    /// from-half 237/237 and a to-half never.
    /// </para>
    /// <para>
    /// ⚠ A tempting "inputs↔inputs, outputs↔outputs" reading is <b>wrong</b> and these cases pin that down:
    /// <see cref="ProductOutput_ToFbInput_IsLegal"/> (crossed kinds, yet legal) and
    /// <see cref="ProductOutput_ToFbOutput_IsRejected"/> (matching kinds, yet rejected).
    /// </para>
    /// </summary>
    public class LinkLegalityTests
    {
        // An id is _0x<counter><typecode>: the HIGH byte is the project-unique counter, the LOW byte is the
        // tag's TypeCode. Both halves matter — a reused counter trips the edit-commit invariant, and a
        // typecode that disagrees with the tag is not the element the allocator thinks it is.
        private const string Tryk   = "_0x605a";   // dataline_input  0x5a — a button: produces a signal, never consumes one
        private const string Led    = "_0x615b";   // dataline_output 0x5b — an output terminal: drivable AND state-readable
        private const string Kip    = "_0x6211";   // resource_input  0x11 — an FB trigger: consumes only
        private const string OnPuls = "_0x6312";   // resource_output 0x12 — an FB result: produces only

        /// <summary>One locality holding one product (button + output) and one function block (input + output pin).</summary>
        private static ProjectEditor NewEditor()
        {
            ProjectElement product = Node("product_dataline", "_0x5153",
                new[] { ("product_identifier", "_0x2107"), ("name", "LK FUGA Tryk 6 tast 3 dioder") },
                Node("dataline_input",  Tryk, new[] { ("name", "Tryk (midt højre)") }),
                Node("dataline_output", Led,  new[] { ("name", "LED (øverst)") }));

            ProjectElement block = Node("functionblock", "_0x5228", new[] { ("name", "Lampeudtag i loft") },
                Node("inputs",  "_0x5323", new[] { ("name", "Input") },
                    Node("resource_input", Kip, new[] { ("name", "Kip") })),
                Node("outputs", "_0x5424", new[] { ("name", "Output") },
                    Node("resource_output", OnPuls, new[] { ("name", "ON puls") })));

            ProjectElement root = Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("last_unique_id", "_0x70") },
                Node("groups", "_0x2031", new[] { ("name", "Lokaliteter") },
                    Node("group", "_0x2132", new[] { ("name", "Entré/Gang") }, product, block)));

            return new Project(root).Edit();
        }

        private static ElementId Id(string token)
        {
            ElementId.TryParse(token, out ElementId id);
            return id;
        }

        private static int HalvesOn(Project project, string pin, string halfTag)
        {
            int count = 0;
            foreach (ProjectElement child in project.FindById(Id(pin))!.Children)
            {
                if (child.Tag == halfTag)
                    count++;
            }
            return count;
        }

        // ----- the 4 shapes IHC Visual accepts (and the only 4 its corpus ever contains) -----

        [TestCase(Tryk, Kip, TestName = "ProductInput_ToFbInput")]              // cell 1  · 134 in corpus
        [TestCase(OnPuls, Led, TestName = "FbOutput_ToProductOutput")]          // cell 2  ·  83 in corpus
        [TestCase(OnPuls, Kip, TestName = "FbOutput_ToFbInput")]                // T1      · 154 in corpus
        public void LegalShapes_AreAccepted_AndWriteBothHalves(string from, string to)
        {
            ProjectEditor editor = NewEditor();

            Assert.That(editor.CanLink(Id(from), Id(to)), Is.True, "IHC Visual accepts this shape");

            editor.Link(Id(from), Id(to));
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(HalvesOn(after, from, "link_from_resource"), Is.EqualTo(1),
                    "the source end carries the from-half — every vendor link does");
                Assert.That(HalvesOn(after, to, "link_to_resource"), Is.EqualTo(1),
                    "the sink end carries the to-half");
                Assert.That(HalvesOn(after, from, "link_to_resource"), Is.Zero, "the source is never a sink");
                Assert.That(HalvesOn(after, to, "link_from_resource"), Is.Zero, "the sink is never a source");
            });
        }

        // D06 (T019): CanLink(pin, pin) must AGREE with Link — and both now REFUSE a pin-to-itself link. A value FB
        // pin (resource_flag) is tag-linkable, so id == id is the only reason it is refused; the engine rejects the
        // self-link the vendor never produces, closing the earlier divergence where CanLink allowed what only the
        // session refused. The refusal is clean — it fails before any half is written.
        [Test]
        public async Task SelfLink_SamePin_CanLinkAndLinkBothRefuse()
        {
            Project project = await new ProjectAppService(TestSetup.Settings)
                .Load("testdata/projects/project2-CustomBlock.vis");
            ProjectEditor editor = project.Edit();
            ElementId flag = project.Root.Descendants()
                .First(e => e.Tag == "resource_flag" && e.Id is not null).Id!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(editor.CanLink(flag, flag), Is.False,
                    "a pin cannot link to itself (D06) — CanLink agrees with Link");
                Assert.That(() => editor.Link(flag, flag), Throws.InvalidOperationException,
                    "Link refuses the self-link too — no engine/session/doc divergence");
                ProjectElement pin = editor.ToProject().FindById(flag)!;
                Assert.That(pin.ChildrenOrEmpty().Any(c => c.Tag is "link_from_resource" or "link_to_resource"), Is.False,
                    "the refused self-link wrote no half — it fails before any mutation");
            });
        }

        /// <summary>
        /// Crossed kinds, yet legal: an output terminal's state is a perfectly good signal source. Measured
        /// as accepted on the vendor 3×. The corpus never uses it, but "unused" is not "refused" — and a
        /// kind-matching rule would reject it.
        /// </summary>
        [Test]
        public void ProductOutput_ToFbInput_IsLegal()
        {
            ProjectEditor editor = NewEditor();
            Assert.That(editor.CanLink(Id(Led), Id(Kip)), Is.True,
                "vendor cell 3: dataline_output → resource_input is accepted");
        }

        // A-16amd/US-033b (F-080): a block output feeding its OWN input (both pins in one block) is a legitimate
        // feedback pattern the vendor allows — the data-flow rule is the only gate, there is no same-block refusal.
        // The measured negatives (a sink that cannot consume) still refuse.
        [Test]
        public void SelfLink_OutputToOwnInput_IsAllowed()
        {
            ProjectEditor editor = NewEditor();
            Assert.Multiple(() =>
            {
                Assert.That(editor.CanLink(Id(OnPuls), Id(Kip)), Is.True,
                    "a block output → its own input (feedback) is legal — OnPuls and Kip are the same block's pins");
                Assert.That(editor.CanLink(Id(OnPuls), Id(Tryk)), Is.False,
                    "the 3-clause rule still refuses a sink that cannot consume (a button)");
            });

            editor.Link(Id(OnPuls), Id(Kip));
            Project after = editor.ToProject();
            Assert.Multiple(() =>
            {
                Assert.That(HalvesOn(after, OnPuls, "link_from_resource"), Is.EqualTo(1), "the source end carries the from-half");
                Assert.That(HalvesOn(after, Kip, "link_to_resource"), Is.EqualTo(1), "the sink end carries the to-half");
            });
        }

        // ----- the refusals: a sink that cannot consume, or a source that cannot produce -----

        [TestCase(OnPuls, Tryk, TestName = "FbOutput_ToProductInput_ButtonIsNotDrivable")]   // cell 4
        [TestCase(Tryk, OnPuls, TestName = "ProductInput_ToFbOutput_FbResultIsNotASink")]    // cell 5
        [TestCase(Kip, Led, TestName = "FbInput_ToProductOutput_FbTriggerIsNotASource")]     // cell 7
        [TestCase(Kip, Tryk, TestName = "FbInput_ToProductInput_NeitherEndFits")]            // cell 8
        [TestCase(Kip, OnPuls, TestName = "FbInput_ToFbOutput_Reversed")]                    // T2
        [TestCase(Kip, Kip, TestName = "FbInput_ToItself")]                                  // T4 shape
        public void IllegalShapes_AreRejected(string from, string to)
        {
            ProjectEditor editor = NewEditor();

            Assert.That(editor.CanLink(Id(from), Id(to)), Is.False, "IHC Visual refuses this shape");
            Assert.That(() => editor.Link(Id(from), Id(to)),
                Throws.InvalidOperationException,
                "Link must fail loudly rather than write a link the vendor would never produce");
        }

        /// <summary>
        /// Matching kinds, yet refused — and the sharpest cell in the matrix: the SAME pin pair
        /// (<c>LED</c> ↔ <c>ON puls</c>) is <b>accepted</b> as <c>ON puls → LED</c> (cell 2) and
        /// <b>refused</b> as <c>LED → ON puls</c>. Direction decides; the pair alone does not.
        /// </summary>
        [Test]
        public void ProductOutput_ToFbOutput_IsRejected()
        {
            ProjectEditor editor = NewEditor();
            Assert.Multiple(() =>
            {
                Assert.That(editor.CanLink(Id(Led), Id(OnPuls)), Is.False, "vendor cell 6: refused");
                Assert.That(editor.CanLink(Id(OnPuls), Id(Led)), Is.True, "vendor cell 2: the same pair, accepted");
            });
        }

        // ----- no function-block end: IHC routes every product-to-product path through a block -----

        [TestCase(Tryk, Led, TestName = "ProductInput_ToProductOutput_BypassesTheBlock")]    // U1
        [TestCase(Led, Tryk, TestName = "ProductOutput_ToProductInput")]                     // U2
        [TestCase(Led, Led, TestName = "ProductOutput_ToProductOutput")]                     // U3
        public void ProductToProduct_IsRejected_EvenWhenBothEndsFitTheFlow(string from, string to)
        {
            ProjectEditor editor = NewEditor();

            Assert.That(editor.CanLink(Id(from), Id(to)), Is.False,
                "the vendor cannot wire two product pins together at all — a function block always sits between");
            Assert.That(() => editor.Link(Id(from), Id(to)), Throws.InvalidOperationException);
        }

        /// <summary>A rejected link must leave the project untouched — no half-written link, no burnt ids.</summary>
        [Test]
        public void RejectedLink_MutatesNothing()
        {
            ProjectEditor editor = NewEditor();

            Assert.That(() => editor.Link(Id(Tryk), Id(Led)), Throws.InvalidOperationException);

            Project after = editor.ToProject();
            Assert.Multiple(() =>
            {
                Assert.That(after.FindById(Id(Tryk))!.Children, Is.Empty, "no half appended to the source");
                Assert.That(after.FindById(Id(Led))!.Children, Is.Empty, "no half appended to the target");
                Assert.That(after.Root.GetAttribute("last_unique_id"), Is.EqualTo("_0x70"),
                    "no id was allocated for a link that was never written");
            });
        }

        /// <summary>
        /// Pin kinds the vendor corpus never exercises (wireless outputs, flags) stay permitted: this guard
        /// encodes only what was measured. Refusing an unmeasured kind would break real wiring — a wireless
        /// relay output is the airlink twin of <c>dataline_output</c>, and <c>resource_flag</c> is US-033b's
        /// block-to-block variable link.
        /// </summary>
        [TestCase("airlink_relay", "_0x645e", TestName = "WirelessRelayOutput_StaysLinkable")]
        [TestCase("resource_flag", "_0x650a", TestName = "BlockFlag_StaysLinkable")]
        public void UnmeasuredPinKinds_AreNotRefused(string tag, string pinId)
        {
            ProjectElement product = Node("product_airlink", "_0x5154",
                new[] { ("product_identifier", "_0x4201"), ("name", "W") },
                Node(tag, pinId, new[] { ("name", "Pin") }));
            ProjectElement block = Node("functionblock", "_0x5228", new[] { ("name", "FB") },
                Node("outputs", "_0x5424", new[] { ("name", "Output") },
                    Node("resource_output", OnPuls, new[] { ("name", "ON puls") })));
            ProjectElement root = Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("last_unique_id", "_0x70") },
                Node("groups", "_0x2031", new[] { ("name", "Lokaliteter") },
                    Node("group", "_0x2132", new[] { ("name", "A") }, product, block)));

            ProjectEditor editor = new Project(root).Edit();

            Assert.That(editor.CanLink(Id(OnPuls), Id(pinId)), Is.True,
                "no measurement says this kind is not a sink — do not refuse it on a guess");
        }
    }
}
