#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The structured-grammar round trip (decision D1): every synthetic oracle's header strict-parses into a
    /// <see cref="CatalogGrammar"/> whose re-emission is equivalent to the original header <em>and</em> yields a
    /// well-formed document when spliced onto the oracle body; the well-formedness gate catches the malformation
    /// classes whitespace normalization is blind to; strict mode rejects every construct outside the corpus
    /// envelope while lenient mode falls back to a byte-faithful verbatim head <b>plus</b> a best-effort
    /// structured projection (so exotic user files keep defaults/IDREF/hoist semantics); and an orphan ATTLIST
    /// renders differently for catalog files (vendor-faithful) vs project hoisting (synthesized ELEMENT line).
    /// </summary>
    public class CatalogDtdParserTests
    {
        private static readonly string ProductDir = TestData.PathOf("products", "synthetic");

        private static readonly string FunctionBlockDir = TestData.PathOf("functionblocks", "synthetic");

        private static IEnumerable<string> AllOracles() =>
            Directory.EnumerateFiles(ProductDir, "*.def")
                .Concat(Directory.EnumerateFiles(FunctionBlockDir, "*.ifb"))
                .OrderBy(p => p, System.StringComparer.Ordinal);

        // ----- oracle round trip: strict parse → re-emit → splice → reparse + equivalence -----

        [TestCaseSource(nameof(AllOracles))]
        public void Oracle_Header_StrictParsesAndReEmitsEquivalently(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            string head = CatalogDtdParser.CaptureHeadText(file);
            CatalogGrammar grammar = CatalogDtdParser.ParseStrict(head);

            string emitted = CatalogDtdEmitter.RenderHead(grammar, grammar.DoctypeRoot!);

            Encoding encoding = CatalogTextEncodingExtensions.Classify(file).TextEncoding();
            Assert.That(CatalogTextCompare.Equivalent(encoding.GetBytes(head), encoding.GetBytes(emitted)), Is.True,
                $"re-emitted header of '{Path.GetFileName(path)}' differs from the source header " +
                $"(whitespace-normalized). Emitted:\n{emitted}");
        }

        [TestCaseSource(nameof(AllOracles))]
        public void Oracle_EmittedHeader_SplicedOntoBody_ReparsesWellFormed(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            string head = CatalogDtdParser.CaptureHeadText(file);
            CatalogGrammar grammar = CatalogDtdParser.ParseStrict(head);
            string emitted = CatalogDtdEmitter.RenderHead(grammar, grammar.DoctypeRoot!);

            CatalogTextEncoding kind = CatalogTextEncodingExtensions.Classify(file);
            string fullText;
            using (var reader = new StreamReader(new MemoryStream(file, writable: false),
                                                 CatalogReader.SniffEncoding(file), detectEncodingFromByteOrderMarks: true))
            {
                fullText = reader.ReadToEnd();
            }
            string spliced = emitted + fullText.Substring(head.Length);
            byte[] document = kind.Preamble().Concat(kind.TextEncoding().GetBytes(spliced)).ToArray();

            Assert.That(CatalogWellFormedness.Check(document), Is.Null,
                $"emitted header spliced onto the body of '{Path.GetFileName(path)}' must reparse clean");
        }

        [Test]
        public void Oracle_LenientParse_OfEnvelopeHeader_YieldsPureStructuredState()
        {
            byte[] file = File.ReadAllBytes(Path.Combine(ProductDir, "synthetic_9f09_logging.def"));
            CatalogGrammar grammar = CatalogDtdParser.ParseLenient(CatalogDtdParser.CaptureHeadText(file));

            Assert.Multiple(() =>
            {
                Assert.That(grammar.VerbatimHead, Is.Null, "an in-envelope header parses without the fallback");
                Assert.That(grammar.Declarations.Select(d => d.Tag), Is.EqualTo(new[]
                {
                    "product_dataline", "resource_temperature", "resource_enum", "resource_sample_log",
                }), "ordered per-tag records, orphans included");
                Assert.That(grammar.TryGetDeclaration("resource_enum")!.HasElementDecl, Is.False, "vendor-style orphan");
                Assert.That(grammar.TryGetDeclaration("resource_sample_log")!.FindAttr("inivalue")!.RawLiteral,
                    Is.EqualTo("500.00"), "orphan defaults are data");
            });
        }

        // ----- malformation negatives: what normalization cannot see, the gate must catch -----

        private static byte[] Doc(string text) => Encoding.ASCII.GetBytes(text);

        private const string ValidTinyDoc =
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<!DOCTYPE r[\r\n   <!ELEMENT r ANY>\r\n]>\r\n<r />";

        [Test]
        public void Gate_Catches_MissingTokenSeparatorInElementDecl()
        {
            const string valid =
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<!DOCTYPE foo[\r\n   <!ELEMENT foo ANY>\r\n]>\r\n<foo />";
            string malformed = valid.Replace("<!ELEMENT foo ANY>", "<!ELEMENTfooANY>");

            Assert.Multiple(() =>
            {
                Assert.That(CatalogTextCompare.Equivalent(Doc(malformed), Doc(valid)), Is.True,
                    "whitespace normalization is blind to the missing separators");
                Assert.That(CatalogWellFormedness.Check(Doc(malformed)), Is.Not.Null, "the reparse gate is not");
                Assert.That(CatalogWellFormedness.Check(Doc(valid)), Is.Null);
            });
        }

        [Test]
        public void Gate_Catches_MissingSpaceBeforeDefaultDecl()
        {
            const string valid =
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<!DOCTYPE r[\r\n   <!ELEMENT r ANY>\r\n" +
                "   <!ATTLIST r id CDATA #REQUIRED>\r\n]>\r\n<r id=\"x\" />";
            string malformed = valid.Replace("CDATA #REQUIRED", "CDATA#REQUIRED");

            Assert.Multiple(() =>
            {
                Assert.That(CatalogTextCompare.Equivalent(Doc(malformed), Doc(valid)), Is.True);
                Assert.That(CatalogWellFormedness.Check(Doc(malformed)), Is.Not.Null);
                Assert.That(CatalogWellFormedness.Check(Doc(valid)), Is.Null);
            });
        }

        [Test]
        public void Gate_Catches_BareAmpersandInAttributeValue()
        {
            byte[] malformed = Doc(ValidTinyDoc.Replace("<r />", "<r note=\"a & b\" />"));

            Assert.Multiple(() =>
            {
                Assert.That(CatalogTextCompare.Equivalent(malformed, malformed), Is.True,
                    "the comparer alone approves a malformed stream against itself");
                Assert.That(CatalogWellFormedness.Check(malformed), Is.Not.Null);
            });
        }

        [Test]
        public void Gate_Catches_UnclosedQuoteInDtd()
        {
            byte[] malformed = Doc(ValidTinyDoc.Replace(
                "   <!ELEMENT r ANY>", "   <!ELEMENT r ANY>\r\n   <!ATTLIST r name CDATA \"unclosed>"));

            Assert.Multiple(() =>
            {
                Assert.That(CatalogTextCompare.Equivalent(malformed, malformed), Is.True);
                Assert.That(CatalogWellFormedness.Check(malformed), Is.Not.Null);
            });
        }

        [Test]
        public void Gate_Accepts_OrphanAttlistAndUndeclaredBodyType()
        {
            byte[] document = Doc(
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<!DOCTYPE r[\r\n   <!ELEMENT r ANY>\r\n" +
                "   <!ATTLIST orphan inivalue CDATA \"1\">\r\n]>\r\n<r><undeclared /><orphan /></r>");

            Assert.That(CatalogWellFormedness.Check(document), Is.Null,
                "non-validating parse: orphan ATTLISTs and undeclared body types are authentic vendor shapes");
        }

        // ----- strict-mode envelope rejections -----

        private static string Head(string subset, string prolog = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>") =>
            prolog + "\r\n<!DOCTYPE r[\r\n" + subset + "]>\r\n";

        private static readonly TestCaseData[] OutsideEnvelopeHeads =
        {
            new TestCaseData(Head("   <!-- a comment -->\r\n   <!ELEMENT r ANY>\r\n")).SetName("DtdComment"),
            new TestCaseData(Head("   <!ENTITY nbsp \"&#160;\">\r\n   <!ELEMENT r ANY>\r\n")).SetName("EntityDecl"),
            new TestCaseData(Head("   <!NOTATION gif SYSTEM \"gif\">\r\n   <!ELEMENT r ANY>\r\n")).SetName("NotationDecl"),
            new TestCaseData(Head("   %params;\r\n   <!ELEMENT r ANY>\r\n")).SetName("ParameterEntityRef"),
            new TestCaseData(Head("   <!ELEMENT r (a, b)>\r\n")).SetName("NonAnyContentModel"),
            new TestCaseData(Head("   <!ELEMENT r ANY>\r\n   <!ATTLIST r a CDATA #FIXED \"v\">\r\n")).SetName("FixedDefault"),
            new TestCaseData(Head("   <!ELEMENT r ANY>\r\n   <!ATTLIST r a IDREFS #IMPLIED>\r\n")).SetName("IdrefsType"),
            new TestCaseData(Head("   <!ELEMENT r ANY>\r\n   <!ATTLIST r a NMTOKEN #IMPLIED>\r\n")).SetName("NmtokenType"),
            new TestCaseData(Head("   <!ELEMENT r ANY>\r\n   <!ELEMENT r ANY>\r\n")).SetName("DuplicateElementDecl"),
            new TestCaseData(Head("   <!ELEMENT r ANY>\r\n   <!ATTLIST r a CDATA \"\">\r\n   <!ATTLIST r b CDATA \"\">\r\n"))
                .SetName("SecondAttlistForTag"),
            new TestCaseData(Head("   <!ELEMENT r ANY>\r\n   <!ELEMENT s ANY>\r\n   <!ATTLIST r a CDATA \"\">\r\n"))
                .SetName("InterleavedPair"),
            new TestCaseData(Head("   <!ELEMENT r ANY>\r\n   <!ATTLIST r a CDATA 'single'>\r\n")).SetName("SingleQuotedDefault"),
            new TestCaseData("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<!DOCTYPE r SYSTEM \"r.dtd\">\r\n")
                .SetName("SystemExternalId"),
            new TestCaseData(Head("   <!ELEMENT r ANY>\r\n", prolog: "<?xml version=\"1.0\"?>")).SetName("PrologWithoutEncoding"),
            new TestCaseData(Head("   <!ELEMENT r ANY>\r\n",
                prolog: "<?xml version=\"1.0\" encoding=\"ISO-8859-1\" standalone=\"yes\"?>")).SetName("PrologWithStandalone"),
            new TestCaseData("<!DOCTYPE r[\r\n   <!ELEMENT r ANY>\r\n]>\r\n").SetName("MissingProlog"),
        };

        [TestCaseSource(nameof(OutsideEnvelopeHeads))]
        public void StrictParse_Rejects_OutsideEnvelopeConstruct(string head)
        {
            Assert.Throws<CatalogFormatException>(() => CatalogDtdParser.ParseStrict(head));
        }

        [Test]
        public void StrictParse_Accepts_QuotedSubsetCloseInsideDefaultLiteral()
        {
            string head = Head("   <!ELEMENT r ANY>\r\n   <!ATTLIST r a CDATA \"x]>y\">\r\n");

            CatalogGrammar grammar = CatalogDtdParser.ParseStrict(head);

            Assert.That(grammar.TryGetDeclaration("r")!.FindAttr("a")!.RawLiteral, Is.EqualTo("x]>y"));
        }

        [Test]
        public void CaptureHeadText_IsNotTruncatedByQuotedSubsetClose()
        {
            string document = Head("   <!ELEMENT r ANY>\r\n   <!ATTLIST r a CDATA \"x]>y\">\r\n") + "<r />";

            string head = CatalogDtdParser.CaptureHeadText(Encoding.ASCII.GetBytes(document));

            Assert.That(head, Does.EndWith("]>\r\n"), "the quote-aware scan must pass over the quoted ]>");
            Assert.That(head, Does.Contain("x]>y"));
        }

        [Test]
        public void CaptureHeadText_IsNotBrokenByApostropheInComment()
        {
            // Finding 4: a lone apostrophe inside a DTD comment used to flip the quote tracker so ']>' was never
            // found — CaptureHeadText fell back to a prolog-only head and the whole grammar was silently dropped.
            string document = Head("   <!-- Peter's dimmer -->\r\n   <!ELEMENT r ANY>\r\n   <!ATTLIST r a CDATA \"v\">\r\n") + "<r a=\"v\" />";

            string head = CatalogDtdParser.CaptureHeadText(Encoding.ASCII.GetBytes(document));

            Assert.Multiple(() =>
            {
                Assert.That(head, Does.EndWith("]>\r\n"), "the comment-aware scan finds the true subset end");
                Assert.That(head, Does.Contain("Peter's dimmer"), "the comment is inside the captured head");
                Assert.That(head, Does.Contain("<!ELEMENT r ANY>"), "declarations after the comment are captured, not dropped");
            });
        }

        [Test]
        public void LenientParse_HeaderWithApostropheComment_KeepsDeclarations()
        {
            // Finding 4 (end-to-end): the same header must project its declarations, not collapse to a prolog-only
            // (0-declaration) grammar the way the pre-fix CaptureHeadText caused.
            string head = CatalogDtdParser.CaptureHeadText(Encoding.ASCII.GetBytes(
                Head("   <!-- Peter's dimmer -->\r\n   <!ELEMENT r ANY>\r\n   <!ATTLIST r inivalue CDATA \"500.00\">\r\n") + "<r />"));

            CatalogGrammar grammar = CatalogDtdParser.ParseLenient(head);

            Assert.That(grammar.TryGetDeclaration("r")!.FindAttr("inivalue")!.RawLiteral, Is.EqualTo("500.00"),
                "the declaration survives the apostrophe-bearing comment");
        }

        [Test]
        public void StrictParse_Accepts_DigitLeadingEnumerationTokens()
        {
            string head = Head("   <!ELEMENT r ANY>\r\n   <!ATTLIST r pulse (24 | 48 | none) \"24\">\r\n");

            CatalogGrammar grammar = CatalogDtdParser.ParseStrict(head);

            GrammarAttr attr = grammar.TryGetDeclaration("r")!.FindAttr("pulse")!;
            Assert.That(attr.EnumTokens, Is.EqualTo(new[] { "24", "48", "none" }),
                "enumeration tokens are NMTOKENS — a digit-leading token is legal (VC: Enumeration)");
        }

        // ----- lenient fallback: verbatim head + best-effort projection -----

        [Test]
        public void LenientParse_OnExoticHeader_KeepsVerbatimHeadAndProjection()
        {
            string head = Head(
                "   <!-- exotic construct the model does not carry -->\r\n" +
                "   <!ELEMENT widget ANY>\r\n" +
                "   <!ATTLIST widget id ID #REQUIRED\r\n" +
                "                  scene IDREF #IMPLIED\r\n" +
                "                  size CDATA \"42\">\r\n");

            CatalogGrammar grammar = CatalogDtdParser.ParseLenient(head);

            Assert.Multiple(() =>
            {
                Assert.That(grammar.VerbatimHead, Is.EqualTo(head), "the whole header survives byte-faithfully");
                GrammarDeclaration? widget = grammar.TryGetDeclaration("widget");
                Assert.That(widget, Is.Not.Null, "the parseable declaration is still projected");
                Assert.That(widget!.FindAttr("scene")!.Type, Is.EqualTo(GrammarAttrType.IdRef), "IDREF typing survives");
                Assert.That(widget.FindAttr("size")!.RawLiteral, Is.EqualTo("42"), "defaults survive");
                Assert.That(grammar.DoctypeRoot, Is.EqualTo("r"));
                Assert.That(grammar.DeclaredEncoding, Is.EqualTo("ISO-8859-1"));
            });
        }

        [Test]
        public void LenientParse_SkipsOutOfEnvelopeDeclaration_WithoutLosingNeighbours()
        {
            string head = Head(
                "   <!ELEMENT alpha ANY>\r\n" +
                "   <!ATTLIST alpha id ID #REQUIRED>\r\n" +
                "   <!ATTLIST fixedone locked CDATA #FIXED \"yes\">\r\n" +
                "   <!ELEMENT omega ANY>\r\n" +
                "   <!ATTLIST omega inivalue CDATA \"500.00\">\r\n");

            CatalogGrammar grammar = CatalogDtdParser.ParseLenient(head);

            Assert.Multiple(() =>
            {
                Assert.That(grammar.VerbatimHead, Is.EqualTo(head));
                Assert.That(grammar.Declarations.Select(d => d.Tag), Is.EqualTo(new[] { "alpha", "omega" }),
                    "the #FIXED declaration is skipped; its neighbours are kept");
                Assert.That(grammar.TryGetDeclaration("omega")!.FindAttr("inivalue")!.RawLiteral, Is.EqualTo("500.00"));
            });
        }

        // ----- the two renderings of an orphan declaration -----

        [Test]
        public void RenderCatalogDeclaration_OfOrphan_IsVendorFaithful()
        {
            GrammarDeclaration orphan = GrammarDeclaration.AttlistOnly("resource_log",
                GrammarAttr.Id("id"), GrammarAttr.Cdata("inivalue", "500.00"));

            string text = CatalogDtdEmitter.RenderCatalogDeclaration(orphan);

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Not.Contain("<!ELEMENT"), "an orphan re-emits exactly as the vendor wrote it");
                Assert.That(text, Does.Contain("<!ATTLIST resource_log id ID #REQUIRED"));
                Assert.That(text, Does.Contain("inivalue CDATA \"500.00\""));
            });
        }

        [Test]
        public void RenderProjectBlock_OfOrphan_SynthesizesElementLine_AndRoundTripsAsVisBlock()
        {
            GrammarDeclaration orphan = GrammarDeclaration.AttlistOnly("resource_log",
                GrammarAttr.Id("id"), GrammarAttr.Cdata("inivalue", "500.00"), GrammarAttr.IdRef("typedef"));

            string block = CatalogDtdEmitter.RenderProjectBlock(orphan);

            Assert.That(block, Does.Contain("<!ELEMENT resource_log ANY>"),
                "the project block model requires the ELEMENT line — a catalog-faithful orphan block would make " +
                "the saved project unloadable");
            ElementSchema parsed = ProjectSchemaRegistry.ParseBlock(block);
            Assert.Multiple(() =>
            {
                Assert.That(parsed.Tag, Is.EqualTo("resource_log"));
                Assert.That(parsed.Attrs.Select(a => a.Name), Is.EqualTo(new[] { "id", "inivalue", "typedef" }));
                Assert.That(parsed.FindAttr("inivalue")!.Default, Is.EqualTo("500.00"));
                Assert.That(parsed.IsIdRef("typedef"), Is.True);
            });
        }

        [Test]
        public void ElementOnlyDeclaration_RendersWithoutAttlist_InBothForms()
        {
            GrammarDeclaration elementOnly = GrammarDeclaration.ElementOnly("resource_Skew");

            Assert.Multiple(() =>
            {
                Assert.That(CatalogDtdEmitter.RenderCatalogDeclaration(elementOnly),
                    Is.EqualTo("   <!ELEMENT resource_Skew ANY>\r\n"));
                Assert.That(CatalogDtdEmitter.RenderProjectBlock(elementOnly),
                    Is.EqualTo("   <!ELEMENT resource_Skew ANY>\r\n"));
            });
        }

        // ----- schema-view projection (no text round trip) -----

        [Test]
        public void SchemaView_ForGrammar_ProjectsDefaultsAndIdRefs_WithRegistryFallback()
        {
            CatalogGrammar grammar = CatalogGrammar.Create(new[]
            {
                GrammarDeclaration.Element("gadget",
                    GrammarAttr.Id("id"),
                    GrammarAttr.IdRef("scene"),
                    GrammarAttr.Cdata("size", "4&#48;"),
                    GrammarAttr.Enumerated("mode", new[] { "on", "off" }, "off")),
            });

            ProjectSchemaView view = ProjectSchemaView.For(grammar);

            Assert.Multiple(() =>
            {
                ElementSchema? gadget = view.TryGet("gadget");
                Assert.That(gadget, Is.Not.Null);
                Assert.That(gadget!.IsIdRef("scene"), Is.True);
                Assert.That(gadget.FindAttr("size")!.Default, Is.EqualTo("40"),
                    "the schema view sees the DECODED default literal");
                Assert.That(gadget.FindAttr("mode")!.EnumValues, Is.EqualTo(new[] { "on", "off" }));
                Assert.That(view.TryGet("group"), Is.Not.Null, "registry fallback for undeclared types");
            });
        }
    }
}
