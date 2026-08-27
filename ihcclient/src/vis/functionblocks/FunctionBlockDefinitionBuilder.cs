#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Problems;
using Ihc.Vis.Catalog;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;
using TypeCode = Ihc.Vis.Schema.TypeCode;
namespace Ihc.Vis.FunctionBlocks
{
    /// <summary>
    /// Authors a <see cref="FunctionBlockDefinition"/> from code — the function-block-level peer of
    /// <see cref="Ihc.Vis.Projects.NewProjectBuilder"/>, and the code-based replacement for reading a
    /// <c>FunctionBlocks\*.ifb</c> file off the IHC Visual install. Covers the master identity, the four resource
    /// containers in fixed order (<c>inputs</c>/<c>outputs</c>/<c>settings</c>/<c>internalsettings</c>) plus the
    /// <c>programs</c> container holding the deep program graph. The fluent style mirrors the edit-session handle
    /// <see cref="Ihc.Vis.Editing.FunctionBlockRef"/> (resources configured with an
    /// <c>Action&lt;FbResourceDefBuilder&gt;</c> callback), and the program graph parallels
    /// <see cref="Ihc.Vis.Editing.ProgramBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Each resource add returns an <see cref="FbResourceHandle"/> — the definition-local reference used to wire
    /// <c>link1</c>/<c>link2</c> in the program graph. It is deliberately <b>not</b>
    /// <see cref="Ihc.Vis.Editing.ResourceRef"/>: that is an <c>Editing</c>-layer live-session type, and
    /// <c>Editing</c> already depends on <c>FunctionBlocks</c>, so the definition layer must not depend back on it.
    /// Placeholder ids are internally consistent (a per-body allocator mints one id per element, carrying the correct
    /// type-code low byte) so wired IDREFs resolve without any project present; the insert transform re-mints the ids
    /// and remaps the IDREFs on insert. <c>method</c> and non-registry <c>icon</c> tokens are opaque, per-block,
    /// fidelity-critical data and so are caller-supplied.
    /// <para><b>Opaque tokens (method / icon):</b> a GUI does not invent these — it enumerates the legal operation and
    /// icon vocabulary from the catalog seam (<see cref="Ihc.Vis.Catalog.ICatalog"/>, typically the SDK-embedded
    /// <c>BuiltInCatalog</c>) and binds pickers to it, the same way resource operands
    /// are already picked by <see cref="FbResourceHandle"/> object rather than by id token.</para>
    /// <para><b>Layering:</b> a pure, dependency-free authoring <i>primitive</i>. A GUI backend hands the
    /// <see cref="Build()"/> output to the app-service insert door (<c>project.Edit().Group(..).AddFunctionBlock(def)</c>
    /// via <see cref="Ihc.Vis.ProjectAppService"/>), which owns telemetry, IO and the single project-mutation entry
    /// point — the builder deliberately owns none of that.</para>
    /// </remarks>
    public sealed class FunctionBlockDefinitionBuilder : DefinitionBuilderBase<FunctionBlockDefinitionBuilder>
    {
        private static readonly (string Name, string Value)[] NoAttrs = Array.Empty<(string, string)>();
        private static readonly ProjectElement[] NoChildren = Array.Empty<ProjectElement>();

        private ProjectElement? builtBody;   // memoized so repeated Build() is idempotent (no id drift off the shared allocator)
        private readonly string masterType;
        private readonly string masterVersion;
        private readonly string masterName;
        private string? displayNameOverride;
        // rootAttrs (the ordered root-attribute list) + SetRoot/Attribute live on DefinitionBuilderBase (M7).
        private bool stampResourceDefaults = true;
        private bool isEmptyTemplate;
        // review F1: carried from From(existing) and stamped in Build(), so a rebuilt library-export block keeps its
        // explicit two-tag close set instead of silently defaulting to Empty (which would re-emit self-closing pins).
        private ImmutableHashSet<ElementId> explicitCloseIds = ImmutableHashSet<ElementId>.Empty;
        private string emptyIcon = "_0xf";
        private readonly Dictionary<string, string> containerNameOverrides = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> containerNoteOverrides = new(StringComparer.Ordinal);
        private readonly List<ProjectElement> inputs = new();
        private readonly List<ProjectElement> outputs = new();
        private readonly List<ProjectElement> settings = new();
        private readonly List<ProjectElement> internalVars = new();

        private readonly List<FbEnumDefRef> enumDefs = new();
        private readonly List<ProjectElement> rawBodyChildren = new();
        private readonly List<FbProgramBuilder> programs = new();
        private ProjectElement? decodedBody;

        private FunctionBlockDefinitionBuilder(string masterType, string masterVersion, string masterName)
            : base(CatalogGrammarPresets.FunctionBlock)   // effective grammar starts at the FB preset (From/assign replaces)
        {
            this.masterType = masterType;
            this.masterVersion = masterVersion;
            this.masterName = masterName;
        }

        private protected override FunctionBlockDefinitionBuilder Self => this;

        /// <summary>Begins a function block keyed by <paramref name="masterType"/> (e.g. <c>1.1.01</c>),
        /// <paramref name="masterVersion"/> (e.g. <c>e</c>) and <paramref name="masterName"/> (e.g.
        /// <c>Kip tænd sluk</c>, reproduced verbatim incl. any vendor trailing space).</summary>
        public static FunctionBlockDefinitionBuilder Create(string masterType, string masterVersion, string masterName) =>
            new(masterType, masterVersion, masterName);

        /// <summary>
        /// Seeds a builder from an already-authored or catalog-discovered <paramref name="existing"/> block, decoding
        /// its <c>Body</c> (containers + program graph) back into editable builder state — the "open an existing block
        /// type and edit it" entry a GUI library editor needs. The returned builder is independent of
        /// <paramref name="existing"/> (which stays immutable).
        /// </summary>
        public static FunctionBlockDefinitionBuilder From(FunctionBlockDefinition existing)
        {
            ArgumentNullException.ThrowIfNull(existing);
            var builder = new FunctionBlockDefinitionBuilder(existing.MasterType, existing.MasterVersion, existing.MasterName)
            {
                categoryPath = existing.CategoryPath,
                displayNameOverride = existing.DisplayName,
                isEmptyTemplate = existing.IsEmptyTemplate,
                decodedBody = existing.Body,
                ids = new IdAllocator(IdAllocator.MaxCounterPresent(existing.Body)),
            };
            foreach ((string name, string value) in existing.Body.Attrs)
            {
                if (name is "id" or "name" or "master_type" or "master_version" or "master_name")
                {
                    continue;
                }
                builder.rootAttrs.Add((name, value));
            }
            // Carry the grammar (including a lenient-fallback verbatim head and its projection) and the physical
            // encoding verbatim — without them the rebuilt definition would silently lose its on-disk form. Only
            // an explicit .Grammar(...) replaces the carried grammar; .ExtendGrammar(...) starts from it.
            builder.grammar = existing.Grammar;
            builder.sourceEncoding = existing.SourceEncoding;
            builder.explicitCloseIds = existing.ExplicitCloseIds.AsImmutableHashSet();   // review F1: else From(x).Build() drops the two-tag close set
            builder.SeedDocumentation(existing.Documentation);
            return builder;
        }

        // ---- identity / master ----

        /// <summary>Overrides the composed display name (defaults to
        /// <c>"{MasterType}.{MasterVersion}. {MasterName}"</c>, e.g. <c>1.1.01.e. Kip tænd sluk</c>).</summary>
        public FunctionBlockDefinitionBuilder DisplayName(string displayName)
        {
            displayNameOverride = displayName;
            return this;
        }

        /// <summary>Sets the <c>master_programmer</c> attribute.</summary>
        public FunctionBlockDefinitionBuilder MasterProgrammer(string programmer) => SetRoot("master_programmer", programmer);

        /// <summary>Sets <c>master_date_year</c>/<c>_month</c>/<c>_day</c> from <paramref name="date"/>.</summary>
        public FunctionBlockDefinitionBuilder MasterDate(DateOnly date)
        {
            SetRoot("master_date_year", DecToken.Format(date.Year));
            SetRoot("master_date_month", DecToken.Format(date.Month));
            return SetRoot("master_date_day", DecToken.Format(date.Day));
        }

        // Authors from the RAW catalog body: suppresses the per-type resource default stamping (icon + #REQUIRED value
        // initials) so a raw-bodied block reproduces its .ifb byte-for-byte — every attribute is supplied
        // verbatim in file order, and the file's DTD defaults are re-materialized on insert. Catalog authoring calls
        // this; hand authors do not (they need the stamping, having no catalog template).
        internal FunctionBlockDefinitionBuilder SuppressResourceDefaults()
        {
            stampResourceDefaults = false;
            return this;
        }

        /// <summary>Sets whether the block is a vendor/factory master (<c>master_schneider_electric="yes"</c>) — a
        /// manufacturer-shipped block, as opposed to a user-created one.</summary>
        public FunctionBlockDefinitionBuilder VendorMaster(bool vendor = true) =>
            SetRoot("master_schneider_electric", vendor ? "yes" : "no");

        /// <summary>Sets whether the block is locked (<c>locked="yes"</c>).</summary>
        public FunctionBlockDefinitionBuilder Locked(bool locked = true) => SetRoot("locked", locked ? "yes" : "no");

        /// <summary>Sets the block's <c>note</c> attribute — the <b>serialized</b> note carried on the definition root
        /// and written out to the <c>.ifb</c> and any project <c>.vis</c> the block is placed in. Contrast
        /// <see cref="DefinitionBuilderBase{TSelf}.Documentation(string)"/>, which attaches the block's help text as
        /// programmatic-lookup-only metadata that never reaches a file: <c>Note</c> is project data,
        /// <c>Documentation</c> is help.</summary>
        public FunctionBlockDefinitionBuilder Note(string note) => SetRoot("note", note);

        // Attribute(name, value) — the raw root-attribute escape hatch (e.g. the block icon) — lives on DefinitionBuilderBase (M7).

        /// <summary>Authors the empty "Tom blok" scaffold — the five containers plus one empty <c>program_simple</c>
        /// (<c>events</c>+<c>actions</c>) and the vendor icon — and flags
        /// <see cref="FunctionBlockDefinition.IsEmptyTemplate"/>. This is the code peer of <c>Data\fb.def</c>.</summary>
        public FunctionBlockDefinitionBuilder AsEmptyTemplate(string iconToken = "_0xf")
        {
            isEmptyTemplate = true;
            emptyIcon = iconToken;
            return this;
        }

        // ---- container note overrides (only inputs/outputs vary per block; the rest are fixed grammar) ----

        /// <summary>Overrides the <c>inputs</c> container's <c>note</c> (defaults to
        /// <see cref="FbGrammar.InputsNoteDefault"/>). The per-block help text a vendor <c>.ifb</c> gives the inputs
        /// grouping.</summary>
        public FunctionBlockDefinitionBuilder InputsNote(string note) => OverrideNote("inputs", note);

        /// <summary>Overrides the <c>outputs</c> container's <c>note</c> (defaults to
        /// <see cref="FbGrammar.OutputsNoteDefault"/>).</summary>
        public FunctionBlockDefinitionBuilder OutputsNote(string note) => OverrideNote("outputs", note);

        /// <summary>Overrides the <c>settings</c> container's <c>note</c> (defaults to
        /// <see cref="FbGrammar.SettingsNote"/>). Vendor <c>.ifb</c> files use a different note dialect than the
        /// synthetic default, so a code-authored recreation of a stock block sets it explicitly.</summary>
        public FunctionBlockDefinitionBuilder SettingsNote(string note) => OverrideNote("settings", note);

        /// <summary>Overrides the <c>internalsettings</c> container's <c>note</c> (defaults to
        /// <see cref="FbGrammar.InternalNote"/>).</summary>
        public FunctionBlockDefinitionBuilder InternalVariablesNote(string note) => OverrideNote("internalsettings", note);

        /// <summary>Overrides the <c>programs</c> container's <c>note</c> (defaults to
        /// <see cref="FbGrammar.ProgramsNote"/>).</summary>
        public FunctionBlockDefinitionBuilder ProgramsNote(string note) => OverrideNote("programs", note);

        /// <summary>Overrides the <c>inputs</c> container's display <c>name</c> (defaults to
        /// <see cref="FbGrammar.InputsName"/>). Vendor blocks use different labels per language/revision, so a
        /// code-authored recreation of a stock block sets it when it differs.</summary>
        public FunctionBlockDefinitionBuilder InputsName(string name) => OverrideName("inputs", name);

        /// <summary>Overrides the <c>outputs</c> container's display <c>name</c> (defaults to
        /// <see cref="FbGrammar.OutputsName"/>).</summary>
        public FunctionBlockDefinitionBuilder OutputsName(string name) => OverrideName("outputs", name);

        /// <summary>Overrides the <c>settings</c> container's display <c>name</c> (defaults to
        /// <see cref="FbGrammar.SettingsName"/>).</summary>
        public FunctionBlockDefinitionBuilder SettingsName(string name) => OverrideName("settings", name);

        /// <summary>Overrides the <c>internalsettings</c> container's display <c>name</c> (defaults to
        /// <see cref="FbGrammar.InternalName"/>).</summary>
        public FunctionBlockDefinitionBuilder InternalVariablesName(string name) => OverrideName("internalsettings", name);

        /// <summary>Overrides the <c>programs</c> container's display <c>name</c> (defaults to
        /// <see cref="FbGrammar.ProgramsName"/>).</summary>
        public FunctionBlockDefinitionBuilder ProgramsName(string name) => OverrideName("programs", name);

        private FunctionBlockDefinitionBuilder OverrideName(string containerTag, string name)
        {
            containerNameOverrides[containerTag] = name;
            return this;
        }

        private FunctionBlockDefinitionBuilder OverrideNote(string containerTag, string note)
        {
            containerNoteOverrides[containerTag] = note;
            return this;
        }

        private string ContainerName(string containerTag, string fallback) =>
            containerNameOverrides.TryGetValue(containerTag, out string? name) ? name : fallback;

        private string ContainerNote(string containerTag, string fallback) =>
            containerNoteOverrides.TryGetValue(containerTag, out string? note) ? note : fallback;

        // ---- resources → containers (each returns a handle for program wiring) ----

        /// <summary>Adds a <c>resource_input</c> pin under <c>inputs</c>; returns its handle.</summary>
        public FbResourceHandle AddInput(string name) => AddResourceTo(inputs, "inputs", "resource_input", name, null);

        /// <summary>Adds a <c>resource_input</c> pin under <c>inputs</c> and configures it — the tag-free short form
        /// with a configurator, so a default-typed pin can carry its note/icon and its help
        /// <see cref="FbResourceDefBuilder.Documentation"/> without the caller spelling the tag. Returns its handle.</summary>
        public FbResourceHandle AddInput(string name, Action<FbResourceDefBuilder> configure) =>
            AddResourceTo(inputs, "inputs", "resource_input", name, configure);

        /// <summary>Adds an input of an explicit type <paramref name="tag"/> under <c>inputs</c> (value types are legal
        /// there too); <paramref name="configure"/> sets type-specific attributes and the resource's help
        /// <see cref="FbResourceDefBuilder.Documentation"/>. Returns its handle.</summary>
        public FbResourceHandle AddInput(string tag, string name, Action<FbResourceDefBuilder>? configure = null) =>
            AddResourceTo(inputs, "inputs", tag, name, configure);

        /// <summary>Adds a <c>resource_output</c> pin under <c>outputs</c>; returns its handle.</summary>
        public FbResourceHandle AddOutput(string name) => AddResourceTo(outputs, "outputs", "resource_output", name, null);

        /// <summary>Adds a <c>resource_output</c> pin under <c>outputs</c> and configures it — the tag-free short form
        /// with a configurator (see <see cref="AddInput(string,Action{FbResourceDefBuilder})"/>). Returns its handle.</summary>
        public FbResourceHandle AddOutput(string name, Action<FbResourceDefBuilder> configure) =>
            AddResourceTo(outputs, "outputs", "resource_output", name, configure);

        /// <summary>Adds an output of an explicit type <paramref name="tag"/> under <c>outputs</c>;
        /// <paramref name="configure"/> sets type-specific attributes and the resource's help
        /// <see cref="FbResourceDefBuilder.Documentation"/>. Returns its handle.</summary>
        public FbResourceHandle AddOutput(string tag, string name, Action<FbResourceDefBuilder>? configure = null) =>
            AddResourceTo(outputs, "outputs", tag, name, configure);

        /// <summary>Adds a value variable of type <paramref name="tag"/> (e.g. <c>resource_timer</c>,
        /// <c>resource_enum</c>) under <c>settings</c>; <paramref name="configure"/> sets its value and its help
        /// <see cref="FbResourceDefBuilder.Documentation"/>. Returns its handle.</summary>
        public FbResourceHandle AddSetting(string tag, string name, Action<FbResourceDefBuilder>? configure = null) =>
            AddResourceTo(settings, "settings", tag, name, configure);

        /// <summary>Adds a private value variable of type <paramref name="tag"/> under <c>internalsettings</c>;
        /// <paramref name="configure"/> sets its value and its help
        /// <see cref="FbResourceDefBuilder.Documentation"/>. Returns its handle.</summary>
        public FbResourceHandle AddInternalVariable(string tag, string name, Action<FbResourceDefBuilder>? configure = null) =>
            AddResourceTo(internalVars, "internalsettings", tag, name, configure);

        /// <summary>
        /// Authors an <c>enum_definition</c> embedded in the block body and returns a typed handle to it — the
        /// definition-layer peer of <see cref="Ihc.Vis.Editing.EnumDefinitionRef"/>. Add its values with
        /// <c>AddValue</c>, then wire it by handle + human value name through the typed <c>Enum</c> / <c>AddEnumOperand</c>
        /// overloads, so a GUI works in value names ("Nat"/"Dag") rather than opaque typedef/inivalue tokens.
        /// </summary>
        public FbEnumDefRef AddEnumDefinition(string name) => AddEnumDefinition(name, null);

        /// <summary>Authors an <c>enum_definition</c> carrying the opaque <paramref name="typeid"/> token a built-in
        /// enumerator embedded into a stock block keeps (e.g. the shared "Persienne tilstand" type <c>_0x10</c>);
        /// pass <c>null</c> for a block-local enum with no shared type identity.</summary>
        public FbEnumDefRef AddEnumDefinition(string name, string? typeid)
        {
            var reference = new FbEnumDefRef(ids, ids.Allocate(TypeCode.RequireForTag("enum_definition")), name, typeid);
            enumDefs.Add(reference);
            return reference;
        }

        /// <summary>Opens a <c>program_simple</c> for authoring under <c>programs</c>, named
        /// <paramref name="name"/>. Callable more than once — each call appends another program (a block may carry
        /// several).</summary>
        public FbProgramBuilder Program(string name = "Program")
        {
            var program = new FbProgramBuilder(ids, name);
            programs.Add(program);
            return program;
        }

        // ---- documentation (help metadata; programmatic-lookup only, never serialized) ----
        // The block-level Documentation(string) summary lives on the shared DefinitionBuilderBase. Per-resource help
        // has one door and one door only: FbResourceDefBuilder.Documentation inside the
        // AddInput/AddOutput/AddSetting/AddInternalVariable configurator, which needs neither a name key nor a handle
        // variable. The retired by-handle and name-keyed overloads were second doors onto the same map.

        // ---- escape hatch ----

        /// <summary>Splices an arbitrary pre-built subtree at the function-block <c>Body</c> root, for elements that
        /// live directly under <c>functionblock</c> rather than in a resource container: an embedded
        /// <c>enum_definition</c>, or an additional <c>program_simple</c> under <c>programs</c>. Returns this for
        /// chaining.</summary>
        public FunctionBlockDefinitionBuilder RawChild(ProjectElement child)
        {
            ArgumentNullException.ThrowIfNull(child);
            rawBodyChildren.Add(ids.MintMissingIds(child));
            return this;
        }

        // Grammar(CatalogGrammar) and ExtendGrammar(Action<CatalogGrammarBuilder>) live on DefinitionBuilderBase.

        /// <summary>
        /// Checks the builder's current state against the locally-decidable authoring preconditions (identity present,
        /// wired enum operands have a definition, container/type legality, ...) <b>without</b> building, returning the
        /// structured <see cref="ProjectValidationResult"/> a GUI filters and navigates by — the non-throwing path for
        /// live field-level validation as the user edits, phrased in authoring-call terms rather than placeholder ids.
        /// </summary>
        public ProjectValidationResult Validate()
        {
            var findings = CollectErrors();
            foreach (FbProgramBuilder program in programs)
            {
                if (!program.HasEvents)
                {
                    findings.Add(new ProjectValidationFinding(ValidationSeverity.Warning, "program-empty", null,
                        "Program uden hændelser")
                    {
                        Diagnostic = "A program has no events, so it will never run.",
                    });
                }
            }
            // The grammar↔body advisories (non-blocking warnings; skipped for an Empty grammar) — over the decoded
            // body when editing an existing block, else a light preview (root + containers with the added resources
            // + raw children) that leaves the id allocator untouched. Program-graph internals are omitted from the
            // preview: their node types are covered by the block preset by construction, and materializing them
            // here would burn allocator ids Build() needs.
            findings.AddRange(CatalogGrammarAdvisor.Advise(
                decodedBody is { } decoded ? SpliceAuthoredOnto(decoded, forBuild: false) : AdvisoryPreviewBody(), grammar));
            return ProjectValidationResult.FromFindings(findings.ToImmutable());
        }

        // The blocking (error-severity) preconditions alone — the gate Build() checks without paying for the
        // advisory body walk it would discard (advisories and the program-empty findings are warnings and never
        // block a build).
        private ImmutableArray<ProjectValidationFinding>.Builder CollectErrors()
        {
            var findings = ImmutableArray.CreateBuilder<ProjectValidationFinding>();
            if (!isEmptyTemplate && string.IsNullOrEmpty(masterName))
            {
                findings.Add(new ProjectValidationFinding(ValidationSeverity.Error, "block-identity-missing", null,
                    "Mangler blokidentitet")
                {
                    Diagnostic =
                        "The block needs a master_name (or AsEmptyTemplate for a Tom blok). "
                        + "master_type/master_version are optional — many stock blocks carry no version, and a "
                        + "keyless user block carries no type (it is then addressable only by name).",
                });
            }
            return findings;
        }

        private ProjectElement AdvisoryPreviewBody()
        {
            var previewChildren = new List<ProjectElement>();
            void Container(string tag, List<ProjectElement> resources) =>
                previewChildren.Add(ProjectElement.Create(tag, id: null,
                    Array.Empty<(string, string)>(), resources));
            Container("inputs", inputs);
            Container("outputs", outputs);
            Container("settings", settings);
            Container("internalsettings", internalVars);
            previewChildren.AddRange(rawBodyChildren);
            return ProjectElement.Create("functionblock", id: null,
                new[] { ("name", ComposedDisplayName), ("master_type", masterType),
                        ("master_version", masterVersion), ("master_name", masterName) },
                previewChildren);
        }

        /// <summary>Materializes the deep <c>Body</c> (five containers in fixed order + program graph, placeholder ids)
        /// and returns the finished <see cref="FunctionBlockDefinition"/>. Throws <see cref="ProjectValidationException"/>
        /// when <see cref="Validate"/> would report an error — call <see cref="Validate"/> first for non-throwing UI feedback.</summary>
        public FunctionBlockDefinition Build()
        {
            if (CollectErrors().Count > 0)
            {
                throw new ProjectValidationException(new ProblemCode("import.definition-invalid"), Validate());   // full result, advisories included
            }

            // The materialized body is deliberately left un-canonicalized (raw placeholder ids + effective
            // attributes), exactly as CatalogReader.Read yields an .ifb's parsed tree: the insert transform
            // canonicalizes against the project on insert, and the oracle component tests canonicalize against the
            // block's own grammar.
            // Materialize ONCE and memoize: the placeholder-id allocations (5 containers, the program graph, enum
            // values, the functionblock root) advance the single shared IdAllocator every call, so re-materializing on
            // a second Build() would drift every allocated id. Matching ProductDefinitionBuilder's allocate-once
            // idempotence, the id-bearing Body is built on the first call and reused, so Build()→Build() is byte-
            // identical; the wrapper (Grammar/Documentation/encoding) is still re-read each call.
            if (builtBody is null)
            {
                ProjectElement body = decodedBody is not null ? MaterializeDecoded()
                    : isEmptyTemplate ? MaterializeEmptyTemplate()
                    : MaterializeBody();

                // A raw-bodied block reproduces its .ifb, which never writes an empty note="" (unlike a
                // product .def) — it rides the note CDATA "" default. The structural/program builders stamp a default
                // (often empty) note on containers and program-graph nodes; strip those empty notes so the body matches
                // the file byte-for-byte (the insert transform re-derives them from the block's DTD when needed).
                if (!stampResourceDefaults)
                {
                    body = DropEmptyDefaultAttrs(body);
                }
                builtBody = body;
            }

            var definition = new FunctionBlockDefinition(masterType, masterVersion, masterName, ComposedDisplayName, categoryPath, builtBody)
            {
                Grammar = grammar,
                IsEmptyTemplate = isEmptyTemplate,
                Documentation = BuildDocumentation(),
                ExplicitCloseIds = explicitCloseIds,   // review F1: carry the From-seeded close set through the rebuild
            };
            // Stamp the From-carried physical SourceEncoding when one was carried, else keep the definition's default.
            return sourceEncoding is { } encoding ? definition with { SourceEncoding = encoding } : definition;
        }

        private string ComposedDisplayName =>
            displayNameOverride ?? FbGrammar.ComposeDisplayName(masterType, masterVersion, masterName);

        // Recursively removes empty note="" and name="" attributes: an .ifb rides those CDATA "" DTD defaults rather
        // than writing them (verified: no vendor .ifb writes note="" or name="", unlike a product .def). Applied only
        // for raw-bodied blocks, where the raw file body is the fidelity target; the structural/program
        // builders stamp a default (often empty) note/name that this strips so the body matches the file.
        private static ProjectElement DropEmptyDefaultAttrs(ProjectElement element)
        {
            var keptAttrs = ImmutableArray.CreateBuilder<(string, string)>();
            foreach ((string name, string value) in element.Attrs)
            {
                if (!(value.Length == 0 && name is "note" or "name"))
                {
                    keptAttrs.Add((name, value));
                }
            }
            var children = ImmutableArray.CreateBuilder<ProjectElement>();
            foreach (ProjectElement child in element.Children)
            {
                children.Add(DropEmptyDefaultAttrs(child));
            }
            return new ProjectElement(element.Tag, element.Id, keptAttrs.ToImmutable(), children.ToImmutable());
        }

        /// <summary>
        /// The block's five fixed containers, in the fixed order both materialization paths emit them — which is also
        /// the order their ids burn, so the shape stays here rather than being spelled out per path. Names and notes
        /// route through the same <c>ContainerName</c>/<c>ContainerNote</c> override lookup either way, so a per-block
        /// override is honoured uniformly (the empty template once ignored the name overrides entirely). The paths
        /// differ only in what each container holds.
        /// <para>
        /// <paramref name="programRows"/> is forced HERE, in the programs container's own argument list, and must
        /// therefore be passed un-materialized: a program's nodes burn their ids at materialization, and the oracles
        /// pin them to fall between the internalsettings container and the programs container. Materializing at the
        /// call site instead moves every container id.
        /// </para>
        /// </summary>
        private ProjectElement[] BuildContainers(
            IEnumerable<ProjectElement> inputRows, IEnumerable<ProjectElement> outputRows,
            IEnumerable<ProjectElement> settingRows, IEnumerable<ProjectElement> internalRows,
            IEnumerable<ProjectElement> programRows) =>
        [
            FbGrammar.Container(ids, "inputs", ContainerName("inputs", FbGrammar.InputsName), FbGrammar.InputsIcon,
                ContainerNote("inputs", FbGrammar.InputsNoteDefault), inputRows),
            FbGrammar.Container(ids, "outputs", ContainerName("outputs", FbGrammar.OutputsName), FbGrammar.OutputsIcon,
                ContainerNote("outputs", FbGrammar.OutputsNoteDefault), outputRows),
            FbGrammar.Container(ids, "settings", ContainerName("settings", FbGrammar.SettingsName), FbGrammar.SettingsIcon,
                ContainerNote("settings", FbGrammar.SettingsNote), settingRows),
            FbGrammar.Container(ids, "internalsettings", ContainerName("internalsettings", FbGrammar.InternalName), FbGrammar.InternalIcon,
                ContainerNote("internalsettings", FbGrammar.InternalNote), internalRows),
            FbGrammar.Container(ids, "programs", ContainerName("programs", FbGrammar.ProgramsName), FbGrammar.ProgramsIcon,
                ContainerNote("programs", FbGrammar.ProgramsNote), programRows.ToArray()),
        ];

        private ProjectElement MaterializeBody()
        {
            var bodyChildren = new List<ProjectElement>();
            bodyChildren.AddRange(enumDefs.Select(e => e.Materialize()));
            bodyChildren.AddRange(rawBodyChildren);
            bodyChildren.AddRange(BuildContainers(inputs, outputs, settings, internalVars,
                programs.Select(p => p.Materialize())));   // deferred on purpose — see BuildContainers

            ProjectElement root = FbGrammar.Node("functionblock",
                ids.Allocate(TypeCode.RequireForTag("functionblock")), NoAttrs, bodyChildren);
            return ApplyIdentityAndRootAttrs(root);
        }

        private ProjectElement MaterializeEmptyTemplate()
        {
            ProjectElement events = FbGrammar.Container(ids, "events", FbGrammar.EventsName, FbGrammar.EventsIcon,
                FbGrammar.EventsNote, NoChildren);
            ProjectElement actions = FbGrammar.Node("actions", ids.Allocate(TypeCode.RequireForTag("actions")),
                new[]
                {
                    ("name", FbGrammar.RootActionsName), ("icon", FbGrammar.ActionsIcon),
                    ("note", FbGrammar.RootActionsEmptyNote), ("type", FbGrammar.RootActionsType),
                }, NoChildren);
            ProjectElement programSimple = FbGrammar.Node("program_simple",
                ids.Allocate(TypeCode.RequireForTag("program_simple")),
                new[] { ("name", "Program"), ("icon", FbGrammar.ProgramSimpleIcon) },
                new[] { events, actions });

            ProjectElement[] bodyChildren =
                BuildContainers(NoChildren, NoChildren, NoChildren, NoChildren, new[] { programSimple });
            ProjectElement root = FbGrammar.Node("functionblock",
                ids.Allocate(TypeCode.RequireForTag("functionblock")),
                new[] { ("name", ComposedDisplayName), ("icon", emptyIcon) }, bodyChildren);
            // Honor any authored root attributes (Note/Locked/Attribute) on top of the fixed name+icon scaffold, for
            // parity with the normal path. The empty template deliberately omits the master_* identity (the vendor
            // fb.def scaffold carries none), so it does NOT run the identity-stamping ApplyIdentityAndRootAttrs; but a
            // per-block root attribute is applied (WithAttribute replaces in place / appends), rather than dropped.
            foreach ((string name, string value) in rootAttrs)
            {
                root = root.WithAttribute(name, value);
            }
            return root;
        }

        // Re-emits a body decoded via From(): the preserved children plus any post-From() authored edits spliced onto
        // the matching containers (ProductDefinitionBuilder.From keeps such edits too), with the identity/root-attribute
        // edits re-applied on top. With no edits every list is empty and the decoded body is returned verbatim, so a
        // From(x).Build() round-trip stays byte-identical.
        private ProjectElement MaterializeDecoded() => ApplyIdentityAndRootAttrs(SpliceAuthoredOnto(decodedBody!, forBuild: true));

        // Appends the authored working-list children to the decoded body's matching containers: inputs/outputs/
        // settings/internalsettings (already-materialized resources — no new id burn) plus, on the Build path only,
        // the materialized program graph; authored top-level enum stubs and raw children go first, mirroring
        // MaterializeBody's order. The advisory path (forBuild=false) omits the program graph (materializing it would
        // burn allocator ids Build() needs) and never throws — it is the non-throwing Validate() preview.
        private ProjectElement SpliceAuthoredOnto(ProjectElement body, bool forBuild)
        {
            var appended = new Dictionary<string, IReadOnlyList<ProjectElement>>(StringComparer.Ordinal)
            {
                [ResourceContainerTags[0]] = inputs,
                [ResourceContainerTags[1]] = outputs,
                [ResourceContainerTags[2]] = settings,
                [ResourceContainerTags[3]] = internalVars,
            };
            if (forBuild)
            {
                appended["programs"] = programs.Select(p => p.Materialize()).ToList();
            }
            bool hasEdits = enumDefs.Count > 0 || rawBodyChildren.Count > 0 || appended.Values.Any(v => v.Count > 0);
            if (!hasEdits)
            {
                return body;
            }
            if (forBuild)
            {
                foreach ((string tag, IReadOnlyList<ProjectElement> extra) in appended)
                {
                    if (extra.Count > 0 && body.FindChild(tag) is null)
                    {
                        throw new InvalidOperationException(
                            $"From()-seeded block has no <{tag}> container to receive the authored children; the " +
                            "decoded body is missing that standard function-block container.");
                    }
                }
            }
            var newChildren = new List<ProjectElement>();
            newChildren.AddRange(enumDefs.Select(e => e.Materialize()));
            newChildren.AddRange(rawBodyChildren);
            foreach (ProjectElement child in body.Children)
            {
                if (appended.TryGetValue(child.Tag, out IReadOnlyList<ProjectElement>? extra) && extra.Count > 0)
                {
                    newChildren.Add(child with { Children = child.Children.Concat(extra).ToImmutableArray() });
                }
                else
                {
                    newChildren.Add(child);
                }
            }
            return body with { Children = newChildren.ToImmutableArray() };
        }

        // The functionblock root's attribute order is a fixed vendor sequence (verified constant across the catalog);
        // authored components emit it in that canonical order regardless of which setter set which attribute, so the
        // saved bytes match the vendor file. Attributes not in the sequence keep their first-set order at the end.
        private static readonly string[] CanonicalRootOrder =
        {
            "name", "master_schneider_electric", "master_type", "master_version", "master_name", "master_programmer",
            "master_date_year", "master_date_month", "master_date_day", "locked", "icon", "note", "helpid",
        };

        // Re-applies the master identity and the accumulated root attributes onto a freshly-built or decoded root —
        // the tail MaterializeBody and MaterializeDecoded share.
        private ProjectElement ApplyIdentityAndRootAttrs(ProjectElement root)
        {
            // Raw-bodied (SuppressResourceDefaults): EVERY root attribute was already added —
            // name, master_*, locked, icon, note — to rootAttrs in the file's own order (which is not constant across
            // the corpus, e.g. 1.2.07 writes master_name early), so emit them verbatim. Hand-authored blocks fall
            // through to the canonical vendor ordering below.
            if (!stampResourceDefaults)
            {
                foreach ((string name, string value) in rootAttrs)
                {
                    root = root.WithAttribute(name, value);
                }
                return root;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var firstSeen = new List<string>();
            void Set(string name, string value)
            {
                if (!values.ContainsKey(name))
                {
                    firstSeen.Add(name);
                }
                values[name] = value;
            }

            Set("name", ComposedDisplayName);
            // The master identity attributes are written only when non-empty — a user-saved block (AutoProof) has no
            // master_type/version; a version-less block omits master_version.
            if (masterType.Length > 0) { Set("master_type", masterType); }
            if (masterVersion.Length > 0) { Set("master_version", masterVersion); }
            if (masterName.Length > 0) { Set("master_name", masterName); }
            foreach ((string name, string value) in rootAttrs)
            {
                Set(name, value);
            }

            var emitted = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in CanonicalRootOrder)
            {
                if (values.TryGetValue(name, out string? value))
                {
                    root = root.WithAttribute(name, value);
                    emitted.Add(name);
                }
            }
            foreach (string name in firstSeen)
            {
                if (emitted.Add(name))
                {
                    root = root.WithAttribute(name, values[name]);
                }
            }
            return root;
        }

        // The four standard resource containers, in the order the vendor body declares them. Shared by the From()
        // offset seeding and SpliceAuthoredOnto so "which containers hold resources" is answered once.
        // Derived from the shared FB-grammar descriptor rather than re-listed, so the four container tags have one home.
        internal static readonly string[] ResourceContainerTags = [.. FunctionBlockSections.All.Select(s => s.Container)];

        private FbResourceHandle AddResourceTo(List<ProjectElement> container, string containerTag, string tag, string name,
            Action<FbResourceDefBuilder>? configure)
        {
            var configurator = new FbResourceDefBuilder();
            configure?.Invoke(configurator);
            ElementId id = ids.Allocate(TypeCode.RequireForTag(tag));
            ProjectElement resource = FbGrammar.Node(tag, id, new[] { ("name", name) }, NoChildren);
            // Hand-authored resources get the vendor's per-type presentation/value defaults (icon + #REQUIRED value
            // initials) stamped, since there is no catalog template to supply them. A raw-bodied block
            // (SuppressResourceDefaults) is authored from the RAW .ifb body instead — every attribute already arrives
            // verbatim in file order, so stamping would inject them at the wrong position.
            if (stampResourceDefaults)
            {
                foreach ((string attrName, string attrValue) in ResourceMaterialization.NewResourceDefaults(tag))
                {
                    resource = resource.WithAttribute(attrName, attrValue);
                }
            }
            foreach ((string attrName, string attrValue) in configurator.Attributes)
            {
                resource = resource.WithAttribute(attrName, attrValue);
            }
            // The one place that has both the resource and the text authored on it: route documentation to the
            // definition-level map, keyed by the position this very call is appending to — never to an attribute.
            // The offset makes that position the one in the BUILT container, not just in the authored tail.
            if (configurator.DocumentationText is { } documentation)
            {
                // The authored tail is spliced AFTER whatever a From()-seeded body already holds, so the position in
                // the BUILT container — and hence the ResourceDocKey — is offset by that count. Read straight off
                // `decodedBody` (assigned once, in From); a cached count could only ever restate it.
                int position = (decodedBody?.FindChild(containerTag)?.Children.Length ?? 0) + container.Count;
                SetResourceDoc(ResourceDocKey.ForBlock(containerTag, position), documentation);
            }
            container.Add(resource);
            return new FbResourceHandle(name, id);
        }

        // SetRoot(name, value) lives on DefinitionBuilderBase (M7) — the shared ordered-append seam.

    }

    /// <summary>
    /// A definition-local handle to a resource authored into a function-block definition: its display name and the
    /// placeholder id used to wire <c>link1</c>/<c>link2</c>/<c>scene_resource</c>. Deliberately not
    /// <see cref="Ihc.Vis.Editing.ResourceRef"/> — see the remarks on <see cref="FunctionBlockDefinitionBuilder"/>.
    /// </summary>
    public sealed class FbResourceHandle
    {
        internal FbResourceHandle(string name, ElementId placeholderId)
        {
            Name = name;
            PlaceholderId = placeholderId;
        }

        /// <summary>The resource's display name.</summary>
        public string Name { get; }

        /// <summary>The body-local placeholder id the insert transform re-mints and remaps IDREFs through.</summary>
        internal ElementId PlaceholderId { get; }
    }

    /// <summary>
    /// A definition-local handle to an <c>enum_definition</c> authored via
    /// <see cref="FunctionBlockDefinitionBuilder.AddEnumDefinition(string)"/> — the definition-layer peer of
    /// <see cref="Ihc.Vis.Editing.EnumDefinitionRef"/>. Carries the enum's placeholder typedef token and resolves a
    /// human value name to its <c>inivalue</c> token, so a GUI wires enum operands by name, not by opaque token.
    /// Values are added fluently with <see cref="AddValue(string)"/> rather than upfront as the Editing peer takes them
    /// (<c>ProjectEditor.AddEnumDefinition(name, values)</c> allocates real value-ids in declaration order at that
    /// moment): definition-layer value-ids are throwaway placeholders the insert transform re-mints, so there is
    /// nothing to allocate atomically and the incremental form reads better for GUI authoring.
    /// </summary>
    public sealed class FbEnumDefRef
    {
        private readonly IdAllocator ids;
        private readonly ElementId defId;
        private readonly string name;
        private readonly string? typeid;
        private readonly List<(string Name, ElementId Id, int Index, string? Typeid)> values = new();

        internal FbEnumDefRef(IdAllocator ids, ElementId defId, string name, string? typeid = null)
        {
            this.ids = ids;
            this.defId = defId;
            this.name = name;
            this.typeid = typeid;
        }

        /// <summary>The enum's placeholder <c>typedef</c> token (remapped on insert), for raw interop.</summary>
        public string Typedef => defId.ToToken();

        /// <summary>Adds an <c>enum_value</c> whose <c>index</c> is its declaration order (0-based); returns this for
        /// chaining.</summary>
        public FbEnumDefRef AddValue(string valueName) => AddValue(valueName, values.Count);

        /// <summary>Adds an <c>enum_value</c> with an explicit <paramref name="index"/> (for enums whose value order
        /// differs from their index order); returns this for chaining.</summary>
        public FbEnumDefRef AddValue(string valueName, int index) => AddValue(valueName, index, null);

        /// <summary>Adds an <c>enum_value</c> with an explicit <paramref name="index"/> and the opaque per-value
        /// <paramref name="typeid"/> token a built-in enumerator's values carry (else <c>null</c>); returns this.</summary>
        public FbEnumDefRef AddValue(string valueName, int index, string? typeid)
        {
            values.Add((valueName, ids.Allocate(TypeCode.RequireForTag("enum_value")), index, typeid));
            return this;
        }

        /// <summary>The <c>inivalue</c> token for a previously-added value name, for raw interop.</summary>
        public string InitialValue(string valueName)
        {
            foreach ((string Name, ElementId Id, int Index, string? Typeid) value in values)
            {
                if (value.Name == valueName)
                {
                    return value.Id.ToToken();
                }
            }
            throw new ArgumentException($"Enum '{name}' has no value named '{valueName}'.", nameof(valueName));
        }

        internal ProjectElement Materialize()
        {
            var defAttrs = new List<(string, string)> { ("name", name) };
            if (typeid is not null)
            {
                defAttrs.Insert(0, ("typeid", typeid));
            }
            return FbGrammar.Node("enum_definition", defId, defAttrs,
                values.Select(v => FbGrammar.Leaf("enum_value", v.Id, ValueAttrs(v))));
        }

        private static IEnumerable<(string, string)> ValueAttrs((string Name, ElementId Id, int Index, string? Typeid) value)
        {
            var attrs = new List<(string, string)>();
            if (value.Typeid is not null)
            {
                attrs.Add(("typeid", value.Typeid));
            }
            attrs.Add(("name", value.Name));
            // index is a DTD default of 0 — the vendor omits it for the first (index-0) value and writes it only when
            // non-zero; matching that keeps the raw catalog body byte-faithful (and a hand-authored 0 is dropped on
            // insert either way).
            if (value.Index != 0)
            {
                attrs.Add(("index", DecToken.Format(value.Index)));
            }
            return attrs;
        }
    }

    /// <summary>
    /// Fluent configurator for a function-block resource of any value/pin type — the definition-layer peer of
    /// <see cref="Ihc.Vis.Editing.SettingBuilder"/>, with a typed core plus a raw <see cref="Attribute"/> escape hatch
    /// (the ~18-type resource palette is too heterogeneous for per-type builders).
    /// </summary>
    public sealed class FbResourceDefBuilder
    {
        private readonly List<(string Name, string Value)> attrs = new();
        // Deliberately NOT in attrs: documentation is programmatic-lookup-only help metadata, harvested by
        // AddResourceTo into the definition's DefinitionDocumentation — never an attribute of the serialized element.
        private string? documentation;

        internal FbResourceDefBuilder()
        {
        }

        internal IReadOnlyList<(string Name, string Value)> Attributes => attrs;

        // The help text authored on this resource, or null when none — the second thing AddResourceTo reads back.
        internal string? DocumentationText => documentation;

        /// <summary>Sets the resource's <c>note</c> attribute — the <b>serialized</b> installer-facing text: it is
        /// written into the body and out to the <c>.ifb</c> and any project <c>.vis</c> the block is placed in, and
        /// the GUI shows it in the pin's properties dialog. Contrast <see cref="Documentation"/>, which attaches help
        /// text <i>about</i> the resource as programmatic-lookup-only metadata that never reaches a file. Rule of
        /// thumb: <c>Note</c> is project data, <c>Documentation</c> is help.</summary>
        public FbResourceDefBuilder Note(string note) => Set("note", note);

        /// <summary>Attaches this resource's documentation text — <b>programmatic-lookup-only</b> help metadata that
        /// is read back off the pin itself, on <see cref="ResourceSummary.Documentation"/> of the built definition's
        /// <see cref="FunctionBlockDefinition.Inputs"/>/<see cref="FunctionBlockDefinition.Outputs"/>/
        /// <see cref="FunctionBlockDefinition.Settings"/>/<see cref="FunctionBlockDefinition.InternalVariables"/>
        /// projections, and is never serialized into the body or an <c>.ifb</c>. This is the <b>only</b> door for
        /// per-resource help on a block: authored here the text belongs to <i>this</i> pin, so a sibling sharing its
        /// display name (block 1.4.03 has both a <c>"Sluk"</c> input and a <c>"Sluk"</c> output) documents itself
        /// independently. Contrast <see cref="Note"/>, which sets the resource's serialized <c>note</c> attribute:
        /// <c>Note</c> is project data, <c>Documentation</c> is help.</summary>
        public FbResourceDefBuilder Documentation(string documentation)
        {
            this.documentation = documentation;
            return this;
        }

        /// <summary>Marks the resource value as backed-up (<c>backup="yes"</c>).</summary>
        public FbResourceDefBuilder Backup(bool backup = true) => Set("backup", backup ? "yes" : "no");

        /// <summary>Overrides the GUI icon token.</summary>
        public FbResourceDefBuilder Icon(string iconToken) => Set("icon", iconToken);

        /// <summary>Sets the raw initial value (<c>inivalue</c>) — the general escape hatch for scalar settings.</summary>
        public FbResourceDefBuilder Inivalue(string value) => Set("inivalue", value);

        /// <summary>For a <c>resource_enum</c>: wires the enum by a typed <see cref="FbEnumDefRef"/> handle and a human
        /// value name (tokens resolved internally) — the GUI-friendly form.</summary>
        public FbResourceDefBuilder Enum(FbEnumDefRef definition, string valueName)
        {
            ArgumentNullException.ThrowIfNull(definition);
            Set("typedef", definition.Typedef);
            return Set("inivalue", definition.InitialValue(valueName));
        }

        /// <summary>For a <c>resource_enum</c>: sets the enum-definition/value IDREF tokens directly (the raw escape
        /// hatch when referencing a pre-existing/catalog enum; remapped on insert when the enum is embedded in the body).</summary>
        public FbResourceDefBuilder Enum(string typedefToken, string inivalueToken)
        {
            Set("typedef", typedefToken);
            return Set("inivalue", inivalueToken);
        }

        /// <summary>Sets a timer resource's <c>hour</c>/<c>minute</c>/<c>second</c>(/<c>millisecond</c>) value.</summary>
        public FbResourceDefBuilder TimerHms(int hour, int minute, int second, int millisecond = 0)
        {
            Set("hour", DecToken.Format(hour));
            Set("minute", DecToken.Format(minute));
            Set("second", DecToken.Format(second));
            return Set("millisecond", DecToken.Format(millisecond));
        }

        /// <summary>Sets a date resource's <c>year</c>/<c>month</c>/<c>day</c> value.</summary>
        public FbResourceDefBuilder DateYmd(int year, int month, int day)
        {
            Set("year", DecToken.Format(year));
            Set("month", DecToken.Format(month));
            return Set("day", DecToken.Format(day));
        }

        /// <summary>Bakes a raw attribute verbatim (escape hatch for type-specific attributes).</summary>
        public FbResourceDefBuilder Attribute(string name, string value) => Set(name, value);

        private FbResourceDefBuilder Set(string name, string value)
        {
            attrs.Add((name, value));
            return this;
        }

    }
}
