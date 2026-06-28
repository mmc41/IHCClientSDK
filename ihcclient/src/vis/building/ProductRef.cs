#nullable enable
using System;

namespace Ihc.Vis.Building
{
    /// <summary>
    /// A live, mutable handle to a product instance (<c>product_dataline</c> / <c>product_airlink</c>)
    /// in the edit session. Fluent setters mutate instance-level fields in place; resource methods add
    /// or look up I/O children and return their live <see cref="ResourceRef"/> handles for linking.
    /// </summary>
    /// <remarks>Stage 1: full method signatures, stub bodies.</remarks>
    public sealed class ProductRef
    {
        internal ProductRef()
        {
        }

        /// <summary>Sets the product display name.</summary>
        public ProductRef Name(string name) => throw new NotImplementedException();

        /// <summary>Marks the product locked (read-only in IHC Visual).</summary>
        public ProductRef Locked() => throw new NotImplementedException();

        /// <summary>Includes the product in the end-user report.</summary>
        public ProductRef EnduserReport() => throw new NotImplementedException();

        /// <summary>Sets the product note.</summary>
        public ProductRef Note(string note) => throw new NotImplementedException();

        /// <summary>Sets the physical position description.</summary>
        public ProductRef Position(string position) => throw new NotImplementedException();

        /// <summary>Sets the cable type.</summary>
        public ProductRef CableType(string cableType) => throw new NotImplementedException();

        /// <summary>Sets the cable number.</summary>
        public ProductRef CableNumber(string cableNumber) => throw new NotImplementedException();

        /// <summary>Sets the documentation tag.</summary>
        public ProductRef DocumentationTag(string tag) => throw new NotImplementedException();

        /// <summary>Sets the power group.</summary>
        public ProductRef PowerGroup(string powerGroup) => throw new NotImplementedException();

        /// <summary>Adds and configures an input child, returning its live handle.</summary>
        public ResourceRef AddInput(string name, Func<InputBuilder, InputBuilder> configure) =>
            throw new NotImplementedException();

        /// <summary>Adds and configures an output child, returning its live handle.</summary>
        public ResourceRef AddOutput(string name, Func<OutputBuilder, OutputBuilder> configure) =>
            throw new NotImplementedException();

        /// <summary>Adds a <c>scenes</c> container bound to this product's output(s); returns this.</summary>
        public ProductRef AddScenes() => throw new NotImplementedException();

        /// <summary>Looks up an existing input by name (for editing a loaded product), returning its live handle.</summary>
        public ResourceRef Input(string name) => throw new NotImplementedException();

        /// <summary>Looks up an existing output by name (for editing a loaded product), returning its live handle.</summary>
        public ResourceRef Output(string name) => throw new NotImplementedException();

        /// <summary>
        /// Removes an input child (and any links to/from it). Its retired <c>_0x</c> id is not reused
        /// (plan §3.4); the passed handle is dead afterwards.
        /// </summary>
        public void RemoveInput(ResourceRef input) => throw new NotImplementedException();

        /// <summary>
        /// Removes an output child (and any links to/from it, plus a dependent <c>scenes</c> container).
        /// Its retired id is not reused; the passed handle is dead afterwards.
        /// </summary>
        public void RemoveOutput(ResourceRef output) => throw new NotImplementedException();

        /// <summary>Removes this product's <c>scenes</c> container.</summary>
        public void RemoveScenes() => throw new NotImplementedException();
    }

    /// <summary>Fluent configurator for a product input child (<c>dataline_input</c>/<c>airlink_input</c>).</summary>
    public sealed class InputBuilder
    {
        internal InputBuilder()
        {
        }

        /// <summary>Sets the opaque input address token.</summary>
        public InputBuilder Address(string address) => throw new NotImplementedException();

        /// <summary>Sets the cable colour.</summary>
        public InputBuilder CableColour(string colour) => throw new NotImplementedException();

        /// <summary>Sets the input note.</summary>
        public InputBuilder Note(string note) => throw new NotImplementedException();
    }

    /// <summary>Fluent configurator for a product output child (<c>dataline_output</c>/<c>airlink_output</c>).</summary>
    public sealed class OutputBuilder
    {
        internal OutputBuilder()
        {
        }

        /// <summary>Sets the opaque output address token.</summary>
        public OutputBuilder Address(string address) => throw new NotImplementedException();

        /// <summary>Marks the output as backed-up.</summary>
        public OutputBuilder Backup() => throw new NotImplementedException();
    }
}
