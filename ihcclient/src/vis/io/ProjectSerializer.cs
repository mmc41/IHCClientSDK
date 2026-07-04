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

        // Precomputed indent strings for depths 0..MaxIndentDepth (3 spaces per level), so the per-element save
        // path reuses them instead of allocating a fresh string on every element.
        private static readonly string[] IndentCache = BuildIndentCache();

        private static string[] BuildIndentCache()
        {
            var cache = new string[MaxIndentDepth + 1];
            for (int depth = 0; depth <= MaxIndentDepth; depth++)
            {
                cache[depth] = new string(' ', 3 * depth);
            }
            return cache;
        }

        /// <summary>Serializes a project to its <c>.vis</c> byte representation, verbatim.</summary>
        public static byte[] Serialize(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            ProjectSchemaView view = ProjectSchemaView.For(project);
            var sb = new StringBuilder(4096);
            sb.Append(XmlDeclaration).Append(Crlf);
            AppendDtd(sb, project.Root, view);
            AppendElement(sb, project.Root, depth: 0, view);
            return Encode(sb.ToString(), project.Root);
        }

        private static byte[] Encode(string text, ProjectElement root)
        {
            try
            {
                return ProjectFile.StrictEncoding.GetBytes(text);
            }
            catch (EncoderFallbackException ex)
            {
                throw new InvalidOperationException(
                    "The project contains text outside the ISO-8859-1 (Latin-1) repertoire, which the .vis format " +
                    $"cannot represent.{LocateNonLatin1(root)} Restrict all text to Latin-1.", ex);
            }
        }

        // One bad character in a 200 KB project is a needle in a haystack — name the first offender.
        private static string LocateNonLatin1(ProjectElement element)
        {
            if (!element.Attrs.IsDefaultOrEmpty)
            {
                foreach ((string name, string value) in element.Attrs)
                {
                    foreach (char c in value)
                    {
                        if (c > 0xFF)
                        {
                            string id = element.Id is { } eid ? $" (id {eid.ToToken()})" : string.Empty;
                            return $" First offender: attribute '{name}' on <{element.Tag}>{id} containing " +
                                   $"'{c}' (U+{(int)c:X4}).";
                        }
                    }
                }
            }
            if (!element.Children.IsDefaultOrEmpty)
            {
                foreach (ProjectElement child in element.Children)
                {
                    string found = LocateNonLatin1(child);
                    if (found.Length > 0)
                    {
                        return found;
                    }
                }
            }
            return string.Empty;
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
            SchemaGuards.GuardNoUnknownAttributes(element, schema);
            foreach (AttrSchema attr in schema.Attrs)
            {
                string? value = element.GetAttribute(attr.Name);
                if (value is null)
                {
                    if (attr.Kind == AttrKind.Required)
                    {
                        // Writing without it would violate the DTD this very file declares inline — IHC Visual
                        // (a validating consumer) then refuses the file after the original was already replaced.
                        throw new InvalidOperationException(
                            $"Element '{element.Tag}' is missing #REQUIRED attribute '{attr.Name}' declared by its " +
                            $"DTD block; run {nameof(ProjectAppService)}.{nameof(ProjectAppService.Validate)} to " +
                            "list every problem before saving.");
                    }
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
                    case '\t': sb.Append("&#x9;"); break;   // a raw tab silently becomes a space on re-read (§3.3.3)
                    default:
                        if (c < 0x20)
                        {
                            // XML 1.0 forbids these outright — written raw, the file opens in no parser.
                            throw new InvalidOperationException(
                                $"Attribute value contains control character U+{(int)c:X4}, which XML cannot " +
                                "represent; remove it from the offending text.");
                        }
                        sb.Append(c);
                        break;
                }
            }
        }

        private static string Indent(int depth) =>
            IndentCache[depth <= MaxIndentDepth ? depth : 0]; // depth ≥ 21 mis-emits at column 0 (vendor bug)
    }
}
