#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ihc.App;

namespace Ihc.Projects
{
    /// <summary>
    /// High-level, tech-agnostic backend for loading, saving, creating and editing IHC project files as pure
    /// C# business logic — the single door for project IO. A project is one <c>utcs_project</c> v4.0 model
    /// (<see cref="Project"/>) regardless of where it lives: a desktop <c>.vis</c> file (<see cref="Load(string)"/>
    /// / <see cref="Save(Project, string, ProjectSaveOptions)"/>) or a live controller (<see cref="DownloadFrom"/>
    /// / <see cref="UploadTo"/>). Intended as the backend for a future GUI that replicates IHC Visual without any
    /// GUI/infrastructure concerns baked in.
    /// </summary>
    /// <remarks>
    /// Stage 1: the file engine (<c>Load</c>/<c>Save</c>/<c>CreateNew</c>/<c>Validate</c>) bodies are stubs; the
    /// byte-identical IO, catalog discovery, validation and builder engine are delivered in Stage 2. The
    /// controller bridge (<see cref="DownloadFrom"/>/<see cref="UploadTo"/>) is implemented now by reusing the
    /// <c>Load</c>/<c>Save</c> stream overloads, so it lights up automatically once Stage 2 lands.
    /// </remarks>
    public sealed class ProjectAppService : AppServiceBase
    {
        private const string DefaultProjectFilename = "Project.ihc";

        private readonly IhcSettings settings;

        /// <summary>
        /// Creates a service from settings only; it builds its own <see cref="CatalogDiscovery"/> from
        /// <see cref="IhcSettings.IhcVisualInstallDir"/> and uses the system clock (<see cref="TimeProvider.System"/>).
        /// </summary>
        public ProjectAppService(IhcSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            this.settings = settings;
        }

        /// <summary>
        /// Creates a service with an injected catalog and time provider (used by tests for determinism).
        /// </summary>
        public ProjectAppService(IhcSettings settings, ICatalog catalog, TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(timeProvider);
            this.settings = settings;
        }

        /// <summary>
        /// Creates a new empty project replicating IHC Visual's File→New: seeds the default rooms, the
        /// two built-in enums and the fixed skeleton from the catalog's <c>NewDoc.idf</c> /
        /// <c>EnumeratorDefinitions.def</c>. Because it reads those template files it is an instance
        /// operation (like <c>Load</c>/<c>Save</c>), using the service's injected catalog and time provider.
        /// <c>id1</c>/<c>id2</c>/<c>modified</c> are stamped from the clock at creation time; a later
        /// <c>Save</c> re-stamps <c>id2</c>.
        /// </summary>
        public Project CreateNew(ProjectDetails details) => throw new NotImplementedException();

        /// <summary>Loads a project from a file path.</summary>
        public Task<Project> Load(string path) => throw new NotImplementedException();

        /// <summary>Loads a project from a stream.</summary>
        public Task<Project> Load(Stream stream) => throw new NotImplementedException();

        /// <summary>
        /// Saves a project to a file path. Defaults to vendor-like re-stamping; pass
        /// <see cref="ProjectSaveOptions.PreserveExistingMetadata"/> for byte-exact round-trips.
        /// </summary>
        public Task Save(Project project, string path, ProjectSaveOptions? options = null) =>
            throw new NotImplementedException();

        /// <summary>Saves a project to a stream.</summary>
        public Task Save(Project project, Stream stream, ProjectSaveOptions? options = null) =>
            throw new NotImplementedException();

        /// <summary>
        /// Downloads the project from a live controller and parses it into a <see cref="Project"/>. The
        /// controller blob is the gzip-compressed form of the same <c>utcs_project</c> XML a <c>.vis</c>
        /// holds — <see cref="IControllerService.GetProject"/> already decompresses it — so this reuses the
        /// same reader as <see cref="Load(Stream)"/>.
        /// </summary>
        public async Task<Project> DownloadFrom(IControllerService controller)
        {
            ArgumentNullException.ThrowIfNull(controller);
            ProjectFile file = await controller.GetProject().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            using MemoryStream ms = new MemoryStream(ProjectFile.Encoding.GetBytes(file.Data));
            return await Load(ms).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Serializes a project and uploads it to a live controller. Re-stamps <c>id2</c>/<c>modified</c> like a
        /// vendor save by default; pass <see cref="ProjectSaveOptions.PreserveExistingMetadata"/> for a byte-exact
        /// re-upload. Reuses <see cref="Save(Project, Stream, ProjectSaveOptions)"/> and
        /// <see cref="IControllerService.StoreProject"/> (which handles gzip + the controller change-mode
        /// transitions). Does not reboot — call <c>IConfigurationService.DelayedReboot</c> separately if the
        /// controller should apply the new project immediately.
        /// </summary>
        public async Task<bool> UploadTo(IControllerService controller, Project project,
                                         ProjectSaveOptions? options = null, string? filename = null)
        {
            ArgumentNullException.ThrowIfNull(controller);
            ArgumentNullException.ThrowIfNull(project);
            using MemoryStream ms = new MemoryStream();
            await Save(project, ms, options ?? ProjectSaveOptions.Default).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            ProjectFile file = new ProjectFile(filename ?? DefaultProjectFilename,
                                               ProjectFile.Encoding.GetString(ms.ToArray()));
            return await controller.StoreProject(file).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>The products available for insertion, discovered from the install dir.</summary>
        public IReadOnlyList<ProductDescriptor> GetAvailableProducts() =>
            throw new NotImplementedException();

        /// <summary>The function blocks available for insertion, discovered from the install dir.</summary>
        public IReadOnlyList<FunctionBlockDescriptor> GetAvailableFunctionBlocks() =>
            throw new NotImplementedException();

        // To edit a project, call the project.Edit() extension on a loaded/created Project — there is no
        // service-level Edit, to keep a single mutation entry point.

        /// <summary>Validates a project against the pre-serialize checklist.</summary>
        public ProjectValidationResult Validate(Project project) => throw new NotImplementedException();
    }
}
