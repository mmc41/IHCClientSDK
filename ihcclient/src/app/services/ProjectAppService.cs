#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ihc.Vis.Catalog;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Validation;

namespace Ihc.App
{
    /// <summary>
    /// High-level, tech-agnostic backend for loading, saving, creating and editing IHC project
    /// (<c>.vis</c>) files as pure C# business logic — the file-based counterpart to the
    /// controller-backed application services. Intended as the backend for a future GUI that
    /// replicates IHC Visual without any GUI/infrastructure concerns baked in.
    /// </summary>
    /// <remarks>
    /// Stage 1: this is the authoring-API preview surface. The bodies are stubs; the byte-identical
    /// IO, catalog discovery, validation and builder engine are delivered in Stage 2.
    /// </remarks>
    public sealed class ProjectAppService : AppServiceBase
    {
        /// <summary>
        /// Creates a service from settings only; it builds its own <see cref="CatalogDiscovery"/> from
        /// <see cref="IhcSettings.IhcVisualInstallDir"/> and uses the system clock.
        /// </summary>
        public ProjectAppService(IhcSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
        }

        /// <summary>
        /// Creates a service with an injected catalog and clock (used by tests for determinism).
        /// </summary>
        public ProjectAppService(IhcSettings settings, ICatalog catalog, IVisClock clock)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(clock);
        }

        /// <summary>
        /// Creates a new empty project replicating IHC Visual's File→New: seeds the default rooms, the
        /// two built-in enums and the fixed skeleton from the catalog's <c>NewDoc.idf</c> /
        /// <c>EnumeratorDefinitions.def</c>. Because it reads those template files it is an instance
        /// operation (like <c>Load</c>/<c>Save</c>), using the service's injected catalog and clock.
        /// <c>id1</c>/<c>id2</c>/<c>modified</c> are stamped from the clock at creation time; a later
        /// <c>Save</c> re-stamps <c>id2</c>.
        /// </summary>
        public VisProject CreateNew(VisProjectInfo projectInfo, InstallerInfo installer) =>
            throw new NotImplementedException();

        /// <summary>
        /// Serializes a project to bytes exactly as-is — no clock, no re-stamping (used directly by
        /// byte-fidelity tests). Metadata re-stamping is the job of the instance <see cref="Save(VisProject, string, VisSaveOptions?)"/>
        /// overloads, which hold the clock; this pure serializer writes whatever metadata the project already carries.
        /// </summary>
        public static byte[] SaveToBytes(VisProject project) =>
            throw new NotImplementedException();

        /// <summary>Loads a project from a file path.</summary>
        public Task<VisProject> Load(string path) => throw new NotImplementedException();

        /// <summary>Loads a project from a stream.</summary>
        public Task<VisProject> Load(Stream stream) => throw new NotImplementedException();

        /// <summary>
        /// Saves a project to a file path. Defaults to vendor-like re-stamping; pass
        /// <see cref="VisSaveOptions.PreserveExistingMetadata"/> for byte-exact round-trips.
        /// </summary>
        public Task Save(VisProject project, string path, VisSaveOptions? options = null) =>
            throw new NotImplementedException();

        /// <summary>Saves a project to a stream.</summary>
        public Task Save(VisProject project, Stream stream, VisSaveOptions? options = null) =>
            throw new NotImplementedException();

        /// <summary>The products available for insertion, discovered from the install dir.</summary>
        public IReadOnlyList<ProductDescriptor> GetAvailableProducts() =>
            throw new NotImplementedException();

        /// <summary>The function blocks available for insertion, discovered from the install dir.</summary>
        public IReadOnlyList<FunctionBlockDescriptor> GetAvailableFunctionBlocks() =>
            throw new NotImplementedException();

        // To edit a project, call the project.Edit() extension (Ihc.Vis.Building) on a loaded/created
        // VisProject — there is no service-level Edit, to keep a single mutation entry point.

        /// <summary>Validates a project against the pre-serialize checklist.</summary>
        public ValidationResult Validate(VisProject project) => throw new NotImplementedException();
    }
}
