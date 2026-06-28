#nullable enable
using System;

namespace Ihc.Projects
{
    /// <summary>
    /// A live handle to a single <c>group</c> (locality/room) in the edit session. Adds catalog
    /// products and function blocks to the room, returning their live handles for further editing
    /// and linking.
    /// </summary>
    /// <remarks>Stage 1: full method signatures, stub bodies.</remarks>
    public sealed class GroupRef
    {
        internal GroupRef()
        {
        }

        /// <summary>
        /// Deep-copies the given catalog product into this room and returns its live handle for
        /// instance-level editing.
        /// </summary>
        public ProductRef AddProduct(ProductDescriptor descriptor) => throw new NotImplementedException();

        /// <summary>
        /// Deep-copies the given catalog function block (including its catalog internals: programs,
        /// resources, settings) into this room and returns its live handle.
        /// </summary>
        public FunctionBlockRef AddFunctionBlock(FunctionBlockDescriptor descriptor) => throw new NotImplementedException();

        /// <summary>
        /// Looks up an existing product in this room by name (for editing a loaded project), returning
        /// its live handle.
        /// </summary>
        public ProductRef Product(string name) => throw new NotImplementedException();

        /// <summary>
        /// Looks up an existing function block in this room by name (for editing a loaded project),
        /// returning its live handle.
        /// </summary>
        public FunctionBlockRef FunctionBlock(string name) => throw new NotImplementedException();

        /// <summary>
        /// Removes a product from this room (and any links to/from its resources). The product's
        /// <c>_0x</c> ids are retired permanently — deletes leave counter holes and ids are never reused
        /// (plan §3.4). The passed handle is dead afterwards.
        /// </summary>
        public void RemoveProduct(ProductRef product) => throw new NotImplementedException();

        /// <summary>
        /// Removes a function block from this room (and any links to/from its resources). Retired ids are
        /// not reused. The passed handle is dead afterwards.
        /// </summary>
        public void RemoveFunctionBlock(FunctionBlockRef functionBlock) => throw new NotImplementedException();
    }
}
