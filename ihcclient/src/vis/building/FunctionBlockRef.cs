#nullable enable
using System;

namespace Ihc.Projects
{
    /// <summary>
    /// A live handle to a function-block instance in the edit session. The block's internals (programs,
    /// resources, settings) arrive whole from the catalog deep-copy; this handle mutates instance-level
    /// fields, overrides individual catalog default settings, and exposes its catalog-sourced resources
    /// by name for linking.
    /// </summary>
    /// <remarks>Stage 1: full method signatures, stub bodies.</remarks>
    public sealed class FunctionBlockRef
    {
        internal FunctionBlockRef()
        {
        }

        /// <summary>
        /// Overrides the function-block display name (the <c>name</c> attribute). A catalog-sourced block
        /// already arrives with its composed provenance label
        /// (<c>FunctionBlockDescriptor.DisplayName</c>, e.g. <c>1.1.01.e. Kip tænd sluk</c>) carried verbatim
        /// by the deep-copy, so call this <b>only for a genuine user rename</b>; do not re-set it to the bare
        /// master name. Returns this.
        /// </summary>
        public FunctionBlockRef Name(string name) => throw new NotImplementedException();

        /// <summary>Marks the function block locked; returns this.</summary>
        public FunctionBlockRef Locked() => throw new NotImplementedException();

        /// <summary>Overrides one named setting whose default came from the catalog; returns this.</summary>
        public FunctionBlockRef Setting(string name, Func<SettingBuilder, SettingBuilder> configure) =>
            throw new NotImplementedException();

        /// <summary>References a catalog-sourced input by name, returning its live handle.</summary>
        public ResourceRef Input(string name) => throw new NotImplementedException();

        /// <summary>References a catalog-sourced output by name, returning its live handle.</summary>
        public ResourceRef Output(string name) => throw new NotImplementedException();
    }

    /// <summary>Fluent configurator for a single function-block setting value.</summary>
    public sealed class SettingBuilder
    {
        internal SettingBuilder()
        {
        }

        /// <summary>Sets the setting to a duration in minutes (typed convenience for time settings).</summary>
        public SettingBuilder Minutes(int minutes) => throw new NotImplementedException();

        /// <summary>
        /// Sets the setting to a raw value — the general escape hatch for enum/boolean/number settings
        /// that do not yet have a typed setter. Typed setters can layer over this in Stage 2.
        /// </summary>
        public SettingBuilder Value(string value) => throw new NotImplementedException();

        /// <summary>Marks the setting value as backed-up.</summary>
        public SettingBuilder Backup() => throw new NotImplementedException();
    }
}
