#nullable enable
using System;

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
        /// Looks up an existing product in this room by name (for editing a loaded project), returning its live handle.
        /// </summary>
        public ProductRef Product(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ElementId id = editor.FindChildIdByName(Id, "product_dataline", name)
                ?? throw new InvalidOperationException($"No product named '{name}' in this room.");
            return new ProductRef(editor, id);
        }

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
        /// Removes a product from this room (and any links to/from its resources). The product's <c>_0x</c> ids
        /// are retired permanently — deletes leave counter holes and ids are never reused.
        /// </summary>
        public void RemoveProduct(ProductRef product)
        {
            ArgumentNullException.ThrowIfNull(product);
            editor.RemoveSubtree(product.Id);
        }

        /// <summary>
        /// Removes a function block from this room (and any links to/from its resources). Retired ids are not reused.
        /// </summary>
        public void RemoveFunctionBlock(FunctionBlockRef functionBlock)
        {
            ArgumentNullException.ThrowIfNull(functionBlock);
            editor.RemoveSubtree(functionBlock.Id);
        }
    }
}
