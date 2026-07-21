#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Ihc.Vis.Catalog;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;
using TypeCode = Ihc.Vis.Schema.TypeCode;
namespace Ihc.Vis.Products
{
    /// <summary>
    /// Authors a <see cref="ProductDefinition"/> from code — the product-level peer of
    /// <see cref="Ihc.Vis.Projects.NewProjectBuilder"/>, and the code-based replacement for reading a
    /// <c>Products\*.def</c> file off the IHC Visual install. The fluent setters mirror the edit-session handle
    /// <see cref="Ihc.Vis.Editing.ProductRef"/> (they return <c>this</c>), but resource configuration deliberately
    /// unifies into one optional <c>Action&lt;ProductResourceDefBuilder&gt;</c> callback — as the function-block side
    /// does — rather than ProductRef's separate <c>Func&lt;InputBuilder&gt;</c>/<c>Func&lt;OutputBuilder&gt;</c>
    /// configurators. The product here has no project yet: it is a reusable
    /// <b>type template</b> whose <c>Build()</c> output is deep-copied into a project by the unchanged insert transform.
    /// </summary>
    /// <remarks>
    /// The build produces exactly what catalog discovery yields from a <c>.def</c>: a shallow <c>product_*</c>
    /// <see cref="ProjectElement"/> <c>Body</c> (as <c>CatalogReader.Read</c> returns) plus, optionally, the
    /// structured <see cref="ProductDefinition.Grammar"/>. Because the downstream engine
    /// does the heavy lifting, the builder stays small — it only has to
    /// (1) mint placeholder ids that are unique within the body and carry the correct type-code low byte (the id
    /// counters are throwaway map keys the insert transform re-mints), and
    /// (2) bake the caller's effective attribute values verbatim, letting the canonicalizer normalize attribute order
    /// and drop default-valued attributes.
    /// Unlike the function-block builder, the product builder does <b>not</b> auto-stamp resource icons or
    /// <c>#REQUIRED</c> value initials: authentic <c>Products\*.def</c> files carry the canonical icon as the resource's
    /// own DTD default (so the vendor body omits it), which the reader materializes and the canonicalizer then drops —
    /// a leanly-authored body reaches the same canonical form by emitting nothing. Each named factory seeds its
    /// family's standard grammar preset; <see cref="DefinitionBuilderBase{TSelf}.Grammar(CatalogGrammar)"/> replaces it
    /// wholesale and <see cref="DefinitionBuilderBase{TSelf}.ExtendGrammar(System.Action{CatalogGrammarBuilder})"/>
    /// add-or-replaces single declarations (body verbs never mutate the grammar). The escape hatches —
    /// <see cref="Attribute"/>, <see cref="AddResource"/>, <see cref="RawChild"/> plus an <c>ExtendGrammar</c>
    /// declaration — cover exotic/open-world families so any product is authorable from code.
    /// <para><b>Opaque tokens (address / icon / product-identifier):</b> these are per-family, fidelity-critical wire
    /// tokens taken verbatim. A GUI does not invent them — it enumerates the legal vocabulary from the catalog seam
    /// (<see cref="Ihc.Vis.Catalog.ICatalog"/>, typically the SDK-embedded <c>BuiltInCatalog</c>) and binds pickers
    /// to it, exactly as it already does for the by-handle resource wiring on the block side.</para>
    /// <para><b>Layering:</b> this is a pure, dependency-free authoring <i>primitive</i>. A GUI backend consumes it and
    /// hands the <see cref="Build()"/> output to the app-service insert door
    /// (<c>project.Edit().Group(..).AddProduct(def)</c> via <see cref="Ihc.Vis.ProjectAppService"/>), which owns the
    /// telemetry, IO and the single project-mutation entry point — the builder deliberately owns none of that.</para>
    /// </remarks>
    public sealed class ProductDefinitionBuilder : DefinitionBuilderBase<ProductDefinitionBuilder>
    {
        /// <summary>The vendor default display name of a product's <c>scenes</c> container — a user-facing,
        /// user-editable label (it varies per catalog family, e.g. "Scenarier/regulering" on dimmers), not fixed
        /// wire grammar; surfaced as a named constant so a localizer can see it is a translatable default.</summary>
        public const string DefaultScenesName = "Scenarier";

        private static readonly (string Name, string Value)[] NoAttrs = Array.Empty<(string, string)>();
        private static readonly ProjectElement[] NoChildren = Array.Empty<ProjectElement>();

        private readonly string rootTag;
        private string productIdentifier;
        private string displayName;
        private string? bodyName;
        // rootAttrs (the ordered root-attribute list) + SetRoot/Attribute live on DefinitionBuilderBase (M7).
        private readonly List<ProjectElement> children = new();
        private ElementId? lastResourceId;
        private ElementId? builtRootId;     // memoized so repeated Build() is idempotent (no id drift off the allocator)
        private ElementId? builtScenesId;
        private string? scenes;   // the scenes container's label when requested (null = no scenes)

        private ProductDefinitionBuilder(string rootTag, string productIdentifier, string displayName,
            CatalogGrammar grammar)
            : base(grammar)   // the EFFECTIVE grammar: family preset / From-carried / assigned
        {
            this.rootTag = rootTag;
            this.productIdentifier = productIdentifier;
            this.displayName = displayName;
        }

        private protected override ProductDefinitionBuilder Self => this;

        /// <summary>Begins a dataline product (root <c>product_dataline</c>), keyed by its opaque
        /// <c>product_identifier</c> token (e.g. <c>_0x2101</c>) and shown as <paramref name="displayName"/>.
        /// Seeds the family's standard grammar preset (see the base <c>Grammar</c>/<c>ExtendGrammar</c>).</summary>
        public static ProductDefinitionBuilder Dataline(string productIdentifier, string displayName) =>
            new("product_dataline", productIdentifier, displayName, CatalogGrammarPresets.Dataline);

        /// <summary>Begins an airlink (wireless) product (root <c>product_airlink</c>); seeds the airlink preset.</summary>
        public static ProductDefinitionBuilder Airlink(string productIdentifier, string displayName) =>
            new("product_airlink", productIdentifier, displayName, CatalogGrammarPresets.Airlink);

        /// <summary>Begins an RS485 LED-dimmer product (root <c>product_rs485_led_dimmer</c>); seeds the family preset.</summary>
        public static ProductDefinitionBuilder Rs485LedDimmer(string productIdentifier, string displayName) =>
            new("product_rs485_led_dimmer", productIdentifier, displayName, CatalogGrammarPresets.Rs485LedDimmer);

        /// <summary>Begins an RS485 SMS-modem product (root <c>product_rs485_sms_modem</c>); seeds the family preset.</summary>
        public static ProductDefinitionBuilder Rs485SmsModem(string productIdentifier, string displayName) =>
            new("product_rs485_sms_modem", productIdentifier, displayName, CatalogGrammarPresets.Rs485SmsModem);

        /// <summary>Begins an S0 metering device (root <c>s0_device</c>); seeds the S0 preset.</summary>
        public static ProductDefinitionBuilder S0Device(string productIdentifier, string displayName) =>
            new("s0_device", productIdentifier, displayName, CatalogGrammarPresets.S0Device);

        /// <summary>
        /// Begins a product of an explicit family root tag — the open-world escape hatch for any product family not
        /// covered by a named factory (<see cref="Dataline"/>/<see cref="Airlink"/>/<see cref="Rs485LedDimmer"/>/
        /// <see cref="Rs485SmsModem"/>/<see cref="S0Device"/>). Throws when <paramref name="rootTag"/> is not a known
        /// product-family tag (has no schema type-code).
        /// </summary>
        public static ProductDefinitionBuilder Create(string rootTag, string productIdentifier, string displayName)
        {
            _ = TypeCode.RequireForTag(rootTag);   // reject an unknown family tag up front
            // Open-world: no preset — the grammar stays Empty until .Grammar(...)/.ExtendGrammar(...). Build()
            // remains legal (insert resolves against the registry), but writing to a catalog file is refused.
            return new ProductDefinitionBuilder(rootTag, productIdentifier, displayName, CatalogGrammar.Empty);
        }

        /// <summary>
        /// Seeds a builder from an already-authored or catalog-discovered <paramref name="existing"/> definition,
        /// decoding its <c>Body</c> back into editable builder state — the "open an existing product type and edit it"
        /// entry a GUI library editor needs (the counterpart to authoring one from scratch). The returned builder is
        /// independent of <paramref name="existing"/> (which stays immutable).
        /// </summary>
        public static ProductDefinitionBuilder From(ProductDefinition existing)
        {
            ArgumentNullException.ThrowIfNull(existing);
            // Carry the grammar (including a lenient-fallback verbatim head and its projection) verbatim — the
            // effective grammar an explicit .Grammar(...) replaces and .ExtendGrammar(...) starts from.
            var builder = new ProductDefinitionBuilder(existing.Body.Tag, existing.ProductIdentifier,
                existing.DisplayName, existing.Grammar)
            {
                categoryPath = existing.CategoryPath,
                sourceEncoding = existing.SourceEncoding,
                ids = new IdAllocator(IdAllocator.MaxCounterPresent(existing.Body)),
            };
            foreach ((string name, string value) in existing.Body.AttrsOrEmpty())
            {
                switch (name)
                {
                    case "id" or "product_identifier":
                        break;
                    case "name":
                        builder.SetName(value);   // records name at its file position in the ordered root attributes
                        break;
                    default:
                        builder.rootAttrs.Add((name, value));
                        break;
                }
            }
            foreach (ProjectElement child in existing.Body.ChildrenOrEmpty())
            {
                builder.children.Add(child);
                if (child.Id is { } id)
                {
                    builder.lastResourceId = id;
                }
            }
            builder.SeedDocumentation(existing.Documentation);
            return builder;
        }

        // ---- identity / library placement ----
        // CategoryPath(string) lives on the shared DefinitionBuilderBase (GUI menu placement).

        /// <summary>Overrides the display name shown in the IHC Visual library/tree (the
        /// <see cref="ProductDefinition.DisplayName"/> field, defaulted from the factory argument) — the library label
        /// a caller re-titles when editing a definition opened via <see cref="From"/>. Distinct from <see cref="Name"/>,
        /// which sets the placed product's own <c>name</c> attribute. Mirrors the function-block builder's
        /// <see cref="Ihc.Vis.FunctionBlocks.FunctionBlockDefinitionBuilder.DisplayName"/>.</summary>
        public ProductDefinitionBuilder DisplayName(string displayName)
        {
            this.displayName = displayName;
            return this;
        }

        /// <summary>Overrides the product body's <c>name</c> attribute — the placed instance's own label, which defaults
        /// to the display name. Distinct from <see cref="DisplayName"/>, the library/tree label.</summary>
        public ProductDefinitionBuilder Name(string name) => SetName(name);

        // Records the body name attribute at its authored position in the ordered root attribute list (so the emitted
        // order matches the file — e.g. an airlink root writes device_type before name), updating in place if already
        // present. Also tracked in bodyName for validation.
        private ProductDefinitionBuilder SetName(string name)
        {
            bodyName = name;
            for (int i = 0; i < rootAttrs.Count; i++)
            {
                if (rootAttrs[i].Name == "name")
                {
                    rootAttrs[i] = ("name", name);
                    return this;
                }
            }
            rootAttrs.Add(("name", name));
            return this;
        }

        // ---- product-level install attributes (mirror ProductRef) ----

        /// <summary>Sets whether the product is locked (bakes <c>locked="yes"</c>/<c>"no"</c>; the canonicalizer drops
        /// it when it equals the project DTD default). The defaulted bool lets a GUI checkbox bind both states.</summary>
        public ProductDefinitionBuilder Locked(bool locked = true) => SetRoot("locked", locked ? "yes" : "no");

        /// <summary>Sets whether the product is included in the end-user report (<c>enduser_report="yes"</c>).</summary>
        public ProductDefinitionBuilder EnduserReport(bool enabled = true) => SetRoot("enduser_report", enabled ? "yes" : "no");

        /// <summary>Sets the product note.</summary>
        public ProductDefinitionBuilder Note(string note) => SetRoot("note", note);

        /// <summary>Sets the physical position description.</summary>
        public ProductDefinitionBuilder Position(string position) => SetRoot("position", position);

        /// <summary>Sets the cable type.</summary>
        public ProductDefinitionBuilder CableType(string cableType) => SetRoot("cabletype", cableType);

        /// <summary>Sets the cable number.</summary>
        public ProductDefinitionBuilder CableNumber(string cableNumber) => SetRoot("cablenumber", cableNumber);

        /// <summary>Sets the documentation tag.</summary>
        public ProductDefinitionBuilder DocumentationTag(string tag) => SetRoot("documentation_tag", tag);

        /// <summary>Sets the power group.</summary>
        public ProductDefinitionBuilder PowerGroup(string powerGroup) => SetRoot("power_group", powerGroup);

        // ---- resources ----

        /// <summary>Adds a <c>dataline_input</c> pin; <paramref name="configure"/> sets its address / cable colour /
        /// note / icon. Returns this for chaining.</summary>
        public ProductDefinitionBuilder AddInput(string name, Action<ProductResourceDefBuilder>? configure = null) =>
            AddResource("dataline_input", name, configure);

        /// <summary>Adds a <c>dataline_output</c> pin; <paramref name="configure"/> sets its address / backup / icon.
        /// Returns this for chaining.</summary>
        public ProductDefinitionBuilder AddOutput(string name, Action<ProductResourceDefBuilder>? configure = null) =>
            AddResource("dataline_output", name, configure);

        /// <summary>Adds a <c>scenes</c> container bound to the most-recently-added output-family resource. The
        /// <paramref name="name"/> defaults to the vendor label <see cref="DefaultScenesName"/> (translatable).
        /// Returns this.</summary>
        public ProductDefinitionBuilder AddScenes(string name = DefaultScenesName)
        {
            scenes = name;
            return this;
        }

        // ---- documentation (help metadata; programmatic-lookup only, never serialized) ----
        // Both the product-level Documentation(string) and the name-keyed Documentation(string, string) live on the
        // shared DefinitionBuilderBase — a product keys per-resource docs by name (no handle), which is exactly the
        // base's name-keyed overload; the function-block side adds a by-FbResourceHandle overload on top.

        // ---- escape hatches (exotic families / open world) ----

        // Attribute(name, value) — the raw root-attribute escape hatch — lives on DefinitionBuilderBase (M7).

        /// <summary>Adds a resource child of an explicit family tag (e.g. <c>airlink_input</c>,
        /// <c>rs485_led_dimmer_channel</c>) for non-dataline families; <paramref name="configure"/> sets its address
        /// (resolved to the family's address attribute — see <see cref="ProductResourceDefBuilder.Address"/>), note,
        /// icon and any family-specific attributes (via <see cref="ProductResourceDefBuilder.Attribute"/>). Returns
        /// this for chaining.</summary>
        public ProductDefinitionBuilder AddResource(string tag, string name,
            Action<ProductResourceDefBuilder>? configure = null)
        {
            var configurator = new ProductResourceDefBuilder(tag);
            configure?.Invoke(configurator);
            ElementId id = ids.Allocate(TypeCode.RequireForTag(tag));
            ProjectElement resource = ProjectElement.Create(tag, id, new[] { ("name", name) }, NoChildren);
            foreach ((string attrName, string attrValue) in configurator.Attributes)
            {
                resource = resource.WithAttribute(attrName, attrValue);
            }
            children.Add(resource);
            lastResourceId = id;
            return this;
        }

        /// <summary>Splices an arbitrary pre-built subtree (e.g. an embedded <c>enum_definition</c> stub for a
        /// "med logning" product) into the body at the current position. Returns this for chaining.</summary>
        public ProductDefinitionBuilder RawChild(ProjectElement child)
        {
            ArgumentNullException.ThrowIfNull(child);
            children.Add(ids.MintMissingIds(child));
            return this;
        }

        // Grammar(CatalogGrammar) and ExtendGrammar(Action<CatalogGrammarBuilder>) live on DefinitionBuilderBase.

        /// <summary>
        /// Checks the builder's current state against the locally-decidable authoring preconditions (identity present,
        /// scenes-requires-an-output, a <c>resource_enum</c> has its type wired, ...) <b>without</b> building, returning
        /// the structured <see cref="ProjectValidationResult"/> a GUI filters and navigates by — the non-throwing path
        /// for live field-level validation as the user edits. Findings are phrased in authoring-call terms (e.g. the
        /// resource's name), since the throwaway placeholder ids mean nothing to the user.
        /// </summary>
        public ProjectValidationResult Validate()
        {
            var findings = CollectErrors();
            // The grammar↔body advisories (non-blocking warnings; skipped for an Empty grammar) over a preview
            // body assembled without touching the id allocator — Build() after Validate() must allocate the same ids.
            findings.AddRange(CatalogGrammarAdvisor.Advise(ComposeRoot(rootId: null, AdvisoryChildren()), grammar));
            return ProjectValidationResult.FromFindings(findings.ToImmutable());
        }

        // The blocking (error-severity) preconditions alone — the gate Build() checks without paying for the
        // advisory body walk it would discard (advisories are warnings and never block a build).
        private ImmutableArray<ProjectValidationFinding>.Builder CollectErrors()
        {
            var findings = ImmutableArray.CreateBuilder<ProjectValidationFinding>();
            if (string.IsNullOrEmpty(productIdentifier) || string.IsNullOrEmpty(bodyName ?? displayName)
                || TypeCode.ForTag(rootTag) is null)
            {
                findings.Add(new ProjectValidationFinding(ValidationSeverity.Error, "identity-missing", rootTag,
                    "The product needs a product_identifier, a display name and a known family root tag."));
            }
            if (scenes is { } scenesName && lastResourceId is null)
            {
                findings.Add(new ProjectValidationFinding(ValidationSeverity.Error, "scenes-without-output", scenesName,
                    "AddScenes needs a preceding resource (typically an output) to bind its scene_resource to."));
            }
            foreach (ProjectElement child in children)
            {
                if (child.Tag == "resource_enum" && child.GetAttribute("typedef") is null)
                {
                    findings.Add(new ProjectValidationFinding(ValidationSeverity.Error, "resource-enum-unwired",
                        child.GetAttribute("name"), "A resource_enum has no typedef wired to an enum_definition."));
                }
            }
            return findings;
        }

        // The scenes stub (name + optional scene_resource binding), shared by Build() (allocated id) and the
        // Validate() advisory preview (null id, allocator untouched) so their assembly can never drift — the same
        // shape ComposeRoot already uses.
        private ProjectElement MakeScenesElement(string label, ElementId? scenesId)
        {
            ProjectElement scenesElement = ProjectElement.Create("scenes", scenesId, new[] { ("name", label) }, NoChildren);
            return lastResourceId is { } bound ? scenesElement.WithAttribute("scene_resource", bound.ToToken()) : scenesElement;
        }

        private List<ProjectElement> AdvisoryChildren()
        {
            var childElements = new List<ProjectElement>(children);
            if (scenes is { } scenesLabel)
            {
                childElements.Add(MakeScenesElement(scenesLabel, scenesId: null));   // no id: allocator untouched
            }
            return childElements;
        }

        /// <summary>Materializes the <c>Body</c> (placeholder ids + effective attribute values) and returns the
        /// finished <see cref="ProductDefinition"/>. Throws <see cref="ProjectValidationException"/> when
        /// <see cref="Validate"/> would report an error — call <see cref="Validate"/> first for non-throwing UI feedback.</summary>
        public ProductDefinition Build()
        {
            if (CollectErrors().Count > 0)
            {
                throw new ProjectValidationException(Validate());   // full result, advisories included
            }

            var childElements = new List<ProjectElement>(children);
            if (scenes is { } scenesLabel)
            {
                // Allocate-once (memoized) so Build→Build produces identical bytes instead of drifting ids off the
                // persistent allocator that is never reset/reused.
                builtScenesId ??= ids.Allocate(TypeCode.RequireForTag("scenes"));
                childElements.Add(MakeScenesElement(scenesLabel, builtScenesId));
            }

            builtRootId ??= ids.Allocate(TypeCode.RequireForTag(rootTag));
            ProjectElement root = ComposeRoot(builtRootId, childElements);

            // Return the raw body (placeholder ids + effective attributes), exactly as a CatalogReader.Read yields a
            // .def's parsed tree — deliberately NOT canonicalized: the .def's own DTD defaults (e.g. a product family's
            // locked="yes") differ from the project registry's, so canonicalizing here against the registry would drop
            // attributes the catalog grammar keeps. The insert transform canonicalizes against the project on insert.
            var definition = new ProductDefinition(productIdentifier, displayName, categoryPath, root)
            {
                Grammar = grammar,
                Documentation = BuildDocumentation(),
            };
            // Stamp the From-carried physical SourceEncoding when one was carried, else keep the definition's default.
            return sourceEncoding is { } encoding ? definition with { SourceEncoding = encoding } : definition;
        }

        // Emits the root attributes in authored order — name rides at its recorded position (SetName) so families
        // that interleave another attribute before it (an airlink root's device_type) stay file-faithful. A hand
        // author who never set a name defaults to the display name, positioned right after product_identifier.
        // Shared by Build() (allocated id) and the Validate() advisory preview (no id, allocator untouched).
        private ProjectElement ComposeRoot(ElementId? rootId, IReadOnlyList<ProjectElement> childElements)
        {
            ProjectElement root = ProjectElement.Create(rootTag, rootId, NoAttrs, childElements)
                .WithAttribute("product_identifier", productIdentifier);
            bool hasName = false;
            foreach ((string name, string _) in rootAttrs)
            {
                if (name == "name") { hasName = true; break; }
            }
            if (!hasName)
            {
                root = root.WithAttribute("name", displayName);
            }
            foreach ((string name, string value) in rootAttrs)
            {
                root = root.WithAttribute(name, value);
            }
            return root;
        }
        // SetRoot(name, value) lives on DefinitionBuilderBase (M7) — the shared ordered-append seam.
    }

    /// <summary>
    /// Fluent configurator for a product resource (a <c>dataline_input</c>/<c>dataline_output</c> pin or an
    /// explicit-family resource added via <see cref="ProductDefinitionBuilder.AddResource"/>) — the definition-layer
    /// peer of <see cref="Ihc.Vis.Editing.InputBuilder"/>/<see cref="Ihc.Vis.Editing.OutputBuilder"/>, unified into one
    /// configurator (as the function-block side does with <see cref="Ihc.Vis.FunctionBlocks.FbResourceDefBuilder"/>,
    /// which likewise carries its resource tag) with a raw <see cref="Attribute"/> escape hatch for family-specific
    /// attributes.
    /// </summary>
    public sealed class ProductResourceDefBuilder
    {
        private readonly string tag;
        private readonly List<(string Name, string Value)> attrs = new();

        // Carries the resource's element tag (like FbResourceDefBuilder) so a family-agnostic setter such as Address
        // can resolve the correct per-family attribute name instead of hardcoding the dataline one.
        internal ProductResourceDefBuilder(string tag) => this.tag = tag;

        internal IReadOnlyList<(string Name, string Value)> Attributes => attrs;

        /// <summary>Sets the resource address token, resolved to the family's address attribute from the resource's
        /// element tag (<c>address_dataline</c> for dataline pins, <c>address_channel</c> for airlink channels,
        /// <c>address</c> for modems). rs485 channels have no single address attribute (they use
        /// <c>channel</c>/<c>channel_id</c>) — set those via <see cref="Attribute"/>; likewise for any exotic
        /// family's address attribute.</summary>
        public ProductResourceDefBuilder Address(string addressToken)
        {
            // A null resolution (rs485) would otherwise bake an undeclared 'address' attribute that only fails
            // far later at insert/open/save.
            string attribute = AddressAttributeFor(tag) ?? throw new InvalidOperationException(
                $"<{tag}> has no single 'address' attribute — rs485 channels are addressed by 'channel'/" +
                "'channel_id'. Use .Attribute(\"channel\", …) / .Attribute(\"channel_id\", …) instead.");
            return Set(attribute, addressToken);
        }

        // The family-to-attribute resolution Address uses — internal so the catalog decompiler recognises the
        // attribute .Address would produce for a resource's family without re-encoding this map. Total over the
        // families: null marks one with NO single address attribute (rs485 channels are addressed by
        // channel/channel_id), which .Address rejects and the decompiler's comparison never matches, leaving
        // those to render as ordinary .Attribute calls.
        internal static string? AddressAttributeFor(string tag) =>
            tag.StartsWith("dataline_", StringComparison.Ordinal) ? "address_dataline"
            : tag.StartsWith("airlink_", StringComparison.Ordinal) ? "address_channel"
            : tag.StartsWith("rs485_", StringComparison.Ordinal) ? null
            : "address";

        /// <summary>Sets the cable colour.</summary>
        public ProductResourceDefBuilder CableColour(string colour) => Set("cable_colour", colour);

        /// <summary>Sets the resource note.</summary>
        public ProductResourceDefBuilder Note(string note) => Set("note", note);

        /// <summary>Marks the resource as backed-up (<c>backup="yes"</c>) — applies to outputs and any resource that
        /// supports a backup flag; harmless on families that do not.</summary>
        public ProductResourceDefBuilder Backup(bool backup = true) => Set("backup", backup ? "yes" : "no");

        /// <summary>Overrides the GUI icon token (defaults to the family's canonical icon).</summary>
        public ProductResourceDefBuilder Icon(string iconToken) => Set("icon", iconToken);

        /// <summary>Bakes a raw attribute verbatim (escape hatch for family-specific attributes).</summary>
        public ProductResourceDefBuilder Attribute(string name, string value) => Set(name, value);

        private ProductResourceDefBuilder Set(string name, string value)
        {
            attrs.Add((name, value));
            return this;
        }
    }
}
