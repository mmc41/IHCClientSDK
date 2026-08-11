#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// A live handle to a function-block instance in the edit session. The block's internals (programs, resources,
    /// settings) arrive whole from the catalog deep-copy; this handle mutates instance-level fields, overrides
    /// individual catalog default settings, and exposes its catalog-sourced resources by name for linking.
    /// </summary>
    public sealed class FunctionBlockRef
    {
        private readonly ProjectEditor editor;

        internal FunctionBlockRef(ProjectEditor editor, ElementId id)
        {
            this.editor = editor;
            Id = id;
        }

        internal ElementId Id { get; }

        /// <summary>
        /// Overrides the function-block display name. A catalog-sourced block already arrives with its composed
        /// provenance label (e.g. <c>1.1.01.e. Kip tænd sluk</c>) carried verbatim by the deep-copy, so call this
        /// <b>only for a genuine user rename</b>; do not re-set it to the bare master name. Returns this.
        /// </summary>
        public FunctionBlockRef Name(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            editor.SetAttributeById(Id, "name", name);
            return this;
        }

        /// <summary>Marks the function block locked; returns this.</summary>
        public FunctionBlockRef Locked()
        {
            editor.SetAttributeById(Id, "locked", "yes");
            return this;
        }

        /// <summary>
        /// Unlocks the function block (US-020 "Oplås") and takes ownership of it. Clearing <c>locked</c> is only half
        /// of it: the block stops being a LIBRARY block, so the three library-identity keys
        /// (<c>master_schneider_electric</c>/<c>master_type</c>/<c>master_version</c>) are dropped to their defaults —
        /// the same keys <see cref="ExportDefinition"/> strips — while <c>master_name</c> is kept as the name it came
        /// from, and <c>master_programmer</c>/<c>master_date_*</c> are re-stamped to whoever unlocked it and when.
        /// The icon becomes the unlocked-block glyph <c>_0xf</c>; <c>name</c> and <c>note</c> are left alone (this is
        /// not a rename). Returns this.
        /// </summary>
        /// <param name="programmer">The user taking ownership (the vendor stamps the current Windows user; explicit
        /// here so the result is deterministic).</param>
        /// <param name="unlocked">The date to stamp (the vendor stamps "today"; explicit for the same reason).</param>
        public FunctionBlockRef Unlock(string programmer, DateOnly unlocked)
        {
            ArgumentNullException.ThrowIfNull(programmer);
            ClearLibraryIdentity();
            StampOwner(programmer, unlocked);
            editor.SetAttributeById(Id, "icon", "_0xf");
            editor.SetAttributeById(Id, "locked", "no");
            return this;
        }

        /// <summary>
        /// Transforms this in-project block into a locked library instance (US-021 Save-to-library, PG-3a): renames it
        /// and its <c>master_name</c> to <paramref name="name"/>, stamps the export <paramref name="programmer"/> and
        /// <paramref name="date"/>, applies the user-library badge (<c>icon _0x10</c>) and <paramref name="note"/>, and
        /// locks it — no re-insertion (the same in-project element, re-attributed in place). After this the T003/T004
        /// locked-ancestor guard makes it view-only, while <i>Show program</i> stays available. Returns this.
        /// </summary>
        public FunctionBlockRef SaveAsLibraryInstance(string name, string programmer, DateOnly date, string? note)
        {
            editor.SetAttributeById(Id, "name", name);
            // It is the installer's library block now, not the one it came from, so the source's library identity goes
            // — the same three keys the exported .ifb drops and Unlock clears (S-22/S-20).
            ClearLibraryIdentity();
            editor.SetAttributeById(Id, "master_name", name);
            StampOwner(programmer, date);
            editor.SetAttributeById(Id, "icon", "_0x10");
            if (note is not null)
            {
                editor.SetAttributeById(Id, "note", note);
            }
            editor.SetAttributeById(Id, "locked", "yes");   // lock last, so the block is fully stamped before it is sealed
            return this;
        }

        // The two halves of an ownership transfer, shared by Unlock and SaveAsLibraryInstance: drop the source's
        // library identity, then stamp who owns it now. One rule, written once — the vendor keys involved are the
        // same three / same four either way (S-22/S-20), and the emitted attribute order is the canonicalizer's.
        private void ClearLibraryIdentity()
        {
            editor.SetAttributeById(Id, "master_schneider_electric", "no");
            editor.SetAttributeById(Id, "master_type", string.Empty);
            editor.SetAttributeById(Id, "master_version", string.Empty);
        }

        private void StampOwner(string programmer, DateOnly date)
        {
            editor.SetAttributeById(Id, "master_programmer", programmer);
            editor.SetAttributeById(Id, "master_date_year", DecToken.Format(date.Year));
            editor.SetAttributeById(Id, "master_date_month", DecToken.Format(date.Month));
            editor.SetAttributeById(Id, "master_date_day", DecToken.Format(date.Day));
        }

        /// <summary>
        /// Overrides one named setting whose default came from the catalog; returns this. The lookup is scoped
        /// to the block's two value-variable containers (<c>settings</c>, then <c>internalsettings</c>) — vendor
        /// blocks reuse display names across sections, so a whole-block search could silently write onto an
        /// input/output pin of the same name. A name present in both containers must be addressed by id instead.
        /// </summary>
        public FunctionBlockRef Setting(string name, Func<SettingBuilder, SettingBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(configure);
            ProjectElement block = editor.Require(Id);
            ElementId? inSettings = NamedRowIdIn(block, "settings", name);
            ElementId? inInternal = NamedRowIdIn(block, "internalsettings", name);
            if (inSettings is { } ambiguous && inInternal is { } other)
            {
                throw new InvalidOperationException(
                    $"Setting name '{name}' is ambiguous on this function block: settings has " +
                    $"{ambiguous.ToToken()} and internalsettings has {other.ToToken()}; address it by id via " +
                    $"{nameof(ProjectEditor.TryResolve)}.");
            }
            ElementId settingId = inSettings ?? inInternal
                ?? throw new InvalidOperationException(
                    $"No setting named '{name}' on this function block (searched the settings and " +
                    "internalsettings containers).");
            SettingBuilder builder = configure(new SettingBuilder());
            foreach ((string attr, string value) in builder.Attributes)
            {
                editor.SetAttributeById(settingId, attr, value);
            }
            return this;
        }

        // The container-scoped, type-agnostic row-by-name lookup shared by Setting(name, configure) and
        // ResolveResource; null when the container is absent or holds no row of that name.
        private static ElementId? NamedRowIdIn(ProjectElement block, string container, string name) =>
            block.FindChild(container)?.ChildrenOrEmpty().FirstOrDefault(c => c.GetAttribute("name") == name)?.Id;

        /// <summary>
        /// Adds a new input pin (<c>resource_input</c>) under this block's <c>inputs</c> container and returns its
        /// live handle for linking. For an empty (US-019) or unlocked (US-020) block that has no catalog resources.
        /// </summary>
        public ResourceRef AddInput(string name) => AddInput("resource_input", name);

        /// <summary>
        /// Adds a new input of an explicit type <paramref name="tag"/> under this block's <c>inputs</c> container — a
        /// custom block's inputs may be value types too (e.g. project2's enum/date inputs), not only
        /// <c>resource_input</c> pins. The optional <paramref name="configure"/> callback sets type-specific attributes
        /// (e.g. an enum's <c>typedef</c>/<c>inivalue</c>). Returns its live handle.
        /// </summary>
        public ResourceRef AddInput(string tag, string name, Action<ElementRef>? configure = null) =>
            AddResource("inputs", tag, name, configure);

        /// <summary>Adds a new output pin (<c>resource_output</c>) under <c>outputs</c>; returns its live handle.</summary>
        public ResourceRef AddOutput(string name) => AddOutput("resource_output", name);

        /// <summary>
        /// Adds a new output of an explicit type <paramref name="tag"/> under this block's <c>outputs</c> container
        /// (value types and <c>resource_scene</c> outputs are legal there too). The optional
        /// <paramref name="configure"/> callback sets type-specific attributes. Returns its live handle.
        /// </summary>
        public ResourceRef AddOutput(string tag, string name, Action<ElementRef>? configure = null) =>
            AddResource("outputs", tag, name, configure);

        /// <summary>
        /// Adds a new value variable of type <paramref name="tag"/> (e.g. <c>resource_flag</c>, <c>resource_timer</c>,
        /// <c>resource_enum</c>) under this block's <c>settings</c> container. Per the §6.3.1 section↔type matrix,
        /// <c>settings</c> accepts value types only — a pin type (<c>resource_input</c>/<c>_output</c>/<c>_scene</c>)
        /// or <c>functionblock</c> is rejected. The optional <paramref name="configure"/> callback receives the new
        /// resource's handle to set type-specific attributes (e.g. a timer's <c>hour</c>/<c>minute</c>).
        /// </summary>
        public ResourceRef AddSetting(string tag, string name, Action<ElementRef>? configure = null)
        {
            RequireValueType(tag, "settings");
            return AddResource("settings", tag, name, configure);
        }

        /// <summary>
        /// Adds a new value variable of type <paramref name="tag"/> under this block's <c>internalsettings</c>
        /// container (private variables). Accepts value types only — pin types and <c>functionblock</c> are rejected
        /// (§6.3.1). The optional <paramref name="configure"/> callback sets type-specific attributes.
        /// </summary>
        public ResourceRef AddInternalVariable(string tag, string name, Action<ElementRef>? configure = null)
        {
            RequireValueType(tag, "internalsettings");
            return AddResource("internalsettings", tag, name, configure);
        }

        /// <summary>
        /// References an input row by name, returning its live handle. Scoped to the <c>inputs</c> container and
        /// type-agnostic — a custom block's inputs may be value variables (enum/date/flag …), not only
        /// <c>resource_input</c> pins. Same-named rows resolve to the first in document order.
        /// </summary>
        public ResourceRef Input(string name) => ResolveResource("inputs", name, "input");

        /// <summary>References an output row by name (type-agnostic, scoped to <c>outputs</c>); returns its live handle.</summary>
        public ResourceRef Output(string name) => ResolveResource("outputs", name, "output");

        /// <summary>
        /// References a settings value variable by name, returning its live handle for wiring into program rows
        /// (conditions/actions operands, US-029). Scoped to the <c>settings</c> container.
        /// </summary>
        public ResourceRef Setting(string name) => ResolveResource("settings", name, "setting");

        private ResourceRef ResolveResource(string container, string name, string kind)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId? id = NamedRowIdIn(editor.Require(Id), container, name);
            return id is null
                ? throw new InvalidOperationException($"No {kind} named '{name}' on this function block.")
                : new ResourceRef(name, id.Value);
        }

        /// <summary>
        /// References a scene output pin (<c>resource_scene</c>) by name, returning its live handle — the source
        /// side of <see cref="ProjectEditor.LinkScene(ResourceRef,ScenesRef,SceneValue)"/>.
        /// </summary>
        public ResourceRef SceneOutput(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = editor.FindDescendantIdByName(Id, name, "resource_scene")
                ?? throw new InvalidOperationException($"No scene output named '{name}' on this function block.");
            return new ResourceRef(name, id);
        }

        /// <summary>
        /// Opens a <see cref="ProgramBuilder"/> over this block's single <c>program_simple</c> to author its logic by
        /// hand — the entry for an empty ("Tom blok") block, whose <c>fb.def</c> skeleton provides exactly one empty
        /// program. Throws if the block has no or several programs (a multi-program catalog block; address one of
        /// those by id via <see cref="ProjectEditor.Program"/>).
        /// </summary>
        public ProgramBuilder Program()
        {
            ElementId programsId = editor.RequireChildId(Id, "programs");
            return editor.Program(editor.RequireSoleChildId(programsId, "program_simple"));
        }

        /// <summary>
        /// Lifts this placed block to a keyless user-block <see cref="FunctionBlockDefinition"/> — the engine half
        /// of the US-021 "Gem funktionsblok…" dialog, byte-gated by the vendor export oracle
        /// <c>gemoracle-kip.ifb</c> (ENG-A4). Read-only over the session: unlike vendor Gem (which also renames and
        /// re-stamps the placed block in the document — a mutation no capture pins byte-level yet), the project is
        /// left untouched; a GUI wanting that parity composes the rename via <see cref="Name"/>. Per the capture:
        /// the subtree is copied with its project ids VERBATIM (non-contiguous, unrenumbered), every reciprocal
        /// wiring row (follow-link halves, scene links) is stripped — a type definition carries no instance wiring;
        /// no catalog file does — and the root is re-attributed to a keyless user block:
        /// <c>master_schneider_electric</c>/<c>master_type</c>/<c>master_version</c> removed, <c>name</c> and
        /// <c>master_name</c> = <paramref name="name"/>, <c>master_programmer</c>/<c>master_date_*</c> stamped,
        /// <c>locked</c> forced to <c>yes</c> (a library master is always locked, PG-3b/US-021), user-library icon
        /// <c>_0x10</c>. The grammar is assembled from the session's own
        /// schema view — one declaration per element type the body uses, in first-occurrence order, the vendor head
        /// shape — so the result writes to an <c>.ifb</c> via <see cref="Ihc.Vis.Catalog.CatalogFileWriter"/> and
        /// reads back via <see cref="Ihc.Vis.Catalog.CatalogReader"/>.
        /// </summary>
        /// <param name="name">The dialog Navn — the definition's display name and <c>master_name</c>.</param>
        /// <param name="programmer">The exporting user (the vendor stamps the current Windows user; explicit here
        /// so exports are deterministic).</param>
        /// <param name="exported">The export date (the vendor stamps "today"; explicit for the same reason).</param>
        /// <param name="note">The dialog Note (the block tooltip); <c>null</c> leaves the exported root without a
        /// <c>note</c> attribute — the lean vendor form for an empty note.</param>
        public FunctionBlockDefinition ExportDefinition(string name, string programmer, DateOnly exported,
            string? note = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(programmer);
            ProjectElement source = editor.Require(Id);
            ProjectElement body = RestampExportIdentity(WithoutWiringRows(source), name, programmer, exported, note);
            return new FunctionBlockDefinition(
                MasterType: string.Empty,
                MasterVersion: string.Empty,
                MasterName: name,
                DisplayName: FbGrammar.ComposeDisplayName(masterType: null, masterVersion: null, name),
                CategoryPath: string.Empty,
                body)
            {
                // Head and element shape both come from the SOURCE, not the stripped body: the vendor's export keeps
                // declaring the wiring types it removed, and keeps the two-tag form of every pin it emptied (S-22).
                Grammar = AssembleExportGrammar(source, editor.SchemaView),
                ExplicitCloseIds = EmptiedByStrip(source),
            };
        }

        // The ids of elements that HAD children but have none once the wiring rows are stripped — the writer closes
        // those with an explicit end tag, as the vendor does (S-22).
        private static ImmutableHashSet<ElementId> EmptiedByStrip(ProjectElement source)
        {
            var emptied = ImmutableHashSet.CreateBuilder<ElementId>();
            foreach (ProjectElement element in source.DescendantsAndSelf())
            {
                if (element.Id is { } id && !element.Children.IsDefaultOrEmpty
                    && element.ChildrenOrEmpty().All(c => ReciprocalTags.All.Contains(c.Tag)))
                {
                    emptied.Add(id);
                }
            }
            return emptied.ToImmutable();
        }

        // Drops every reciprocal wiring row (follow-link half or scene link) from the copy — unconditionally, not
        // just externally-paired ones: a type definition carries no instance wiring, and no catalog file (stock or
        // vendor-exported) contains such rows.
        private static ProjectElement WithoutWiringRows(ProjectElement element) =>
            element.Children.IsDefaultOrEmpty
                ? element
                : element with
                {
                    Children = element.Children
                        .Where(c => !ReciprocalTags.All.Contains(c.Tag))
                        .Select(WithoutWiringRows)
                        .ToImmutableArray(),
                };

        // Re-attributes the root to a keyless user block (ENG-A4): the three vendor-identity keys and the source
        // note never carry over; the stamps replace in place so the remaining attributes keep their vendor order,
        // and any stamp the source lacked (e.g. a never-stamped hand-authored block) is appended.
        private static ProjectElement RestampExportIdentity(ProjectElement root, string name, string programmer,
            DateOnly exported, string? note)
        {
            var stamps = new List<(string Name, string Value)>
            {
                ("name", name),
                ("master_name", name),
                ("master_programmer", programmer),
                ("master_date_year", DecToken.Format(exported.Year)),
                ("master_date_month", DecToken.Format(exported.Month)),
                ("master_date_day", DecToken.Format(exported.Day)),
                ("locked", "yes"),   // PG-3(b)/US-021: a library master is ALWAYS locked, even from an unlocked source
                ("icon", "_0x10"),
            };
            if (note is not null)
            {
                stamps.Add(("note", note));
            }
            var bag = new List<(string Name, string Value)>();
            foreach ((string attr, string value) in root.AttrsOrEmpty())
            {
                if (attr is "id" or "master_schneider_electric" or "master_type" or "master_version" or "note")
                {
                    continue;   // id re-leads the bag via Create; the stripped keys and source note never carry over
                }
                int stamp = stamps.FindIndex(s => s.Name == attr);
                if (stamp >= 0)
                {
                    bag.Add(stamps[stamp]);
                    stamps.RemoveAt(stamp);
                }
                else
                {
                    bag.Add((attr, value));
                }
            }
            bag.AddRange(stamps);
            return ProjectElement.Create(root.Tag, root.Id, bag, root.ChildrenOrEmpty());
        }

        // The vendor head shape: one declaration per element type the body uses, in preorder first-occurrence
        // order, each taken verbatim from the session's schema view (the file's own inline DTD first, registry
        // fallback — the same one attribute table the vendor renders into both .vis inline DTDs and .ifb heads),
        // then parsed into the structured grammar the catalog writer and a re-import resolve against.
        private static CatalogGrammar AssembleExportGrammar(ProjectElement body, ProjectSchemaView view)
        {
            var head = new System.Text.StringBuilder(4096);
            head.Append("<?xml version=\"1.0\" encoding=\"").Append(CatalogGrammar.DefaultDeclaredEncoding)
                .Append("\"?>\r\n");
            head.Append("<!DOCTYPE ").Append(body.Tag).Append(" [\r\n");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProjectElement element in body.DescendantsAndSelf())
            {
                if (seen.Add(element.Tag))
                {
                    head.Append(view.Get(element.Tag).CanonicalDtdBlock);
                }
            }
            head.Append("]>\r\n");
            return Catalog.CatalogDtdParser.ParseStrict(head.ToString());
        }

        private ResourceRef AddResource(string container, string tag, string name, Action<ElementRef>? configure)
        {
            ArgumentNullException.ThrowIfNull(tag);
            ArgumentNullException.ThrowIfNull(name);
            RequireLegalForContainer(tag, container);
            ElementId containerId = editor.Require(Id).FindChild(container)?.Id
                ?? throw new InvalidOperationException($"This function block has no <{container}> container.");
            // Hand-authored FB resources never upsert — each add is a distinct node (repeat names are legal, e.g.
            // project2's two "Kommatal"/"Scenarie" outputs). Product I/O keeps upserting via UpsertResourceChild.
            ResourceRef resource = editor.AddResourceChild(containerId, tag, name, System.Array.Empty<(string, string)>());
            if (configure is not null && resource.Id is { } id && editor.TryResolve(id, out ElementRef? handle))
            {
                configure(handle);
            }
            return resource;
        }

        // Both guards derive from the single pin-binding encoding (PlacementRules.PinContainerFor, §6.3.1) and stay
        // deliberately open-world beyond it: an unmodeled value type is admitted (the validator warns later), only
        // the hard mis-placements — a pin outside its bound container, or a nested block — are rejected at the add.
        private static void RequireValueType(string tag, string container)
        {
            if (PlacementRules.PinContainerFor(tag) is not null || tag == "functionblock")
            {
                throw new ArgumentException(
                    $"'{tag}' is a pin/block type; a function block's '{container}' container accepts value variables " +
                    $"only (pins belong in inputs/outputs). See spec ch. 06 §6.3.1.", nameof(tag));
            }
        }

        private static void RequireLegalForContainer(string tag, string container)
        {
            if (tag == "functionblock"
                || (PlacementRules.PinContainerFor(tag) is { } bound && bound != container))
            {
                throw new ArgumentException(
                    $"'{tag}' may not live under a function block's '{container}' container (spec ch. 06 §6.3.1).",
                    nameof(tag));
            }
        }
    }

    /// <summary>Fluent configurator for a single function-block setting value.</summary>
    public sealed class SettingBuilder
    {
        private readonly List<(string, string)> attributes = new();

        internal SettingBuilder()
        {
        }

        internal IReadOnlyList<(string, string)> Attributes => attributes;

        /// <summary>Sets the setting to a duration in minutes (typed convenience for time settings).</summary>
        public SettingBuilder Minutes(int minutes)
        {
            attributes.Add(("minute", DecToken.Format(minutes)));
            return this;
        }

        /// <summary>
        /// Sets the setting to a raw value — the general escape hatch for enum/boolean/number settings that do not
        /// yet have a typed setter. Typed setters can layer over this in Stage 2.
        /// </summary>
        public SettingBuilder Value(string value)
        {
            attributes.Add(("value", value));
            return this;
        }

        /// <summary>Marks the setting value as backed-up.</summary>
        public SettingBuilder Backup()
        {
            attributes.Add(("backup", "yes"));
            return this;
        }
    }
}
