#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ihc.App;

using Ihc.Vis.Catalog;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;
namespace Ihc.Vis
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
    /// The full engine is implemented: <c>Load</c>/<c>Save</c> with the byte-identical round-trip
    /// reader/writer/schema registry, <see cref="CreateNew"/> from the catalog File→New template,
    /// <see cref="Validate"/> (the pre-serialize checklist), catalog discovery
    /// (<see cref="GetAvailableProducts"/>/<see cref="GetAvailableFunctionBlocks"/>), and the controller bridge
    /// (<see cref="DownloadFrom"/>/<see cref="UploadTo"/>, which rides the same <c>Load</c>/<c>Save</c> stream
    /// overloads). Editing a loaded/created project starts via its <c>Edit()</c> extension. The catalog is the
    /// SDK-embedded <see cref="BuiltInCatalog"/>, materialized lazily on first catalog use, so no operation —
    /// file/controller IO, <see cref="CreateNew"/>, or the <c>GetAvailable*</c> methods — requires an IHC Visual
    /// install at runtime.
    /// </remarks>
    public sealed class ProjectAppService : AppServiceBase
    {
        private const string DefaultProjectFilename = "Project.ihc";

        private readonly IhcSettings settings;
        private readonly Lazy<CompositeCatalog> catalog;
        private readonly TimeProvider timeProvider;
        private readonly IControllerService? controller;
        // Only the controller bridge (DownloadFrom/UploadTo) authenticates; null for a file-only service.
        private readonly IAuthenticationService? authService;

        /// <summary>
        /// Creates a service from settings, with an optional <paramref name="controller"/> for the
        /// download/upload bridge (omit it for file-only use). Its catalog is the SDK-embedded
        /// <see cref="BuiltInCatalog"/> (materialized lazily, on first catalog use), so it needs no IHC Visual
        /// install at runtime — file/controller IO that needs no catalog never touches it, and
        /// <see cref="CreateNew"/>/<c>GetAvailable*</c> resolve against the embedded catalog. It uses the system
        /// clock (<see cref="TimeProvider.System"/>).
        /// </summary>
        public ProjectAppService(IhcSettings settings, IControllerService? controller = null)
            : this(settings,
                   // Lazy so the built-in catalog (~173 components) is not materialized until a catalog operation
                   // (CreateNew/GetAvailable*/Import) runs — file/controller IO needs no catalog. PublicationOnly
                   // never caches a factory exception.
                   new Lazy<ICatalog>(() => new BuiltInCatalog(), LazyThreadSafetyMode.PublicationOnly),
                   TimeProvider.System,
                   controller,
                   authService: null)
        {
        }

        /// <summary>
        /// Creates a service with an injected catalog and time provider (used by tests for determinism), with
        /// an optional <paramref name="controller"/> for the download/upload bridge and an optional
        /// <paramref name="authService"/> (tests inject a fake; production builds one from settings when a
        /// controller is present).
        /// </summary>
        public ProjectAppService(IhcSettings settings, ICatalog catalog, TimeProvider timeProvider,
                                 IControllerService? controller = null, IAuthenticationService? authService = null)
            : this(settings,
                   new Lazy<ICatalog>(catalog ?? throw new ArgumentNullException(nameof(catalog))),
                   timeProvider,
                   controller,
                   authService)
        {
        }

        private ProjectAppService(IhcSettings settings, Lazy<ICatalog> baseCatalog, TimeProvider timeProvider,
                                  IControllerService? controller, IAuthenticationService? authService)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(timeProvider);
            this.settings = settings;
            // Wrap the base catalog in a CompositeCatalog so runtime ImportCatalogFile/Directory can overlay extra
            // components (imported-wins) on top of the built-ins; an already-composite base is reused as-is. Lazy, so
            // the base is not materialized until a catalog operation (CreateNew/GetAvailable*/Import) runs — file and
            // controller IO still need no IHC Visual install.
            this.catalog = new Lazy<CompositeCatalog>(
                () => baseCatalog.Value as CompositeCatalog ?? new CompositeCatalog(baseCatalog.Value),
                LazyThreadSafetyMode.PublicationOnly);
            this.timeProvider = timeProvider;
            this.controller = controller;
            // Auth is only exercised on the controller bridge. Build it from settings when a controller is present
            // but the caller injected none (mirroring AdminAppService/InformationAppService); a file-only service
            // never authenticates, so it stays null.
            this.authService = authService ?? (controller is not null ? new AuthenticationService(settings) : null);
        }

        /// <summary>Authenticates with the controller if not already authenticated (the controller-bridge paths).</summary>
        private async Task EnsureAuthenticated()
        {
            IAuthenticationService auth = authService ?? throw new InvalidOperationException(
                $"This {nameof(ProjectAppService)} has no {nameof(IAuthenticationService)}; a controller-injecting " +
                "constructor supplies one for the download/upload bridge.");
            if (!await auth.IsAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext))
            {
                await auth.Authenticate().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            }
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
        public Project CreateNew(ProjectDetails details, SeedIdLayout seedLayout = SeedIdLayout.EnumsFirst)
        {
            ArgumentNullException.ThrowIfNull(details);
            using (var activity = StartActivity(nameof(CreateNew)))
            {
                try
                {
                    Project project = NewProjectBuilder.Build(catalog.Value, details, timeProvider.GetLocalNow(), seedLayout);
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

        /// <summary>Loads a project from a file path.</summary>
        public async Task<Project> Load(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            using (var activity = StartActivity(nameof(Load)))
            {
                try
                {
                    byte[] bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    Project project = ProjectReader.Read(bytes);
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

        /// <summary>Loads a project from a stream.</summary>
        public Task<Project> Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            using (var activity = StartActivity(nameof(Load)))
            {
                try
                {
                    Project project = ProjectReader.Read(stream);
                    activity?.SetReturnValue(project);
                    return Task.FromResult(project);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Saves a project to a file path. A <c>null</c> <paramref name="options"/> is treated as
        /// <see cref="ProjectSaveOptions.Default"/> (vendor-like re-stamping); pass
        /// <see cref="ProjectSaveOptions.PreserveExistingMetadata"/> for byte-exact round-trips.
        /// The write is atomic: bytes land in a same-directory temp file first and are swapped in with
        /// <see cref="File.Replace(string, string, string?)"/>, so a failed or interrupted save never
        /// truncates or destroys the existing file (and the previous content becomes <c>.BAK</c> in the
        /// same swap when <see cref="ProjectSaveOptions.CreateBackup"/> is set).
        /// </summary>
        public async Task Save(Project project, string path, ProjectSaveOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(path);
            ProjectSaveOptions effective = options ?? ProjectSaveOptions.Default;
            using (var activity = StartActivity(nameof(Save)))
            {
                try
                {
                    byte[] bytes = SerializeForSave(project, effective);
                    await WriteAtomically(path, bytes, effective).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    activity?.SetReturnValue(bytes.Length);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        private async Task WriteAtomically(string path, byte[] bytes, ProjectSaveOptions options)
        {
            string fullPath = Path.GetFullPath(path);
            string? backup = options.CreateBackup ? Path.ChangeExtension(fullPath, ".BAK") : null;
            if (backup is not null && string.Equals(Path.GetFullPath(backup), fullPath,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new IOException(
                    $"Cannot create a .BAK backup for '{path}': the target itself has the .BAK extension; " +
                    $"save under a different name or disable {nameof(ProjectSaveOptions.CreateBackup)}.");
            }
            string directory = Path.GetDirectoryName(fullPath)
                ?? throw new IOException($"'{path}' has no containing directory.");
            // Same directory ⇒ same volume, which File.Replace/File.Move need for an atomic rename.
            string temp = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                                       bufferSize: 4096, useAsync: true))
                {
                    await file.WriteAsync(bytes).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    file.Flush(flushToDisk: true);   // durable before the swap: a crash must leave old or new, never neither
                }
                if (File.Exists(fullPath))
                {
                    File.Replace(temp, fullPath, backup, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temp, fullPath);
                }
            }
            catch
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
        }

        /// <summary>
        /// Saves a project to a stream. A <c>null</c> <paramref name="options"/> is treated as
        /// <see cref="ProjectSaveOptions.Default"/> (vendor-like re-stamping) — this is the single point that
        /// normalizes the default, so callers such as <see cref="UploadTo"/> may forward a <c>null</c> through.
        /// </summary>
        public async Task Save(Project project, Stream stream, ProjectSaveOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(stream);
            using (var activity = StartActivity(nameof(Save)))
            {
                try
                {
                    byte[] bytes = SerializeForSave(project, options ?? ProjectSaveOptions.Default);
                    await stream.WriteAsync(bytes).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    activity?.SetReturnValue(bytes.Length);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Produces the on-disk bytes for a save: the default path re-stamps <c>id2</c>/<c>modified</c> from the
        /// clock (vendor-like); <see cref="ProjectSaveOptions.PreserveExistingMetadata"/> writes the project's
        /// metadata verbatim for byte-exact round-trips.
        /// </summary>
        private byte[] SerializeForSave(Project project, ProjectSaveOptions options)
        {
            if (options.ValidateBeforeSave)
            {
                ProjectValidationResult validation = ProjectValidator.Validate(project);
                if (!validation.IsValid)
                {
                    throw new ProjectValidationException(validation);
                }
            }
            Project toWrite = options.WriteMetadataVerbatim
                ? project
                : MetadataStamper.Restamp(project, timeProvider.GetLocalNow());
            byte[] bytes = ProjectSerializer.Serialize(toWrite);
            if (options.VerifyRoundTrip)
            {
                Project reparsed = ProjectReader.Read(new MemoryStream(bytes));
                // Tolerant comparison: the serializer omits a Defaulted attribute whose value equals its DTD default
                // (AttrSchema.OmitsOnWrite) and the reader never re-materializes it, so a model that explicitly
                // carried such an attribute is a FAITHFUL write even though a naive re-parse equality would differ.
                // Drop exactly those on both sides before comparing — Project equality is Root-only, so the stripped
                // roots compare directly — and a foreign file with an explicit default-equal attribute round-trips
                // while any genuine loss (a changed/absent non-default value, a dropped subtree) still diverges and
                // throws. Both schema views are memoized (the reader warms the reparsed one eagerly).
                ProjectElement expected = StripDefaultEqualAttrs(toWrite.Root, ProjectSchemaView.For(toWrite));
                ProjectElement actual = StripDefaultEqualAttrs(reparsed.Root, ProjectSchemaView.For(reparsed));
                if (!actual.Equals(expected))
                {
                    throw new InvalidOperationException(
                        "Serialize/re-parse mismatch: the written bytes do not reproduce the in-memory project" +
                        FirstDivergence(expected, actual, path: "utcs_project") +
                        " — the model holds state the .vis format cannot represent.");
                }
            }
            return bytes;
        }

        private static string FirstDivergence(ProjectElement expected, ProjectElement actual, string path)
        {
            if (expected.Tag != actual.Tag)
            {
                return $" (first divergence at {path}: element <{expected.Tag}> re-read as <{actual.Tag}>)";
            }
            var actualAttrs = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!actual.Attrs.IsDefaultOrEmpty)
            {
                foreach ((string name, string value) in actual.Attrs)
                {
                    actualAttrs[name] = value;
                }
            }
            if (!expected.Attrs.IsDefaultOrEmpty)
            {
                foreach ((string name, string value) in expected.Attrs)
                {
                    if (!actualAttrs.Remove(name, out string? reread))
                    {
                        return $" (first divergence at {path}/<{expected.Tag}>: attribute '{name}'='{value}' is absent after re-parse)";
                    }
                    if (reread != value)
                    {
                        return $" (first divergence at {path}/<{expected.Tag}>: attribute '{name}' expected '{value}', re-read '{reread}')";
                    }
                }
            }
            if (actualAttrs.Count > 0)
            {
                string extra = actualAttrs.Keys.First();
                return $" (first divergence at {path}/<{expected.Tag}>: attribute '{extra}' appears only after re-parse)";
            }
            int expectedCount = expected.Children.IsDefaultOrEmpty ? 0 : expected.Children.Length;
            int actualCount = actual.Children.IsDefaultOrEmpty ? 0 : actual.Children.Length;
            if (expectedCount != actualCount)
            {
                return $" (first divergence at {path}/<{expected.Tag}>: {expectedCount} children re-read as {actualCount})";
            }
            for (int i = 0; i < expectedCount; i++)
            {
                if (!expected.Children[i].Equals(actual.Children[i]))
                {
                    return FirstDivergence(expected.Children[i], actual.Children[i], $"{path}/<{expected.Tag}>[{i}]");
                }
            }
            return string.Empty;
        }

        // Drops every attribute the serializer omits on write (AttrSchema.OmitsOnWrite — the serializer's own omit
        // rule), recursively, so the round-trip verification compares only the state that actually reaches the
        // file: the benign omit-if-default asymmetry is normalized away on both sides, genuine differences are not.
        // Copy-on-write: an element with nothing to strip anywhere below it — the overwhelmingly common case, and
        // by construction always true for the reparsed side — is returned as-is, so the walk allocates nothing.
        private static ProjectElement StripDefaultEqualAttrs(ProjectElement element, ProjectSchemaView view)
        {
            ElementSchema? schema = view.TryGet(element.Tag);
            ImmutableArray<(string Name, string Value)> attrs = element.AttrsOrEmpty();
            ImmutableArray<(string, string)>.Builder? keptAttrs = null;   // created on the first dropped attribute
            for (int i = 0; i < attrs.Length; i++)
            {
                if (schema?.FindAttr(attrs[i].Name) is { } attr && attr.OmitsOnWrite(attrs[i].Value))
                {
                    if (keptAttrs is null)
                    {
                        keptAttrs = ImmutableArray.CreateBuilder<(string, string)>(attrs.Length);
                        for (int j = 0; j < i; j++) { keptAttrs.Add(attrs[j]); }
                    }
                    continue;
                }
                keptAttrs?.Add(attrs[i]);
            }
            ImmutableArray<ProjectElement> children = element.ChildrenOrEmpty();
            ImmutableArray<ProjectElement>.Builder? keptChildren = null;   // created on the first changed child
            for (int i = 0; i < children.Length; i++)
            {
                ProjectElement stripped = StripDefaultEqualAttrs(children[i], view);
                if (keptChildren is null && !ReferenceEquals(stripped, children[i]))
                {
                    keptChildren = ImmutableArray.CreateBuilder<ProjectElement>(children.Length);
                    for (int j = 0; j < i; j++) { keptChildren.Add(children[j]); }
                }
                keptChildren?.Add(stripped);
            }
            return keptAttrs is null && keptChildren is null
                ? element
                : new ProjectElement(element.Tag, element.Id,
                    keptAttrs?.ToImmutable() ?? attrs,
                    keptChildren?.ToImmutable() ?? children);
        }

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
                    await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    ProjectFile file = await controller.GetProject().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    if (file?.Data is null)
                    {
                        throw new InvalidOperationException(
                            "The controller returned no project — it likely has none stored. Check " +
                            $"{nameof(IControllerService)}.{nameof(IControllerService.IsIHCProjectAvailable)}() " +
                            $"before calling {nameof(DownloadFrom)}.");
                    }
                    // StrictEncoding (matching the upload side): a real controller only ever produces Latin-1 text,
                    // so an alternative IControllerService that hands back a >U+00FF char is a bug that must fail
                    // loudly here, not be silently transcoded to '?' by the lossy replacement fallback.
                    using MemoryStream ms = new MemoryStream(ProjectFile.StrictEncoding.GetBytes(file.Data));
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
        /// transitions). The project is validated first by default — the controller is the one sink with no
        /// <c>.BAK</c> to roll back to — throwing <see cref="ProjectValidationException"/> with the full result;
        /// pass <paramref name="validate"/> <c>false</c> to re-upload a foreign file with deviations the vendor
        /// tooling tolerates. A controller that declines the store surfaces as <see cref="ProjectUploadException"/>.
        /// Requires a controller-injecting constructor; throws <see cref="InvalidOperationException"/> on a
        /// file-only service. Does not reboot — call <c>IConfigurationService.DelayedReboot</c> separately if the
        /// controller should apply the new project immediately.
        /// </summary>
        public async Task<bool> UploadTo(Project project, ProjectSaveOptions? options = null,
                                         string? filename = null, bool validate = true)
        {
            ArgumentNullException.ThrowIfNull(project);
            IControllerService controller = RequireController();
            using (var activity = StartActivity(nameof(UploadTo)))
            {
                try
                {
                    await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    if (validate)
                    {
                        ProjectValidationResult validation = ProjectValidator.Validate(project);
                        if (!validation.IsValid)
                        {
                            throw new ProjectValidationException(validation);
                        }
                    }
                    // Always verify the write on this path (controller EPROM has no .BAK to roll back to) — a
                    // deliberate, documented postcondition, not a silent discard of the caller's option. The check is
                    // tolerant of the benign omit-if-default asymmetry, so a foreign file with an explicit
                    // default-equal attribute still uploads; only a genuinely non-reproducible model is refused.
                    ProjectSaveOptions effective = (options ?? ProjectSaveOptions.Default) with { VerifyRoundTrip = true };
                    // Serialize straight to the on-wire string — the controller takes a ProjectFile, not a stream — so
                    // no MemoryStream/ToArray copy is needed (the byte[] → string is the only conversion required).
                    ProjectFile file = new ProjectFile(filename ?? DefaultProjectFilename,
                                                       ProjectFile.Encoding.GetString(SerializeForSave(project, effective)));
                    bool stored = await controller.StoreProject(file).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    if (!stored)
                    {
                        throw new ProjectUploadException(
                            $"The controller declined {nameof(IControllerService.StoreProject)} after entering change " +
                            $"mode; its project state is uncertain — verify with " +
                            $"{nameof(IControllerService)}.{nameof(IControllerService.GetProjectInfo)} before retrying.");
                    }
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

        /// <summary>The products available for insertion, from the SDK-embedded catalog (plus any imported).</summary>
        public IReadOnlyList<ProductDefinition> GetAvailableProducts()
        {
            using (var activity = StartActivity(nameof(GetAvailableProducts)))
            {
                try
                {
                    IReadOnlyList<ProductDefinition> result = catalog.Value.Products;
                    activity?.SetReturnValue(result.Count);
                    return result;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>The function blocks available for insertion, from the SDK-embedded catalog (plus any imported).</summary>
        public IReadOnlyList<FunctionBlockDefinition> GetAvailableFunctionBlocks()
        {
            using (var activity = StartActivity(nameof(GetAvailableFunctionBlocks)))
            {
                try
                {
                    IReadOnlyList<FunctionBlockDefinition> result = catalog.Value.FunctionBlocks;
                    activity?.SetReturnValue(result.Count);
                    return result;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Imports one catalog component file at runtime so it resolves and inserts alongside the built-ins: a
        /// <c>.ifb</c> is read as a function block, any other extension (<c>.def</c>) as a product, via the same
        /// encoding/DTD-default/inline-DTD handling as install discovery
        /// (<see cref="CatalogReader.ReadProduct(string, ProductDocumentation?)"/>). The imported component shadows a
        /// built-in with the same key (imported-wins) and appears in <see cref="GetAvailableProducts"/> /
        /// <see cref="GetAvailableFunctionBlocks"/>. Pass <paramref name="documentationProbe"/> (e.g.
        /// <see cref="ReadSiblingDocumentation"/>) to attach help metadata from a sibling file; it maps the component
        /// path to summary text (or null for none).
        /// </summary>
        public void ImportCatalogFile(string path, Func<string, string?>? documentationProbe = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            using (var activity = StartActivity(nameof(ImportCatalogFile)))
            {
                try
                {
                    ImportFile(path, documentationProbe);
                    activity?.SetReturnValue(path);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Imports every product (<c>*.def</c>) and function-block (<c>*.ifb</c>) file found recursively under
        /// <paramref name="directory"/> (ordinal-sorted, so import order — and thus last-wins among the imports — is
        /// deterministic), returning the number imported. See <see cref="ImportCatalogFile"/> for per-file behavior and
        /// the <paramref name="documentationProbe"/> hook.
        /// </summary>
        public int ImportCatalogDirectory(string directory, Func<string, string?>? documentationProbe = null)
        {
            ArgumentNullException.ThrowIfNull(directory);
            using (var activity = StartActivity(nameof(ImportCatalogDirectory)))
            {
                try
                {
                    int count = 0;
                    foreach (string path in EnumerateCatalogFiles(directory))
                    {
                        ImportFile(path, documentationProbe);
                        count++;
                    }
                    activity?.SetReturnValue(count);
                    return count;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// A ready-made <c>documentationProbe</c> for <see cref="ImportCatalogFile"/>/<see cref="ImportCatalogDirectory"/>:
        /// reads a sibling <c>.syn_en</c> (else <c>.md</c>) help file next to the component and returns its full text
        /// as the documentation summary — mirroring the vendor <c>FunctionBlocks\*.md</c> help documents — or null when
        /// neither sibling exists. Opt in by passing this method as the probe argument.
        /// </summary>
        public static string? ReadSiblingDocumentation(string componentPath)
        {
            ArgumentNullException.ThrowIfNull(componentPath);
            foreach (string extension in new[] { ".syn_en", ".md" })
            {
                string sibling = Path.ChangeExtension(componentPath, extension);
                if (File.Exists(sibling))
                {
                    return File.ReadAllText(sibling, System.Text.Encoding.UTF8);   // sibling help docs are UTF-8 (all vendor docs validate as UTF-8)
                }
            }
            return null;
        }

        private void ImportFile(string path, Func<string, string?>? documentationProbe)
        {
            string? summary = documentationProbe?.Invoke(path);
            if (Path.GetExtension(path).Equals(".ifb", StringComparison.OrdinalIgnoreCase))
            {
                FunctionBlockDocumentation? documentation = summary is null
                    ? null
                    : new FunctionBlockDocumentation(summary, ImmutableDictionary<string, string>.Empty);
                catalog.Value.Import(CatalogReader.ReadFunctionBlock(path, documentation));
            }
            else
            {
                ProductDocumentation? documentation = summary is null
                    ? null
                    : new ProductDocumentation(summary, ImmutableDictionary<string, string>.Empty);
                catalog.Value.Import(CatalogReader.ReadProduct(path, documentation));
            }
        }

        private static IEnumerable<string> EnumerateCatalogFiles(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"Catalog import directory '{directory}' does not exist.");
            }
            return Directory.EnumerateFiles(directory, "*.def", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(directory, "*.ifb", SearchOption.AllDirectories))
                .OrderBy(p => p, StringComparer.Ordinal);
        }

        // To edit a project, call the project.Edit() extension on a loaded/created Project — there is no
        // service-level Edit, to keep a single mutation entry point.

        /// <summary>Validates a project against the pre-serialize checklist.</summary>
        public ProjectValidationResult Validate(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            using (var activity = StartActivity(nameof(Validate)))
            {
                ProjectValidationResult result = ProjectValidator.Validate(project);
                activity?.SetReturnValue(result);
                return result;
            }
        }
    }
}
