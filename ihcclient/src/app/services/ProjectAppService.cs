#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ihc.App;

using Ihc.Vis.Problems;
using Ihc.Vis.Catalog;
using Ihc.Vis.Editing;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Reporting;
using Ihc.Vis.Schema;
using Ihc.Vis.Session;
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
    /// overloads). Editing a loaded/created project goes through the <see cref="Commands"/> gateway — the single
    /// discoverable authoring door (a stateless <see cref="ProjectCommands"/> planner that mints the undoable
    /// command vocabulary a frontend applies through its session); its <c>project.Edit()</c> extension is the
    /// low-level mutation entry the gateway is built on. <b>Executing</b> those commands has two doors (crudarch
    /// D01): an INTERACTIVE frontend calls <see cref="OpenDocument"/> and drives every edit through the returned
    /// <see cref="IProjectDocument"/> (labelled undo/redo, dirty/version, change events); ONE-SHOT callers use the
    /// stateless <see cref="Apply(Project, ProjectCommand)"/> / <see cref="Apply{T}(Project, ProjectCommand{T})"/> /
    /// <see cref="CanApply"/> / <see cref="Preview"/>, which run one command against a project on a throwaway
    /// single-use session and return the resulting <see cref="ProjectApplyResult"/>. The catalog is the
    /// SDK-embedded <see cref="BuiltInCatalog"/>, materialized lazily on first catalog use, so no operation —
    /// file/controller IO, <see cref="CreateNew"/>, or the <c>GetAvailable*</c> methods — requires an IHC Visual
    /// install at runtime.
    /// </remarks>
    public sealed class ProjectAppService : AppServiceBase
    {
        private const string DefaultProjectFilename = "Project.ihc";

        private readonly IhcSettings settings;
        private readonly Lazy<CompositeCatalog> catalog;
        private readonly Lazy<ILibraryBlockSource> library;
        private readonly TimeProvider timeProvider;
        private readonly IControllerService? controller;
        // Only the controller bridge (DownloadFrom/UploadTo) authenticates; null for a file-only service.
        private readonly IAuthenticationService? authService;

        /// <summary>
        /// Creates a file-only service (no controller bridge). Its catalog is the SDK-embedded
        /// <see cref="BuiltInCatalog"/> (materialized lazily, on first catalog use), so it needs no IHC Visual
        /// install at runtime — file IO that needs no catalog never touches it, and
        /// <see cref="CreateNew"/>/<c>GetAvailable*</c> resolve against the embedded catalog. It uses the system
        /// clock (<see cref="TimeProvider.System"/>). For the download/upload bridge, use
        /// <see cref="CreateWithControllerBridge"/> (settings-based) or the matching
        /// controller+auth constructor.
        /// </summary>
        public ProjectAppService(IhcSettings settings)
            : this(settings,
                   // Lazy so the built-in catalog (~173 components) is not materialized until a catalog operation
                   // (CreateNew/GetAvailable*/Import) runs — file IO needs no catalog. PublicationOnly never caches
                   // a factory exception.
                   new Lazy<ICatalog>(() => new BuiltInCatalog(), LazyThreadSafetyMode.PublicationOnly),
                   TimeProvider.System,
                   controller: null,
                   authService: null)
        {
        }

        /// <summary>
        /// Creates a controller-bridge service from an already-matched <paramref name="controller"/> and the
        /// <paramref name="authService"/> whose cookie session that controller rides (both required). The bridge
        /// authenticates exactly the session the controller uses — it never self-builds a second, foreign auth
        /// (the R0 defect). Use <see cref="CreateWithControllerBridge"/> to build both from settings. Uses the
        /// SDK-embedded <see cref="BuiltInCatalog"/> (lazy) and the system clock.
        /// </summary>
        public ProjectAppService(IhcSettings settings, IControllerService controller, IAuthenticationService authService)
            : this(settings,
                   new Lazy<ICatalog>(() => new BuiltInCatalog(), LazyThreadSafetyMode.PublicationOnly),
                   TimeProvider.System,
                   controller ?? throw new ArgumentNullException(nameof(controller)),
                   authService ?? throw new ArgumentNullException(nameof(authService)))
        {
        }

        /// <summary>
        /// Builds a settings-based controller bridge the way <see cref="Ihc.App.InformationAppService"/> does:
        /// ONE <see cref="AuthenticationService"/> is created from <paramref name="settings"/> and the
        /// <see cref="ControllerService"/> is built from it, so both share a single cookie session and
        /// <see cref="DownloadFrom"/>/<see cref="UploadTo"/> authenticate exactly the session the controller rides.
        /// </summary>
        public static ProjectAppService CreateWithControllerBridge(IhcSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var authService = new AuthenticationService(settings);
            var controller = new ControllerService(authService);   // rides authService's cookie handler — one session
            return new ProjectAppService(settings, controller, authService);
        }

        /// <summary>
        /// Creates a service with an injected catalog and time provider (used by tests for determinism), with
        /// an optional <paramref name="controller"/> for the download/upload bridge. When a
        /// <paramref name="controller"/> is supplied, its matching <paramref name="authService"/> (the one whose
        /// cookie session the controller rides) is <b>required</b>; the service never self-builds a foreign auth.
        /// A file-only service passes neither.
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

        /// <summary>
        /// The file-only service, with a telemetry configuration. A separate overload rather than an optional
        /// parameter on the existing one: an optional argument is a DIFFERENT symbol and would retire the
        /// shipped constructor.
        /// </summary>
        /// <param name="settings">IHC settings.</param>
        /// <param name="telemetry">Telemetry configuration; only its engine switches are read here.</param>
        public ProjectAppService(IhcSettings settings, TelemetryConfiguration telemetry)
            : this(settings,
                   new Lazy<ICatalog>(() => new BuiltInCatalog(), LazyThreadSafetyMode.PublicationOnly),
                   TimeProvider.System,
                   controller: null,
                   authService: null,
                   telemetry: telemetry)
        {
        }

        private ProjectAppService(IhcSettings settings, Lazy<ICatalog> baseCatalog, TimeProvider timeProvider,
                                  IControllerService? controller, IAuthenticationService? authService,
                                  TelemetryConfiguration? telemetry = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(timeProvider);
            this.settings = settings;
            // Wrap the base catalog in a CompositeCatalog so runtime ImportCatalogFile can overlay extra
            // components (imported-wins) on top of the built-ins; an already-composite base is reused as-is. Lazy, so
            // the base is not materialized until a catalog operation (CreateNew/GetAvailable*/Import) runs — file and
            // controller IO still need no IHC Visual install.
            this.catalog = new Lazy<CompositeCatalog>(
                () => baseCatalog.Value as CompositeCatalog ?? new CompositeCatalog(baseCatalog.Value),
                LazyThreadSafetyMode.PublicationOnly);

            // D27's library port, over the same lazy catalog: a validation run that meets no locked library block
            // never materializes the catalog, and one that does pays for it once.
            this.library = new Lazy<ILibraryBlockSource>(
                () => new CatalogLibraryBlockSource(() => this.catalog.Value.FunctionBlocks),
                LazyThreadSafetyMode.PublicationOnly);
            // D17: the per-rule timing switch reaches the engine through THIS instance, so the shared static
            // executor stays exactly as it is for every other caller. Lazy, because a service that never
            // validates should not build one. Read from IhcSettings' telemetry section when present.
            // Read out here rather than inside the factory: the three Lazy factories in this constructor share one
            // compiler-generated closure, so capturing `telemetry` would pin the whole configuration object - its
            // endpoints and header blob - for the service's lifetime to read a single bool.
            bool perRuleTiming = telemetry?.PerRuleValidationTiming ?? false;
            this.validator = new Lazy<IWholeProjectValidator>(
                () => new WholeProjectValidator(ProjectRules.Registered, perRuleTiming),
                LazyThreadSafetyMode.PublicationOnly);
            this.timeProvider = timeProvider;
            this.controller = controller;
            // R0: a controller-bearing service authenticates the SAME cookie session the controller rides, so the
            // caller must supply the matching auth (CreateWithControllerBridge builds both from one
            // AuthenticationService). Never self-build a second auth here — it would log into a session the injected
            // controller never uses. A file-only service (no controller) never authenticates, so auth stays null.
            if (controller is not null && authService is null)
            {
                throw new ArgumentException(
                    $"A {nameof(ProjectAppService)} with an {nameof(IControllerService)} must also be given the " +
                    $"matching {nameof(IAuthenticationService)} the controller rides; use " +
                    $"{nameof(CreateWithControllerBridge)} to build both from settings.", nameof(authService));
            }
            this.authService = authService;
        }

        /// <summary>Test seam: the auth the controller bridge authenticates (null for a file-only service).</summary>
        internal IAuthenticationService? BridgeAuthentication => authService;

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

        private ProjectCommands? commandsGateway;

        /// <summary>
        /// The single authoring door (R1/D01): a stateless <see cref="ProjectCommands"/> planner that mints
        /// ready-to-apply commands for every domain edit, bound to this service's embedded catalog. A GUI or
        /// console frontend obtains a command here and hands it to its session/apply path — it never constructs
        /// command types directly. Editing a loaded/created project therefore starts at <c>Commands</c> (for the
        /// undoable command vocabulary) or, at the low level, its <c>Edit()</c> extension.
        /// </summary>
        public ProjectCommands Commands => commandsGateway ??= new ProjectCommands(catalog, timeProvider);

        // ---- Command execution: TWO doors (crudarch D01). INTERACTIVE frontends call OpenDocument and drive
        // every edit through the returned IProjectDocument — one persistent, lock-serialized session per open
        // file owning history/dirty/version/events and the per-commit index. The stateless Apply/CanApply/Preview
        // below stay the ONE-SHOT door (D02): each call runs the command on a throwaway single-use session over
        // the given project — no lifecycle, for console tools, tests and non-interactive automation. ----

        /// <summary>
        /// Opens <paramref name="project"/> as an interactive document (crudarch D01, proposal §3.1): the returned
        /// <see cref="IProjectDocument"/> owns command execution with labelled undo/redo, dirty/version tracking
        /// and <c>Changed</c>/<c>StateChanged</c> events over a per-commit index — the door for a GUI, which holds
        /// one document per open file and drives every edit through it. One-shot callers keep using the stateless
        /// <see cref="Apply(Project, ProjectCommand)"/>/<see cref="CanApply"/>/<see cref="Preview"/> door instead.
        /// <paramref name="history"/> sets the undo retention (default <see cref="HistoryPolicy.Unlimited"/>).
        /// <paramref name="startClean"/> is <see cref="IProjectDocument.Open"/>'s save-point flag: pass false for a
        /// project that has no clean state to return to (a recovered auto-backup), so the factory expresses the WHOLE
        /// open and the caller never re-opens after the fact — a second index build the load path would pay, and a
        /// window in which a recovered project reports itself clean (review F04).
        /// See <see cref="IProjectDocument"/> for the D04 threading contract (lock-serialized; events raised
        /// synchronously on the mutating thread; mutate from one thread, read from any).
        /// </summary>
        public IProjectDocument OpenDocument(Project project, HistoryPolicy? history = null, bool startClean = true)
        {
            ArgumentNullException.ThrowIfNull(project);
            return RunTraced(nameof(OpenDocument), activity =>
            {
                var document = new ProjectDocumentSession(history);
                document.Open(project, startClean);
                return (IProjectDocument)document;
            });
        }

        /// <summary>
        /// Applies <paramref name="command"/> to <paramref name="project"/> and returns the resulting snapshot paired
        /// with the <see cref="EditOutcome"/> (D02). The command runs on a fresh single-use
        /// <see cref="ProjectDocumentSession"/> over the project; the immutable <paramref name="project"/> is never
        /// mutated. See <see cref="ProjectApplyResult"/> for the snapshot contract (Committed → the changed project;
        /// NoChange/Refused/Failed → the original input, reference-identical, never null).
        /// </summary>
        public ProjectApplyResult Apply(Project project, ProjectCommand command)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(command);
            return RunTraced(nameof(Apply), activity =>
            {
                ProjectDocumentSession document = OpenScratch(project);
                EditOutcome outcome = document.Apply(command);
                activity?.SetReturnValue(outcome.Status);
                return new ProjectApplyResult(document.Current!, outcome);
            });
        }

        /// <summary>
        /// The value-producing overload of <see cref="Apply(Project, ProjectCommand)"/>: surfaces the command's
        /// produced value (e.g. a new element's id) through the returned <see cref="ProjectApplyResult{T}"/>'s
        /// <see cref="EditOutcome{T}.Value"/> on a committed outcome (<c>default</c> otherwise). Same snapshot contract.
        /// </summary>
        public ProjectApplyResult<T> Apply<T>(Project project, ProjectCommand<T> command)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(command);
            return RunTraced(nameof(Apply), activity =>
            {
                ProjectDocumentSession document = OpenScratch(project);
                EditOutcome<T> outcome = document.Apply(command);
                activity?.SetReturnValue(outcome.Status);
                return new ProjectApplyResult<T>(document.Current!, outcome);
            });
        }

        /// <summary>
        /// The command's legality verdict against <paramref name="project"/> (cheap — no edit, and no additional
        /// validation scan), for drag-over probes and menu gates (D02). Runs the command's own <c>Evaluate</c> on a
        /// fresh single-use session.
        /// <para>"No additional validation scan" is the honest qualifier and not the stronger "no scan": this
        /// overload OPENS a scratch session per call, and opening builds a <c>ProjectIndex</c>, which is a full
        /// pre-order walk of the document. That is fine for a one-shot caller — a console tool, a test — and it is
        /// why an interactive frontend uses <c>IProjectDocument.CanApply</c> on the open document instead, where
        /// the index is already built and shared.</para>
        /// </summary>
        public EditVerdict CanApply(Project project, ProjectCommand command)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(command);
            return RunTraced(nameof(CanApply), activity =>
            {
                EditVerdict verdict = OpenScratch(project).CanApply(command);
                activity?.SetReturnValue(verdict.Ok);
                return verdict;
            });
        }

        /// <summary>
        /// The typed preview of <paramref name="command"/> applied to <paramref name="project"/> now, without
        /// committing (D02/M8): the delta it would commit (<see cref="PreviewStatus.WouldChange"/>), else a
        /// refuse / no-change / engine-fault status. Runs on a fresh single-use session.
        /// </summary>
        public PreviewOutcome Preview(Project project, ProjectCommand command)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(command);
            return RunTraced(nameof(Preview), activity =>
            {
                PreviewOutcome preview = OpenScratch(project).Preview(command);
                activity?.SetReturnValue(preview.Status);
                return preview;
            });
        }

        // Opens a throwaway, single-use document session over the project (D02): the stateless runner behind every
        // facade Apply/CanApply/Preview. Deliberately never hoisted into a persistent field — this door is the
        // one-shot runner; the persistent document an interactive frontend holds is what OpenDocument returns (D01).
        private static ProjectDocumentSession OpenScratch(Project project)
        {
            var document = new ProjectDocumentSession();
            document.Open(project, startClean: true);
            return document;
        }

        /// <summary>
        /// Creates a new empty project replicating IHC Visual's File→New: seeds the default rooms, the
        /// two built-in enums and the fixed skeleton from the catalog's <c>NewDoc.idf</c> /
        /// <c>EnumeratorDefinitions.def</c>. Because it reads those template files it is an instance
        /// operation (like <c>Load</c>/<c>Save</c>), using the service's injected catalog and time provider.
        /// <c>id1</c>/<c>id2</c>/<c>modified</c> are stamped from the clock at creation time; a later
        /// <c>Save</c> re-stamps <c>id2</c>.
        /// </summary>
        public Project CreateNew(ProjectDetails details, SeedIdLayout seedLayout = SeedIdLayout.EnumsFirst,
                                 LocalityLanguage language = LocalityLanguage.Vendor)
        {
            ArgumentNullException.ThrowIfNull(details);
            return RunTraced(nameof(CreateNew), activity =>
            {
                // Vendor (default) leaves the authentic Danish rooms untouched → byte-identical output. English
                // seeds the ten default rooms in English for an English-language authoring frontend (US-002) — the
                // builder names them as it assembles the template, so there is no second pass to repair them after.
                Project project = NewProjectBuilder.Build(
                    catalog.Value, details, timeProvider.GetLocalNow(), seedLayout,
                    language == LocalityLanguage.English ? EnglishLocalities : null);
                activity?.SetReturnValue(project);
                return project;
            });
        }

        // The ten default localities in the fixed vendor order, in English — an English-language authoring frontend
        // (e.g. OpenVisual) seeds these instead of the vendor's Danish rooms (relocated from the app's DefaultLocalities).
        private static readonly string[] EnglishLocalities =
        {
            "Living room", "Hall", "Kitchen", "Bedroom", "Room",
            "Bathroom", "Utility room", "Garage", "Basement", "Outdoors",
        };

        /// <summary>Loads a project from a file path.</summary>
        public async Task<Project> Load(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return await RunTracedAsync(nameof(Load), async activity =>
            {
                byte[] bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                RecordContent(activity, bytes);
                Project project = ProjectReader.Read(bytes);
                // On the load too, so a decrease can be seen ACROSS the open/save pair rather than only
                // between two saves - the corruption may already be in the file that was opened.
                RecordLastUniqueId(activity, project);
                activity?.SetReturnValue(project);
                return project;
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Applies the normalization IHC Visual performs when it OPENS a project: the built-in catalog enum
        /// definitions are re-hoisted to the bottom of <c>enum_definitions</c> with freshly allocated ids, and every
        /// reference to them is repointed. The vendor does this on every open, so a file opened and saved back
        /// unchanged legitimately differs from the file that was opened — an editor that wants to write the same
        /// bytes must do the same thing.
        /// <para>
        /// Deliberately NOT part of <see cref="Load(string)"/>: a passive load is byte-faithful, which is what a tool that
        /// reads a project without rewriting it needs. This is the editor's opt-in, and it is the door a GUI uses —
        /// the underlying <c>ProjectEditor.NormalizeCatalogEnums</c> is engine surface the GUI must not reach.
        /// Call it ONCE per load, before any edit: each call mints a fresh block of ids.
        /// </para>
        /// </summary>
        public Project NormalizeOnOpen(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return RunTraced(nameof(NormalizeOnOpen), activity =>
            {
                Project normalized = project.Edit().NormalizeCatalogEnums().ToProject();
                // WHAT SHAPE open-normalization had. Measured on an authentic vendor file, it is a pure
                // RE-HOIST: the built-in catalog enum definitions are removed and re-added with freshly
                // allocated ids, so added and removed are equal and nothing is changed in place. It is
                // therefore never zero - re-minting ids is what it does, on every open, by design.
                //
                // The three counts are reported separately rather than summed, because the SHAPE is the
                // signal and a sum hides it: an UNBALANCED re-hoist (added != removed) or an in-place
                // CHANGE during normalization would each be anomalous on a file the vendor just wrote,
                // and neither is visible in a total.
                //
                // Guarded, because the diff exists ONLY to answer those three tags: it walks the whole project
                // twice to build its id maps and compares every id-bearing element, which is real work to do on
                // every open for numbers nobody reads when no listener is attached.
                if (activity is not null)
                {
                    ProjectChangeSet diff = ProjectChangeSet.Diff(
                        project, normalized, baseVersion: 0, newVersion: 0,
                        origin: nameof(NormalizeOnOpen), label: nameof(NormalizeOnOpen));
                    activity.SetTag(SdkTelemetryRegistry.Attributes.NormalizeAddedCount, diff.Added.Count);
                    activity.SetTag(SdkTelemetryRegistry.Attributes.NormalizeRemovedCount, diff.Removed.Count);
                    activity.SetTag(SdkTelemetryRegistry.Attributes.NormalizeChangedCount, diff.Changed.Count);
                }
                activity?.SetReturnValue(normalized);
                return normalized;
            });
        }

        /// <summary>Loads a project from a stream.</summary>
        public Task<Project> Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return Task.FromResult(RunTraced(nameof(Load), activity =>
            {
                Project project = ProjectReader.Read(stream);
                activity?.SetReturnValue(project);
                return project;
            }));
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
            await RunTracedAsync(nameof(Save), async activity =>
            {
                RecordSaveOptions(activity, effective);
                RecordLastUniqueId(activity, project);
                byte[] bytes = SerializeForSave(project, effective);
                await WriteAtomically(path, bytes, effective.CreateBackup).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                RecordContent(activity, bytes);
                activity?.SetReturnValue(bytes.Length);
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Saves the way an authoring application saves a document: vendor-like re-stamping, and the file being
        /// replaced is kept as a <c>.BAK</c> side-file so a regretted save can be undone from disk. Saving to a name
        /// that does not exist yet backs up nothing.
        /// <para>
        /// This is the save a GUI calls. <see cref="Save(Project, string, ProjectSaveOptions?)"/> stays the general form for callers that choose their own
        /// options (byte-exact round-trips, exports, controller uploads) and does not write side-files unless asked —
        /// a library that silently produced extra files would be the wrong default. The policy lives here rather than
        /// in the GUI so atomic writes, backups and the byte-fidelity save mode stay in one place.
        /// </para>
        /// </summary>
        public Task SaveDocument(Project project, string path) =>
            // VerifyRoundTrip, unlike the general Save: this is the door an EDITOR saves through, and an
            // editor that writes bytes which do not reproduce the project it was shown has lost the user's
            // work while reporting success. Re-parsing the bytes before declaring the save done turns that
            // silent class of defect into a coded refusal. The general Save keeps its own options, because a
            // byte-exact export or a controller upload chooses its own protections.
            Save(project, path, ProjectSaveOptions.Default with { CreateBackup = true, VerifyRoundTrip = true });


        /// <summary>
        /// The EFFECTIVE save options, as span attributes. Which protections actually ran is not derivable
        /// from anything else in the trace: two saves of the same project through different doors differ only
        /// here, and "was verification on?" is the first question a suspect save raises.
        /// </summary>
        private static void RecordSaveOptions(System.Diagnostics.Activity activity, ProjectSaveOptions options)
        {
            if (activity is null)
            {
                return;
            }
            activity.SetTag(SdkTelemetryRegistry.Attributes.SaveVerifyRoundTrip, options.VerifyRoundTrip);
            activity.SetTag(SdkTelemetryRegistry.Attributes.SaveValidateBeforeSave, options.ValidateBeforeSave);
            activity.SetTag(SdkTelemetryRegistry.Attributes.SaveCreateBackup, options.CreateBackup);
            activity.SetTag(SdkTelemetryRegistry.Attributes.SaveWriteMetadataVerbatim, options.WriteMetadataVerbatim);
        }

        /// <summary>
        /// The id allocator high-water mark the project carries. It only ever grows for a given document, so a
        /// value that DECREASES between two consecutive saves is allocator corruption - ids about to be reused
        /// for different elements. Nothing else in the trace would show that, and the file itself looks fine.
        /// </summary>
        private static void RecordLastUniqueId(System.Diagnostics.Activity activity, Project project)
        {
            if (activity is null)
            {
                return;
            }
            string? lastUniqueId = project?.Root?.GetAttribute("last_unique_id");
            if (lastUniqueId is not null)
            {
                activity.SetTag(SdkTelemetryRegistry.Attributes.ProjectLastUniqueId, lastUniqueId);
            }
        }


        /// <summary>
        /// The size of what was written or read, ALWAYS; and a SHA-256 of those exact bytes only when
        /// <see cref="IhcSettings.LogSensitiveData"/> is set.
        ///
        /// <para>The size is unconditional because a byte count reveals nothing about content. The digest is
        /// not: it is a stable FINGERPRINT of one customer project, so two sessions that touched the same
        /// file become linkable across the backend even though neither record names the file. That is
        /// precisely what makes it useful for diagnosis - "is this the same project the user reported?" is
        /// answerable without ever transmitting the project - and precisely why it is gated. See the note
        /// beside LogSensitiveData in ARCHITECTURE.md.</para>
        /// </summary>
        private void RecordContent(System.Diagnostics.Activity activity, byte[] bytes)
        {
            if (activity is null || bytes is null)
            {
                return;
            }
            activity.SetTag(SdkTelemetryRegistry.Attributes.ProjectFileSize, bytes.Length);
            if (!settings.LogSensitiveData)
            {
                return;
            }
            activity.SetTag(SdkTelemetryRegistry.Attributes.ProjectContentDigest,
                System.Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        }

        private async Task WriteAtomically(string path, byte[] bytes, bool createBackup)
        {
            string fullPath = Path.GetFullPath(path);
            string? backup = createBackup ? Path.ChangeExtension(fullPath, ".BAK") : null;
            if (backup is not null && string.Equals(Path.GetFullPath(backup), fullPath,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new RefusedWriteException(SaveRefusalCodes.TargetUnwritable,
                    $"Cannot create a .BAK backup for '{path}': the target itself has the .BAK extension; " +
                    $"save under a different name or disable {nameof(ProjectSaveOptions.CreateBackup)}.");
            }
            string directory = Path.GetDirectoryName(fullPath)
                ?? throw new RefusedWriteException(SaveRefusalCodes.TargetUnwritable,
                    $"'{path}' has no containing directory.");
            // Same directory ⇒ same volume, which File.Replace/File.Move need for an atomic rename.
            string temp = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                          bufferSize: 4096, useAsync: true);
                // The stream is declared outside the await using so `file` stays a FileStream: configuring the
                // await directly would bind it to the ConfiguredAsyncDisposable wrapper instead.
                await using (file.ConfigureAwait(settings.AsyncContinueOnCapturedContext))
                {
                    await file.WriteAsync(bytes).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    // CA1849: FlushAsync is NOT the async form of this call - it flushes to the OS, not through to
                    // the disk, so swapping it in would silently drop the durability this line exists for.
#pragma warning disable CA1849
                    file.Flush(flushToDisk: true);   // durable before the swap: a crash must leave old or new, never neither
#pragma warning restore CA1849
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
            catch (Exception ex)
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }

                // The destination is what failed — locked, read-only, gone, or full — so the refusal is named
                // here, where the operation is known, rather than left as whatever the platform threw. It stays
                // an IOException: a caller that already handles "the file could not be written" keeps working
                // and gains the code only if it looks for one. Anything that is NOT a write failure (a
                // cancellation, an out-of-memory) is not renamed as one.
                if (ex is IOException or UnauthorizedAccessException)
                {
                    throw new RefusedWriteException(SaveRefusalCodes.TargetUnwritable,
                        $"The project could not be written to '{path}': {ex.Message}", ex);
                }

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
            await RunTracedAsync(nameof(Save), async activity =>
            {
                ProjectSaveOptions streamOptions = options ?? ProjectSaveOptions.Default;
                RecordSaveOptions(activity, streamOptions);
                RecordLastUniqueId(activity, project);
                byte[] bytes = SerializeForSave(project, streamOptions);
                RecordContent(activity, bytes);
                await stream.WriteAsync(bytes).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                activity?.SetReturnValue(bytes.Length);
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
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
                ProjectValidationResult validation = ProjectVerification.Run(project, StructuralProfile, validator.Value);
                if (!validation.IsValid)
                {
                    throw new ProjectValidationException(OperationCodes.Save, validation);
                }
            }
            Project toWrite = options.WriteMetadataVerbatim
                ? project
                : MetadataStamper.Restamp(project, timeProvider.GetLocalNow());
            byte[] bytes = ProjectSerializer.Serialize(toWrite);
            if (options.VerifyRoundTrip)
            {
                // The serializer's own write self-check (M2, ProjectRoundTripVerifier): re-parse the bytes and confirm
                // they reproduce the model, so a project holding state the .vis format cannot represent throws before
                // the file is handed back — instead of silently writing a lossy file.
                ProjectRoundTripVerifier.Verify(toWrite, bytes);
            }
            return bytes;
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
            return await RunTracedAsync(nameof(DownloadFrom), async activity =>
            {
                await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                ProjectFile file = await controller.GetProject().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                if (file?.Data is null)
                {
                    throw new RefusedOperationException(BridgeRefusalCodes.ControllerNoProject,
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
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
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
            return await RunTracedAsync(nameof(UploadTo), async activity =>
            {
                await EnsureAuthenticated().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                // Always verify the write on this path (controller EPROM has no .BAK to roll back to) — a
                // deliberate, documented postcondition, not a silent discard of the caller's option. The check is
                // tolerant of the benign omit-if-default asymmetry, so a foreign file with an explicit
                // default-equal attribute still uploads; only a genuinely non-reproducible model is refused.
                // `validate` rides the same option rather than a second guard here, so the validate-then-serialize
                // order has ONE implementation; ORing keeps an options-supplied opt-in from being overridden off.
                ProjectSaveOptions supplied = options ?? ProjectSaveOptions.Default;
                ProjectSaveOptions effective = supplied with
                {
                    VerifyRoundTrip = true,
                    ValidateBeforeSave = supplied.ValidateBeforeSave || validate,
                };
                // Serialize straight to the on-wire string — the controller takes a ProjectFile, not a stream — so
                // no MemoryStream/ToArray copy is needed (the byte[] → string is the only conversion required).
                ProjectFile file = new ProjectFile(filename ?? DefaultProjectFilename,
                                                   ProjectFile.Encoding.GetString(SerializeForSave(project, effective)));
                bool stored = await controller.StoreProject(file).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                if (!stored)
                {
                    throw new ProjectUploadException(BridgeRefusalCodes.ControllerDeclined,
                        $"The controller declined {nameof(IControllerService.StoreProject)} after entering change " +
                        $"mode; its project state is uncertain — verify with " +
                        $"{nameof(IControllerService)}.{nameof(IControllerService.GetProjectInfo)} before retrying.");
                }
                activity?.SetReturnValue(stored);
                return stored;
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>The products available for insertion, from the SDK-embedded catalog (plus any imported).</summary>
        public IReadOnlyList<ProductDefinition> GetAvailableProducts() =>
            RunTraced(nameof(GetAvailableProducts), activity =>
            {
                IReadOnlyList<ProductDefinition> result = catalog.Value.Products;
                activity?.SetReturnValue(result.Count);
                return result;
            });

        /// <summary>
        /// What the properties dialog of the placed product <paramref name="productId"/> contains, fully resolved
        /// against that element: repeats expanded, every binding resolved to a concrete <c>ElementId</c>, values
        /// read effectively, numeric ranges derived, read-only decided, automation ids formed and the title in the
        /// per-family form the original uses.
        /// <para>The one door a frontend needs: everything family-specific is decided here, so a renderer draws
        /// what it is handed and a write-back writes what it is handed. A product whose family has no preset gets
        /// the minimal fallback rather than an error — an open-world product must still open a dialog.</para>
        /// </summary>
        /// <exception cref="ArgumentException">No element with that id, or it is not a product.</exception>
        public ProductDialogDescriptor GetProductDialog(Project project, ElementId productId)
        {
            ArgumentNullException.ThrowIfNull(project);
            return RunTraced(nameof(GetProductDialog), activity =>
            {
                ProjectElement product = project.FindById(productId)
                    ?? throw new ArgumentException(
                        $"No element with id {productId.ToToken()}.", nameof(productId));
                if (!ProductClassifier.IsProduct(product.Tag))
                {
                    throw new ArgumentException(
                        $"<{product.Tag}> is not a product device root.", nameof(productId));
                }

                // The dialog is titled with the product TYPE, as the original titles it — not with the element's
                // own (possibly renamed) name. Falls back to the element's name for a product whose identifier the
                // catalog CANNOT ANSWER FOR (unknown, or ambiguous per D22 — see ProductCatalogLookup.Resolve).
                // The stored name is the better answer precisely because these products insert `locked`, which
                // fixes that name to the type name.
                string? storedName = project.View(product).Name;
                string displayName =
                    ResolveProduct(product.GetAttribute("product_identifier"), storedName)?.DisplayName
                    ?? storedName
                    ?? product.Tag;

                // Composed through the SAME door the write-back uses, so the descriptor a commit validates against
                // is the one the installer saw — including the identifier-selected shape carrying the end-user-
                // report checkbox (T099).
                ProductDialogDescriptor result = ProductDialogComposer.ComposeFor(project, product, displayName);
                activity?.SetReturnValue(result.Groups.Length);
                return result;
            });
        }

        /// <summary>The function blocks available for insertion, from the SDK-embedded catalog (plus any imported).</summary>
        public IReadOnlyList<FunctionBlockDefinition> GetAvailableFunctionBlocks() =>
            RunTraced(nameof(GetAvailableFunctionBlocks), activity =>
            {
                IReadOnlyList<FunctionBlockDefinition> result = catalog.Value.FunctionBlocks;
                activity?.SetReturnValue(result.Count);
                return result;
            });

        /// <summary>
        /// The variable types that may be inserted directly under <paramref name="containerId"/>, as SDK tags — the
        /// engine's answer to "what does this section accept", so a caller (a GUI variable palette) never keeps a
        /// second copy of the placement rule (uxparity2 W1/RC1, D03). A function-block <c>inputs</c>/<c>outputs</c>
        /// section yields its own signal type plus every <see cref="VariableTypeRegistry.ValueTypeTags"/> value type;
        /// <c>settings</c>/<c>internalsettings</c> yield the value types alone. Anything else — a locality, a product,
        /// an unknown id — yields an empty list, so the caller need not classify the container itself.
        /// <para>
        /// <b>Variable types only.</b> <see cref="PlacementRules.OptionsFor"/> also offers <c>resource_scene</c> under
        /// <c>outputs</c>; a scene is not a variable and reaches the project through US-024's own route, so it is
        /// filtered out here rather than in each caller. Returning tags (not the engine's <c>InsertOption</c>) also
        /// keeps <c>Ihc.Vis.Editing</c> off this facade's signature, which the GUI is barred from referencing.
        /// </para>
        /// </summary>
        public IReadOnlyList<string> GetInsertableVariableTypes(Project project, ElementId containerId) =>
            RunTraced(nameof(GetInsertableVariableTypes), activity =>
            {
                // GetInsertableAt resolves the container's own tag AND its parent's (the grandparent context that
                // separates a block section from a like-named product container), so the rule is asked once, here.
                IReadOnlyList<string> result = project.FindById(containerId) is null
                    ? Array.Empty<string>()
                    : project.Edit().GetInsertableAt(containerId)
                        .Select(o => o.ChildTag)
                        .Where(VariableTypeRegistry.IsVariableType)
                        .ToList();
                activity?.SetReturnValue(result.Count);
                return result;
            });

        /// <summary>The available products projected to slim insert-menu items (<see cref="CatalogItem"/>: the
        /// insert identifier, display name and category path) — the narrow surface a menu needs, without exposing the
        /// full authoring <see cref="ProductDefinition"/>.</summary>
        public IReadOnlyList<CatalogItem> GetProductCatalogItems() =>
            GetAvailableProducts().Select(ToCatalogItem).ToList();

        /// <summary>
        /// The insert-menu projection of ONE product, by its <c>product_identifier</c> — the single-item form of
        /// <see cref="GetProductCatalogItems"/>, for the callers that need one component's display name (a product
        /// dialog titles itself with its catalog type) rather than the whole catalog. Null when no product declares
        /// that identifier, which is the answer a caller wants: a project may name a component this catalog does not
        /// carry, and that is a fall back to the element's own name, not a failure.
        /// </summary>
        public CatalogItem? GetProductCatalogItem(string productIdentifier) =>
            ResolveProduct(productIdentifier) is { } product ? ToCatalogItem(product) : null;

        /// <summary>
        /// The catalog product a caller means, from its <c>product_identifier</c> and — when it has one — the
        /// display name that tells products sharing an identifier apart (D22). Null when the catalog does not
        /// carry the identifier, or carries it more than once and <paramref name="displayName"/> does not decide.
        /// <para>The one door for this question: an insert-menu leaf resolving what it stands for, and a placed
        /// element resolving its own catalog type, are the same lookup and must not answer differently. The rule
        /// itself lives at <see cref="ProductCatalogLookup.Resolve"/>.</para>
        /// </summary>
        public ProductDefinition? ResolveProduct(string? productIdentifier, string? displayName = null) =>
            ProductCatalogLookup.Resolve(GetAvailableProducts(), productIdentifier, displayName);

        /// <summary>
        /// The same lookup, with the CODED REASON when it finds nothing (T043) — for the callers that must tell a
        /// user why an insertion did not happen. The two shapes are one implementation: this is the door that
        /// explains itself, <see cref="ResolveProduct"/> the one for a caller with nothing to explain (a placed
        /// element falling back to its own name is not a failure).
        /// </summary>
        /// <param name="productIdentifier">The product's <c>product_identifier</c>.</param>
        /// <param name="displayName">The display name that tells products sharing an identifier apart (D22).</param>
        /// <param name="product">The resolved product, when the catalog decides.</param>
        /// <param name="refusal">The coded problem, when it does not.</param>
        public bool TryResolveProduct(
            string? productIdentifier, string? displayName,
            [NotNullWhen(true)] out ProductDefinition? product,
            [NotNullWhen(false)] out Problem? refusal)
        {
            product = ResolveProduct(productIdentifier, displayName);
            refusal = product is null
                ? EditRefusalProblems.CatalogProductMissing(productIdentifier ?? string.Empty)
                : null;
            return product is not null;
        }

        // The body's `name` carries the catalog's NN# ordering prefix, which DisplayName has had stripped — an
        // insert menu needs it to list its leaves in the catalog's own order rather than alphabetically.
        private static CatalogItem ToCatalogItem(ProductDefinition product) =>
            new(product.ProductIdentifier, product.DisplayName, product.CategoryPath,
                product.Body.GetAttribute("name"));

        /// <summary>The available function blocks projected to slim insert-menu items (<see cref="CatalogItem"/>,
        /// keyed by <c>master_type</c>) — the narrow surface a menu needs, without the full authoring definition.</summary>
        public IReadOnlyList<CatalogItem> GetFunctionBlockCatalogItems() =>
            GetAvailableFunctionBlocks().Select(b => new CatalogItem(b.MasterType, b.DisplayName, b.CategoryPath)).ToList();

        /// <summary>
        /// Imports one catalog component file at runtime so it resolves and inserts alongside the built-ins: a
        /// <c>.ifb</c> is read as a function block, any other extension (<c>.def</c>) as a product, via the same
        /// encoding/DTD-default/inline-DTD handling as install discovery
        /// (<see cref="CatalogReader.ReadProduct(string, HelpDocument?)"/>). The imported component shadows a
        /// built-in with the same key (imported-wins) and appears in <see cref="GetAvailableProducts"/> /
        /// <see cref="GetAvailableFunctionBlocks"/>. Pass <paramref name="documentationProbe"/> (e.g.
        /// <see cref="ReadSiblingDocumentation"/>) to attach help metadata from a sibling file; it maps the component
        /// path to summary text (or null for none). This is the single-file import primitive by design — to import a
        /// whole folder, the caller enumerates its <c>.def</c>/<c>.ifb</c> files and calls this once per file.
        /// </summary>
        public void ImportCatalogFile(string path, Func<string, string?>? documentationProbe = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            RunTraced(nameof(ImportCatalogFile), activity =>
            {
                string? summary = documentationProbe?.Invoke(path);
                // The optional summary documentation is the same for either component kind — build it once (T028).
                // Summary only: the probe yields prose about the component, never per-resource bullets.
                HelpDocument? documentation = summary is null
                    ? null
                    : new HelpDocument(summary, ImmutableDictionary<string, string>.Empty);
                if (Path.GetExtension(path).Equals(".ifb", StringComparison.OrdinalIgnoreCase))
                {
                    catalog.Value.Import(CatalogReader.ReadFunctionBlock(path, documentation));
                }
                else
                {
                    catalog.Value.Import(CatalogReader.ReadProduct(path, documentation));
                }
                activity?.SetReturnValue(path);
            });
        }

        /// <summary>
        /// A ready-made <c>documentationProbe</c> for <see cref="ImportCatalogFile"/>:
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

        // To edit a project, obtain a command from the Commands gateway (the single authoring door) and apply it
        // through a session; the project.Edit() extension is the low-level mutation entry the gateway is built on.

        /// <summary>
        /// Exports the function block <paramref name="functionBlockId"/> to a reusable <c>.ifb</c> catalog file
        /// (US-021 "Gem…"): lifts the block to a keyless user-block <see cref="FunctionBlockDefinition"/> and writes
        /// it atomically. The write reuses <see cref="Save(Project,string,ProjectSaveOptions)"/>'s atomic mechanics —
        /// bytes land in a same-directory temp file and swap in with <see cref="File.Replace(string,string,string?)"/>
        /// — so a failed or interrupted export never truncates an existing file (no <c>.BAK</c> is kept for an
        /// export). The project is <b>not</b> mutated. <paramref name="author"/> is explicit (no ambient OS user) and
        /// <paramref name="created"/> defaults to <b>today from the service clock</b> when omitted (never
        /// <c>DateTime.Now</c>), so exports are deterministic and testable.
        /// </summary>
        /// <exception cref="InvalidOperationException"><paramref name="functionBlockId"/> is not a function block.</exception>
        /// <exception cref="IOException">The file could not be written.</exception>
        public async Task ExportFunctionBlock(Project project, ElementId functionBlockId, string path, string name,
            string author, DateOnly? created = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            await RunTracedAsync(nameof(ExportFunctionBlock), async activity =>
            {
                FunctionBlockDefinition definition = BuildExportDefinition(project, functionBlockId, name, author, created, note);
                using var buffer = new MemoryStream();
                CatalogFileWriter.Write(definition, buffer, CatalogLayout.Export);
                await WriteAtomically(path, buffer.ToArray(), createBackup: false)
                    .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                activity?.SetReturnValue(path);
            }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// The <see cref="Stream"/> primitive of <see cref="ExportFunctionBlock(Project, ElementId, string, string, string, DateOnly?, string?)"/>:
        /// writes the exported <c>.ifb</c> bytes to <paramref name="stream"/> (no atomic-file handling — the caller
        /// owns the stream). Same author/date/error semantics; the project is not mutated.
        /// </summary>
        /// <exception cref="InvalidOperationException"><paramref name="functionBlockId"/> is not a function block.</exception>
        public void ExportFunctionBlock(Project project, ElementId functionBlockId, Stream stream, string name,
            string author, DateOnly? created = null, string? note = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            RunTraced(nameof(ExportFunctionBlock), activity =>
            {
                FunctionBlockDefinition definition = BuildExportDefinition(project, functionBlockId, name, author, created, note);
                // Save-to-library writes the vendor's EXPORT shape, not the shipped-catalog one (S-22).
                CatalogFileWriter.Write(definition, stream, CatalogLayout.Export);
                activity?.SetReturnValue(name);
            });
        }

        /// <summary>
        /// Save-to-library (US-021, PG-3a): exports the block to <paramref name="ifbStream"/> as a keyless <c>.ifb</c>
        /// master, THEN transforms the in-project block into a locked library instance (rename + <c>master_*</c> stamp
        /// + badge + note + <c>locked="yes"</c>, no re-insertion). <b>Failure ordering:</b> the export runs FIRST, so a
        /// failed export throws before any project mutation and the project is left unmutated. Returns the transform's
        /// <see cref="ProjectApplyResult"/> — the caller commits it, which makes one undo restore the prior unlocked block.
        /// </summary>
        public ProjectApplyResult SaveFunctionBlockToLibrary(Project project, ElementId functionBlockId, Stream ifbStream,
            string author, string name, string? note = null)
        {
            ExportFunctionBlock(project, functionBlockId, ifbStream, name, author, note: note);   // FIRST — throws → no transform
            return Apply(project, Commands.SaveFunctionBlockToLibrary(project, functionBlockId, name, author, note));
        }

        /// <summary>The atomic-file-write overload of
        /// <see cref="SaveFunctionBlockToLibrary(Project, ElementId, Stream, string, string, string)"/> — writes the
        /// <c>.ifb</c> to <paramref name="path"/> before transforming the in-project block (same failure ordering).</summary>
        public async Task<ProjectApplyResult> SaveFunctionBlockToLibrary(Project project, ElementId functionBlockId, string path,
            string author, string name, string? note = null)
        {
            await ExportFunctionBlock(project, functionBlockId, path, name, author, note: note)   // FIRST — throws → no transform
                .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            return Apply(project, Commands.SaveFunctionBlockToLibrary(project, functionBlockId, name, author, note));
        }

        // Lifts a placed block to a keyless user-block definition (read-only over the immutable project — project.Edit()
        // makes a private mutable copy that is never committed back). The date defaults to today from the service clock.
        private FunctionBlockDefinition BuildExportDefinition(Project project, ElementId functionBlockId, string name,
            string author, DateOnly? created, string? note)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(author);
            DateOnly exportDate = created ?? DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
            return project.Edit().FunctionBlock(functionBlockId)
                .ExportDefinition(name, author, exportDate, string.IsNullOrEmpty(note) ? null : note);
        }

        /// <summary>Validates a project against the pre-serialize checklist.</summary>
        public ProjectValidationResult Validate(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return RunTraced(nameof(Validate), activity =>
            {
                ProjectValidationResult result = ProjectVerification.Run(project, StructuralProfile, validator.Value);
                activity?.SetReturnValue(result);
                return result;
            });
        }

        /// <summary>
        /// What the shipped rules declare about one dialog field: its bounds, its required-ness, its allowed set —
        /// the same constraint objects the whole-project run executes, read rather than run.
        /// <para>
        /// THE ONLY LEGAL WIRING POINT, and not for want of alternatives. Composing a <see cref="RuleSet"/> with a
        /// target is business logic, this service is where the SDK composes its faces, and no other type may hold
        /// the rule set — so a frontend asking a field's declared bounds asks here, exactly as it asks here for
        /// everything else. Answering it anywhere else would put the composition in a frontend.
        /// </para>
        /// <para>
        /// It is a READ, not a run: no project is walked and nothing is validated. A field no rule constrains
        /// answers <see cref="FieldConstraintMetadata.Unconstrained"/>, which is the honest answer rather than an
        /// absence — and the value type is what a dialog binds to, the same currency the grammar-declared bounds
        /// already arrive in.
        /// </para>
        /// </summary>
        /// <param name="target">The (tag, attribute) pair the field edits.</param>
        public FieldConstraintMetadata DescribeField(RuleTarget target) =>
            ProjectRules.Registered.DescribeField(target);

        /// <summary>
        /// The coded problem when a value a surface requires is blank, or null when it carries something.
        /// <para>
        /// ONE decision and ONE sentence, for every frontend. It composes two SDK pieces that a caller would
        /// otherwise have to compose for itself — the required-field constraint, which owns what BLANK means
        /// (whitespace-only counts), and the <c>edit.value-required</c> problem, which owns the words. A frontend
        /// doing that composition is a frontend holding business logic, and the OpenVisual shell's four callers
        /// are the measured proof that it spreads: before this member they shared one helper, and before that
        /// helper they were three gates that disagreed about a name of three spaces and two of which said nothing
        /// at all.
        /// </para>
        /// <para>
        /// Only the DECISION and the SENTENCE are here. HOW a surface reports it — an inline line in a modal
        /// prompt, a status bar, a refusal dialog — stays with the surface, which is the half no SDK should own.
        /// </para>
        /// </summary>
        /// <param name="value">The submitted value, UNTRIMMED: the constraint's policy decides what blank means,
        /// so a caller that trimmed first would be answering the question itself.</param>
        public Problem? MissingRequiredField(string? value) =>
            RequiredFieldConstraint.For(EditRefusalCodes.ValueRequired).Check(value).Satisfied
                ? null
                : EditRefusalProblems.ValueRequired();

        /// <summary>
        /// The structural profile WITH the library this service already holds (D27). It is not
        /// <see cref="ProjectVerification.Structural"/>: that overload has no catalog, so every row declaring a
        /// library is skipped — including <c>logic-block-locked-content</c>, whose category is
        /// <see cref="ValidationCategory.Logic"/>. The two entry points are documented to differ by AUDIENCE alone,
        /// so a Logic row visible to one and invisible to the other is a gap, not a profile.
        /// </summary>
        private ValidationProfile StructuralProfile => ValidationProfile.ProjectOnly with { Library = library.Value };

        /// <summary>
        /// This service's OWN validation executor, so the per-rule timing switch reaches the engine without
        /// making the shared <c>ProjectRules.Validator</c> static configurable for every caller in the
        /// process. The rule SET is still the registered one - only the instrumentation differs.
        /// </summary>
        private readonly Lazy<IWholeProjectValidator> validator;

        /// <summary>
        /// The categorized profile with the same library port, for the same reason — stated once here
        /// rather than spelled out at each of the two runs that read it.
        /// </summary>
        private ValidationProfile CategorizedProfile => ValidationProfile.Categorized with { Library = library.Value };

        /// <summary>
        /// Validates a project against the FULL categorized verification (R10): the structural
        /// pre-serialize checklist (<see cref="Validate"/>) plus the documentation-completeness checks
        /// (<see cref="ValidationCategory.Documentation"/>, always
        /// <see cref="ValidationSeverity.Warning"/>). <c>IsValid</c>/<c>Errors</c> mean exactly what
        /// <see cref="Validate"/> means — documentation findings only ever add <c>Warnings</c>.
        /// </summary>
        public ProjectValidationResult ValidateCategorized(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return RunTraced(nameof(ValidateCategorized), activity =>
            {
                // D27: the library port comes from the catalog this service already holds, so the two rows that
                // need a library are evaluated here and skipped by a caller who validates without one.
                ProjectValidationResult result = ProjectVerification.Run(project, CategorizedProfile, validator.Value);
                activity?.SetReturnValue(result);
                return result;
            });
        }

        /// <summary>
        /// The same categorized run as <see cref="ValidateCategorized"/>, in the engine's STRUCTURED finding
        /// shape: each finding keeps its problem, its primary site and every RELATED site.
        /// <para>
        /// It is the door a frontend needs and could not have. The flat
        /// <see cref="ProjectValidationFinding"/> carries one locator, so a grouped rule's other sites — the
        /// remaining elements sharing a duplicate id, the rest of an under-populated module — were dropped at
        /// this boundary and could not be listed or navigated to. Reaching them meant naming
        /// <c>IWholeProjectValidator</c>, which the architecture forbids a GUI (L5): a shell that could construct
        /// an executor would be a second composition root for the engine. This accessor gives the shape without
        /// the port.
        /// </para>
        /// <para>
        /// <see cref="ValidateCategorized"/> stays the door for the flat shape; the two are ONE run's output in
        /// two forms, not two pipelines with their own rules.
        /// </para>
        /// </summary>
        /// <param name="project">The project to validate.</param>
        public EquatableArray<ValidationFinding> ValidateStructured(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            return RunTraced(nameof(ValidateStructured), activity =>
            {
                EquatableArray<ValidationFinding> findings =
                    ProjectVerification.RunStructured(project, CategorizedProfile, validator.Value);
                activity?.SetReturnValue(findings.Length);
                return findings;
            });
        }

        /// <summary>
        /// Generates a documentation report (spec R3): renders <paramref name="project"/> as the given
        /// <paramref name="kind"/> × <paramref name="mode"/> to <paramref name="output"/> in the format
        /// selected by <paramref name="mimeType"/> (<see cref="ReportMimeTypes.Html"/> or
        /// <see cref="ReportMimeTypes.PlainText"/>; anything else is rejected). Bytes are UTF-8 without BOM
        /// with LF line endings. <paramref name="iconProvider"/> customizes icon glyphs (R11); null uses the
        /// default unicode stand-ins. The Full-mode generation timestamp comes from the injected
        /// <see cref="TimeProvider"/>.
        /// </summary>
        public Task GenerateReport(Project project, ReportKind kind, ReportMode mode, string mimeType,
            Stream output, IReportIconProvider? iconProvider = null)
        {
            ArgumentNullException.ThrowIfNull(output);
            return GenerateReportCore(project, kind, mode, mimeType, iconProvider,
                bytes => output.WriteAsync(bytes).AsTask());
        }

        /// <summary>
        /// File convenience overload of
        /// <see cref="GenerateReport(Project, ReportKind, ReportMode, string, Stream, IReportIconProvider?)"/>:
        /// writes the generated report bytes to <paramref name="path"/> (overwriting an existing file).
        /// </summary>
        public Task GenerateReport(Project project, ReportKind kind, ReportMode mode, string mimeType,
            string path, IReportIconProvider? iconProvider = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            return GenerateReportCore(project, kind, mode, mimeType, iconProvider,
                bytes => File.WriteAllBytesAsync(path, bytes));
        }

        /// <summary>
        /// Exports the project's findings as an ISO-8859-1 XML document — the <c>.vis</c> encoding: no BOM,
        /// CRLF, three-space indent — for archiving, diffing, or handing to a support case.
        /// <para>
        /// This runs the SAME categorized validation <see cref="ValidateCategorized"/> and
        /// <see cref="ValidateStructured"/> run, so the file and the screen cannot report different rule sets.
        /// The generation timestamp comes from the injected <see cref="TimeProvider"/>.
        /// </para>
        /// </summary>
        /// <param name="project">The project to validate and export.</param>
        /// <param name="output">Where the document's bytes go.</param>
        /// <param name="options">What the caller knows and the SDK does not — the source name above all
        /// (a <see cref="Project"/> carries no path); null means <see cref="FindingExportOptions.Default"/>.</param>
        public Task ExportFindings(Project project, Stream output, FindingExportOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(project);
            return ExportFindingsCore(
                project, OwnRun(project), options, bytes => output.WriteAsync(bytes).AsTask());
        }

        /// <summary>
        /// File convenience overload of <see cref="ExportFindings(Project, Stream, FindingExportOptions?)"/>:
        /// writes the document to <paramref name="path"/>, overwriting an existing file.
        /// </summary>
        /// <param name="project">The project to validate and export.</param>
        /// <param name="path">The file to write.</param>
        /// <param name="options">As on the stream overload.</param>
        public Task ExportFindings(Project project, string path, FindingExportOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(project);
            return ExportFindingsCore(
                project, OwnRun(project), options, bytes => File.WriteAllBytesAsync(path, bytes));
        }

        /// <summary>
        /// Exports a caller-supplied sequence VERBATIM — the door a frontend uses so its file and its screen
        /// cannot disagree.
        /// <para>
        /// Nothing here re-sorts, re-filters or re-validates: a host that shows a filtered, re-sorted list
        /// hands that list over and gets exactly it. Which tiers were included is not derivable from the
        /// findings that survived the filter, so the caller states it in
        /// <see cref="FindingExportOptions.Severities"/>, and what sequence this is in
        /// <see cref="FindingExportOptions.Order"/>.
        /// </para>
        /// </summary>
        /// <param name="project">The project the findings are about — read for its save stamp.</param>
        /// <param name="findings">The sequence to emit, in the order it is to appear.</param>
        /// <param name="output">Where the document's bytes go.</param>
        /// <param name="options">As on the stream overload.</param>
        public Task ExportFindings(Project project, IReadOnlyList<ValidationFinding> findings, Stream output,
            FindingExportOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(findings);
            return ExportFindingsCore(
                project, () => findings, options, bytes => output.WriteAsync(bytes).AsTask());
        }

        /// <summary>
        /// File convenience overload of
        /// <see cref="ExportFindings(Project, IReadOnlyList{ValidationFinding}, Stream, FindingExportOptions?)"/>:
        /// writes the document to <paramref name="path"/>, overwriting an existing file. This is the pair a
        /// host actually uses — it has a save dialog and wants a file, not a stream to manage.
        /// </summary>
        /// <param name="project">The project the findings are about — read for its save stamp.</param>
        /// <param name="findings">The sequence to emit, in the order it is to appear.</param>
        /// <param name="path">The file to write.</param>
        /// <param name="options">As on the stream overload.</param>
        public Task ExportFindings(Project project, IReadOnlyList<ValidationFinding> findings, string path,
            FindingExportOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(findings);
            return ExportFindingsCore(
                project, () => findings, options, bytes => File.WriteAllBytesAsync(path, bytes));
        }

        // The service's own run, as the two validating overloads state it — once, so a change of profile cannot
        // reach one of them and not the other.
        private Func<IReadOnlyList<ValidationFinding>> OwnRun(Project project) =>
            () => ProjectVerification.RunStructured(project, CategorizedProfile, validator.Value);

        // The one write path all four overloads share; they differ only in where the findings come from and
        // where the bytes go. As with GenerateReport, the sink runs after the document is complete, so a
        // failure mid-format never leaves a truncated file.
        //
        // The findings arrive as a THUNK rather than a list so that the validating overloads' run happens inside
        // the traced scope, like GenerateReport's generation does — an eagerly evaluated argument would have run
        // the whole engine before this method was entered, outside the span and synchronously on the caller's
        // thread. For the verbatim overloads the thunk simply hands back the list it was given.
        private Task ExportFindingsCore(Project project, Func<IReadOnlyList<ValidationFinding>> findings,
            FindingExportOptions? options, Func<byte[], Task> write) =>
            RunTracedAsync(nameof(ExportFindings), async activity =>
            {
                byte[] bytes = FindingExportWriter.Write(
                    project, findings(), CategorizedProfile, options, timeProvider.GetLocalNow());
                await write(bytes).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                activity?.SetReturnValue(bytes.Length);
            });

        // The one generation path both overloads share; they differ only in the sink. The sink runs after
        // generation succeeds, so a rejected mimetype never leaves a truncated file (or a partial stream).
        private Task GenerateReportCore(Project project, ReportKind kind, ReportMode mode, string mimeType,
            IReportIconProvider? iconProvider, Func<byte[], Task> write)
        {
            ArgumentNullException.ThrowIfNull(project);
            ArgumentNullException.ThrowIfNull(mimeType);
            return RunTracedAsync(nameof(GenerateReport), async activity =>
            {
                byte[] bytes = ReportGenerator.Generate(project, kind, mode, mimeType, iconProvider,
                    timeProvider.GetLocalNow());
                await write(bytes).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                activity?.SetReturnValue(bytes.Length);
            });
        }
    }

    /// <summary>
    /// The locality language a <see cref="ProjectAppService.CreateNew"/> seeds the ten default rooms in: the
    /// vendor's authentic Danish rooms (<see cref="Vendor"/>, the default — byte-identical to IHC Visual's empty
    /// project) or their English equivalents (<see cref="English"/>) for an English-language authoring frontend.
    /// </summary>
    public enum LocalityLanguage
    {
        /// <summary>The vendor's Danish default rooms (Stue, Køkken, …) — byte-identical to IHC Visual.</summary>
        Vendor,

        /// <summary>English default rooms (Living room, Hall, …), renamed from the vendor rooms by position.</summary>
        English,
    }
}
