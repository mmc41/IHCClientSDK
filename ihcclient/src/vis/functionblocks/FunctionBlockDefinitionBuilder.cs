#nullable enable
using System;

using Ihc.Vis.Model;
using Ihc.Vis.Validation;
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
    /// icon vocabulary from the catalog seam (<see cref="Ihc.Vis.Catalog.ICatalog"/>; the SDK-embedded
    /// <c>BuiltInCatalog</c> is the token source once it lands) and binds pickers to it, the same way resource operands
    /// are already picked by <see cref="FbResourceHandle"/> object rather than by id token.</para>
    /// <para><b>Layering:</b> a pure, dependency-free authoring <i>primitive</i>. A GUI backend hands the
    /// <see cref="Build()"/> output to the app-service insert door (<c>project.Edit().Group(..).AddFunctionBlock(def)</c>
    /// via <see cref="Ihc.Vis.ProjectAppService"/>), which owns telemetry, IO and the single project-mutation entry
    /// point — the builder deliberately owns none of that.</para>
    /// <para>Stage-1 design preview: every member throws <see cref="NotImplementedException"/>; the implementation
    /// lands in a later session. The signatures exist so the authoring surface can be reviewed and shown to compile.</para>
    /// </remarks>
    public sealed class FunctionBlockDefinitionBuilder
    {
        private FunctionBlockDefinitionBuilder(string masterType, string masterVersion, string masterName) =>
            throw new NotImplementedException();

        /// <summary>Begins a function block keyed by <paramref name="masterType"/> (e.g. <c>1.1.01</c>),
        /// <paramref name="masterVersion"/> (e.g. <c>e</c>) and <paramref name="masterName"/> (e.g.
        /// <c>Kip tænd sluk</c>, reproduced verbatim incl. any vendor trailing space).</summary>
        public static FunctionBlockDefinitionBuilder Create(string masterType, string masterVersion, string masterName) =>
            throw new NotImplementedException();

        /// <summary>
        /// Seeds a builder from an already-authored or catalog-discovered <paramref name="existing"/> block, decoding
        /// its <c>Body</c> (containers + program graph) back into editable builder state — the "open an existing block
        /// type and edit it" entry a GUI library editor needs. The returned builder is independent of
        /// <paramref name="existing"/> (which stays immutable).
        /// </summary>
        public static FunctionBlockDefinitionBuilder From(FunctionBlockDefinition existing) =>
            throw new NotImplementedException();

        // ---- identity / master ----

        /// <summary>Overrides the composed display name (defaults to
        /// <c>"{MasterType}.{MasterVersion}. {MasterName}"</c>, e.g. <c>1.1.01.e. Kip tænd sluk</c>).</summary>
        public FunctionBlockDefinitionBuilder DisplayName(string displayName) => throw new NotImplementedException();

        /// <summary>Sets the library category path the block is filed under.</summary>
        public FunctionBlockDefinitionBuilder CategoryPath(string categoryPath) => throw new NotImplementedException();

        /// <summary>Sets the <c>master_programmer</c> attribute.</summary>
        public FunctionBlockDefinitionBuilder MasterProgrammer(string programmer) => throw new NotImplementedException();

        /// <summary>Sets <c>master_date_year</c>/<c>_month</c>/<c>_day</c> from <paramref name="date"/>.</summary>
        public FunctionBlockDefinitionBuilder MasterDate(DateOnly date) => throw new NotImplementedException();

        /// <summary>Sets whether the block is a vendor/factory master — a manufacturer-shipped block, as opposed to a
        /// user-created one.</summary>
        public FunctionBlockDefinitionBuilder VendorMaster(bool vendor = true) => throw new NotImplementedException();

        /// <summary>Sets whether the block is locked (<c>locked="yes"</c>).</summary>
        public FunctionBlockDefinitionBuilder Locked(bool locked = true) => throw new NotImplementedException();

        /// <summary>Sets the block note.</summary>
        public FunctionBlockDefinitionBuilder Note(string note) => throw new NotImplementedException();

        /// <summary>Bakes a raw block-level attribute verbatim (escape hatch).</summary>
        public FunctionBlockDefinitionBuilder Attribute(string name, string value) => throw new NotImplementedException();

        /// <summary>Authors the empty "Tom blok" scaffold — the five containers plus one empty <c>program_simple</c>
        /// (<c>events</c>+<c>actions</c>) and the vendor icon — and flags
        /// <see cref="FunctionBlockDefinition.IsEmptyTemplate"/>. This is the code peer of <c>Data\fb.def</c>.</summary>
        public FunctionBlockDefinitionBuilder AsEmptyTemplate(string iconToken = "_0xf") => throw new NotImplementedException();

        // ---- resources → containers (each returns a handle for program wiring) ----

        /// <summary>Adds a <c>resource_input</c> pin under <c>inputs</c>; returns its handle.</summary>
        public FbResourceHandle AddInput(string name) => throw new NotImplementedException();

        /// <summary>Adds an input of an explicit type <paramref name="tag"/> under <c>inputs</c> (value types are legal
        /// there too); <paramref name="configure"/> sets type-specific attributes. Returns its handle.</summary>
        public FbResourceHandle AddInput(string tag, string name, Action<FbResourceDefBuilder>? configure = null) =>
            throw new NotImplementedException();

        /// <summary>Adds a <c>resource_output</c> pin under <c>outputs</c>; returns its handle.</summary>
        public FbResourceHandle AddOutput(string name) => throw new NotImplementedException();

        /// <summary>Adds an output of an explicit type <paramref name="tag"/> under <c>outputs</c>;
        /// <paramref name="configure"/> sets type-specific attributes. Returns its handle.</summary>
        public FbResourceHandle AddOutput(string tag, string name, Action<FbResourceDefBuilder>? configure = null) =>
            throw new NotImplementedException();

        /// <summary>Adds a value variable of type <paramref name="tag"/> (e.g. <c>resource_timer</c>,
        /// <c>resource_enum</c>) under <c>settings</c>; <paramref name="configure"/> sets its value. Returns its handle.</summary>
        public FbResourceHandle AddSetting(string tag, string name, Action<FbResourceDefBuilder>? configure = null) =>
            throw new NotImplementedException();

        /// <summary>Adds a private value variable of type <paramref name="tag"/> under <c>internalsettings</c>;
        /// <paramref name="configure"/> sets its value. Returns its handle.</summary>
        public FbResourceHandle AddInternalVariable(string tag, string name, Action<FbResourceDefBuilder>? configure = null) =>
            throw new NotImplementedException();

        /// <summary>
        /// Authors an <c>enum_definition</c> embedded in the block body and returns a typed handle to it — the
        /// definition-layer peer of <see cref="Ihc.Vis.Editing.EnumDefinitionRef"/>. Add its values with
        /// <c>AddValue</c>, then wire it by handle + human value name through the typed <c>Enum</c> / <c>AddEnumOperand</c>
        /// overloads, so a GUI works in value names ("Nat"/"Dag") rather than opaque typedef/inivalue tokens.
        /// </summary>
        public FbEnumDefRef AddEnumDefinition(string name) => throw new NotImplementedException();

        /// <summary>Opens the block's single <c>program_simple</c> for authoring (creating the
        /// <c>programs</c>/<c>program_simple</c>/<c>events</c>/<c>actions</c> skeleton on first use).</summary>
        public FbProgramBuilder Program() => throw new NotImplementedException();

        // ---- documentation (help metadata; programmatic-lookup only, never serialized) ----

        /// <summary>
        /// Sets the block-level documentation text — the whole help document a GUI shows for the block, mirroring a
        /// vendor <c>FunctionBlocks\*.md</c> file's "Anvendelse/Beskrivelse" prose. This is <b>metadata for
        /// programmatic lookup only</b>: it rides on <see cref="FunctionBlockDefinition.Documentation"/> (as
        /// <see cref="FunctionBlockDocumentation.Summary"/>) but is deliberately kept out of the serialized
        /// <see cref="FunctionBlockDefinition.Body"/>, so it is never written into a project <c>.vis</c> or a
        /// function-block description <c>.ifb</c>. Contrast <see cref="Note"/>, which sets the serialized <c>note</c>
        /// attribute. Returns this for chaining.
        /// </summary>
        public FunctionBlockDefinitionBuilder Documentation(string documentation) => throw new NotImplementedException();

        /// <summary>
        /// Attaches documentation text to one resource — the input/output/setting/variable identified by its
        /// <paramref name="resource"/> handle — the per-pin help a vendor <c>*.md</c> lists under "Indgange"/"Udgange".
        /// Like the block-level <see cref="Documentation(string)"/> overload this is <b>programmatic-lookup-only</b>
        /// metadata: it surfaces on <see cref="FunctionBlockDefinition.Documentation"/> (looked up by the resource's
        /// display name via <see cref="FunctionBlockDocumentation.ForResource"/>) and is never serialized into
        /// <see cref="FunctionBlockDefinition.Body"/> or an <c>.ifb</c>. Returns this for chaining.
        /// </summary>
        public FunctionBlockDefinitionBuilder Documentation(FbResourceHandle resource, string documentation) =>
            throw new NotImplementedException();

        // ---- escape hatches ----

        /// <summary>Splices a pre-built resource subtree into the named container (<c>inputs</c>/<c>outputs</c>/
        /// <c>settings</c>/<c>internalsettings</c>) — for exotic resource families. Returns this for chaining.</summary>
        public FunctionBlockDefinitionBuilder RawResource(string container, ProjectElement resource) =>
            throw new NotImplementedException();

        /// <summary>Splices an arbitrary pre-built subtree at the function-block <c>Body</c> root — the body-level peer
        /// of <c>RawResource</c>, for elements that live directly under <c>functionblock</c> rather than in a resource
        /// container: an embedded <c>enum_definition</c>, or an additional <c>program_simple</c> under <c>programs</c>.
        /// Returns this for chaining.</summary>
        public FunctionBlockDefinitionBuilder RawChild(ProjectElement child) => throw new NotImplementedException();

        /// <summary>Supplies a verbatim inline-DTD block for a genuinely non-registry element type (open-world).</summary>
        public FunctionBlockDefinitionBuilder InlineDtdBlock(string tag, string verbatimBlock) =>
            throw new NotImplementedException();

        /// <summary>
        /// Checks the builder's current state against the locally-decidable authoring preconditions (identity present,
        /// wired enum operands have a definition, container/type legality, ...) <b>without</b> building, returning the
        /// structured <see cref="ProjectValidationResult"/> a GUI filters and navigates by — the non-throwing path for
        /// live field-level validation as the user edits, phrased in authoring-call terms rather than placeholder ids.
        /// </summary>
        public ProjectValidationResult Validate() => throw new NotImplementedException();

        /// <summary>Materializes the deep <c>Body</c> (five containers in fixed order + program graph, placeholder ids)
        /// and returns the finished <see cref="FunctionBlockDefinition"/>. Throws <see cref="ProjectValidationException"/>
        /// when <see cref="Validate"/> would report an error — call <see cref="Validate"/> first for non-throwing UI feedback.</summary>
        public FunctionBlockDefinition Build() => throw new NotImplementedException();
    }

    /// <summary>
    /// A definition-local handle to a resource authored into a function-block definition: its display name and the
    /// placeholder id used to wire <c>link1</c>/<c>link2</c>/<c>scene_resource</c>. Deliberately not
    /// <see cref="Ihc.Vis.Editing.ResourceRef"/> — see the remarks on <see cref="FunctionBlockDefinitionBuilder"/>.
    /// </summary>
    public sealed class FbResourceHandle
    {
        internal FbResourceHandle(string name, ElementId placeholderId) => throw new NotImplementedException();

        /// <summary>The resource's display name.</summary>
        public string Name { get; } = null!;

        /// <summary>The body-local placeholder id the insert transform re-mints and remaps IDREFs through.</summary>
        internal ElementId PlaceholderId { get; }
    }

    /// <summary>
    /// A definition-local handle to an <c>enum_definition</c> authored via
    /// <see cref="FunctionBlockDefinitionBuilder.AddEnumDefinition"/> — the definition-layer peer of
    /// <see cref="Ihc.Vis.Editing.EnumDefinitionRef"/>. Carries the enum's placeholder typedef token and resolves a
    /// human value name to its <c>inivalue</c> token, so a GUI wires enum operands by name, not by opaque token.
    /// Values are added fluently with <see cref="AddValue"/> rather than upfront as the Editing peer takes them
    /// (<c>ProjectEditor.AddEnumDefinition(name, values)</c> allocates real value-ids in declaration order at that
    /// moment): definition-layer value-ids are throwaway placeholders the insert transform re-mints, so there is
    /// nothing to allocate atomically and the incremental form reads better for GUI authoring.
    /// </summary>
    public sealed class FbEnumDefRef
    {
        internal FbEnumDefRef() => throw new NotImplementedException();

        /// <summary>The enum's placeholder <c>typedef</c> token (remapped on insert), for raw interop.</summary>
        public string Typedef => throw new NotImplementedException();

        /// <summary>Adds an <c>enum_value</c> (in declaration order); returns this for chaining.</summary>
        public FbEnumDefRef AddValue(string valueName) => throw new NotImplementedException();

        /// <summary>The <c>inivalue</c> token for a previously-added value name, for raw interop.</summary>
        public string InitialValue(string valueName) => throw new NotImplementedException();
    }

    /// <summary>
    /// Fluent configurator for a function-block resource of any value/pin type — the definition-layer peer of
    /// <see cref="Ihc.Vis.Editing.SettingBuilder"/>, with a typed core plus a raw <see cref="Attribute"/> escape hatch
    /// (the ~18-type resource palette is too heterogeneous for per-type builders).
    /// </summary>
    public sealed class FbResourceDefBuilder
    {
        internal FbResourceDefBuilder(string tag) => throw new NotImplementedException();

        /// <summary>Sets the resource note.</summary>
        public FbResourceDefBuilder Note(string note) => throw new NotImplementedException();

        /// <summary>Marks the resource value as backed-up (<c>backup="yes"</c>).</summary>
        public FbResourceDefBuilder Backup(bool backup = true) => throw new NotImplementedException();

        /// <summary>Overrides the GUI icon token.</summary>
        public FbResourceDefBuilder Icon(string iconToken) => throw new NotImplementedException();

        /// <summary>Sets the raw initial value (<c>inivalue</c>) — the general escape hatch for scalar settings.</summary>
        public FbResourceDefBuilder Inivalue(string value) => throw new NotImplementedException();

        /// <summary>For a <c>resource_enum</c>: wires the enum by a typed <see cref="FbEnumDefRef"/> handle and a human
        /// value name (tokens resolved internally) — the GUI-friendly form.</summary>
        public FbResourceDefBuilder Enum(FbEnumDefRef definition, string valueName) => throw new NotImplementedException();

        /// <summary>For a <c>resource_enum</c>: sets the enum-definition/value IDREF tokens directly (the raw escape
        /// hatch when referencing a pre-existing/catalog enum; remapped on insert when the enum is embedded in the body).</summary>
        public FbResourceDefBuilder Enum(string typedefToken, string inivalueToken) => throw new NotImplementedException();

        /// <summary>Sets a timer resource's <c>hour</c>/<c>minute</c>/<c>second</c>(/<c>millisecond</c>) value.</summary>
        public FbResourceDefBuilder TimerHms(int hour, int minute, int second, int millisecond = 0) =>
            throw new NotImplementedException();

        /// <summary>Sets a date resource's <c>year</c>/<c>month</c>/<c>day</c> value.</summary>
        public FbResourceDefBuilder DateYmd(int year, int month, int day) => throw new NotImplementedException();

        /// <summary>Bakes a raw attribute verbatim (escape hatch for type-specific attributes).</summary>
        public FbResourceDefBuilder Attribute(string name, string value) => throw new NotImplementedException();
    }
}
