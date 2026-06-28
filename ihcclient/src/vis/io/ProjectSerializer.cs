#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace Ihc.Projects
{
    /// <summary>
    /// The pure, low-level byte-exact <c>.vis</c> serializer: writes a <see cref="Project"/> to its on-disk bytes
    /// exactly as-is — no clock, no metadata re-stamping (that is <see cref="ProjectAppService"/>'s job). Reproduces
    /// IHC Visual 3.4's output under the full writer contract (spec ch. 01): ISO-8859-1 with no BOM, CRLF everywhere
    /// including a trailing CRLF; the fixed XML prolog; a regenerated inline DTD declaring exactly the element types
    /// present, in first-occurrence (preorder) order, each as its verbatim canonical block; a 3-space-per-depth body
    /// (capped at depth 20) with one self-closing or paired tag per line; attributes in DTD ATTLIST order, written
    /// iff required / implied-and-set / differing from their default; the five XML specials and embedded CRLF escaped.
    /// </summary>
    public static class ProjectSerializer
    {
        private const string Crlf = "\r\n";
        private const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>";
        private const string DoctypeOpen = "<!DOCTYPE utcs_project [";
        private const string DoctypeClose = "]>";
        private const int MaxIndentDepth = 20;   // the vendor's indent cache holds depths 0..20; deeper → column 0

        private static readonly Encoding Latin1Strict =
            Encoding.GetEncoding("ISO-8859-1", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        /// <summary>Serializes a project to its <c>.vis</c> byte representation, verbatim.</summary>
        public static byte[] Serialize(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            ProjectSchemaView view = ProjectSchemaView.For(project);
            var sb = new StringBuilder(4096);
            sb.Append(XmlDeclaration).Append(Crlf);
            AppendDtd(sb, project.Root, view);
            AppendElement(sb, project.Root, depth: 0, view);
            return Encode(sb.ToString());
        }

        private static byte[] Encode(string text)
        {
            try
            {
                return Latin1Strict.GetBytes(text);
            }
            catch (EncoderFallbackException ex)
            {
                throw new InvalidOperationException(
                    "The project contains text outside the ISO-8859-1 (Latin-1) repertoire (e.g. '€' or an emoji), " +
                    "which the .vis format cannot represent. Restrict all text to Latin-1.", ex);
            }
        }

        private static void AppendDtd(StringBuilder sb, ProjectElement root, ProjectSchemaView view)
        {
            sb.Append(DoctypeOpen).Append(Crlf);
            foreach (string tag in FirstOccurrenceOrder(root))
            {
                sb.Append(view.Get(tag).CanonicalDtdBlock); // file-captured block first, registry fallback; ends with CRLF
            }
            sb.Append(DoctypeClose).Append(Crlf);
        }

        /// <summary>The element types present in the tree, in preorder first-occurrence order (root first).</summary>
        private static IEnumerable<string> FirstOccurrenceOrder(ProjectElement root)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var order = new List<string>();
            VisitPreorder(root, seen, order);
            return order;
        }

        private static void VisitPreorder(ProjectElement element, HashSet<string> seen, List<string> order)
        {
            if (seen.Add(element.Tag))
            {
                order.Add(element.Tag);
            }
            if (!element.Children.IsDefaultOrEmpty)
            {
                foreach (ProjectElement child in element.Children)
                {
                    VisitPreorder(child, seen, order);
                }
            }
        }

        private static void AppendElement(StringBuilder sb, ProjectElement element, int depth, ProjectSchemaView view)
        {
            ElementSchema schema = view.Get(element.Tag);
            string indent = Indent(depth);

            sb.Append(indent).Append('<').Append(element.Tag);
            AppendAttributes(sb, element, schema);

            if (element.Children.IsDefaultOrEmpty)
            {
                sb.Append("/>").Append(Crlf);
                return;
            }
            sb.Append('>').Append(Crlf);
            foreach (ProjectElement child in element.Children)
            {
                AppendElement(sb, child, depth + 1, view);
            }
            sb.Append(indent).Append("</").Append(element.Tag).Append('>').Append(Crlf);
        }

        private static void AppendAttributes(StringBuilder sb, ProjectElement element, ElementSchema schema)
        {
            GuardNoUnknownAttributes(element, schema);
            foreach (AttrSchema attr in schema.Attrs)
            {
                string? value = element.GetAttribute(attr.Name);
                if (value is null)
                {
                    continue; // omitted #IMPLIED, or an omitted defaulted attribute
                }
                if (attr.Kind == AttrKind.Defaulted && value == attr.Default)
                {
                    continue; // omit-if-default (exact string compare)
                }
                sb.Append(' ').Append(attr.Name).Append('=').Append('"');
                AppendEscaped(sb, value);
                sb.Append('"');
            }
        }

        private static void GuardNoUnknownAttributes(ProjectElement element, ElementSchema schema)
        {
            if (element.Attrs.IsDefaultOrEmpty)
            {
                return;
            }
            foreach ((string Name, string Value) attr in element.Attrs)
            {
                if (!SchemaHasAttribute(schema, attr.Name))
                {
                    throw new InvalidOperationException(
                        $"Element '{element.Tag}' carries attribute '{attr.Name}' that is not declared in its " +
                        $"canonical DTD block. The schema registry must cover every attribute a project uses.");
                }
            }
        }

        private static bool SchemaHasAttribute(ElementSchema schema, string name)
        {
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Name == name)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AppendEscaped(StringBuilder sb, string value)
        {
            foreach (char c in value)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    case '\r': sb.Append("&#xD;"); break;
                    case '\n': sb.Append("&#xA;"); break;
                    default: sb.Append(c); break;
                }
            }
        }

        private static string Indent(int depth)
        {
            int effective = depth <= MaxIndentDepth ? depth : 0; // depth ≥ 21 mis-emits at column 0 (vendor bug)
            return effective == 0 ? string.Empty : new string(' ', 3 * effective);
        }
    }
}
