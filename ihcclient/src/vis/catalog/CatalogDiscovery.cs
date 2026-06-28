#nullable enable
using System;
using System.Collections.Generic;

namespace Ihc.Projects
{
    /// <summary>
    /// The catalog of products and function blocks available for insertion. Modelled as an interface so
    /// the editor/service can be driven by a fake catalog in tests (matching the SDK's interface-injection
    /// convention), without a real IHC Visual install on disk. <see cref="CatalogDiscovery"/> is the
    /// install-dir-backed implementation.
    /// </summary>
    public interface ICatalog
    {
        /// <summary>Looks a product up by its opaque <c>product_identifier</c> token (e.g. <c>_0x2101</c>).</summary>
        ProductDescriptor Product(string productIdentifier);

        /// <summary>Looks a function block up by its <c>master_type</c> key (e.g. <c>1.1.01</c>).</summary>
        FunctionBlockDescriptor FunctionBlock(string masterType);

        /// <summary>All discovered products.</summary>
        IReadOnlyList<ProductDescriptor> Products { get; }

        /// <summary>All discovered function blocks.</summary>
        IReadOnlyList<FunctionBlockDescriptor> FunctionBlocks { get; }
    }

    /// <summary>
    /// Auto-discovers the products and function blocks installed with IHC Visual by scanning
    /// <c>Products\**\*.def</c> (~100) and <c>FunctionBlocks\**\*.ifb</c> (~72) under the configured
    /// install dir. These catalog files are the source of truth for instance specifics; a <c>.vis</c>
    /// is fully self-sufficient once a component has been inserted (plan §2, §3.7).
    /// </summary>
    /// <remarks>
    /// Stage 1: signatures only. The discovery/parsing/caching is implemented in Stage 2.
    /// </remarks>
    public sealed class CatalogDiscovery : ICatalog
    {
        /// <summary>Builds a catalog by scanning the given IHC Visual install directory.</summary>
        public static CatalogDiscovery FromInstallDir(string installDir) =>
            throw new NotImplementedException();

        /// <inheritdoc/>
        public ProductDescriptor Product(string productIdentifier) =>
            throw new NotImplementedException();

        /// <inheritdoc/>
        public FunctionBlockDescriptor FunctionBlock(string masterType) =>
            throw new NotImplementedException();

        /// <inheritdoc/>
        public IReadOnlyList<ProductDescriptor> Products =>
            throw new NotImplementedException();

        /// <inheritdoc/>
        public IReadOnlyList<FunctionBlockDescriptor> FunctionBlocks =>
            throw new NotImplementedException();
    }
}
