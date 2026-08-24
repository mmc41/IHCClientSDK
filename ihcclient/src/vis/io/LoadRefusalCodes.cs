#nullable enable
using Ihc.Vis.Problems;

namespace Ihc.Vis.Io
{
    /// <summary>
    /// The identity of every condition that stops a project being opened, ready to raise.
    /// <para>
    /// Each member is a whole <see cref="RefusalIdentity"/> rather than a bare code, for the reason its three
    /// sibling families give: a refusing site needs four things at once — the operation, the cause, and the
    /// Danish words for each — and the reader sits below the validation engine, which it may not read. Bundling
    /// them is what keeps ONE spelling of a refusal instead of one per site. Before this family was converted,
    /// its thirteen throw sites in <see cref="ProjectReader"/> hand-typed those sentences, two of them twice, and
    /// the universal drift gate had to carve this family out by name because there was no identity to reflect.
    /// </para>
    /// <para>
    /// THE COMPOSITION RULE, unchanged: the OPERATION carries the dotted family code — <c>io.load</c> — and the
    /// CAUSE keeps the bare catalogue id the rows were published under. No row is renamed into
    /// <c>io.load-empty</c>: that would rename a published id and leave anyone filtering on the old one seeing
    /// nothing. So a refused open is one operation with one cause: the operation is identifiable without reading
    /// the cause, and the cause is the sentence the user reads.
    /// </para>
    /// <para>
    /// The identities live in the IO layer, beside the guards that raise them, because the reader must not depend
    /// on the validation engine. The catalogue's entries are built FROM these members' codes, and
    /// <c>RefusalLabelDriftTests</c> requires each label to be its entry's template — which is what keeps one
    /// identity rather than two spellings of it.
    /// </para>
    /// </summary>
    public static class LoadRefusalCodes
    {
        private static RefusalIdentity Refusing(string cause, string causeLabel) =>
            new(OperationCodes.Load, OperationCodes.LoadLabel, new ProblemCode(cause), causeLabel);

        /// <summary>
        /// The operation every one of these refuses: opening a project. It is <see cref="OperationCodes.Load"/>
        /// itself, not a second spelling of it — an operation is refused from more than one layer, so its head
        /// lives where every raiser can see it and this is the load family's name for the same code.
        /// </summary>
        public static ProblemCode Operation => OperationCodes.Load;

        /// <summary>The stream holds no bytes — not a project file.</summary>
        public static RefusalIdentity Empty { get; } = Refusing("load-empty", "Filen er tom");

        /// <summary>The content is gzip-compressed: a controller blob that was never decompressed.</summary>
        public static RefusalIdentity Gzip { get; } = Refusing("load-gzip", "Filen er komprimeret");

        /// <summary>A UTF-8 byte-order mark precedes the document.</summary>
        public static RefusalIdentity Utf8Bom { get; } = Refusing("load-bom-utf8", "Filen har et UTF-8-BOM");

        /// <summary>A UTF-16 byte-order mark precedes the document; every byte offset is wrong.</summary>
        public static RefusalIdentity Utf16Bom { get; } = Refusing("load-bom-utf16", "Filen har et UTF-16-BOM");

        /// <summary>The XML declaration names an encoding other than the one the writer emits.</summary>
        public static RefusalIdentity DeclaredEncoding { get; } =
            Refusing("load-encoding-declared", "Forkert tegnkodning");

        /// <summary>
        /// The document is not well-formed XML: truncation, a partial write, or not a project file.
        /// <para>
        /// This is also where a TRUNCATED file lands, and deliberately. <c>load-truncated</c> is a catalogue
        /// row with no code member because the condition is not separately decidable: the XML parser refuses
        /// an unclosed document before the reader sees it, and telling truncation apart afterwards would mean
        /// matching a localized exception message. The reader's own end-of-document guard therefore refuses
        /// under this id, keeping its precise English diagnostic.
        /// </para>
        /// </summary>
        public static RefusalIdentity NotXml { get; } = Refusing("load-not-xml", "Filen er ikke gyldig XML");

        /// <summary>The inline DTD block cannot be parsed, so nothing can be validated or written back.</summary>
        public static RefusalIdentity DtdMalformed { get; } = Refusing("load-dtd-malformed", "Ugyldig indbygget DTD");

        /// <summary>The root element is not a project root — another XML file opened as a project.</summary>
        public static RefusalIdentity RootTag { get; } = Refusing("load-root-tag", "Ikke en projektfil");

        /// <summary>The root carries no major version, so the file cannot be identified as a project of any version.</summary>
        public static RefusalIdentity VersionMissing { get; } =
            Refusing("load-version-missing", "Mangler projektversion");

        /// <summary>
        /// An element contains character data. The model is attribute-only, so opening the file would silently
        /// lose the text at the next save — which is the reason this refuses rather than warns.
        /// </summary>
        public static RefusalIdentity CharacterData { get; } =
            Refusing("load-character-data", "Filen indeholder tekst i et element");

        /// <summary>Element nesting exceeds the supported depth: a corrupt or hostile file.</summary>
        public static RefusalIdentity Depth { get; } = Refusing("load-depth", "For dyb elementstruktur");

        /// <summary>Every refusal in this family, for the check that each cause has a catalogue entry.</summary>
        public static Ihc.Vis.Model.EquatableArray<RefusalIdentity> All { get; } =
            System.Collections.Immutable.ImmutableArray.Create(
                Empty, Gzip, Utf8Bom, Utf16Bom, DeclaredEncoding,
                NotXml, DtdMalformed, RootTag, VersionMissing, CharacterData, Depth);
    }
}
