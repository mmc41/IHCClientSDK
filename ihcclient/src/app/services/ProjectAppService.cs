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
        private readonly Lazy<ICatalog> catalog;
        private readonly TimeProvider timeProvider;
        private readonly IControllerService? controller;

        /// <summary>
        /// Creates a service from settings, with an optional <paramref name="controller"/> for the
        /// download/upload bridge (omit it for file-only use). It builds its own <see cref="CatalogDiscovery"/>
        /// from <see cref="IhcSettings.IhcVisualInstallDir"/> (lazily, on first catalog use, so file/controller
        /// IO that needs no catalog never requires an IHC Visual install) and uses the system clock
        /// (<see cref="TimeProvider.System"/>).
        /// </summary>
        public ProjectAppService(IhcSettings settings, IControllerService? controller = null)
            : this(settings,
                   new Lazy<ICatalog>(() => CatalogDiscovery.FromInstallDir(settings.IhcVisualInstallDir)),
                   TimeProvider.System,
                   controller)
        {
        }

        /// <summary>
        /// Creates a service with an injected catalog and time provider (used by tests for determinism), with
        /// an optional <paramref name="controller"/> for the download/upload bridge.
        /// </summary>
        public ProjectAppService(IhcSettings settings, ICatalog catalog, TimeProvider timeProvider,
                                 IControllerService? controller = null)
            : this(settings,
                   new Lazy<ICatalog>(catalog ?? throw new ArgumentNullException(nameof(catalog))),
                   timeProvider,
                   controller)
        {
        }

        private ProjectAppService(IhcSettings settings, Lazy<ICatalog> catalog, TimeProvider timeProvider,
                                  IControllerService? controller)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(timeProvider);
            this.settings = settings;
            this.catalog = catalog;
            this.timeProvider = timeProvider;
            this.controller = controller;
        }

        private IControllerService RequireController() =>
            controller ?? throw new InvalidOperationException(
                $"This {nameof(ProjectAppService)} was created without an {nameof(IControllerService)}; " +
                $"use a controller-injecting constructor to call {nameof(DownloadFrom)}/{nameof(UploadTo)}.");

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
        /// Saves a project to a file path. A <c>null</c> <paramref name="options"/> is treated as
        /// <see cref="ProjectSaveOptions.Default"/> (vendor-like re-stamping); pass
        /// <see cref="ProjectSaveOptions.PreserveExistingMetadata"/> for byte-exact round-trips.
        /// </summary>
        public Task Save(Project project, string path, ProjectSaveOptions? options = null) =>
            throw new NotImplementedException();

        /// <summary>
        /// Saves a project to a stream. A <c>null</c> <paramref name="options"/> is treated as
        /// <see cref="ProjectSaveOptions.Default"/> (vendor-like re-stamping) — this is the single point that
        /// normalizes the default, so callers such as <see cref="UploadTo"/> may forward a <c>null</c> through.
        /// </summary>
        public Task Save(Project project, Stream stream, ProjectSaveOptions? options = null) =>
            throw new NotImplementedException();

        /// <summary>
        /// Downloads the project from the injected controller and parses it into a <see cref="Project"/>. The
        /// controller blob is the gzip-compressed form of the same <c>utcs_project</c> XML a <c>.vis</c>
        /// holds — <see cref="IControllerService.GetProject"/> already decompresses it — so this reuses the
        /// same reader as <see cref="Load(Stream)"/>. Requires a controller-injecting constructor; throws
        /// <see cref="InvalidOperationException"/> on a file-only service.
        /// </summary>
        public async Task<Project> DownloadFrom()
        {
            IControllerService controller = RequireController();
            using (var activity = StartActivity(nameof(DownloadFrom)))
            {
                try
                {
                    ProjectFile file = await controller.GetProject().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    using MemoryStream ms = new MemoryStream(ProjectFile.Encoding.GetBytes(file.Data));
                    Project project = await Load(ms).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    activity?.SetReturnValue(project);
                    return project;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Serializes a project and uploads it to the injected controller. Re-stamps <c>id2</c>/<c>modified</c>
        /// like a vendor save by default; pass <see cref="ProjectSaveOptions.PreserveExistingMetadata"/> for a
        /// byte-exact re-upload. Reuses <see cref="Save(Project, Stream, ProjectSaveOptions)"/> and
        /// <see cref="IControllerService.StoreProject"/> (which handles gzip + the controller change-mode
        /// transitions). Requires a controller-injecting constructor; throws
        /// <see cref="InvalidOperationException"/> on a file-only service. Does not reboot — call
        /// <c>IConfigurationService.DelayedReboot</c> separately if the controller should apply the new project
        /// immediately.
        /// </summary>
        public async Task<bool> UploadTo(Project project, ProjectSaveOptions? options = null,
                                         string? filename = null)
        {
            ArgumentNullException.ThrowIfNull(project);
            IControllerService controller = RequireController();
            using (var activity = StartActivity(nameof(UploadTo)))
            {
                try
                {
                    using MemoryStream ms = new MemoryStream();
                    await Save(project, ms, options).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    ProjectFile file = new ProjectFile(filename ?? DefaultProjectFilename,
                                                       ProjectFile.Encoding.GetString(ms.ToArray()));
                    bool stored = await controller.StoreProject(file).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    activity?.SetReturnValue(stored);
                    return stored;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>The products available for insertion, discovered from the install dir.</summary>
        public IReadOnlyList<ProductDescriptor> GetAvailableProducts() => catalog.Value.Products;

        /// <summary>The function blocks available for insertion, discovered from the install dir.</summary>
        public IReadOnlyList<FunctionBlockDescriptor> GetAvailableFunctionBlocks() => catalog.Value.FunctionBlocks;

        // To edit a project, call the project.Edit() extension on a loaded/created Project — there is no
        // service-level Edit, to keep a single mutation entry point.

        /// <summary>Validates a project against the pre-serialize checklist.</summary>
        public ProjectValidationResult Validate(Project project) => throw new NotImplementedException();
    }
}
