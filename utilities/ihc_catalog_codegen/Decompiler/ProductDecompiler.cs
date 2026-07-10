#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Schema;
using TypeCode = Ihc.Vis.Schema.TypeCode;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// Reverses a parsed catalog product body (a <c>Products\*.def</c> tree as <c>CatalogReader.Read</c> yields it,
    /// with the file's own DTD defaults already materialized) into a <see cref="ProductRecipe"/> of
    /// <see cref="ProductDefinitionBuilder"/> calls. This is the inverse of "a builder is a code-authored
    /// CatalogReader": given the file the builder would reproduce, emit the builder calls that reproduce it.
    /// </summary>
    /// <remarks>
    /// <para><b>Raw-body model.</b> The reader yields the raw file body (no DTD-default materialization), so the
    /// decompiler emits every attribute the file wrote, byte-faithfully and in file order; <c>recipe.Build()</c>
    /// reproduces the source file verbatim (the generator's self-verify gate). The file's own inline-DTD grammar is
    /// consulted only for IDREF classification.</para>
    /// <para><b>Scope.</b> B1a reverses the flat <c>dataline</c>/<c>airlink</c> families — product-level setters, leaf
    /// I/O pins (<c>.AddInput</c>/<c>.AddOutput</c>/<c>.AddResource</c>) and a trailing <c>scenes</c> bound to the last
    /// resource. B1b adds nested containers (the <c>rs485_led_dimmer_channel</c>, <c>sms_modem_settings</c>,
    /// <c>dimmer_settings</c>, generic <c>settings</c> subtrees) via <c>.RawChild</c>, rendered as a verbatim-token
    /// <c>ElRaw</c> literal so any IDREF wiring internal to the subtree is preserved without remapping. Embedded
    /// body-level <c>enum_definition</c>s, cross-child IDREFs (a <c>resource_enum</c>'s <c>typedef</c>/<c>inivalue</c>)
    /// and open-world element types still raise <see cref="DecompileNotSupportedException"/> for B1c/B1d.</para>
    /// </remarks>
    internal static class ProductDecompiler
    {
        /// <summary>Decompiles <paramref name="body"/> (parsed against <paramref name="blocks"/>, the file's inline DTD)
        /// into a builder recipe, tagging it with the discovered <paramref name="displayName"/> (menu-prefix stripped)
        /// and <paramref name="categoryPath"/>. Throws <see cref="DecompileNotSupportedException"/> for constructs this
        /// stage does not yet reverse.</summary>
        public static ProductRecipe Decompile(ProjectElement body, ImmutableDictionary<string, string> blocks,
            string displayName, string categoryPath)
        {
            ArgumentNullException.ThrowIfNull(body);
            ProjectSchemaView grammar = ProjectSchemaView.For(blocks);

            string productIdentifier = body.GetAttribute("product_identifier")
                ?? throw new DecompileNotSupportedException($"Product body '{body.Tag}' has no product_identifier.");
            var recipe = new ProductRecipe(body.Tag, productIdentifier, displayName);

            if (categoryPath.Length > 0)
            {
                recipe.Calls.Add(new FluentCall(b => b.CategoryPath(categoryPath),
                    $".CategoryPath({CSharpLiteral.Quote(categoryPath)})"));
            }

            // The body is the RAW file body (CatalogReader no longer materializes DTD defaults), so it already holds
            // exactly the attributes the file wrote, in file order — emit every one (byte-faithful) except id and
            // product_identifier the factory places. name is emitted here too (via .Name), at its file position, so a
            // family that writes another attribute before it (an airlink root's device_type) stays file-faithful.
            foreach ((string name, string value) in body.AttrsOrEmpty())
            {
                if (name is "id" or "product_identifier")
                {
                    continue;
                }
                recipe.Calls.Add(RootCall(name, value));
            }

            if (BodyIsFluentExpressible(body))
            {
                string? lastResourceId = null;
                foreach (ProjectElement child in body.ChildrenOrEmpty())
                {
                    if (child.Tag == "scenes")
                    {
                        recipe.Calls.Add(ScenesCall(child, lastResourceId));
                        continue;
                    }
                    AppendChildCalls(recipe, child, grammar);
                    if (IsAddableLeaf(child))
                    {
                        lastResourceId = child.GetAttribute("id");
                    }
                }
            }
            else
            {
                // The scenes container is not expressible via AddScenes (it is not the trailing child, binds a resource
                // other than the one before it, or the body has several) — so the whole child list is emitted verbatim
                // as RawChild, keeping every id and cross-child IDREF (scenes → its resource, resource_enum → its enum)
                // exactly as the file wrote them. Product-level setters stay fluent; only the child graph goes raw.
                // This fallback intentionally imposes no IDREF self-containment check (all sibling ids are verbatim too).
                foreach (ProjectElement child in body.ChildrenOrEmpty())
                {
                    recipe.Calls.Add(RawChildCall(child));
                }
            }
            return recipe;
        }

        // Whether the body's scenes wiring fits AddScenes, which can only express a single scenes that is the last child
        // and binds the immediately-preceding addable resource. Anything else routes the whole child list to RawChild.
        private static bool BodyIsFluentExpressible(ProjectElement body)
        {
            ImmutableArray<ProjectElement> children = body.ChildrenOrEmpty();
            int scenesCount = children.Count(c => c.Tag == "scenes");
            if (scenesCount == 0)
            {
                return true;
            }
            if (scenesCount > 1 || children[^1].Tag != "scenes")
            {
                return false;   // several scenes, or scenes is not the trailing child AddScenes would append it as
            }
            ProjectElement scenes = children[^1];
            foreach ((string name, string _) in scenes.AttrsOrEmpty())
            {
                if (name is not ("id" or "name" or "scene_resource"))
                {
                    return false;   // any other present attribute (even an empty note="") AddScenes cannot carry → RawChild
                }
            }
            string? sceneResource = scenes.GetAttribute("scene_resource");
            if (sceneResource is null)
            {
                return true;
            }
            string? lastAddable = null;
            for (int i = 0; i < children.Length - 1; i++)
            {
                if (IsAddableLeaf(children[i]))
                {
                    lastAddable = children[i].GetAttribute("id");
                }
            }
            return sceneResource == lastAddable;
        }

        // ---- body-child dispatch ----

        // A leaf the builder adds via AddInput/AddOutput/AddResource (and which a following scenes binds to): a
        // childless, non-structural, registry-declared element with a type code. A resource_enum counts — its
        // typedef/inivalue are ordinary IDREF attributes the canonicalizer remaps. Everything else (nested container,
        // structural or open-world leaf) is a RawChild.
        private static bool IsAddableLeaf(ProjectElement child) =>
            child.ChildrenOrEmpty().IsEmpty
            && !IsStructural(child.Tag)
            && TypeCode.ForTag(child.Tag) is not null
            && ProjectSchemaView.RegistryOnly.TryGet(child.Tag) is not null;

        private static void AppendChildCalls(ProductRecipe recipe, ProjectElement child, ProjectSchemaView grammar)
        {
            if (IsAddableLeaf(child))
            {
                recipe.Calls.Add(AddResourceCall(child));
                return;
            }

            // RawChild path: nested container, structural leaf, or an open-world leaf the builder cannot type. A
            // container's internal IDREF must resolve inside the subtree (a verbatim-token render preserves it); a
            // reference reaching outside is a cross-child wiring the fluent path does not reverse (the whole-body
            // RawChild fallback handles those, where every sibling id is also verbatim).
            if (!child.ChildrenOrEmpty().IsEmpty && !IdRefsSelfContained(child, grammar))
            {
                throw new DecompileNotSupportedException(
                    $"nested container '{child.Tag}' has an IDREF reaching outside the subtree — needs whole-body RawChild.");
            }
            recipe.Calls.Add(RawChildCall(child));
        }

        // ---- product-level setters ----

        private static FluentCall RootCall(string name, string value)
        {
            switch (name)
            {
                case "name":
                    return new FluentCall(b => b.Name(value), $".Name({CSharpLiteral.Quote(value)})");
                case "note":
                    return new FluentCall(b => b.Note(value), $".Note({CSharpLiteral.Quote(value)})");
                case "position":
                    return new FluentCall(b => b.Position(value), $".Position({CSharpLiteral.Quote(value)})");
                case "cabletype":
                    return new FluentCall(b => b.CableType(value), $".CableType({CSharpLiteral.Quote(value)})");
                case "cablenumber":
                    return new FluentCall(b => b.CableNumber(value), $".CableNumber({CSharpLiteral.Quote(value)})");
                case "documentation_tag":
                    return new FluentCall(b => b.DocumentationTag(value), $".DocumentationTag({CSharpLiteral.Quote(value)})");
                case "power_group":
                    return new FluentCall(b => b.PowerGroup(value), $".PowerGroup({CSharpLiteral.Quote(value)})");
                case "locked" when value is "yes" or "no":
                    return value == "yes"
                        ? new FluentCall(b => b.Locked(), ".Locked()")
                        : new FluentCall(b => b.Locked(false), ".Locked(false)");
                case "enduser_report" when value is "yes" or "no":
                    return value == "yes"
                        ? new FluentCall(b => b.EnduserReport(), ".EnduserReport()")
                        : new FluentCall(b => b.EnduserReport(false), ".EnduserReport(false)");
                default:
                    return new FluentCall(b => b.Attribute(name, value),
                        $".Attribute({CSharpLiteral.Quote(name)}, {CSharpLiteral.Quote(value)})");
            }
        }

        private static FluentCall ScenesCall(ProjectElement scenes, string? lastResourceId)
        {
            foreach ((string name, string _) in scenes.AttrsOrEmpty())
            {
                if (name is not ("id" or "name" or "scene_resource"))
                {
                    throw new DecompileNotSupportedException(
                        $"scenes carries a '{name}' attribute AddScenes cannot express — needs RawChild (B1b).");
                }
            }
            string? sceneResource = scenes.GetAttribute("scene_resource");
            if (sceneResource is not null && sceneResource != lastResourceId)
            {
                throw new DecompileNotSupportedException(
                    "scenes.scene_resource does not bind the immediately-preceding resource — needs RawChild (B1b).");
            }

            string label = scenes.GetAttribute("name") ?? ProductDefinitionBuilder.DefaultScenesName;
            return label == ProductDefinitionBuilder.DefaultScenesName
                ? new FluentCall(b => b.AddScenes(), ".AddScenes()")
                : new FluentCall(b => b.AddScenes(label), $".AddScenes({CSharpLiteral.Quote(label)})");
        }

        // ---- leaf resources (AddInput / AddOutput / AddResource) ----

        private static FluentCall AddResourceCall(ProjectElement child)
        {
            string name = child.GetAttribute("name") ?? string.Empty;
            var config = ImmutableArray.CreateBuilder<ResourceCall>();
            foreach ((string attrName, string attrValue) in child.AttrsOrEmpty())
            {
                if (attrName is "id" or "name")   // set by the AddInput/AddOutput/AddResource factory; the rest ride in file order
                {
                    continue;
                }
                config.Add(ResourceConfigCall(child.Tag, attrName, attrValue));
            }
            ImmutableArray<ResourceCall> calls = config.ToImmutable();

            string tag = child.Tag;
            return tag switch
            {
                "dataline_input" => ResourceAdder($".AddInput({CSharpLiteral.Quote(name)}", "i", calls,
                    (b, cfg) => b.AddInput(name, cfg)),
                "dataline_output" => ResourceAdder($".AddOutput({CSharpLiteral.Quote(name)}", "o", calls,
                    (b, cfg) => b.AddOutput(name, cfg)),
                _ => ResourceAdder($".AddResource({CSharpLiteral.Quote(tag)}, {CSharpLiteral.Quote(name)}", "r", calls,
                    (b, cfg) => b.AddResource(tag, name, cfg)),
            };
        }

        // Builds the AddInput/AddOutput/AddResource FluentCall: applies the recorded config calls to the real resource
        // configurator, and renders the same chain as a lambda (elided entirely when there is no config).
        private static FluentCall ResourceAdder(string renderHead, string lambdaParam, ImmutableArray<ResourceCall> config,
            Action<ProductDefinitionBuilder, Action<ProductResourceDefBuilder>> add)
        {
            void Apply(ProductDefinitionBuilder b) => add(b, r =>
            {
                foreach (ResourceCall call in config)
                {
                    call.Apply(r);
                }
            });

            if (config.IsEmpty)
            {
                return new FluentCall(Apply, $"{renderHead})");
            }
            string body = string.Concat(config.Select(c => c.Render));
            return new FluentCall(Apply, $"{renderHead}, {lambdaParam} => {lambdaParam}{body})");
        }

        private static ResourceCall ResourceConfigCall(string tag, string name, string value)
        {
            if (name == ProductResourceDefBuilder.AddressAttributeFor(tag))
            {
                return new ResourceCall(r => r.Address(value), $".Address({CSharpLiteral.Quote(value)})");
            }
            switch (name)
            {
                case "cable_colour":
                    return new ResourceCall(r => r.CableColour(value), $".CableColour({CSharpLiteral.Quote(value)})");
                case "note":
                    return new ResourceCall(r => r.Note(value), $".Note({CSharpLiteral.Quote(value)})");
                case "icon":
                    return new ResourceCall(r => r.Icon(value), $".Icon({CSharpLiteral.Quote(value)})");
                case "backup" when value is "yes" or "no":
                    return value == "yes"
                        ? new ResourceCall(r => r.Backup(), ".Backup()")
                        : new ResourceCall(r => r.Backup(false), ".Backup(false)");
                default:
                    return new ResourceCall(r => r.Attribute(name, value),
                        $".Attribute({CSharpLiteral.Quote(name)}, {CSharpLiteral.Quote(value)})");
            }
        }

        // ---- nested containers (RawChild) ----

        // Reverses a child to .RawChild(ElRaw(..)): the raw subtree (file attributes, ids and internal IDREF wiring
        // verbatim) drives the real builder, and the same tree renders as an ElRaw literal, keeping the executed and
        // emitted forms in lock-step. An open-world element type inside the subtree needs no separate grammar call:
        // the definition's complete structured grammar (every declaration, open-world types included) is carried by
        // the recipe's baked source grammar, which BakeSourceFidelity applies at Build().
        private static FluentCall RawChildCall(ProjectElement child) =>
            new(b => b.RawChild(child), $".RawChild({RenderSubtree(child, 20)})");

        // Renders a raw subtree as ElRaw("tag", "idToken", attrs, children...). Verbatim id tokens keep the subtree
        // self-consistent (an internal IDREF still names its target's token), so no id remapping is needed at all.
        private static string RenderSubtree(ProjectElement node, int childIndent)
        {
            // The full attribute list (id included at its file position) is passed verbatim — a few vendor elements
            // write id after another attribute (e.g. product4409's error-state resources put icon before id), and
            // ElRaw must preserve that order for byte fidelity. StampDocumentOrder re-mints the id value in place.
            string attrs = RenderAttrs(node);
            string head = $"ElRaw({CSharpLiteral.Quote(node.Tag)}, {attrs}";
            ImmutableArray<ProjectElement> children = node.ChildrenOrEmpty();
            if (children.IsEmpty)
            {
                return head + ")";
            }
            var builder = new StringBuilder(head);
            string pad = new string(' ', childIndent);
            foreach (ProjectElement kid in children)
            {
                builder.Append(",\n").Append(pad).Append(RenderSubtree(kid, childIndent + 4));
            }
            builder.Append(')');
            return builder.ToString();
        }

        private static string RenderAttrs(ProjectElement node)
        {
            IEnumerable<string> pairs = node.AttrsOrEmpty()
                .Select(a => $"({CSharpLiteral.Quote(a.Name)}, {CSharpLiteral.Quote(a.Value)})");
            string joined = string.Join(", ", pairs);
            return joined.Length == 0 ? "System.Array.Empty<(string, string)>()" : $"new[] {{ {joined} }}";
        }

        // ---- IDREF helpers ----

        private static bool IdRefsSelfContained(ProjectElement container, ProjectSchemaView grammar)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            CollectIds(container, ids);
            return AllIdRefsResolve(container, grammar, ids);
        }

        private static void CollectIds(ProjectElement element, HashSet<string> ids)
        {
            foreach (ProjectElement e in element.DescendantsAndSelf())
            {
                if (e.GetAttribute("id") is { } id)
                {
                    ids.Add(id);
                }
            }
        }

        private static bool AllIdRefsResolve(ProjectElement element, ProjectSchemaView grammar, HashSet<string> ids)
        {
            ElementSchema? schema = grammar.TryGet(element.Tag);
            if (schema is not null)
            {
                foreach ((string name, string value) in element.AttrsOrEmpty())
                {
                    if (schema.IsIdRef(name) && value.Length > 0 && !ids.Contains(value))
                    {
                        return false;
                    }
                }
            }
            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                if (!AllIdRefsResolve(child, grammar, ids))
                {
                    return false;
                }
            }
            return true;
        }

        // ---- shared ----

        private static bool IsStructural(string tag) =>
            tag is "scenes" or "enum_definition" or "settings"
            || tag.EndsWith("_settings", StringComparison.Ordinal);
    }
}
