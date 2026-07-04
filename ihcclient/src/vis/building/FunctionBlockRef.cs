#nullable enable
using System;
using System.Collections.Generic;

namespace Ihc.Projects
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
        /// Unlocks the function block (US-020 "Oplås") — the inverse of <see cref="Locked"/>: clears <c>locked</c> to
        /// its default <c>no</c>, so the canonicalizer omits it and a block loaded with <c>locked="yes"</c> becomes
        /// an editable custom block. Returns this.
        /// </summary>
        public FunctionBlockRef Unlock()
        {
            editor.SetAttributeById(Id, "locked", "no");
            return this;
        }

        /// <summary>Overrides one named setting whose default came from the catalog; returns this.</summary>
        public FunctionBlockRef Setting(string name, Func<SettingBuilder, SettingBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(configure);
            ElementId settingId = editor.FindDescendantIdByName(Id, name)
                ?? throw new InvalidOperationException($"No setting named '{name}' on this function block.");
            SettingBuilder builder = configure(new SettingBuilder());
            foreach ((string attr, string value) in builder.Attributes)
            {
                editor.SetAttributeById(settingId, attr, value);
            }
            return this;
        }

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

        /// <summary>References a catalog-sourced input by name, returning its live handle.</summary>
        public ResourceRef Input(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = editor.FindDescendantIdByName(Id, name, "resource_input")
                ?? throw new InvalidOperationException($"No input named '{name}' on this function block.");
            return new ResourceRef(name, id);
        }

        /// <summary>References a catalog-sourced output by name, returning its live handle.</summary>
        public ResourceRef Output(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = editor.FindDescendantIdByName(Id, name, "resource_output")
                ?? throw new InvalidOperationException($"No output named '{name}' on this function block.");
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

        // Pin (I/O) resource types are container-bound: they may live only under inputs/outputs, never under a
        // value-variable container (settings/internalsettings). Value types may live under any container. §6.3.1.
        private static readonly HashSet<string> PinTypes = new(StringComparer.Ordinal)
        {
            "resource_input", "resource_output", "resource_scene",
        };

        private ResourceRef AddResource(string container, string tag, string name, Action<ElementRef>? configure)
        {
            ArgumentNullException.ThrowIfNull(tag);
            ArgumentNullException.ThrowIfNull(name);
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

        private static void RequireValueType(string tag, string container)
        {
            if (PinTypes.Contains(tag) || tag == "functionblock")
            {
                throw new ArgumentException(
                    $"'{tag}' is a pin/block type; a function block's '{container}' container accepts value variables " +
                    $"only (pins belong in inputs/outputs). See spec ch. 06 §6.3.1.", nameof(tag));
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
            attributes.Add(("minute", minutes.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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
