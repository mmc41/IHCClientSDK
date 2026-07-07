#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

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
        // The five named family factories on ProductDefinitionBuilder; anything else routes through Create(tag, ..).
        private static readonly Dictionary<string, string> FactoryByTag = new(StringComparer.Ordinal)
        {
            ["product_dataline"] = "Dataline",
            ["product_airlink"] = "Airlink",
            ["product_rs485_led_dimmer"] = "Rs485LedDimmer",
            ["product_rs485_sms_modem"] = "Rs485SmsModem",
            ["s0_device"] = "S0Device",
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

        /// <summary>Replays the recorded plan against a fresh real builder and returns its <see cref="Build"/> output —
        /// the in-process product the generator normalizes against the source <c>.def</c> to gate emission.</summary>
        public ProductDefinition Build()
        {
            ProductDefinitionBuilder builder = CreateBuilder();
            foreach (FluentCall call in Calls)
            {
                call.Apply(builder);
            }
            return builder.Build();
        }

        /// <summary>Emits the plan as a committed <c>BuiltInCatalog</c> factory method
        /// <c>private static ProductDefinition {methodName}() =&gt; ...Build();</c>.</summary>
        public string RenderMethod(string methodName)
        {
            var builder = new StringBuilder();
            builder.Append("        private static ProductDefinition ").Append(methodName).Append("() =>\n");
            builder.Append("            ").Append(RenderFactory()).Append('\n');
            foreach (FluentCall call in Calls)
            {
                builder.Append("                ").Append(call.Render).Append('\n');
            }
            builder.Append("                .Build();\n");
            return builder.ToString();
        }

        private ProductDefinitionBuilder CreateBuilder() =>
            FactoryByTag.TryGetValue(RootTag, out string? factory)
                ? factory switch
                {
                    "Dataline" => ProductDefinitionBuilder.Dataline(ProductIdentifier, DisplayName),
                    "Airlink" => ProductDefinitionBuilder.Airlink(ProductIdentifier, DisplayName),
                    "Rs485LedDimmer" => ProductDefinitionBuilder.Rs485LedDimmer(ProductIdentifier, DisplayName),
                    "Rs485SmsModem" => ProductDefinitionBuilder.Rs485SmsModem(ProductIdentifier, DisplayName),
                    "S0Device" => ProductDefinitionBuilder.S0Device(ProductIdentifier, DisplayName),
                    _ => throw new InvalidOperationException($"Unmapped factory '{factory}'."),
                }
                : ProductDefinitionBuilder.Create(RootTag, ProductIdentifier, DisplayName);

        private string RenderFactory() =>
            FactoryByTag.TryGetValue(RootTag, out string? factory)
                ? $"ProductDefinitionBuilder.{factory}({CSharpLiteral.Quote(ProductIdentifier)}, {CSharpLiteral.Quote(DisplayName)})"
                : $"ProductDefinitionBuilder.Create({CSharpLiteral.Quote(RootTag)}, {CSharpLiteral.Quote(ProductIdentifier)}, {CSharpLiteral.Quote(DisplayName)})";
    }
}
