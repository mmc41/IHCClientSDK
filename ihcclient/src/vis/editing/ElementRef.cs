#nullable enable
using System;
using System.Collections.Generic;

using Ihc.Vis.Model;
using Ihc.Vis.Schema;
namespace Ihc.Vis.Editing
{
    /// <summary>
    /// A live, generic, id-addressed handle to any element in the edit session — the foundation of a GUI
    /// selection model and the backend of its F2 "Egenskaber" properties panel. Unlike the curated
    /// <see cref="GroupRef"/>/<see cref="ProductRef"/>/<see cref="ResourceRef"/> handles it is obtained from an
    /// <see cref="ElementId"/> (via <see cref="ProjectEditor.TryResolve"/>), so it addresses resources, links,
    /// program nodes and same-named siblings a name-addressed lookup cannot, and reads/writes their attributes
    /// generically (<see cref="GetAttribute"/>/<see cref="SetAttribute"/>/<see cref="EditableAttributes"/>).
    /// </summary>
    /// <remarks>
    /// The handle stores only the stable <see cref="Id"/> and re-resolves its target from the session on each
    /// access, so it survives the session's per-mutation tree rebuilds and reflects edits made through it. Later
    /// backlog items hang the remaining generic mutators (delete, copy, move) off this same handle.
    /// </remarks>
    public sealed class ElementRef
    {
        private readonly ProjectEditor editor;

        internal ElementRef(ProjectEditor editor, ElementId id)
        {
            this.editor = editor;
            Id = id;
        }

        /// <summary>The stable <c>_0x</c> identity this handle addresses (unchanged for the element's life).</summary>
        public ElementId Id { get; }

        /// <summary>The element's tag (e.g. <c>functionblock</c>, <c>product_dataline</c>, <c>resource_input</c>).</summary>
        public string Tag => editor.Require(Id).Tag;

        /// <summary>
        /// The current immutable node this handle addresses, re-resolved from the session on each access so it
        /// reflects edits made through the handle. Throws if the element has since been deleted.
        /// </summary>
        public ProjectElement Element => editor.Require(Id);

        /// <summary>Reads the logical value of the named attribute, or <c>null</c> when it is absent.</summary>
        public string? GetAttribute(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return editor.Require(Id).GetAttribute(name);
        }

        /// <summary>
        /// Writes a declared attribute, validated against the element's schema: the name must be a declared
        /// attribute of this element type (and not its immutable <c>id</c>), and an enumerated attribute's value
        /// must be one of its permitted tokens. A value equal to the DTD default is dropped on serialize
        /// (omit-if-default), so setting an attribute back to its default clears it. Returns this handle for chaining.
        /// </summary>
        public ElementRef SetAttribute(string name, string value)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(value);
            AttrSchema attr = RequireWritableAttr(name);
            if (!attr.EnumValues.IsDefaultOrEmpty && !attr.EnumValues.Contains(value))
            {
                throw new ArgumentException(
                    $"'{value}' is not a permitted value for '{name}' on <{editor.Require(Id).Tag}>; " +
                    $"expected one of ({string.Join(" | ", attr.EnumValues)}).", nameof(value));
            }
            editor.SetAttributeById(Id, name, value);
            return this;
        }

        /// <summary>
        /// The element's editable attributes as property-grid rows (name, requirement kind, default, and any
        /// closed enumeration) — its own <c>id</c> excluded, since identity is not a user-editable property.
        /// </summary>
        public IReadOnlyList<AttrInfo> EditableAttributes()
        {
            ElementSchema schema = editor.SchemaView.Get(editor.Require(Id).Tag);
            var result = new List<AttrInfo>(schema.Attrs.Length);
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Render != AttrRender.Id)
                {
                    result.Add(AttrInfo.From(attr));
                }
            }
            return result;
        }

        /// <summary>
        /// Whether <paramref name="name"/> is a writable declared attribute of this element's type — the
        /// question <see cref="SetAttribute"/> answers by throwing.
        /// <para>Exists because a product's documentation attribute set varies by FAMILY, not by any coarser
        /// property: <c>product_rs485_led_dimmer</c> declares neither <c>power_group</c> nor
        /// <c>cabletype</c>/<c>cablenumber</c> and yet is not wireless, so a writer that gated on
        /// wired-versus-wireless threw on the OK button of a dialog that had opened perfectly well. Asking the
        /// schema is the only answer that stays correct as families are added.</para>
        /// <para>Answered from the SAME <c>SchemaView</c> that <see cref="SetAttribute"/> validates against, so
        /// the predicate and the write can never disagree. Use it to skip a field a family does not have —
        /// never to swallow a misspelled attribute name, which it cannot tell apart from an absent one.</para>
        /// </summary>
        public bool DeclaresAttribute(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementSchema declaringSchema = editor.SchemaView.Get(editor.Require(Id).Tag);
            foreach (AttrSchema attr in declaringSchema.Attrs)
            {
                if (attr.Name == name)
                {
                    return attr.Render != AttrRender.Id;
                }
            }
            return false;
        }

        private AttrSchema RequireWritableAttr(string name)
        {
            string tag = editor.Require(Id).Tag;
            ElementSchema schema = editor.SchemaView.Get(tag);
            foreach (AttrSchema attr in schema.Attrs)
            {
                if (attr.Name == name)
                {
                    return attr.Render == AttrRender.Id
                        ? throw new ArgumentException(
                            "The element id is identity and cannot be changed through the property surface.", nameof(name))
                        : attr;
                }
            }
            throw new ArgumentException($"'{name}' is not a declared attribute of <{tag}>.", nameof(name));
        }
    }
}
