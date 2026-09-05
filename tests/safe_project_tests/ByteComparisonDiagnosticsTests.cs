using System;
using System.Text;

using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The machinery that explains a byte-fidelity failure.
    ///
    /// These paths run only WHEN a comparison has already failed, which is why nothing exercised them - and
    /// exactly why they are worth exercising. A `.vis` write that does not reproduce its model, or a catalog file
    /// that does not match its oracle, is the repository's most serious kind of failure; if the code that
    /// describes it throws, or points at the wrong place, that failure arrives unreadable and the diagnosis
    /// starts from nothing.
    ///
    /// So what is asserted here is not that a mismatch is DETECTED - the comparisons themselves are covered by
    /// the fidelity suites - but that the report of one is correct: the right divergence, at the right offset,
    /// in a window that renders.
    /// </summary>
    [TestFixture]
    public sealed class ByteComparisonDiagnosticsTests
    {
        // ---------------------------------------------------------------- ProjectRoundTripVerifier

        /// <summary>
        /// Reaches the divergence report the only way it can be reached without a serializer defect: hand
        /// <see cref="ProjectRoundTripVerifier.Verify"/> the bytes of a DIFFERENT project. What the check exists
        /// to catch cannot be relied on to exist for the test to use.
        /// </summary>
        private static string DivergenceBetween(Project model, Project written)
        {
            RefusedOperationException refusal = Assert.Throws<RefusedOperationException>(
                () => ProjectRoundTripVerifier.Verify(model, ProjectSerializer.Serialize(written)))!;
            return refusal.Message;
        }

        private static ProjectElement Group(string name, params ProjectElement[] children) =>
            Tree.Node("groups", "_0x20", [],
                Tree.Node("group", "_0x21", [("name", name)], children));

        private static ProjectElement Wireless(string name, string serial = "_0xaa11") =>
            Tree.Node("product_airlink", "_0x40",
                [("product_identifier", "_0x4306"), ("device_type", "_0x80a"), ("name", name), ("serialnumber", serial)]);

        [Test]
        public void RoundTripDivergence_OnAChangedAttribute_NamesTheAttributeAndBothValues()
        {
            string message = DivergenceBetween(
                Tree.WithRoot(Group("Stue")),
                Tree.WithRoot(Group("Køkken")));

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("attribute 'name' expected 'Stue', re-read 'Køkken'"));
                Assert.That(message, Does.Contain("<group>"), "and says which element it was on");
            });
        }

        /// <summary>
        /// The path is what makes the report actionable in an 88 KB file: it has to walk down to the element
        /// that actually differs rather than stopping at the root that contains it.
        /// </summary>
        [Test]
        public void RoundTripDivergence_ReportsThePathDownToTheDifferingElement()
        {
            string message = DivergenceBetween(
                Tree.WithRoot(Group("Stue", Wireless("Trådløs"))),
                Tree.WithRoot(Group("Stue", Wireless("Anden"))));

            Assert.Multiple(() =>
            {
                Assert.That(message, Does.Contain("utcs_project/<utcs_project>[0]/<groups>[0]/<group>[0]/<product_airlink>"));
                Assert.That(message, Does.Contain("attribute 'name' expected 'Trådløs', re-read 'Anden'"));
            });
        }

        [Test]
        public void RoundTripDivergence_OnADroppedAttribute_SaysItIsAbsentAfterReparse()
        {
            string message = DivergenceBetween(
                Tree.MinimalProject(("icon", "_0x8")),
                Tree.MinimalProject());

            Assert.That(message, Does.Contain("attribute 'icon'='_0x8' is absent after re-parse"));
        }

        [Test]
        public void RoundTripDivergence_OnAnAddedAttribute_SaysItAppearsOnlyAfterReparse()
        {
            string message = DivergenceBetween(
                Tree.MinimalProject(),
                Tree.MinimalProject(("icon", "_0x8")));

            Assert.That(message, Does.Contain("attribute 'icon' appears only after re-parse"));
        }

        [Test]
        public void RoundTripDivergence_OnALostSubtree_ReportsTheChildCounts()
        {
            string message = DivergenceBetween(
                Tree.WithRoot(Group("Stue")),
                Tree.WithRoot());

            Assert.That(message, Does.Contain("1 children re-read as 0"));
        }

        /// <summary>
        /// An element read back as a different type - the divergence the tag comparison exists for, and the one
        /// arm the attribute and child-count checks can never reach.
        /// </summary>
        [Test]
        public void RoundTripDivergence_OnAChangedElementType_NamesBothTags()
        {
            string message = DivergenceBetween(
                Tree.WithRoot(Group("Stue", Wireless("Enhed"))),
                Tree.WithRoot(Group("Stue",
                    Tree.Node("product_dataline", "_0x40",
                        [("product_identifier", "_0x2202"), ("name", "Enhed")]))));

            Assert.That(message, Does.Contain("element <product_airlink> re-read as <product_dataline>"));
        }

        /// <summary>
        /// The refusal still has to be a refusal: the diagnostic is appended to the coded message, not
        /// substituted for it, so a caller that branches on the code is unaffected by anything asserted above.
        /// </summary>
        [Test]
        public void RoundTripDivergence_IsAppendedToTheCodedRefusalRatherThanReplacingIt()
        {
            RefusedOperationException refusal = Assert.Throws<RefusedOperationException>(
                () => ProjectRoundTripVerifier.Verify(
                    Tree.MinimalProject(("icon", "_0x8")),
                    ProjectSerializer.Serialize(Tree.MinimalProject(("icon", "_0x9")))))!;

            Assert.Multiple(() =>
            {
                Assert.That(refusal.Problems!.Cause.Code, Is.EqualTo(SaveRefusalCodes.RoundTripMismatch.Cause));
                Assert.That(refusal.Message, Does.Contain("Serialize/re-parse mismatch"));
                Assert.That(refusal.Message, Does.Contain("the model holds state the .vis format cannot represent."),
                    "the divergence is inserted before the closing clause, not in place of it");
            });
        }

        // ---------------------------------------------------------------- CatalogTextCompare

        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

        [Test]
        public void CatalogFirstDifference_OnEquivalentFiles_IsMinusOne()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CatalogTextCompare.FirstDifference(Bytes("<a b=\"1\"/>"), Bytes("<a b=\"1\"/>")), Is.EqualTo(-1));
                Assert.That(CatalogTextCompare.FirstDifference(Bytes("<a  b=\"1\" />"), Bytes("<a b=\"1\"/>")), Is.EqualTo(-1),
                    "the offset is reported in the NORMALIZED stream, where the whitespace is already gone");
            });
        }

        /// <summary>
        /// The offset is an index into the normalized stream, so it has to be usable against
        /// <see cref="CatalogTextCompare.Normalize"/>'s output - not against the file on disk.
        /// </summary>
        [Test]
        public void CatalogFirstDifference_PointsAtTheFirstDifferingNormalizedByte()
        {
            byte[] a = Bytes("<a\n  b=\"1\"/>");
            byte[] b = Bytes("<a b=\"2\"/>");

            int offset = CatalogTextCompare.FirstDifference(a, b);

            Assert.Multiple(() =>
            {
                Assert.That(offset, Is.EqualTo(5), "'1' vs '2' inside the quoted value, after the insignificant space is gone");
                Assert.That(CatalogTextCompare.Normalize(a)[offset], Is.EqualTo((byte)'1'));
                Assert.That(CatalogTextCompare.Normalize(b)[offset], Is.EqualTo((byte)'2'));
            });
        }

        /// <summary>
        /// A file that is a PREFIX of the other differs at its own end - the one case with no differing byte to
        /// point at, and the one a naive loop reports as "equivalent".
        /// </summary>
        [TestCase("<a/>", "<a/><b/>", TestName = "CatalogFirstDifference_WhenTheSecondIsLonger_PointsPastTheShorter")]
        [TestCase("<a/><b/>", "<a/>", TestName = "CatalogFirstDifference_WhenTheFirstIsLonger_PointsPastTheShorter")]
        public void CatalogFirstDifference_OnAPrefix_PointsPastTheShorterStream(string first, string second)
        {
            Assert.That(CatalogTextCompare.FirstDifference(Bytes(first), Bytes(second)), Is.EqualTo(4));
        }

        /// <summary>
        /// Line endings are insignificant to the relation, so a CRLF file and an LF file must not be reported as
        /// differing - the `.vis`/`.def` oracles check out as CRLF on Windows and LF elsewhere, and a diagnostic
        /// that pointed at the newline would send every cross-platform investigation down the wrong path.
        /// </summary>
        [Test]
        public void CatalogFirstDifference_IgnoresTheLineEndingTheCheckoutChose()
        {
            Assert.That(CatalogTextCompare.FirstDifference(
                Bytes("<a>\r\n  <b/>\r\n</a>"), Bytes("<a>\n<b/>\n</a>")), Is.EqualTo(-1));
        }

        [Test]
        public void CatalogContext_RendersAWindowAroundTheOffset()
        {
            byte[] bytes = Bytes("<root><name>Stue</name></root>");
            int offset = CatalogTextCompare.FirstDifference(bytes, Bytes("<root><name>Køkken</name></root>"));

            string window = CatalogTextCompare.Context(bytes, offset, span: 8);

            Assert.Multiple(() =>
            {
                Assert.That(window, Does.Contain("Stue"), "the window has to show the bytes that differ");
                Assert.That(window.Length, Is.LessThanOrEqualTo(12), "and stay a window - four bytes of lead plus the span");
            });
        }

        /// <summary>
        /// The window is a byte view, not a text view: a Danish name is multi-byte in UTF-8, and its
        /// continuation bytes are not printable characters. They render as dots rather than as mojibake or a
        /// decoder exception, which is what keeps the window readable for the ASCII structure around them.
        /// </summary>
        [Test]
        public void CatalogContext_RendersNonAsciiBytesAsDotsRatherThanFailing()
        {
            byte[] bytes = Bytes("<name>Køkken</name>");

            string window = CatalogTextCompare.Context(bytes, offset: 6, span: 12);

            Assert.Multiple(() =>
            {
                Assert.That(window, Does.StartWith("ame>K"), "four bytes of lead, then the offset");
                Assert.That(window, Does.Contain(".."), "the two bytes of 'ø' render as dots, not as a decode failure");
                Assert.That(window, Does.Not.Contain("ø"));
            });
        }

        /// <summary>
        /// The window is asked for an offset near an end whenever the divergence is near one, including the
        /// past-the-end offset a prefix mismatch reports. It must clamp rather than throw - a diagnostic that
        /// throws replaces the failure being diagnosed.
        /// </summary>
        [Test]
        public void CatalogContext_ClampsAtBothEndsInsteadOfThrowing()
        {
            byte[] shorter = Bytes("<a/>");

            Assert.Multiple(() =>
            {
                Assert.That(CatalogTextCompare.Context(shorter, offset: 0), Is.EqualTo("<a/>"));
                Assert.That(CatalogTextCompare.Context(shorter, offset: CatalogTextCompare.FirstDifference(shorter, Bytes("<a/><b/>"))),
                    Is.EqualTo("<a/>"), "the offset past the end still renders the whole short stream rather than throwing");
            });
        }
    }
}
