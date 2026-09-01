using System;
using System.Collections.Immutable;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The grammar model's validated factories (review-verified invariant list — every rule corpus-checked to
    /// hold with zero violations before being enforced as a hard rejection) and its structural value equality
    /// (the precondition for the generated catalog's declaration/grammar interning).
    /// </summary>
    public class CatalogGrammarModelTests
    {
        // ----- factory rejections -----

        [Test]
        public void Factories_Reject_InvalidXmlNames()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => GrammarAttr.Cdata("1bad", ""), "digit-leading attr name");
                Assert.Throws<ArgumentException>(() => GrammarAttr.CdataImplied("has space"));
                Assert.Throws<ArgumentException>(() => GrammarDeclaration.ElementOnly("2tag"));
                Assert.Throws<ArgumentException>(() =>
                    CatalogGrammar.Create(ImmutableArray<GrammarDeclaration>.Empty, "ISO-8859-1", doctypeRoot: "bad tag"));
            });
        }

        [Test]
        public void Declaration_Rejects_DuplicateAttributeNames()
        {
            Assert.Throws<ArgumentException>(() =>
                GrammarDeclaration.Element("r", GrammarAttr.Cdata("a", ""), GrammarAttr.Cdata("a", "x")));
        }

        [Test]
        public void Declaration_Rejects_TwoIdTypedAttributes()
        {
            Assert.Throws<ArgumentException>(() =>
                GrammarDeclaration.Element("r", GrammarAttr.Id("id"), GrammarAttr.Id("id2")));
        }

        [Test]
        public void Declaration_Rejects_EmptyOrphanAttlist()
        {
            Assert.Throws<ArgumentException>(() => GrammarDeclaration.AttlistOnly("r"));
        }

        [Test]
        public void Grammar_Rejects_DuplicateDeclarationTags()
        {
            Assert.Throws<ArgumentException>(() => CatalogGrammar.Create(new[]
            {
                GrammarDeclaration.ElementOnly("r"),
                GrammarDeclaration.AttlistOnly("r", GrammarAttr.Cdata("a", "")),
            }));
        }

        [Test]
        public void Grammar_TreatsCaseSkewedTags_AsDistinct()
        {
            CatalogGrammar grammar = CatalogGrammar.Create(new[]
            {
                GrammarDeclaration.ElementOnly("resource_Skew"),
                GrammarDeclaration.AttlistOnly("resource_skew", GrammarAttr.Id("id")),
            });

            Assert.Multiple(() =>
            {
                Assert.That(grammar.TryGetDeclaration("resource_Skew")!.HasElementDecl, Is.True);
                Assert.That(grammar.TryGetDeclaration("resource_skew")!.HasElementDecl, Is.False,
                    "ordinal comparison — case folding would merge the vendor's resource_Light/resource_light pair");
            });
        }

        [Test]
        public void Enumeration_Rejects_DuplicateTokens()
        {
            Assert.Throws<ArgumentException>(() =>
                GrammarAttr.EnumeratedRequired("mode", new[] { "on", "on" }));
        }

        [Test]
        public void Enumeration_Rejects_NonNmtokenToken_ButAcceptsDigitLeading()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => GrammarAttr.EnumeratedRequired("mode", new[] { "to ken" }));
                Assert.That(GrammarAttr.EnumeratedRequired("pulse", new[] { "24", "48" }).EnumTokens,
                    Is.EqualTo(new[] { "24", "48" }),
                    "a digit-leading token is a legal NMTOKEN — rejecting it would over-restrict user files");
            });
        }

        [Test]
        public void Enumeration_DefaultLiteral_IsComparedDecoded()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => GrammarAttr.Enumerated("mode", new[] { "on", "off" }, "maybe"),
                    "a literal default outside the token list is invalid");
                GrammarAttr encoded = GrammarAttr.Enumerated("mode", new[] { "on", "off" }, "o&#110;");
                Assert.That(encoded.RawLiteral, Is.EqualTo("o&#110;"), "the raw text is preserved for emission");
            });
        }

        [Test]
        public void IdAttribute_Rejects_LiteralDefault()
        {
            Assert.Throws<ArgumentException>(() =>
                GrammarAttr.Create("id", GrammarAttrType.Id, ImmutableArray<string>.Empty,
                                   GrammarDefault.Literal, "_0x1"));
        }

        [Test]
        public void RawLiteral_And_DefaultKind_MustAgree()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() =>
                    GrammarAttr.Create("a", GrammarAttrType.Cdata, ImmutableArray<string>.Empty,
                                       GrammarDefault.Required, "x"), "literal on #REQUIRED");
                Assert.Throws<ArgumentException>(() =>
                    GrammarAttr.Create("a", GrammarAttrType.Cdata, ImmutableArray<string>.Empty,
                                       GrammarDefault.Literal, rawLiteral: null), "Literal kind without text");
            });
        }

        [Test]
        public void RawLiteral_Rejects_TextThatCannotReEmitWellFormed()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => GrammarAttr.Cdata("a", "x<y"));
                Assert.Throws<ArgumentException>(() => GrammarAttr.Cdata("a", "x\"y"));
                Assert.Throws<ArgumentException>(() => GrammarAttr.Cdata("a", "x & y"), "bare ampersand");
                Assert.That(GrammarAttr.Cdata("a", "x &amp; y&#59;").RawLiteral, Is.EqualTo("x &amp; y&#59;"),
                    "entity and character references are fine");
            });
        }

        [Test]
        public void NonEnumerated_Rejects_EnumTokens_AndEnumerated_RequiresThem()
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() =>
                    GrammarAttr.Create("a", GrammarAttrType.Cdata, ImmutableArray.Create("on"),
                                       GrammarDefault.Implied, rawLiteral: null));
                Assert.Throws<ArgumentException>(() =>
                    GrammarAttr.Create("a", GrammarAttrType.Enumerated, ImmutableArray<string>.Empty,
                                       GrammarDefault.Implied, rawLiteral: null));
            });
        }

        [Test]
        public void Grammar_Rejects_MalformedEncodingLabel()
        {
            Assert.Throws<ArgumentException>(() =>
                CatalogGrammar.Create(ImmutableArray<GrammarDeclaration>.Empty, "ISO 8859 1"));
        }

        // ----- structural value equality (the interning precondition) -----

        private static CatalogGrammar BuildSample(string inivalueDefault) => CatalogGrammar.Create(new[]
        {
            GrammarDeclaration.Element("product_dataline",
                GrammarAttr.Id("id"),
                GrammarAttr.CdataRequired("product_identifier"),
                GrammarAttr.Enumerated("locked", new[] { "yes", "no" }, "yes")),
            GrammarDeclaration.AttlistOnly("resource_enum",
                GrammarAttr.Id("id"),
                GrammarAttr.IdRef("typedef"),
                GrammarAttr.Cdata("inivalue", inivalueDefault)),
        }, "ISO-8859-1", "product_dataline");

        [Test]
        public void Grammars_BuiltIndependentlyWithEqualContent_AreEqualAndHashEqual()
        {
            CatalogGrammar a = BuildSample("500.00");
            CatalogGrammar b = BuildSample("500.00");

            Assert.Multiple(() =>
            {
                Assert.That(a, Is.EqualTo(b));
                Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            });
        }

        [Test]
        public void Grammars_DifferingInOneDefaultLiteral_AreNotEqual()
        {
            Assert.That(BuildSample("500.00"), Is.Not.EqualTo(BuildSample("501.00")));
        }

        [Test]
        public void DefaultStructImmutableArrays_NormalizeToEmpty()
        {
            GrammarDeclaration declaration = GrammarDeclaration.Create("r", hasElementDecl: true,
                attrs: default);

            // The factory used to have to normalize a default ImmutableArray by hand, or reads would throw.
            // EquatableArray<T> makes default and empty the same value, so this holds by construction — and
            // there is deliberately no IsDefault to assert against, since observing it would distinguish two
            // values that equality says are identical.
            Assert.Multiple(() =>
            {
                Assert.That(declaration.Attrs.IsEmpty, Is.True);
                Assert.That(declaration.Attrs.Count, Is.Zero, "a default instance reads as empty, it does not throw");
                Assert.That(declaration, Is.EqualTo(GrammarDeclaration.ElementOnly("r")));
            });
        }
    }
}
