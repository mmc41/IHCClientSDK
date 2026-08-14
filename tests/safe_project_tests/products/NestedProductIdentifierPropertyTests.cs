#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using CsCheck;
using Ihc.Vis.Catalog;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The property-based half of the nested-identifier law. <see cref="NestedProductIdentifierTests"/> pins the one
    /// shipped fixture that exhibits the hazard and the vendor catalog that motivates it; this pins the LAW that must
    /// hold for every arrangement of the attribute a <c>.def</c> can contain — <b>a product's identity is its ROOT
    /// element's <c>product_identifier</c>, whatever any descendant declares</b>.
    ///
    /// <para>The generator emits <c>.def</c> TEXT rather than building a tree in memory, because the hazard lives at
    /// the text-to-tree boundary and nowhere else: once a file is parsed, asking the root for its attribute is
    /// trivially correct, so a property over generated <see cref="Ihc.Vis.Model.ProjectElement"/>s would be
    /// tautological. Feeding bytes through <see cref="CatalogReader"/> is what exercises the real risk — a future
    /// reader that scans the file as text instead of walking it as a tree.</para>
    ///
    /// <para>The arrangements are chosen to kill every plausible text-scan strategy at once, which a single fixture
    /// cannot: descendants whose identifier sorts BEFORE the root's (kills "smallest"), AFTER it (kills "largest"),
    /// several descendants carrying identifiers (kills "last"), identifiers on grandchildren (kills "any depth-1
    /// child"), and the inline DTD's own <c>product_identifier CDATA #REQUIRED</c> declaration, which is present in
    /// every generated file and is a DECLARATION rather than a value (kills a naive regex over the whole file).</para>
    /// </summary>
    public class NestedProductIdentifierPropertyTests
    {
        /// <summary>Identifier tokens spanning the lexical range, so a descendant can sort either side of the root.</summary>
        private static readonly Gen<string> Identifier =
            Gen.OneOfConst("_0x01", "_0x4409", "_0x4410", "_0x9f05", "_0x9f15", "_0xa000", "_0xffff", "_0x21000007");

        /// <summary>Where a descendant carrying its own identifier sits, and what it says.</summary>
        private record Nested(int Depth, string? Value);

        private static readonly Gen<Nested> NestedGen =
            Gen.Select(Gen.Int[1, 3], Gen.OneOf(Identifier.Select(v => (string?)v), Gen.Const((string?)null)))
               .Select(t => new Nested(t.Item1, t.Item2));

        private static readonly Gen<(string Root, Nested[] Descendants)> Arrangement =
            Gen.Select(Identifier, NestedGen.Array[0, 5]).Select(t => (t.Item1, t.Item2));

        /// <summary>
        /// Renders a well-formed single-family <c>.def</c>: a <c>product_dataline</c> root carrying
        /// <paramref name="root"/>, and one <c>dataline_input</c> chain per entry in <paramref name="descendants"/>,
        /// each nested to its own depth and optionally declaring its own <c>product_identifier</c>. The inline DTD
        /// declares the attribute on both elements, so every file is legal against its own grammar.
        /// </summary>
        private static byte[] Render(string root, IReadOnlyList<Nested> descendants)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n");
            sb.Append("<!DOCTYPE product_dataline[\n");
            sb.Append("   <!ELEMENT product_dataline ANY>\n");
            sb.Append("   <!ATTLIST product_dataline id ID #REQUIRED\n");
            sb.Append("                  product_identifier CDATA #REQUIRED\n");
            sb.Append("                  name CDATA \"\">\n");
            sb.Append("   <!ELEMENT dataline_input ANY>\n");
            sb.Append("   <!ATTLIST dataline_input id ID #REQUIRED\n");
            sb.Append("                  product_identifier CDATA \"\"\n");
            sb.Append("                  name CDATA \"\">\n");
            sb.Append("]>\n");
            sb.Append($"<product_dataline id=\"_0x01\" product_identifier=\"{root}\" name=\"Generated\">\n");

            int id = 2;
            foreach (Nested d in descendants)
            {
                var open = new List<string>();
                for (int level = 1; level <= d.Depth; level++)
                {
                    // Only the DEEPEST element of each chain carries the identifier, so a value can sit on a
                    // grandchild with nothing declared in between.
                    string attr = level == d.Depth && d.Value is not null
                        ? $" product_identifier=\"{d.Value}\""
                        : string.Empty;
                    sb.Append($"  <dataline_input id=\"_0x{id:x2}\"{attr} name=\"L{level}\">\n");
                    open.Add("dataline_input");
                    id++;
                }
                for (int level = open.Count - 1; level >= 0; level--)
                {
                    sb.Append("  </dataline_input>\n");
                }
            }

            sb.Append("</product_dataline>\n");
            return Encoding.GetEncoding("ISO-8859-1").GetBytes(sb.ToString());
        }

        private static ProductDefinition ReadRendered(string root, IReadOnlyList<Nested> descendants)
        {
            using var stream = new MemoryStream(Render(root, descendants));
            return CatalogReader.ReadProduct(stream);
        }

        /// <summary>
        /// THE law: for any arrangement of <c>product_identifier</c> across a product's descendants, the identifier
        /// the reader reports is the ROOT's.
        /// </summary>
        [Test]
        public void ReadIdentifier_IsAlwaysTheRoots_ForAnyArrangementOfNestedIdentifiers()
        {
            Arrangement.Sample(a => ReadRendered(a.Root, a.Descendants).ProductIdentifier == a.Root,
                iter: 1000, threads: 1);
        }

        /// <summary>
        /// The law above is satisfiable by a reader that returns the root for the arrangements the generator
        /// happens to draw; this pins that the generator actually REACHES the hazardous shapes. Without it the
        /// property could pass on a sample of files that never nest an identifier at all.
        /// </summary>
        [Test]
        public void TheGenerator_ReachesTheHazardousArrangements()
        {
            var sawDifferingDescendant = false;
            var sawDescendantSortingBefore = false;
            var sawDescendantSortingAfter = false;
            var sawTwoOrMoreDescendants = false;
            var sawGrandchild = false;

            Arrangement.Sample(a =>
            {
                List<string> values = a.Descendants.Where(d => d.Value is not null).Select(d => d.Value!).ToList();
                List<string> differing = values.Where(v => v != a.Root).ToList();
                sawDifferingDescendant |= differing.Count > 0;
                sawDescendantSortingBefore |= differing.Any(v => string.CompareOrdinal(v, a.Root) < 0);
                sawDescendantSortingAfter |= differing.Any(v => string.CompareOrdinal(v, a.Root) > 0);
                sawTwoOrMoreDescendants |= values.Count >= 2;
                sawGrandchild |= a.Descendants.Any(d => d.Value is not null && d.Depth >= 2);
                return true;
            }, iter: 1000, threads: 1);

            Assert.Multiple(() =>
            {
                Assert.That(sawDifferingDescendant, Is.True, "no descendant ever declared a differing identifier");
                Assert.That(sawDescendantSortingBefore, Is.True, "never generated a descendant sorting BEFORE the root");
                Assert.That(sawDescendantSortingAfter, Is.True, "never generated a descendant sorting AFTER the root");
                Assert.That(sawTwoOrMoreDescendants, Is.True, "never generated two or more identifier-bearing descendants");
                Assert.That(sawGrandchild, Is.True, "never generated an identifier below depth 1");
            });
        }

        /// <summary>
        /// Positive control: the rendered files really are hazardous, i.e. a naive whole-file text scan gets a
        /// DIFFERENT answer than the reader does. If this ever stops finding a disagreement the property above has
        /// become decorative — the generated corpus would no longer contain the trap it exists to set.
        /// </summary>
        [Test]
        public void ANaiveTextScan_DisagreesWithTheReader_OnAtLeastSomeGeneratedFiles()
        {
            var disagreements = 0;
            Arrangement.Sample(a =>
            {
                string text = Encoding.GetEncoding("ISO-8859-1").GetString(Render(a.Root, a.Descendants));
                // What a text scan would return: the LAST product_identifier="..." occurrence in the file.
                var matches = System.Text.RegularExpressions.Regex
                    .Matches(text, "product_identifier=\"([^\"]*)\"")
                    .Select(m => m.Groups[1].Value)
                    .ToList();
                if (matches.Count > 0 && matches[^1] != a.Root)
                {
                    disagreements++;
                }
                return true;
            }, iter: 1000, threads: 1);

            Assert.That(disagreements, Is.GreaterThan(0),
                "no generated file would fool a last-match text scan, so the property proves nothing");
        }
    }
}
