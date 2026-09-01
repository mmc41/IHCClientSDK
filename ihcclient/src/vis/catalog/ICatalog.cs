using System.Collections.Generic;

using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Model;
using Ihc.Vis.Products;

namespace Ihc.Vis.Catalog
{
    /// <summary>
    /// The catalog of products and function blocks available for insertion, plus the File→New templates. Modelled
    /// as an interface so the editor/service can be driven by a fake catalog in tests (matching the SDK's
    /// interface-injection convention) without a real IHC Visual install on disk. <see cref="MaterializedCatalog"/>
    /// is the concrete, source-agnostic implementation.
    /// </summary>
    /// <remarks>
    /// This interface is the catalog <b>provider seam</b>: it returns the level-owned definition models
    /// (<see cref="ProductDefinition"/>, <see cref="FunctionBlockDefinition"/>) without owning how they are
    /// sourced. <see cref="CatalogDiscovery"/> materializes one by scanning an IHC Visual install; an SDK-embedded
    /// <c>BuiltInCatalog</c> materializes one from code-authored builder invocations, letting the SDK create and
    /// insert components without any desktop application present. Its home is this <c>Ihc.Vis.Catalog</c> namespace,
    /// alongside <see cref="CatalogDiscovery"/> and <see cref="MaterializedCatalog"/>.
    /// </remarks>
    public interface ICatalog
    {
        /// <summary>Looks a product up by its opaque <c>product_identifier</c> token (e.g. <c>_0x2101</c>).</summary>
        ProductDefinition Product(string productIdentifier);

        /// <summary>Looks a function block up by its <c>master_type</c> key (e.g. <c>1.1.01</c>).</summary>
        FunctionBlockDefinition FunctionBlock(string masterType);

        /// <summary>
        /// Looks a function block up by its display name — for user-saved library blocks (e.g. <c>AutoProof</c>) that
        /// carry no <c>master_type</c> and so cannot be found via <see cref="FunctionBlock"/>.
        /// </summary>
        FunctionBlockDefinition FunctionBlockByName(string name);

        /// <summary>All discovered products.</summary>
        IReadOnlyList<ProductDefinition> Products { get; }

        /// <summary>All discovered function blocks.</summary>
        IReadOnlyList<FunctionBlockDefinition> FunctionBlocks { get; }

        /// <summary>
        /// The parsed <c>Data\NewDoc.idf</c> File→New skeleton (legacy v1 format, DTD defaults applied) — the
        /// source of the ten default rooms and the fixed template ids used by <see cref="ProjectAppService.CreateNew"/>.
        /// </summary>
        ProjectElement NewProjectSkeleton { get; }

        /// <summary>
        /// The parsed <c>Data\EnumeratorDefinitions.def</c> — the two built-in enums (Persienne tilstand, Logning)
        /// seeded into every new project, matched by <c>typeid</c>.
        /// </summary>
        ProjectElement BuiltInEnumerators { get; }

        /// <summary>
        /// The empty function-block ("Tom blok") template parsed from <c>Data\fb.def</c> — the five containers in
        /// fixed order plus one empty <c>program_simple(events, actions)</c> and vendor icon <c>_0xf</c>. Deep-copied
        /// by <see cref="Ihc.Vis.Editing.GroupRef.AddEmptyFunctionBlock"/> to scaffold a from-scratch block (spec ch. 09 §9.4.4).
        /// </summary>
        FunctionBlockDefinition EmptyFunctionBlockTemplate { get; }
    }
}
