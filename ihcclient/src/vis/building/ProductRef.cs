#nullable enable
using System;
using System.Collections.Generic;

namespace Ihc.Projects
{
    /// <summary>
    /// A live, mutable handle to a product instance (<c>product_dataline</c>) in the edit session. Fluent setters
    /// mutate instance-level fields in place; resource methods add/configure or look up I/O children and return
    /// their live <see cref="ResourceRef"/> handles for linking.
    /// </summary>
    public sealed class ProductRef
    {
        private readonly ProjectEditor editor;

        internal ProductRef(ProjectEditor editor, ElementId id)
        {
            this.editor = editor;
            Id = id;
        }

        internal ElementId Id { get; }

        /// <summary>Sets the product display name.</summary>
        public ProductRef Name(string name) => Set("name", name);

        /// <summary>Marks the product locked (read-only in IHC Visual).</summary>
        public ProductRef Locked() => Set("locked", "yes");

        /// <summary>Includes the product in the end-user report.</summary>
        public ProductRef EnduserReport() => Set("enduser_report", "yes");

        /// <summary>Sets the product note.</summary>
        public ProductRef Note(string note) => Set("note", note);

        /// <summary>Sets the physical position description.</summary>
        public ProductRef Position(string position) => Set("position", position);

        /// <summary>Sets the cable type.</summary>
        public ProductRef CableType(string cableType) => Set("cabletype", cableType);

        /// <summary>Sets the cable number.</summary>
        public ProductRef CableNumber(string cableNumber) => Set("cablenumber", cableNumber);

        /// <summary>Sets the documentation tag.</summary>
        public ProductRef DocumentationTag(string tag) => Set("documentation_tag", tag);

        /// <summary>Sets the power group.</summary>
        public ProductRef PowerGroup(string powerGroup) => Set("power_group", powerGroup);

        /// <summary>
        /// Adds or configures an input child by name (catalog deep-copies already provide a product's inputs, so a
        /// matching name configures that one in place; otherwise a new input is appended), returning its live handle.
        /// </summary>
        public ResourceRef AddInput(string name, Func<InputBuilder, InputBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(configure);
            InputBuilder builder = configure(new InputBuilder());
            return editor.UpsertResourceChild(Id, "dataline_input", name, builder.Attributes);
        }

        /// <summary>
        /// Adds or configures an output child by name (see <see cref="AddInput"/> for the upsert semantics),
        /// returning its live handle.
        /// </summary>
        public ResourceRef AddOutput(string name, Func<OutputBuilder, OutputBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(configure);
            OutputBuilder builder = configure(new OutputBuilder());
            return editor.UpsertResourceChild(Id, "dataline_output", name, builder.Attributes);
        }

        /// <summary>Ensures a <c>scenes</c> container bound to this product's output exists; returns this.</summary>
        public ProductRef AddScenes()
        {
            editor.EnsureScenesBoundToFirstOutput(Id);
            return this;
        }

        /// <summary>Looks up an existing input by name, returning its live handle.</summary>
        public ResourceRef Input(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = editor.FindChildIdByName(Id, "dataline_input", name)
                ?? throw new InvalidOperationException($"No input named '{name}' on this product.");
            return new ResourceRef(name, id);
        }

        /// <summary>Looks up an existing output by name, returning its live handle.</summary>
        public ResourceRef Output(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = editor.FindChildIdByName(Id, "dataline_output", name)
                ?? throw new InvalidOperationException($"No output named '{name}' on this product.");
            return new ResourceRef(name, id);
        }

        /// <summary>Removes an input child (and its retired id is not reused).</summary>
        public void RemoveInput(ResourceRef input)
        {
            ArgumentNullException.ThrowIfNull(input);
            editor.RemoveSubtree(RequireId(input));
        }

        /// <summary>Removes an output child (and its retired id is not reused).</summary>
        public void RemoveOutput(ResourceRef output)
        {
            ArgumentNullException.ThrowIfNull(output);
            editor.RemoveSubtree(RequireId(output));
        }

        /// <summary>Removes this product's <c>scenes</c> container by name.</summary>
        public void RemoveScenes()
        {
            ElementId? scenes = editor.FindChildIdByName(Id, "scenes", "Scenarier");
            if (scenes is { } id)
            {
                editor.RemoveSubtree(id);
            }
        }

        private ProductRef Set(string name, string value)
        {
            editor.SetAttributeById(Id, name, value);
            return this;
        }

        private static ElementId RequireId(ResourceRef resource) => resource.Id
            ?? throw new InvalidOperationException($"Resource '{resource.Name}' has no id.");
    }

    /// <summary>Fluent configurator for a product input child (<c>dataline_input</c>).</summary>
    public sealed class InputBuilder
    {
        private readonly List<(string, string)> attributes = new();

        internal InputBuilder()
        {
        }

        internal IReadOnlyList<(string, string)> Attributes => attributes;

        /// <summary>Sets the opaque input address token.</summary>
        public InputBuilder Address(string address)
        {
            attributes.Add(("address_dataline", address));
            return this;
        }

        /// <summary>Sets the cable colour.</summary>
        public InputBuilder CableColour(string colour)
        {
            attributes.Add(("cable_colour", colour));
            return this;
        }

        /// <summary>Sets the input note.</summary>
        public InputBuilder Note(string note)
        {
            attributes.Add(("note", note));
            return this;
        }
    }

    /// <summary>Fluent configurator for a product output child (<c>dataline_output</c>).</summary>
    public sealed class OutputBuilder
    {
        private readonly List<(string, string)> attributes = new();

        internal OutputBuilder()
        {
        }

        internal IReadOnlyList<(string, string)> Attributes => attributes;

        /// <summary>Sets the opaque output address token.</summary>
        public OutputBuilder Address(string address)
        {
            attributes.Add(("address_dataline", address));
            return this;
        }

        /// <summary>Marks the output as backed-up.</summary>
        public OutputBuilder Backup()
        {
            attributes.Add(("backup", "yes"));
            return this;
        }
    }
}
