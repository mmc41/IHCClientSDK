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
