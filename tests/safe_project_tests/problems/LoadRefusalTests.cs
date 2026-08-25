using System;
using System.IO;
using System.Linq;
using System.Text;

using Ihc.Vis.Io;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The TWELVE conditions that stop a project being opened, each now refusing with an identity: five about
    /// the container and its encoding, seven about the document's shape.
    ///
    /// <para><b>No posture changed.</b> All twelve already refused; what they gained is a code and a Danish
    /// sentence. The English diagnostic each has always produced is unchanged and is still what a developer
    /// reads in a log — it moved nowhere, it was joined.</para>
    ///
    /// <para><b>The composition is the interesting part.</b> A refused open is an OPERATION and its ONE CAUSE:
    /// the operation carries the dotted family code <c>io.load</c>, and the cause keeps the bare catalogue id it
    /// was published under. No row was renamed into <c>io.load-empty</c>, because that would rename a published
    /// id and leave anyone filtering on the old one seeing nothing. The user reads the cause's sentence; the
    /// operation is identifiable without reading it.</para>
    /// </summary>
    [TestFixture]
    public sealed class LoadRefusalTests
    {
        private static readonly byte[] MinimalProject = Encoding.Latin1.GetBytes(
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><utcs_project version_major=\"4\"></utcs_project>");

        /// <summary>The same minimal document one major version above the highest this SDK models.</summary>
        private static readonly byte[] VersionFiveProject = Encoding.Latin1.GetBytes(
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><utcs_project version_major=\"5\"></utcs_project>");

        private static ProjectFormatException Refused(byte[] bytes) =>
            Assert.Throws<ProjectFormatException>(() => ProjectReader.Read(new MemoryStream(bytes)))!;

        [Test]
        public void AnEmptyStreamIsRefusedAsLoadEmpty() =>
            AssertRefusal(Refused([]), LoadRefusalCodes.Empty, "Filen er tom", "empty");

        [Test]
        public void GzippedContentIsRefusedAsLoadGzip() =>
            AssertRefusal(Refused([0x1F, 0x8B, 0x08, 0x00]), LoadRefusalCodes.Gzip, "Filen er komprimeret", "gzip");

        [Test]
        public void AUtf8BomIsRefusedAsLoadBomUtf8() =>
            AssertRefusal(Refused([0xEF, 0xBB, 0xBF, .. MinimalProject]),
                LoadRefusalCodes.Utf8Bom, "Filen har et UTF-8-BOM", "UTF-8 BOM");

        [Test]
        public void AUtf16BomIsRefusedAsLoadBomUtf16() =>
            AssertRefusal(Refused([0xFF, 0xFE, .. MinimalProject]),
                LoadRefusalCodes.Utf16Bom, "Filen har et UTF-16-BOM", "UTF-16 BOM");

        [Test]
        public void AForeignDeclaredEncodingIsRefusedAsLoadEncodingDeclared() =>
            AssertRefusal(
                Refused(Encoding.Latin1.GetBytes(
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?><utcs_project version_major=\"4\"></utcs_project>")),
                LoadRefusalCodes.DeclaredEncoding, "Forkert tegnkodning", "declares encoding");

        /// <summary>
        /// A well-formed project still opens. Every guard above rejects something specific, and the point of
        /// naming them is that they reject only that — a refusal that also caught a good file would be a far
        /// worse defect than the ones it prevents.
        /// </summary>
        [Test]
        public void AWellFormedProjectStillOpens()
        {
            Project project = ProjectReader.Read(new MemoryStream(MinimalProject));

            Assert.That(project.Root.Tag, Is.EqualTo("utcs_project"));
        }

        /// <summary>
        /// Every cause is a governed catalogue entry keeping its BARE published id, and the operation head is one
        /// too. A dotted <c>io.load-empty</c> anywhere would mean a published id had been renamed.
        /// </summary>
        [Test]
        public void EveryCauseKeepsItsPublishedIdAndIsGoverned()
        {
            Assert.Multiple(() =>
            {
                foreach (ProblemCode cause in LoadRefusalCodes.All.Select(r => r.Cause))
                {
                    Assert.That(cause.Family, Is.EqualTo(ProblemFamily.Validation),
                        cause.Value + " keeps its bare catalogue id");
                    Assert.That(cause.Value, Does.Not.StartWith("io."), cause.Value);
                    Assert.That(ProblemCatalog.Current.TryGet(cause, out ProblemCatalogEntry entry), Is.True, cause.Value);
                    Assert.That(entry.MessageTemplate, Is.Not.Empty, cause.Value);
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Refusal), cause.Value);
                }

                Assert.That(LoadRefusalCodes.Operation.Family, Is.EqualTo(ProblemFamily.Io));
                Assert.That(ProblemCatalog.Current.TryGet(LoadRefusalCodes.Operation, out ProblemCatalogEntry head), Is.True);
                Assert.That(head.MessageTemplate, Is.EqualTo("Projektet kunne ikke åbnes"));
            });
        }

        /// <summary>
        /// The catalogue's Danish label and the sentence the reader hands over must be the same words. The reader
        /// cannot read the catalogue — the IO layer must not depend on the validation engine — so this is what
        /// keeps the two in step.
        /// </summary>
        [Test]
        public void TheReadersLabelIsTheCataloguesTemplate()
        {
            (byte[] Bytes, RefusalIdentity Refusal)[] cases =
            [
                ([], LoadRefusalCodes.Empty),
                ([0x1F, 0x8B], LoadRefusalCodes.Gzip),
                ([0xEF, 0xBB, 0xBF, .. MinimalProject], LoadRefusalCodes.Utf8Bom),
                ([0xFF, 0xFE, .. MinimalProject], LoadRefusalCodes.Utf16Bom),
            ];

            Assert.Multiple(() =>
            {
                foreach ((byte[] bytes, RefusalIdentity refusal) in cases)
                {
                    ProblemChain chain = Refused(bytes).Problems!;
                    ProblemCatalog.Current.TryGet(refusal.Cause, out ProblemCatalogEntry entry);
                    Assert.That(chain.Cause.Message, Is.EqualTo(entry.MessageTemplate), refusal.Cause.Value);
                }
            });
        }

        // ── the seven DOCUMENT-SHAPE conditions ────────────────────────────────────────────────────

        [Test]
        public void MalformedXmlIsRefusedAsLoadNotXml() =>
            AssertRefusal(Refused(Encoding.Latin1.GetBytes("<utcs_project><unclosed></utcs_project>")),
                LoadRefusalCodes.NotXml, "Filen er ikke gyldig XML", "well-formed");

        [Test]
        public void AWrongRootIsRefusedAsLoadRootTag() =>
            AssertRefusal(
                Refused(Encoding.Latin1.GetBytes(
                    "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><product_definition/>")),
                LoadRefusalCodes.RootTag, "Ikke en projektfil", "Root element is");

        [Test]
        public void AMissingVersionIsRefusedAsLoadVersionMissing() =>
            AssertRefusal(
                Refused(Encoding.Latin1.GetBytes(
                    "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><utcs_project/>")),
                LoadRefusalCodes.VersionMissing, "Mangler projektversion", "version_major");

        /// <summary>
        /// Character data refuses rather than warns, and the reason is worth stating: the model is
        /// attribute-only, so a file that loaded would lose its text at the next save — silently, and only for
        /// whoever saved it. A refusal at open is the one place that loss can still be prevented.
        /// </summary>
        [Test]
        public void CharacterDataIsRefusedAsLoadCharacterData() =>
            AssertRefusal(
                Refused(Encoding.Latin1.GetBytes(
                    "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>"
                    + "<utcs_project version_major=\"4\">noget tekst</utcs_project>")),
                LoadRefusalCodes.CharacterData, "Filen indeholder tekst i et element", "character data");

        [Test]
        public void ExcessiveNestingIsRefusedAsLoadDepth()
        {
            const int tooDeep = 300;
            StringBuilder document = new("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><utcs_project version_major=\"4\">");
            for (int i = 0; i < tooDeep; i++)
            {
                document.Append("<group>");
            }

            for (int i = 0; i < tooDeep; i++)
            {
                document.Append("</group>");
            }

            document.Append("</utcs_project>");

            AssertRefusal(Refused(Encoding.Latin1.GetBytes(document.ToString())),
                LoadRefusalCodes.Depth, "For dyb elementstruktur", "nesting exceeds");
        }

        /// <summary>
        /// Whitespace between elements is NOT character data, and this is the control the previous test needs: a
        /// guard that treated indentation as content would refuse every hand-formatted file in existence.
        /// </summary>
        [Test]
        public void WhitespaceBetweenElementsStillLoads()
        {
            Project project = ProjectReader.Read(new MemoryStream(Encoding.Latin1.GetBytes(
                """
                <?xml version="1.0" encoding="ISO-8859-1"?>
                <utcs_project version_major="4">
                  <groups />
                </utcs_project>
                """)));

            Assert.That(project.Root.Children, Has.Length.EqualTo(1));
        }

        /// <summary>
        /// A malformed inline DTD refuses from EITHER of the two places that parse it — the capture that runs
        /// before the XML is read, and the eager schema view built after. Both were uncoded when this row was
        /// written and both are the same condition, so both carry the same identity: an id that only half its
        /// sites raise is exactly the ambiguity a code is supposed to remove.
        /// </summary>
        [Test]
        public void AMalformedDtdIsRefusedAsLoadDtdMalformedFromEitherSite()
        {
            ProjectFormatException atCapture = Refused(Encoding.Latin1.GetBytes(
                """
                <?xml version="1.0" encoding="ISO-8859-1"?>
                <!DOCTYPE utcs_project [
                   <!ELEMENT >
                ]>
                <utcs_project version_major="4"/>
                """));
            ProjectFormatException atSchemaView = Refused(Encoding.Latin1.GetBytes(
                """
                <?xml version="1.0" encoding="ISO-8859-1"?>
                <!DOCTYPE utcs_project [
                   <!ELEMENT utcs_project ANY>
                   <!ATTLIST utcs_project version_major>
                ]>
                <utcs_project version_major="4"/>
                """));

            Assert.Multiple(() =>
            {
                AssertRefusal(atCapture, LoadRefusalCodes.DtdMalformed, "Ugyldig indbygget DTD", "no element name");
                AssertRefusal(atSchemaView, LoadRefusalCodes.DtdMalformed, "Ugyldig indbygget DTD",
                    "no type or default");
            });
        }

        /// <summary>
        /// A TRUNCATED file refuses as <c>load-not-xml</c>, and this test is the measurement behind ruling
        /// <c>load-truncated</c> out rather than the assertion that it fires. An XML parser at
        /// <c>ConformanceLevel.Document</c> refuses an unclosed document before the reader's own
        /// end-of-document guard can be reached, so the two conditions are not separately decidable — and
        /// <c>load-not-xml</c>'s own text already names truncation. What the user loses is one Danish
        /// sentence; what a message-matching predicate would cost is a refusal that changes behaviour with
        /// the .NET UI culture.
        /// </summary>
        [Test]
        public void ATruncatedDocumentIsRefusedAsLoadNotXml()
        {
            ProjectFormatException refusal = Refused(Encoding.Latin1.GetBytes(
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><utcs_project version_major=\"4\"><groups>"));

            Assert.Multiple(() =>
            {
                Assert.That(refusal.Problems!.Cause.Code, Is.EqualTo(LoadRefusalCodes.NotXml.Cause));
                Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("load-truncated"),
                    out ProblemCatalogEntry truncated), Is.True, "the ruled-out row keeps its id occupied");
                Assert.That(truncated.Status, Is.EqualTo(ProblemCodeStatus.RuledOut));
                Assert.That(LoadRefusalCodes.All.Select(r => r.Cause.Value), Does.Not.Contain("load-truncated"),
                    "a ruled-out row has no code member, so nothing can mint it");
            });
        }

        /// <summary>
        /// A project one major version too NEW still opens, and that is the posture this test exists to hold
        /// still — not to endorse.
        ///
        /// <para><b>The three faces disagree, deliberately and visibly.</b> The publication
        /// (<c>problem-catalogue.md</c>, the <c>root-version</c> row) reads "Fatal error | Open": a reader that
        /// refuses. The reader checks version_major's PRESENCE only (<see cref="ProjectReader"/>, beside the
        /// <c>load-version-missing</c> guard), so the open succeeds. The validator reports the row as an Error
        /// finding, which is the only face that fires today. <c>root-version</c> is therefore a catalogued row
        /// with no member on <see cref="LoadRefusalCodes"/>, and nothing can mint it as a refusal.</para>
        ///
        /// <para><b>Why a test and not a bug report.</b> Closing the gap is a PRODUCT ruling (D13): refusing the
        /// open protects a v5 file from being misread and saved back in a v4 shape, and also stops a user from
        /// opening a file the vendor's own newer tool wrote. Neither direction is obviously right, so the ruling
        /// is left unmade — and this test is the tripwire that forces it to be made CONSCIOUSLY. It fails the day
        /// someone codes the refusal, which is the moment the publication, the reader and this comment have to be
        /// reconciled in one change.</para>
        /// </summary>
        [Test]
        public void AProjectAboveVersionFourStillOpensToday()
        {
            Project project = ProjectReader.Read(new MemoryStream(VersionFiveProject));
            ProjectValidationResult validation = ProjectVerification.Structural(project);

            Assert.Multiple(() =>
            {
                Assert.That(project.Root.Tag, Is.EqualTo("utcs_project"),
                    "the reader checks version_major's presence, not its value — the open succeeds");
                Assert.That(validation.Findings.Any(f => f.RuleId == "root-version"), Is.True,
                    "the other face: validation reports it — errors: " + string.Join(" | ", validation.Errors));
                Assert.That(LoadRefusalCodes.All.Select(r => r.Cause.Value), Does.Not.Contain("root-version"),
                    "the published 'Fatal error | Open' cell has no member behind it, so no site can raise it");
            });
        }

        private static void AssertRefusal(
            ProjectFormatException refusal, RefusalIdentity identity, string label, string diagnosticFragment)
        {
            Assert.Multiple(() =>
            {
                Assert.That(refusal.Problems, Is.Not.Null, "a refused open carries its operation and its cause");
                Assert.That(refusal.Problems!.Operation.Code, Is.EqualTo(LoadRefusalCodes.Operation));
                Assert.That(refusal.Problems.Cause.Code, Is.EqualTo(identity.Cause));
                Assert.That(refusal.Problems.Cause.Message, Is.EqualTo(label), "the Danish sentence the user reads");

                // T063: and that sentence is the CATALOGUE's, not just the one typed into this test. The reader
                // cannot look a label up — the IO layer must not depend on the validation engine — so every case
                // in this suite is also a drift gate between the two copies of the words.
                Assert.That(ProblemCatalog.Current.TryGet(identity.Cause, out ProblemCatalogEntry entry), Is.True,
                    $"{identity.Cause.Value} is governed");
                Assert.That(label, Is.EqualTo(entry.MessageTemplate),
                    $"{identity.Cause.Value}: the reader's sentence and its catalogue template must be the same words");
                Assert.That(refusal.Message, Does.Contain(diagnosticFragment).IgnoreCase,
                    "and the English diagnostic is unchanged — it was joined, not replaced");
            });
        }
    }
}
