#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

using Ihc.Vis.Catalog;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// One recorded configurator call on a product resource (a <c>dataline_input</c>/<c>dataline_output</c> pin or an
    /// <see cref="ProductDefinitionBuilder.AddResource"/> child): it bundles the live <see cref="Apply"/> that drives
    /// the real <see cref="ProductResourceDefBuilder"/> with the <see cref="Render"/> that emits the equivalent C#
    /// fluent call (e.g. <c>.Address("_0x1")</c>). The two are authored together so the generator's in-process
    /// self-verify and its committed source can never disagree about what a call does.
    /// </summary>
    internal sealed class ResourceCall
    {
        public ResourceCall(Action<ProductResourceDefBuilder> apply, string render)
        {
            Apply = apply;
            Render = render;
        }

        public Action<ProductResourceDefBuilder> Apply { get; }
        public string Render { get; }
    }

    /// <summary>
    /// One recorded fluent call on a <see cref="ProductDefinitionBuilder"/> — a product-level setter (<c>.Note(..)</c>),
    /// a resource adder (<c>.AddInput(name, cfg)</c>) or the scenes container (<c>.AddScenes(..)</c>). Like
    /// <see cref="ResourceCall"/> it pairs the live <see cref="Apply"/> with the <see cref="Render"/>ed C# so the
    /// executed and emitted forms stay in lock-step.
    /// </summary>
    internal sealed class FluentCall
    {
        public FluentCall(Action<ProductDefinitionBuilder> apply, string render)
        {
            Apply = apply;
            Render = render;
        }

        public Action<ProductDefinitionBuilder> Apply { get; }
        public string Render { get; }
    }

    /// <summary>
    /// The decompiled plan for authoring one catalog product from code: the family factory choice plus the ordered
    /// <see cref="FluentCall"/> chain. It is deliberately <b>dual-nature</b> — <see cref="Build"/> replays the plan
    /// against the real <see cref="ProductDefinitionBuilder"/> (so the generator can self-verify the plan reproduces
    /// the source <c>.def</c> before committing anything), and <see cref="RenderMethod"/> emits the identical plan as a
    /// C# factory method for <c>BuiltInCatalog</c>. Because a single recorded call carries both behaviours, the shipped
    /// source is exactly what self-verify proved correct.
    /// </summary>
    internal sealed class ProductRecipe
    {
        // The five named family factories on ProductDefinitionBuilder — each tag maps to the rendered method name and
        // the live invocation together, so the emitted call and the self-verify replay can never diverge; anything
        // else routes through Create(tag, ..).
        private static readonly Dictionary<string, (string Name, Func<string, string, ProductDefinitionBuilder> Make)> FactoryByTag =
            new(StringComparer.Ordinal)
            {
                ["product_dataline"] = ("Dataline", ProductDefinitionBuilder.Dataline),
                ["product_airlink"] = ("Airlink", ProductDefinitionBuilder.Airlink),
                ["product_rs485_led_dimmer"] = ("Rs485LedDimmer", ProductDefinitionBuilder.Rs485LedDimmer),
                ["product_rs485_sms_modem"] = ("Rs485SmsModem", ProductDefinitionBuilder.Rs485SmsModem),
                ["s0_device"] = ("S0Device", ProductDefinitionBuilder.S0Device),
            };

        public ProductRecipe(string rootTag, string productIdentifier, string displayName)
        {
            RootTag = rootTag;
            ProductIdentifier = productIdentifier;
            DisplayName = displayName;
        }

        public string RootTag { get; }
        public string ProductIdentifier { get; }

        /// <summary>The menu-prefix-stripped library display name — the factory's <c>displayName</c> argument.</summary>
        public string DisplayName { get; }

        public List<FluentCall> Calls { get; } = new();

        /// <summary>The source file's structured grammar (strict-parsed — the envelope guard: any construct outside
        /// the catalog grammar model fails generation loudly) and text encoding, baked onto the built definition so
        /// <c>CatalogFileWriter</c> reproduces the file and the insert transform re-materializes the file's DTD
        /// defaults install-free. Set by the emitter from the source bytes.</summary>
        public CatalogGrammar SourceGrammar { get; set; } = CatalogGrammar.Empty;

        public CatalogTextEncoding SourceEncoding { get; set; } = CatalogTextEncoding.Utf8Bom;

        /// <summary>The source file's document-order id tokens, stamped onto the built body (D1) so it carries the
        /// vendor file's exact ids instead of the builder's placeholder allocation. Set by the emitter.</summary>
        public IReadOnlyList<string> SourceIdTokens { get; set; } = Array.Empty<string>();

        /// <summary>Bakes the source file's fidelity data (strict-parsed grammar, text encoding, document-order id
        /// tokens) onto the recipe, so <see cref="Build"/> reproduces the file byte-faithfully. Every verify path
        /// (emit and self-test) must call this before <see cref="SelfVerify.Verify"/>.</summary>
        public void BakeSourceFidelity(ProductSource source)
        {
            SourceGrammar = CatalogDtdParser.ParseStrict(CatalogDtdParser.CaptureHeadText(source.FileBytes));
            SourceEncoding = source.Definition.SourceEncoding;
            SourceIdTokens = CatalogIds.ExtractDocumentOrderIds(source.Definition.Body);
        }

        /// <summary>Replays the recorded plan against a fresh real builder and returns its <see cref="Build"/> output —
        /// the in-process product the generator normalizes against the source <c>.def</c> to gate emission.</summary>
        public ProductDefinition Build()
        {
            ProductDefinitionBuilder builder = CreateBuilder();
            foreach (FluentCall call in Calls)
            {
                call.Apply(builder);
            }
            ProductDefinition definition = builder.Grammar(SourceGrammar).Build();
            return definition with
            {
                SourceEncoding = SourceEncoding,
                Body = CatalogIds.StampDocumentOrder(definition.Body, SourceIdTokens, SourceGrammar),
            };
        }

        /// <summary>Emits the plan as a committed <c>BuiltInCatalog</c> factory method, its grammar carried as a
        /// single reference (<paramref name="grammarRef"/>) into the interned grammar table.</summary>
        public string RenderMethod(string methodName, string grammarRef)
        {
            var builder = new StringBuilder();
            builder.Append("        private static ProductDefinition ").Append(methodName).Append("()\n");
            builder.Append("        {\n");
            builder.Append("            ProductDefinition definition =\n");
            builder.Append("                ").Append(RenderFactory()).Append('\n');
            foreach (FluentCall call in Calls)
            {
                builder.Append("                    ").Append(call.Render).Append('\n');
            }
            builder.Append("                    .Grammar(").Append(grammarRef).Append(")\n");
            builder.Append("                    .Build();\n");
            builder.Append("            return definition with\n");
            builder.Append("            {\n");
            builder.Append("                SourceEncoding = CatalogTextEncoding.").Append(SourceEncoding).Append(",\n");
            builder.Append("                Body = CatalogIds.StampDocumentOrder(definition.Body, ")
                   .Append(RenderIdTokens()).Append(", ").Append(grammarRef).Append("),\n");
            builder.Append("            };\n");
            builder.Append("        }\n");
            return builder.ToString();
        }

        private string RenderIdTokens()
        {
            if (SourceIdTokens.Count == 0)
            {
                return "System.Array.Empty<string>()";
            }
            var sb = new StringBuilder("new[] { ");
            for (int i = 0; i < SourceIdTokens.Count; i++)
            {
                if (i > 0) { sb.Append(", "); }
                sb.Append(CSharpLiteral.Quote(SourceIdTokens[i]));
            }
            sb.Append(" }");
            return sb.ToString();
        }

        private ProductDefinitionBuilder CreateBuilder() =>
            FactoryByTag.TryGetValue(RootTag, out (string Name, Func<string, string, ProductDefinitionBuilder> Make) factory)
                ? factory.Make(ProductIdentifier, DisplayName)
                : ProductDefinitionBuilder.Create(RootTag, ProductIdentifier, DisplayName);

        private string RenderFactory() =>
            FactoryByTag.TryGetValue(RootTag, out (string Name, Func<string, string, ProductDefinitionBuilder> Make) factory)
                ? $"ProductDefinitionBuilder.{factory.Name}({CSharpLiteral.Quote(ProductIdentifier)}, {CSharpLiteral.Quote(DisplayName)})"
                : $"ProductDefinitionBuilder.Create({CSharpLiteral.Quote(RootTag)}, {CSharpLiteral.Quote(ProductIdentifier)}, {CSharpLiteral.Quote(DisplayName)})";
    }
}
