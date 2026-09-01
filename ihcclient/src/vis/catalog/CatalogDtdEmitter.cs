using System.Text;

using Ihc.Vis.Model;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// Renders a <see cref="CatalogGrammar"/> as catalog-file header text, in one fixed canonical layout mirroring
    /// the dominant vendor style (3-space declaration indent, first attribute on the ATTLIST line,
    /// continuation-indented attributes, CRLF) so human diffs against vendor files stay readable — the layout is
    /// free under the whitespace-normalized fidelity relation. Deliberately takes no
    /// <see cref="CatalogTextEncoding"/>: the physical byte encoding must never shape the header <em>text</em>;
    /// <see cref="CatalogFileWriter"/> alone maps text to bytes.
    /// </summary>
    /// <remarks>
    /// A declaration has <b>two</b> renderings because catalog files and project hoisting need different text for
    /// an orphan ATTLIST: <see cref="RenderCatalogDeclaration"/> is vendor-faithful (no ELEMENT line for an
    /// orphan, exactly as the source wrote it), while <see cref="RenderProjectBlock"/> synthesizes the
    /// <c>&lt;!ELEMENT tag ANY&gt;</c> line the <c>.vis</c>-side block model requires
    /// (<c>ProjectSchemaRegistry.ReadTag</c> throws on a block without one — a catalog-faithful orphan block
    /// hoisted into a project would make the saved file unloadable).
    /// </remarks>
    internal static class CatalogDtdEmitter
    {
        private const string Crlf = "\r\n";
        private const string DeclarationIndent = "   ";
        private const string ContinuationIndent = "                  ";

        /// <summary>The complete header text — prolog (from <see cref="CatalogGrammar.DeclaredEncoding"/>),
        /// <c>&lt;!DOCTYPE root[</c> (root from <see cref="CatalogGrammar.DoctypeRoot"/>, defaulted from
        /// <paramref name="bodyRootTag"/>), the declarations in list order (catalog-faithful rendering), and
        /// <c>]&gt;</c> — ending with the CRLF that separates it from the body root element.</summary>
        public static string RenderHead(CatalogGrammar grammar, string bodyRootTag,
            CatalogLayout layout = CatalogLayout.Catalog)
        {
            var sb = new StringBuilder(1024);
            sb.Append("<?xml version=\"1.0\" encoding=\"").Append(grammar.DeclaredEncoding).Append("\"?>").Append(Crlf);
            // The export writer puts a space before the bracket, the catalog writer does not (S-22).
            sb.Append("<!DOCTYPE ").Append(grammar.DoctypeRoot ?? bodyRootTag)
                .Append(layout == CatalogLayout.Export ? " [" : "[").Append(Crlf);
            foreach (GrammarDeclaration declaration in grammar.Declarations)
            {
                AppendDeclaration(sb, declaration, forceElementLine: false);
            }
            sb.Append("]>").Append(Crlf);
            return sb.ToString();
        }

        /// <summary>The catalog-file rendering of one declaration: the <c>&lt;!ELEMENT tag ANY&gt;</c> line only
        /// when the record carries one, so an orphan ATTLIST re-emits exactly as the vendor wrote it.</summary>
        public static string RenderCatalogDeclaration(GrammarDeclaration declaration)
        {
            var sb = new StringBuilder(256);
            AppendDeclaration(sb, declaration, forceElementLine: false);
            return sb.ToString();
        }

        /// <summary>The <c>.vis</c>-side block rendering of one declaration: synthesizes the
        /// <c>&lt;!ELEMENT tag ANY&gt;</c> line for an orphan (mirroring the open-world capture's orphan
        /// synthesis), so the block satisfies the project block model and a project it is hoisted into reloads.</summary>
        public static string RenderProjectBlock(GrammarDeclaration declaration)
        {
            var sb = new StringBuilder(256);
            AppendDeclaration(sb, declaration, forceElementLine: true);
            return sb.ToString();
        }

        private static void AppendDeclaration(StringBuilder sb, GrammarDeclaration declaration, bool forceElementLine)
        {
            if (declaration.HasElementDecl || forceElementLine)
            {
                sb.Append(DeclarationIndent).Append("<!ELEMENT ").Append(declaration.Tag).Append(" ANY>").Append(Crlf);
            }
            if (declaration.Attrs.IsEmpty)
            {
                return;
            }
            sb.Append(DeclarationIndent).Append("<!ATTLIST ").Append(declaration.Tag);
            for (int i = 0; i < declaration.Attrs.Length; i++)
            {
                if (i == 0)
                {
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(Crlf).Append(ContinuationIndent);
                }
                AppendAttr(sb, declaration.Attrs[i]);
            }
            sb.Append('>').Append(Crlf);
        }

        private static void AppendAttr(StringBuilder sb, GrammarAttr attr)
        {
            sb.Append(attr.Name).Append(' ');
            switch (attr.Type)
            {
                case GrammarAttrType.Id:
                    sb.Append("ID");
                    break;
                case GrammarAttrType.IdRef:
                    sb.Append("IDREF");
                    break;
                case GrammarAttrType.Cdata:
                    sb.Append("CDATA");
                    break;
                default:
                    sb.Append('(');
                    for (int i = 0; i < attr.EnumTokens.Length; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(" | ");
                        }
                        sb.Append(attr.EnumTokens[i]);
                    }
                    sb.Append(')');
                    break;
            }
            sb.Append(' ');
            switch (attr.Default)
            {
                case GrammarDefault.Required:
                    sb.Append("#REQUIRED");
                    break;
                case GrammarDefault.Implied:
                    sb.Append("#IMPLIED");
                    break;
                default:
                    sb.Append('"').Append(attr.RawLiteral).Append('"');
                    break;
            }
        }
    }
}
