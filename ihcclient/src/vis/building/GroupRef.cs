#nullable enable
using System;
using System.Globalization;

namespace Ihc.Projects
{
    /// <summary>
    /// A live handle to a single <c>group</c> (locality/room) in the edit session. Adds catalog products and
    /// function blocks to the room (deep-copying their bodies), and looks up existing ones by name for editing a
    /// loaded project.
    /// </summary>
    public sealed class GroupRef
    {
        private readonly ProjectEditor editor;

        internal GroupRef(ProjectEditor editor, ElementId id)
        {
            this.editor = editor;
            Id = id;
        }

        internal ElementId Id { get; }

        /// <summary>Renames this room in place (an attribute edit — allocates nothing, R3). Returns this for chaining.</summary>
        public GroupRef Name(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            editor.SetAttributeById(Id, "name", name);
            return this;
        }

        /// <summary>Sets this room's note (appended after the existing attributes). Returns this for chaining.</summary>
        public GroupRef Note(string note)
        {
            ArgumentNullException.ThrowIfNull(note);
            editor.SetAttributeById(Id, "note", note);
            return this;
        }

        /// <summary>
        /// Deep-copies the given catalog product into this room and returns its live handle for instance-level editing.
        /// </summary>
        public ProductRef AddProduct(ProductDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ElementId productId = editor.InsertComponent(Id, descriptor.Body, descriptor.InlineDtdBlocks);
            return new ProductRef(editor, productId);
        }

        /// <summary>
        /// Deep-copies the given catalog function block (including its catalog internals: programs, resources,
        /// settings) into this room and returns its live handle.
        /// </summary>
        public FunctionBlockRef AddFunctionBlock(FunctionBlockDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ElementId blockId = editor.InsertComponent(Id, descriptor.Body, descriptor.InlineDtdBlocks);
            return new FunctionBlockRef(editor, blockId);
        }

        /// <summary>
        /// Scaffolds a from-scratch empty function block ("Tom blok") into this room from the catalog's
        /// <see cref="ICatalog.EmptyFunctionBlockTemplate"/> (<c>Data\fb.def</c>): the five mandatory containers in
        /// fixed order plus one empty <c>program_simple(events, actions)</c>, vendor icon <c>_0xf</c> and fresh ids.
        /// The block carries no LK/Schneider master identity — only its <paramref name="created"/> date is stamped
        /// (<c>master_date_year</c>/<c>_month</c>/<c>_day</c>), matching what IHC Visual writes for an authored block.
        /// Returns the block's live handle for adding pins/variables.
        /// </summary>
        public FunctionBlockRef AddEmptyFunctionBlock(FunctionBlockDescriptor template, DateOnly created,
            string name = "Tom blok")
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(name);
            if (!template.IsEmptyTemplate)
            {
                throw new ArgumentException(
                    $"'{template.DisplayName}' is a full catalog block, not the empty 'Tom blok' template — " +
                    $"renaming and re-dating it would forge its identity. Use {nameof(AddFunctionBlock)} for " +
                    $"catalog blocks, or pass {nameof(ICatalog)}.{nameof(ICatalog.EmptyFunctionBlockTemplate)}.",
                    nameof(template));
            }
            ElementId blockId = editor.InsertComponent(Id, template.Body, template.InlineDtdBlocks);
            editor.SetAttributeById(blockId, "name", name);
            editor.SetAttributeById(blockId, "master_date_year", created.Year.ToString(CultureInfo.InvariantCulture));
            editor.SetAttributeById(blockId, "master_date_month", created.Month.ToString(CultureInfo.InvariantCulture));
            editor.SetAttributeById(blockId, "master_date_day", created.Day.ToString(CultureInfo.InvariantCulture));
            return new FunctionBlockRef(editor, blockId);
        }

        /// <summary>
        /// Clones an existing in-project subtree (by id) into this room — the clipboard paste — deep-copying it with
        /// fresh ids (type-code suffix preserved), remapped internal IDREFs and shared enums, governing cross-boundary
        /// follow-link halves by <paramref name="policy"/> (default: drop them). Returns a live handle to the paste.
        /// </summary>
        public ElementRef PasteInto(ElementId sourceId, LinkCopyPolicy policy = LinkCopyPolicy.DropExternal)
        {
            ElementId copyId = editor.CopySubtree(sourceId, Id, policy);
            if (!editor.TryResolve(copyId, out ElementRef? handle))
            {
                throw new InvalidOperationException(
                    $"Paste of {sourceId.ToToken()} produced no live element: the copied subtree was a bare " +
                    $"follow-link half whose partner lies outside the copy, so {nameof(LinkCopyPolicy)}." +
                    $"{nameof(LinkCopyPolicy.DropExternal)} removed the entire copy. Copy the owning resource instead.");
            }
            return handle;
        }

        /// <summary>
        /// Looks up an existing product in this room by name (for editing a loaded project) — any product family
        /// (<c>product_dataline</c>, <c>product_airlink</c>, rs485 variants, <c>s0_device</c>, …) — returning its
        /// live handle. Note the dataline-specific I/O methods on <see cref="ProductRef"/> reject other families.
        /// </summary>
        public ProductRef Product(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = editor.FindChildIdByName(Id, IsProductFamilyTag, name)
                ?? throw new InvalidOperationException($"No product named '{name}' in this room.");
            return new ProductRef(editor, id);
        }

        private static bool IsProductFamilyTag(string tag) =>
            tag == "s0_device" || tag.StartsWith("product_", StringComparison.Ordinal);

        /// <summary>
        /// Looks up an existing function block in this room by name (for editing a loaded project), returning its
        /// live handle.
        /// </summary>
        public FunctionBlockRef FunctionBlock(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = editor.FindChildIdByName(Id, "functionblock", name)
                ?? throw new InvalidOperationException($"No function block named '{name}' in this room.");
            return new FunctionBlockRef(editor, id);
        }

        /// <summary>
        /// Removes a product from this room, cascading the reciprocal follow-link halves outside it that point into
        /// its resources (via <see cref="ProjectEditor.DeleteById"/>). The product's <c>_0x</c> ids are retired
        /// permanently — deletes leave counter holes and ids are never reused.
        /// </summary>
        public void RemoveProduct(ProductRef product)
        {
            ArgumentNullException.ThrowIfNull(product);
            editor.DeleteById(product.Id);
        }

        /// <summary>
        /// Removes a function block from this room, cascading the reciprocal follow-link halves outside it that
        /// point into its resources (via <see cref="ProjectEditor.DeleteById"/>). Retired ids are not reused.
        /// </summary>
        public void RemoveFunctionBlock(FunctionBlockRef functionBlock)
        {
            ArgumentNullException.ThrowIfNull(functionBlock);
            editor.DeleteById(functionBlock.Id);
        }
    }
}
