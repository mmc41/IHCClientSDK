using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ArchUnitNET.Domain;
using static Ihc.Tests.ArchRuleHelpers;
// ArchUnitNET.Loader also exports a `Type`; the reflection helpers in this fixture mean System.Type throughout.
using Type = System.Type;

namespace Ihc.Tests
{
    /// <summary>
    /// The <c>ihc_openvisual</c> desktop app's "thin GUI" boundary, enforced mechanically.
    ///
    /// The app is a thin MVVM shell over <c>ProjectAppService</c>: all <c>.vis</c> parsing, editing, validation,
    /// catalog and controller logic stays in the SDK. ArchUnitNET cannot judge whether a method <i>is</i>
    /// complicated business logic (that is a complexity/intent property, not a dependency), so these rules instead
    /// enforce the boundary that makes thick logic hard to write in the first place: cut off the GUI's access to
    /// the primitives such logic needs (generated SOAP, hand-rolled XML) and keep view-models framework-free. The
    /// matching/assert mechanics live in <see cref="ArchRuleHelpers"/>; this fixture only states the GUI policy.
    ///
    /// The GUI assembly is loaded here from a public anchor type via <c>typeof(T).Assembly</c>; the ProjectReference
    /// to <c>ihc_openvisual</c> pulls Avalonia into this test project only — never into the SDK, whose own
    /// no-Avalonia rule loads the SDK assembly in isolation (see <see cref="IhcClientArchitectureTests"/>).
    /// </summary>
    [TestFixture]
    public partial class OpenVisualArchitectureTests
    {
        // The desktop GUI read into ArchUnitNET's model once for the whole fixture.
        private static readonly Architecture Gui = ArchitectureModels.Gui;

        // The GUI assembly's own namespace root, anchored to the composition-root type; the name-based boundary
        // scans below judge only the GUI's own code (types under this root), never the referenced SDK/Avalonia stubs.
        private static readonly string GuiRoot =
            typeof(global::ihc_openvisual.App).Namespace!; // ihc_openvisual

        // The view-model layer namespace, anchored to a public type so a rename fails the compile, not the check.
        private static readonly string ViewModels =
            typeof(global::ihc_openvisual.ViewModels.MainWindowViewModel).Namespace!; // ihc_openvisual.ViewModels

        // The Avalonia view layer namespaces view-models must stay off (windows/dialogs, custom controls, value
        // converters), each anchored to a public type so a rename fails the compile, not the check.
        private static readonly string Views =
            typeof(global::ihc_openvisual.Views.MainWindow).Namespace!; // ihc_openvisual.Views
        private static readonly string Controls =
            typeof(global::ihc_openvisual.Controls.AccessibleTreeView).Namespace!; // ihc_openvisual.Controls
        private static readonly string Converters =
            typeof(global::ihc_openvisual.Converters.BoolToBrushConverter).Namespace!; // ihc_openvisual.Converters

        private static readonly string Services =
            typeof(global::ihc_openvisual.Services.ProjectWorkflow).Namespace!; // ihc_openvisual.Services

        private static IEnumerable<string> ViewLayerNamespaces()
        {
            yield return Views;
            yield return Controls;
            yield return Converters;
        }

        // The offline .vis IO layer, anchored to a public engine type so a rename fails the compile, not the check.
        private static readonly string VisIo =
            typeof(global::Ihc.Vis.Io.ProjectSerializer).Namespace!; // Ihc.Vis.Io

        // The live-session editing layer, anchored to a public type so a rename fails the compile, not the check.
        private static readonly string Editing =
            typeof(global::Ihc.Vis.Editing.ProjectEditor).Namespace!; // Ihc.Vis.Editing

        // The SDK's report-generation pipeline is internal (its public contract lives in root Ihc.Vis), so no
        // public typeof anchor exists; the SDK fixture's
        // ReportingPipelineTypes_AreInternal pins that this namespace is populated and internal-only.
        private const string Reporting = "Ihc.Vis.Reporting";

        /// <summary>
        /// The GUI is a thin shell over the <c>ProjectAppService</c> facade; it reaches the controller only through
        /// that facade's API-service interfaces, so it must never touch the generated <c>Ihc.Soap.*</c> layer. If
        /// the GUI cannot see the SOAP types, it cannot hand-roll controller protocol logic.
        /// </summary>
        [Test]
        public void Gui_DoesNotDependOn_Soap() =>
            AssertAssemblyHasNoDependency(Gui, SoapNs,
                "the GUI is a thin shell over ProjectAppService and must never touch the generated SOAP types");

        /// <summary>
        /// All <c>.vis</c>/<c>.ifb</c>/<c>.def</c> XML IO belongs to the <c>Ihc.Vis</c> engine. The GUI must not
        /// parse or emit that XML itself: forbidding <c>System.Xml.*</c> makes it structurally impossible for the
        /// UI to hand-roll project serialization instead of going through the engine.
        /// </summary>
        [Test]
        public void Gui_DoesNotDependOn_SystemXml() =>
            AssertAssemblyHasNoDependency(Gui, SystemXmlNs,
                "the GUI must not depend on System.Xml; project-file XML IO belongs to the Ihc.Vis engine");

        /// <summary>
        /// Loading and saving <c>.vis</c> files belongs to the engine's IO layer (<c>Ihc.Vis.Io</c> —
        /// <c>ProjectSerializer</c>, <c>ProjectReader</c>), reached only through <c>ProjectAppService.Load</c>/
        /// <c>Save</c>. The GUI must not call that layer directly: going through the facade is what keeps atomic
        /// writes, <c>.BAK</c> backups, validate-before-save and the byte-fidelity save mode in one place instead of
        /// re-implemented in the UI.
        /// </summary>
        [Test]
        public void Gui_DoesNotDependOn_VisIoLayer() =>
            AssertAssemblyHasNoDependency(Gui, VisIo,
                "the GUI must load/save .vis files through ProjectAppService, not the Ihc.Vis.Io engine types directly");

        /// <summary>
        /// Mutating a project belongs to the SDK's live-session command layer (<c>Ihc.Vis.Session</c> commands
        /// executed via <c>ProjectEditor</c>), surfaced to the GUI as undoable command objects. The GUI must not
        /// reach into the low-level editing types (<c>ProjectEditor</c>, the <c>*Ref</c>/<c>*Builder</c> handles)
        /// directly: that would bypass the undo/redo history and change-set reconciliation the command layer
        /// provides. The read-only model interpretation the GUI legitimately needs (log-row detection, scene-value
        /// parsing) lives on the <c>Ihc.Vis</c> read surface, not in the editing layer.
        /// </summary>
        [Test]
        public void Gui_DoesNotDependOn_EditingLayer() =>
            AssertAssemblyHasNoDependency(Gui, Editing,
                "the GUI must mutate projects through the Ihc.Vis.Session command layer, not the low-level editing types directly");

        /// <summary>
        /// Report generation and formatting belong to the SDK: the GUI receives
        /// finished report BYTES from <c>ProjectAppService.GenerateReport</c> and must never reach the
        /// <c>Ihc.Vis.Reporting</c> pipeline (builders, shape document, format writers). The forbidden set
        /// is REFLECTED from the SDK assembly — the pipeline is internal-only, so a fluent referenced-stub
        /// ban would go vacuous the moment the GUI is compliant (the false-negative shape the name-based
        /// edge scan exists for); armed by the set-non-empty guard and the shared positive control
        /// <see cref="DependencyNameScan_DetectsKnownFacadeEdge"/>.
        /// </summary>
        [Test]
        public void Gui_DoesNotDependOn_Reporting() =>
            AssertNoDependencyOnTypeNames(Gui, GuiRoot, ReportingPipelineTypeNames(),
                "Ihc.Vis.Reporting pipeline types",
                "report generation and formatting live in the SDK; the GUI holds finished report bytes, never the pipeline");

        /// <summary>Every type of the SDK's internal report pipeline, reflected by full name from the SDK
        /// assembly (compiler-generated nested types included — an edge onto any of them is an edge too).</summary>
        private static IReadOnlyCollection<string> ReportingPipelineTypeNames() =>
            typeof(global::Ihc.Vis.ProjectAppService).Assembly.GetTypes()
                .Where(type => type.Namespace is { } ns
                               && (ns == Reporting || ns.StartsWith(Reporting + ".", StringComparison.Ordinal)))
                .Select(type => type.FullName!)
                .ToHashSet();

        /// <summary>
        /// The GUI does not compose report HTML/text: its single report door is
        /// <c>ProjectAppService.GenerateReport</c>, and only the report workflow calls it (view-models and
        /// dialogs hand kind+mode to the workflow). Together with the Reporting ban above this makes GUI-side
        /// report composition structurally impossible; the sanctioned <c>SvgReportIconProvider</c> only
        /// answers icon fragments through the root-<c>Ihc.Vis</c> contract. Armed by
        /// <see cref="ArchRuleHelpers.AssertMembersCalledOnlyFrom"/>'s calls-must-exist guard — if the
        /// workflow stopped calling <c>GenerateReport</c> (or the chokepoint name rotted), the rule fails
        /// loudly instead of watching nothing.
        /// </summary>
        [Test]
        public void Gui_GeneratesReportsOnlyThroughTheReportWorkflow() =>
            AssertMembersCalledOnlyFrom(Gui, GuiRoot,
                typeof(global::Ihc.Vis.ProjectAppService).FullName!,
                new[] { "GenerateReport" },
                new[] { "ihc_openvisual.Services.ProjectReportWorkflow" },
                "report generation calls",
                "ProjectReportWorkflow is the single GenerateReport caller — the GUI never composes report output elsewhere");

        /// <summary>
        /// Command execution belongs to the SDK: interactive code holds the <c>IProjectDocument</c> PORT from
        /// <c>ProjectAppService.OpenDocument</c>, and the stateless <c>Apply/CanApply/Preview</c>
        /// facade serves one-shot callers — either way the GUI must never open the concrete
        /// <c>ProjectDocumentSession</c> engine runner itself. This bans the one engine TYPE by name,
        /// NEVER the <c>Ihc.Vis.Session</c> namespace: the command / outcome / change-set contract types live there
        /// and the GUI legitimately consumes them. (Armed by <see cref="DependencyNameScan_DetectsKnownFacadeEdge"/>, the
        /// positive control over the same <c>AssertNoDependencyOnTypeNames</c> scan.)
        /// </summary>
        [Test]
        public void Gui_DoesNotDependOn_ProjectDocumentSession() =>
            AssertNoDependencyOnTypeNames(Gui, GuiRoot, ProjectDocumentSessionTypeName(), "the ProjectDocumentSession engine runner",
                "the interactive GUI must execute commands through IProjectDocument from ProjectAppService.OpenDocument, not open ProjectDocumentSession itself");

        /// <summary>
        /// View-models carry the app's presentation logic and must stay free of Avalonia types so that logic is
        /// testable headlessly (the same layering the SDK's no-Avalonia rule protects, applied one tier up). This
        /// rule is scoped to the view-model namespace only — Views, Controls and Converters legitimately depend on
        /// Avalonia.
        /// </summary>
        [Test]
        public void ViewModels_DoNotDependOn_Avalonia() =>
            AssertNoDependency(Gui, Subtree(ViewModels), AvaloniaNs,
                "view-models must stay free of Avalonia types so their presentation logic is testable headlessly");

        /// <summary>
        /// The GUI is file-only: it reaches a controller solely through <c>ProjectAppService</c>'s
        /// <c>DownloadFrom</c>/<c>UploadTo</c> bridge (which takes an already-authenticated <c>IControllerService</c>
        /// from its host), and must never touch a controller API service itself. This bans any dependency onto a
        /// type of the <c>IIHCApiService</c> tier. The forbidden types are named by reflection over the SDK rather
        /// than matched inside the GUI's own model on purpose: the GUI references none of them today — that is the
        /// property under test — so they are absent from its loaded model and a fluent namespace rule would have an
        /// empty, permanently-passing target set. Adding <c>new ControllerService(...)</c> to the GUI would light it up.
        /// </summary>
        [Test]
        public void Gui_DoesNotDependOn_ControllerApiServices() =>
            AssertNoDependencyOnTypeNames(Gui, GuiRoot, ApiServiceTypeNames(), "the IIHCApiService controller tier",
                "the file-only GUI must reach the controller only through ProjectAppService's upload/download bridge, never an IIHCApiService controller service directly");

        /// <summary>
        /// Every authoring mutation must be obtained from a <c>ProjectCommands</c> factory (the single authoring
        /// door surfaced as <c>ProjectAppService.Commands</c>) and applied through a session; the GUI must never
        /// <c>new</c> a <see cref="Ihc.Vis.Session.ProjectCommand"/> itself. This is not expressible as a plain
        /// dependency ban — the GUI legitimately <i>depends</i> on the concrete command types because a factory
        /// hands each one back and the GUI passes it to <c>ApplyAsync</c> — so the rule forbids the one edge that is
        /// illegitimate: constructing a command. Bypassing the factory would skip its parent-context resolution and
        /// legality checks, exactly what the gateway exists to centralise. (The vocabulary's completeness — that a
        /// factory exists for every command — is the SDK-side <c>ProjectCommandsCompletenessTests</c>' job; this is
        /// the GUI-side other half: that the GUI uses those factories.)
        /// </summary>
        [Test]
        public void Gui_DoesNotConstruct_ProjectCommands() =>
            AssertDoesNotConstructTypeNames(Gui, GuiRoot, CommandTypeNames(), "the ProjectCommand vocabulary",
                "authoring commands must be obtained from a ProjectCommands factory, never constructed directly, so the gateway's context resolution and legality checks cannot be bypassed");

        // The stateless one-shot facade members interactive code must not call; the document port
        // (ProjectAppService.OpenDocument → IProjectDocument) is the interactive door.
        private static readonly IReadOnlyCollection<string> StatelessFacadeMemberNames =
            new[]
            {
                nameof(global::Ihc.Vis.ProjectAppService.Apply),
                nameof(global::Ihc.Vis.ProjectAppService.CanApply),
                nameof(global::Ihc.Vis.ProjectAppService.Preview),
            };

        /// <summary>
        /// Interactive edits go through the <c>IProjectDocument</c> port: <b>no type anywhere in the GUI
        /// assembly</b> may call the stateless
        /// one-shot facade (<c>ProjectAppService.Apply/Apply&lt;T&gt;/CanApply/Preview</c>), which would silently
        /// reinstate a per-call scratch session and bypass the document's undo history. The likeliest regressors
        /// are precisely the non-view-model types (<c>TreeDragDropController</c>, the <c>Services</c>), so the scan is rooted at
        /// <see cref="GuiRoot"/> rather than the view-model namespace;
        /// <see cref="GuiScanScope_CoversEveryTypeTheAssemblyDeclares"/> pins that this root really does span the
        /// whole assembly. Only the named members are banned — the GUI may hold the service and call its other
        /// members (Load/Save/OpenDocument/Commands/…). Armed by
        /// <see cref="StatelessFacadeCallScan_IsArmed"/> over a seeded caller in this test assembly.
        /// </summary>
        [Test]
        public void Gui_DoesNotCallTheStatelessFacade() =>
            AssertDoesNotCallMembers(Gui, GuiRoot, typeof(global::Ihc.Vis.ProjectAppService).FullName!,
                StatelessFacadeMemberNames, "the stateless Apply/CanApply/Preview facade",
                "the interactive GUI runs edits through the IProjectDocument port, never the stateless one-shot facade");

        /// <summary>
        /// Every name-rooted boundary scan in this fixture judges "the GUI" by the
        /// <see cref="GuiRoot"/> namespace SUBTREE, but the rules they encode are about the whole GUI ASSEMBLY.
        /// Those two coincide only while every type the assembly declares lives under that root — a type in the
        /// global namespace, or under a differently-rooted one, would be silently unscanned and could call the
        /// stateless facade, construct a command or reach the SOAP layer entirely unnoticed, with every rule above
        /// still green. This turns that coincidence into a checked invariant rather than an assumption.
        /// Synthesised types are excluded: the compiler's closures/state machines and the Avalonia XAML compiler's
        /// generated resource/loader types are not authored GUI code and are not bound by these rules.
        /// </summary>
        [Test]
        public void GuiScanScope_CoversEveryTypeTheAssemblyDeclares()
        {
            var authored = AuthoredGuiTypes().Select(t => t.FullName!).ToList();
            Assert.That(authored, Is.Not.Empty, "sanity: the GUI assembly declares authored types");

            var outsideTheScannedRoot = authored
                .Where(name => !name.StartsWith(GuiRoot + ".", StringComparison.Ordinal))
                .ToList();

            Assert.That(outsideTheScannedRoot, Is.Empty,
                $"every authored GUI type must live under '{GuiRoot}.' — otherwise the namespace-rooted scans do not "
                + "cover the whole assembly and the boundary rules have a blind spot: "
                + string.Join(", ", outsideTheScannedRoot));
        }

        // Build output that lands in the GUI assembly without being authored GUI code, and so is not bound by the
        // boundary rules: source-generator emissions marked [GeneratedCode] (CommunityToolkit.Mvvm parks its
        // property-name caches in its OWN root, CommunityToolkit.Mvvm.ComponentModel.__Internals), and the Avalonia
        // XAML compiler's resource/loader plumbing, which uses unspeakable '!' names under CompiledAvaloniaXaml.
        // Note this excludes generated TYPES only: the [ObservableProperty]/[RelayCommand] generators emit MEMBERS
        // into the authored partial view-models, which therefore stay fully in scope.
        //
        // The third source is IL WEAVING rather than code generation: the HotAvalonia hot-reload plugin injects its
        // own attribute/extension types and a Fody marker into the assembly it processes. They exist only in a
        // Debug build (the plugin removes itself from Release entirely, so this fixture's own Release run never
        // sees them), are authored by nobody here, and cannot be moved under the GUI root. They are named exactly
        // rather than excluded by a broad rule: an unexpected injected type should still fail this scan.
        private static bool IsGeneratedBuildOutput(Type type) =>
            type.IsDefined(typeof(GeneratedCodeAttribute), inherit: false)
            || (type.FullName is { } name
                && (name.StartsWith("CompiledAvaloniaXaml", StringComparison.Ordinal)
                    || name.Contains('!', StringComparison.Ordinal)
                    || WeavedInTypeNames.Contains(name)));

        // Injected by the Debug-only HotAvalonia weaver; see IsGeneratedBuildOutput.
        private static readonly IReadOnlyCollection<string> WeavedInTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "HotAvalonia.AvaloniaHotReloadAttribute",
            "HotAvalonia.AvaloniaHotReloadExtensions",
            "ihc_openvisual_ProcessedByFody",
        };

        // The document-lifecycle members exactly one GUI type may call. Query and edit
        // members (Current/CanApply/Apply/Undo/Redo/…) are deliberately NOT here: the drag-over probe and the
        // registry's availability gates must stay free to ask the document questions from anywhere.
        private static readonly IReadOnlyCollection<string> DocumentLifecycleMemberNames =
            new[]
            {
                nameof(global::Ihc.Vis.IProjectDocument.Open),
                nameof(global::Ihc.Vis.IProjectDocument.MarkSaved),
                nameof(global::Ihc.Vis.IProjectDocument.Close),
            };

        // The single sanctioned owner of document lifecycle in the GUI.
        private static readonly IReadOnlyCollection<string> LifecycleChokepoint =
            new[] { typeof(global::ihc_openvisual.Services.ProjectWorkflow).FullName! };

        /// <summary>
        /// <c>ProjectWorkflow</c> is the one GUI type that may
        /// open a document — <c>ProjectAppService.OpenDocument</c> — or drive its lifecycle
        /// (<c>Open</c>/<c>MarkSaved</c>/<c>Close</c>). The disease this prevents is a second document opened from a
        /// dialog or view-model: two documents over one file means two undo histories, so edits made through one are
        /// invisible to the other and are silently lost on save. Nothing behavioural catches that until a user's work
        /// disappears, which is precisely why it is pinned structurally. Armed by
        /// <see cref="LifecycleChokepointScan_IsArmed"/> over a seeded off-workflow caller in this test assembly.
        /// </summary>
        [Test]
        public void DocumentLifecycle_IsOwnedOnlyByTheWorkflow()
        {
            AssertMembersCalledOnlyFrom(Gui, GuiRoot, typeof(global::Ihc.Vis.ProjectAppService).FullName!,
                new[] { nameof(global::Ihc.Vis.ProjectAppService.OpenDocument) }, LifecycleChokepoint,
                "the document-opening door",
                "only ProjectWorkflow may open a document — a second document over the same file splits the undo history and silently loses edits");

            AssertMembersCalledOnlyFrom(Gui, GuiRoot, typeof(global::Ihc.Vis.IProjectDocument).FullName!,
                DocumentLifecycleMemberNames, LifecycleChokepoint,
                "the document lifecycle members",
                "only ProjectWorkflow may drive document lifecycle (Open/MarkSaved/Close) — query and edit members stay open to all");
        }

        // Seeded violator for the lifecycle chokepoint's positive control: opens a document and drives its whole
        // lifecycle from a type that is NOT the workflow.
        private static class SeededOffChokepointLifecycleCaller
        {
            public static void Call(global::Ihc.Vis.ProjectAppService service, global::Ihc.Vis.Projects.Project project)
            {
                global::Ihc.Vis.IProjectDocument document = service.OpenDocument(project);
                document.Open(project);
                document.MarkSaved(project);
                document.Close();
            }
        }

        /// <summary>The positive control for <see cref="DocumentLifecycle_IsOwnedOnlyByTheWorkflow"/>: pointed at the
        /// seeded off-workflow caller, the chokepoint scan MUST report every lifecycle member — proving it detects
        /// real call edges rather than passing because the GUI happens to make none.</summary>
        [Test]
        public void LifecycleChokepointScan_IsArmed()
        {
            string testRoot = typeof(OpenVisualArchitectureTests).Namespace!;
            var seededCalls = MethodCallEdges(OwnTestAssembly.Value, testRoot)
                .Where(edge => edge.TargetType == typeof(global::Ihc.Vis.IProjectDocument).FullName
                               && DocumentLifecycleMemberNames.Contains(edge.Member))
                .Select(edge => edge.Member)
                .Distinct()
                .ToList();

            Assert.That(seededCalls, Is.EquivalentTo(DocumentLifecycleMemberNames),
                "the chokepoint scan must detect every seeded Open/MarkSaved/Close call");
            Assert.Throws<AssertionException>(() =>
                AssertMembersCalledOnlyFrom(OwnTestAssembly.Value, testRoot,
                    typeof(global::Ihc.Vis.ProjectAppService).FullName!,
                    new[] { nameof(global::Ihc.Vis.ProjectAppService.OpenDocument) }, LifecycleChokepoint,
                    "seeded probe", "seeded probe"),
                "the production assertion must reject the seeded off-workflow OpenDocument call");
            Assert.Throws<AssertionException>(() =>
                AssertMembersCalledOnlyFrom(OwnTestAssembly.Value, testRoot,
                    typeof(global::Ihc.Vis.IProjectDocument).FullName!, DocumentLifecycleMemberNames,
                    LifecycleChokepoint, "seeded probe", "seeded probe"),
                "the production assertion must reject the seeded off-workflow document-lifecycle calls");
        }

        // Seeded violator for the member-call scan's positive control: genuinely CALLS all three facade members.
        private static class SeededStatelessFacadeCaller
        {
            public static void Call(global::Ihc.Vis.ProjectAppService service,
                global::Ihc.Vis.Projects.Project project, global::Ihc.Vis.Session.ProjectCommand command)
            {
                service.CanApply(project, command);
                service.Preview(project, command);
                service.Apply(project, command);
            }
        }

        // This test assembly read into a second small model so the seeded violator above is scannable.
        private static readonly System.Lazy<Architecture> OwnTestAssembly = ArchitectureModels.ArchitectureTests;

        /// <summary>The positive control for <see cref="Gui_DoesNotCallTheStatelessFacade"/>: pointed at the
        /// seeded caller, the member-call scan MUST report — proving it detects real Apply/CanApply/Preview call
        /// edges rather than passing because it sees nothing.</summary>
        [Test]
        public void StatelessFacadeCallScan_IsArmed()
        {
            var detectedMembers = MethodCallEdges(OwnTestAssembly.Value,
                    typeof(OpenVisualArchitectureTests).Namespace!)
                .Where(edge => edge.TargetType == typeof(global::Ihc.Vis.ProjectAppService).FullName
                               && StatelessFacadeMemberNames.Contains(edge.Member))
                .Select(edge => edge.Member)
                .Distinct()
                .ToList();

            Assert.That(detectedMembers, Is.EquivalentTo(StatelessFacadeMemberNames),
                "the member-call scan must detect every seeded Apply/CanApply/Preview call");

            Assert.Throws<AssertionException>(() =>
                AssertDoesNotCallMembers(OwnTestAssembly.Value, typeof(OpenVisualArchitectureTests).Namespace!,
                    typeof(global::Ihc.Vis.ProjectAppService).FullName!, StatelessFacadeMemberNames,
                    "seeded stateless facade", "seeded stateless facade"),
                "the production assertion must reject the seeded calls, not merely expose raw call edges");
        }

        /// <summary>
        /// The MVVM dependency direction: the view layer binds to view-models, never the reverse. View-models must
        /// not depend on the Avalonia view layer — the Views (windows/dialogs), custom Controls, or value Converters
        /// — which keeps their presentation logic headlessly testable and prevents a view-model from reaching a
        /// concrete window. (The complement of <see cref="ViewModels_DoNotDependOn_Avalonia"/>, which bans the
        /// framework itself; this bans the first-party Avalonia-touching layers a transitive-free dependency check
        /// would otherwise miss.)
        /// </summary>
        [Test]
        public void ViewModels_DoNotDependOn_ViewLayer()
        {
            AssertNoDependency(Gui, Subtree(ViewModels), Views,
                "view-models must not depend on the Views — the view layer binds to view-models, not the reverse");
            AssertNoDependency(Gui, Subtree(ViewModels), Controls,
                "view-models must not depend on the Avalonia custom controls");
            AssertNoDependency(Gui, Subtree(ViewModels), Converters,
                "view-models must not depend on the Avalonia value converters");
        }

        /// <summary>
        /// The Humble Object direction: view-models depend on UI-effect ports such as <c>IDialogService</c> and
        /// <c>IThemeService</c>, never a concrete implementation. Semantic discovery includes Avalonia adapters,
        /// null objects, and future implementations while leaving unrelated Services workflows legal.
        /// </summary>
        [Test]
        public void ViewModels_DependOnUiEffectPortsNotImplementations() =>
            AssertNoDependencyOnTypeNames(Gui, ViewModels, UiEffectAdapterTypeNames(), "concrete UI-effect service implementations",
                "view-models must depend on Services-layer UI-effect ports, not concrete adapters or null objects");

        /// <summary>
        /// The mirror of the MVVM direction: the view layer drives the model only through its bound view-model, so a
        /// view, control, or converter must not reach the session, SDK facade, command gateway, or a
        /// Services-layer workflow that drives them. The composition root is outside those namespaces and may wire
        /// the layers together. Session payload records remain legal; only driver types are forbidden.
        /// </summary>
        [TestCaseSource(nameof(ViewLayerNamespaces))]
        public void ViewLayer_DoesNotDriveTheSessionDirectly(string viewLayerNamespace) =>
            AssertNoDependencyOnTypeNames(Gui, viewLayerNamespace, SessionDriverTypeNames(), "the session/facade/command drivers",
                "the view layer must drive the model through its view-model, not IProjectDocument, ProjectWorkflow, ProjectAppService, or the ProjectCommands gateway directly");

        [Test]
        public void UiEffectAdapterDiscovery_IsArmed() =>
            Assert.That(UiEffectAdapterTypeNames(),
                Is.SupersetOf(new[]
                {
                    typeof(global::ihc_openvisual.Services.AvaloniaDialogService).FullName!,
                    typeof(global::ihc_openvisual.Services.ThemeService).FullName!,
                    typeof(global::ihc_openvisual.Services.NullDialogService).FullName!,
                    typeof(global::ihc_openvisual.Services.NullThemeService).FullName!,
                }),
                "every concrete implementation of a Services-layer UI-effect port must be discovered");

        [Test]
        public void SessionDriverDiscovery_IsArmed() =>
            Assert.That(SessionDriverTypeNames(),
                Is.SupersetOf(new[]
                {
                    typeof(global::Ihc.Vis.IProjectDocument).FullName!,
                    typeof(global::Ihc.Vis.ProjectAppService).FullName!,
                    typeof(global::ihc_openvisual.Services.ProjectWorkflow).FullName!,
                    Services + ".CatalogImportWorkflow",
                    Services + ".ProjectReportWorkflow",
                }),
                "the driver set must include the engine ports and Services-layer workflows that reach them");

        /// <summary>
        /// Identity that survives tree rebuilds (ARCHITECTURE.md Design Challenge 4): every edit rebuilds the
        /// immutable project tree, so any <see cref="Ihc.Vis.Projects.Project"/> / <see cref="Ihc.Vis.Model.ProjectElement"/>
        /// reference retained in bound UI state goes stale at once — the GUI therefore points at elements by
        /// <see cref="Ihc.Vis.Model.ElementId"/>, never by object reference. No instance or static field may directly
        /// retain <c>Project</c>/<c>ProjectElement</c>, including through collections and value wrappers such as
        /// <c>ElementView</c>. The assembly-wide editing-layer dependency rule separately excludes editing handles.
        ///
        /// Scope is the whole assembly, not just view-models. It was view-model-scoped while
        /// the workflow kept snapshot stacks, and the exemption was justified as "Services legitimately hold a
        /// Project"; the document port deleted those stacks, and an assembly-wide audit confirmed no GUI type
        /// outside the view-models holds one any more — so the exemption was retired rather than inherited.
        /// Holding a snapshot is what produces both stale references and the dual-history bug, so the ban belongs
        /// everywhere, and <c>Services</c> is precisely where a snapshot would most plausibly be hoarded again.
        ///
        /// <c>ProjectTreeProjector</c> is the one allowlisted survivor: it retains a snapshot only for the duration
        /// of a single projection pass and is never bound or stored as UI state. Parameters, returns and locals stay
        /// legal throughout — reading a project into a local, using it and dropping it is the sanctioned pattern.
        /// </summary>
        [Test]
        public void Gui_DoesNotDirectlyRetainProjectSnapshots()
        {
            var stateOwners = typeof(global::ihc_openvisual.App).Assembly.GetTypes()
                .Where(IsGuiStateOwner)
                .ToList();
            Assert.That(stateOwners, Is.Not.Empty, "sanity: the GUI assembly exposes authored state-owner types");

            IReadOnlyList<string> offences = RetainedSnapshotOffences(stateOwners);

            Assert.That(offences, Is.Empty,
                "GUI fields must reference project elements by ElementId, not retain Project/ProjectElement snapshots that go stale on the next edit: "
                + string.Join(", ", offences));
        }

        /// <summary>
        /// Pins the GUI namespace topology the rules above are anchored to. Anchors read <c>typeof(T).Namespace</c>,
        /// so a namespace rename is followed automatically; the gap that leaves — an anchor type <i>moved</i> into a
        /// different existing namespace, silently retargeting its rule — is turned into a named failure here (the GUI
        /// analogue of the SDK fixture's <c>LayerAnchors_ResolveToTheirDocumentedNamespaces</c>).
        /// </summary>
        [Test]
        public void GuiLayerAnchors_ResolveToTheirDocumentedNamespaces() =>
            Assert.Multiple(() =>
            {
                Assert.That(GuiRoot, Is.EqualTo("ihc_openvisual"), $"{nameof(global::ihc_openvisual.App)} anchors the GUI assembly root");
                Assert.That(ViewModels, Is.EqualTo("ihc_openvisual.ViewModels"), "the view-model layer");
                Assert.That(Views, Is.EqualTo("ihc_openvisual.Views"), "the Avalonia view layer");
                Assert.That(Controls, Is.EqualTo("ihc_openvisual.Controls"), "the Avalonia custom controls");
                Assert.That(Converters, Is.EqualTo("ihc_openvisual.Converters"), "the Avalonia value converters");
                Assert.That(Services, Is.EqualTo("ihc_openvisual.Services"), "the GUI service and workflow layer");
                Assert.That(VisIo, Is.EqualTo("Ihc.Vis.Io"), $"{nameof(global::Ihc.Vis.Io.ProjectSerializer)} anchors the offline IO engine");
                Assert.That(Editing, Is.EqualTo("Ihc.Vis.Editing"), $"{nameof(global::Ihc.Vis.Editing.ProjectEditor)} anchors the editing layer");
                Assert.That(SoapNs, Is.EqualTo("Ihc.Soap"), "the generated SOAP parent namespace");
            });

        /// <summary>
        /// A backstop that the GUI suite is green because its rules hold, not because the mechanism is broken. The
        /// <c>App</c> composition root derives from <c>Avalonia.Application</c>, so a rule forbidding a dependency on
        /// Avalonia MUST be reported as violated. If this stops throwing, either the <c>Check()</c> plumbing has
        /// stopped detecting violations or — the specific footgun this fixture's referenced-type target sets guard
        /// against — the Avalonia stubs stopped being loaded, and every green GUI rule above is suspect.
        /// </summary>
        [Test]
        public void GuiFixture_DetectsKnownDependencyViolation() =>
            AssertDependencyIsDetected(Gui, typeof(global::ihc_openvisual.App), AvaloniaNs,
                $"{nameof(global::ihc_openvisual.App)} derives from Avalonia.Application; a rule forbidding an Avalonia dependency must fail — otherwise the fitness function (or the referenced-type target set) is not detecting dependencies");

        /// <summary>
        /// Backstop proving the two <b>custom</b> boundary scans (<see cref="Gui_DoesNotDependOn_ControllerApiServices"/>'s
        /// name-based dependency scan and <see cref="Gui_DoesNotConstruct_ProjectCommands"/>'s constructor scan) can
        /// actually report a violation — they are separate mechanisms from the fluent <c>Check()</c> the SDK fixture's
        /// known-violation test guards. The GUI genuinely both depends on and constructs <c>ProjectAppService</c> (the
        /// composition root composes it), so placing that facade in each scan's forbidden set MUST fail. If either
        /// stops throwing, that scan has silently stopped detecting its edge kind and its green rule above is worthless.
        /// (Not wrapped in <c>Assert.Multiple</c> on purpose: the scans assert internally, and multiple-assert mode
        /// would swallow that inner failure instead of surfacing it as the exception these positive controls expect.)
        /// </summary>
        [Test]
        public void DependencyNameScan_DetectsKnownFacadeEdge()
        {
            string facade = typeof(global::Ihc.Vis.ProjectAppService).FullName!;
            Assert.Throws<AssertionException>(() =>
                AssertNoDependencyOnTypeNames(Gui, GuiRoot, new HashSet<string> { facade },
                    "seeded facade dependency", "seeded facade dependency"),
                "the production dependency assertion must reject the GUI's known ProjectAppService edge");
        }

        [Test]
        public void ConstructorCallScan_DetectsKnownFacadeEdge()
        {
            string facade = typeof(global::Ihc.Vis.ProjectAppService).FullName!;
            Assert.Throws<AssertionException>(() =>
                AssertDoesNotConstructTypeNames(Gui, GuiRoot, new HashSet<string> { facade },
                    "seeded facade construction", "seeded facade construction"),
                "the production constructor assertion must reject the GUI's known ProjectAppService construction");
        }

        private static class SeededStaticSnapshotOwner
        {
#pragma warning disable CS0649 // detector seed: intentionally never assigned
            internal static global::Ihc.Vis.Projects.Project? Snapshot;
            internal static global::Ihc.Vis.ElementView View;
#pragma warning restore CS0649
        }

        /// <summary>
        /// Backstop proving the reflection detector behind <see cref="Gui_DoesNotDirectlyRetainProjectSnapshots"/>
        /// is armed: it must flag a retained <c>ProjectElement</c> (directly and through a collection) and must NOT
        /// flag an <c>ElementId</c>; and it must include long-lived helpers while excluding the transient projector.
        /// Without this, that rule could pass because the detector matches nothing, not because the state
        /// is clean.
        /// </summary>
        [Test]
        public void IdentityDetector_IsArmed() =>
            Assert.Multiple(() =>
            {
                Assert.That(RetainedModelType(typeof(global::Ihc.Vis.Model.ProjectElement)), Is.Not.Null,
                    "the detector must flag a retained ProjectElement");
                Assert.That(RetainedModelType(typeof(List<global::Ihc.Vis.Model.ProjectElement>)), Is.Not.Null,
                    "the detector must flag a collection of ProjectElement");
                Assert.That(RetainedModelType(typeof(global::Ihc.Vis.Projects.Project)), Is.Not.Null,
                    "the detector must flag a retained Project");
                Assert.That(RetainedModelType(typeof(global::Ihc.Vis.ElementView)), Is.Not.Null,
                    "the detector must inspect a non-generic value wrapper and flag ElementView's Project fields");
                Assert.That(RetainedModelType(typeof(global::Ihc.Vis.Model.ElementId)), Is.Null,
                    "the detector must not flag ElementId — the sanctioned reference");
                Assert.That(RetainedModelType(typeof(global::Ihc.Vis.Model.ElementId?)), Is.Null,
                    "the detector must not flag ElementId?");
                Assert.That(RetainedModelType(typeof(Func<global::Ihc.Vis.Projects.Project?>)), Is.Null,
                    "a callback that obtains the current immutable snapshot does not retain that snapshot");
                Assert.That(RetainedSnapshotOffences(new[] { typeof(SeededStaticSnapshotOwner) }), Has.Count.EqualTo(2),
                    "the production detector must report static direct and ElementView-wrapped snapshots");
                Assert.That(IsGuiStateOwner(typeof(global::ihc_openvisual.ViewModels.TreeNodeViewModel)), Is.True,
                    "TreeNodeViewModel is a bound view-model and in scope");
                Assert.That(IsGuiStateOwner(typeof(global::ihc_openvisual.ViewModels.TreeDragDropController)), Is.True,
                    "long-lived controllers owned by a view-model are in scope");

                // These checks pin that the scope really is the whole GUI rather than only the view-model namespace.
                Assert.That(IsGuiStateOwner(typeof(global::ihc_openvisual.Services.ProjectWorkflow)), Is.True,
                    "the Services layer is in scope — it is where a snapshot would most plausibly be hoarded");
                Assert.That(IsGuiStateOwner(typeof(global::ihc_openvisual.App)), Is.True,
                    "the composition root is in scope — the rule is assembly-wide, not layer-wide");
                Assert.That(RetainedStateFields(typeof(global::ihc_openvisual.Services.ProjectWorkflow)), Is.Not.Empty,
                    "the field walk must actually yield fields for a Services type — scope without a working walk inspects nothing");
                Assert.That(IsGuiStateOwner(typeof(global::ihc_openvisual.ViewModels.ProjectTreeProjector)), Is.False,
                    "the per-projection helper is the explicit transient exemption");

                // The synthesised-type exclusion, armed against the real assembly: the registry's command bodies are
                // async lambdas, so the view-model namespace genuinely contains Roslyn state machines whose hoisted
                // locals would otherwise read as retained fields. Prove they exist, then prove they are out of scope.
                var synthesised = typeof(global::ihc_openvisual.App).Assembly.GetTypes()
                    .Where(t => t.Namespace == ViewModels && t.Name.Contains("b__", StringComparison.Ordinal))
                    .ToList();
                Assert.That(synthesised, Is.Not.Empty,
                    "sanity: the view-models really do compile lambdas into synthesised closure/state-machine types");
                Assert.That(synthesised.Where(IsGuiStateOwner), Is.Empty,
                    "a synthesised closure/state machine is not a state owner — its <x>5__n members are hoisted locals of one operation");
            });

        // ---- Reflection helpers shared by the automation-surface rules ---------------------------------------------

        /// <summary>The GUI's own authored types: everything the assembly declares except the compiler's and the XAML
        /// compiler's emissions. The one definition of "authored", shared with
        /// <see cref="GuiScanScope_CoversEveryTypeTheAssemblyDeclares"/> — which is what makes that test's claim (the
        /// namespace-rooted scans span the whole assembly) hold for the reflection rules below too.</summary>
        private static IEnumerable<Type> AuthoredGuiTypes() =>
            typeof(global::ihc_openvisual.App).Assembly.GetTypes()
                .Where(type => !IsSynthesised(type) && !IsGeneratedBuildOutput(type));

        // ---- Forbidden-type name sets (reflected from the loaded assemblies) --------------------------------------

        /// <summary>Every type of the controller API-service tier (the <c>IIHCApiService</c> contract, its
        /// per-service interfaces, and their implementations), by full name.</summary>
        private static IReadOnlyCollection<string> ApiServiceTypeNames() =>
            typeof(global::Ihc.IIHCApiService).Assembly.GetTypes()
                .Where(t => typeof(global::Ihc.IIHCApiService).IsAssignableFrom(t))
                .Select(t => t.FullName!)
                .ToHashSet();

        /// <summary>Every <see cref="Ihc.Vis.Session.ProjectCommand"/> type by full name (the abstract base, the
        /// generic result variant, <c>CompositeCommand</c>, and the concrete authoring commands).</summary>
        private static IReadOnlyCollection<string> CommandTypeNames() =>
            typeof(global::Ihc.Vis.Session.ProjectCommand).Assembly.GetTypes()
                .Where(t => typeof(global::Ihc.Vis.Session.ProjectCommand).IsAssignableFrom(t))
                .Select(t => t.FullName!)
                .ToHashSet();

        /// <summary>Every concrete implementation of a UI-effect port, discovered by assignability rather than a
        /// closed adapter roster.</summary>
        private static IReadOnlyCollection<string> UiEffectAdapterTypeNames()
        {
            Type[] uiEffectPorts =
            {
                typeof(global::ihc_openvisual.Services.IDialogService),
                typeof(global::ihc_openvisual.Services.IThemeService),
            };

            return AuthoredGuiTypes()
                .Where(type => !type.IsInterface && !type.IsAbstract
                               && uiEffectPorts.Any(port => port.IsAssignableFrom(type)))
                .Select(type => type.FullName!)
                .ToHashSet();
        }

        /// <summary>The SDK driver ports plus every Services-layer workflow that reaches one, directly or through
        /// another workflow.</summary>
        private static IReadOnlyCollection<string> SessionDriverTypeNames()
        {
            var drivers = new HashSet<string>
            {
                typeof(global::Ihc.Vis.ProjectAppService).FullName!,
                typeof(global::Ihc.Vis.ProjectCommands).FullName!,
                typeof(global::Ihc.Vis.IProjectDocument).FullName!,
            };
            var edges = DependencyEdges(Gui, GuiRoot)
                .Select(edge => (Origin: OutermostTypeName(edge.Origin), Target: OutermostTypeName(edge.Target)))
                .Distinct()
                .ToList();

            bool changed;
            do
            {
                changed = false;
                foreach (var edge in edges.Where(edge => drivers.Contains(edge.Target)
                                                         && IsServicesType(edge.Origin)))
                    changed |= drivers.Add(edge.Origin);
            }
            while (changed);

            return drivers;

            static bool IsServicesType(string fullName) =>
                typeof(global::ihc_openvisual.App).Assembly.GetType(fullName)?.Namespace == Services;
        }

        /// <summary>The engine's <see cref="Ihc.Vis.Session.ProjectDocumentSession"/> command-runner, by full name —
        /// the single <c>Ihc.Vis.Session</c> type the GUI must reach only behind the <c>ProjectAppService</c> facade
        /// (the command / outcome / change-set contract types in that namespace stay allowed, so this is a
        /// single-TYPE ban, never a namespace ban).</summary>
        private static IReadOnlyCollection<string> ProjectDocumentSessionTypeName() => new HashSet<string>
        {
            typeof(global::Ihc.Vis.Session.ProjectDocumentSession).FullName!,
        };

        // ---- Reflection helpers for the identity (ElementId-not-reference) rule ------------------------------------

        private static bool IsGuiStateOwner(Type type) =>
            type.FullName is { } name
            && name.StartsWith(GuiRoot + ".", StringComparison.Ordinal)
            && !IsSynthesised(type)
            && !IsGeneratedBuildOutput(type)
            && !IdentityRuleAllowlist.Contains(type);

        // Types allowed to hold a Project/ProjectElement, each for an audited reason.
        private static readonly IReadOnlyCollection<Type> IdentityRuleAllowlist = new HashSet<Type>
        {
            // Retains one snapshot for the duration of a single projection pass; never bound, never stored as UI state.
            typeof(global::ihc_openvisual.ViewModels.ProjectTreeProjector),
        };

        // A type Roslyn synthesised rather than the author writing it: a lambda display class, or an async/iterator
        // state machine. Only the OUTERMOST synthesised type carries [CompilerGenerated] — the state machine of an
        // async lambda (`<<Delete>b__0>d`, nested in `<>c__DisplayClass9_0`) carries no attributes at all — so the
        // enclosing chain must be walked, with Roslyn's unspeakable-name convention as the second signal. Their
        // `<x>5__n` members are hoisted LOCALS of one operation, never retained UI state, and are therefore outside
        // the identity rule: reading a Project into a local, using it and dropping it is exactly the sanctioned use.
        private static bool IsSynthesised(Type type) =>
            EnclosingChain(type).Any(t =>
                t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                || t.Name.Contains('<', StringComparison.Ordinal));

        private static IEnumerable<Type> EnclosingChain(Type type)
        {
            for (Type? t = type; t is not null; t = t.DeclaringType)
                yield return t;
        }

        // Instance and static fields declared by the owner's first-party hierarchy. This includes explicit fields
        // and compiler-generated backing fields while stopping before external framework base classes.
        private static IEnumerable<FieldInfo> RetainedStateFields(Type owner)
        {
            for (Type? t = owner; t is not null && t.Assembly == owner.Assembly; t = t.BaseType)
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Static
                                                     | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    yield return f;
        }

        private static IReadOnlyList<string> RetainedSnapshotOffences(IEnumerable<Type> owners) =>
            owners.SelectMany(owner => RetainedStateFields(owner)
                    .Select(field => (Owner: owner, Field: field, Stale: RetainedModelType(field.FieldType))))
                .Where(hit => hit.Stale is not null)
                .Select(hit => $"{hit.Owner.Name}.{hit.Field.Name} : {hit.Stale!.Name}")
                .ToList();

        // The stale model type a field would retain, reached through nullable/array/generic shapes and user-defined
        // value wrappers. ElementId (and collections of it) is the sanctioned reference and is never flagged.
        private static Type? RetainedModelType(Type fieldType) =>
            FirstReferenced(fieldType, candidate =>
                candidate == typeof(global::Ihc.Vis.Projects.Project)
                || candidate == typeof(global::Ihc.Vis.Model.ProjectElement));

        // The first type a member's declared type REACHES that matches the predicate — through Nullable&lt;T&gt;,
        // arrays and generic arguments. Delegates are opaque: a callback that obtains the current snapshot does not
        // retain it. Shared by the identity rule and the purity zone so both traverse types identically.
        private static Type? FirstReferenced(Type memberType, Func<Type, bool> forbidden) =>
            typeof(Delegate).IsAssignableFrom(memberType)
                ? null
                : TypeAndArguments(memberType).FirstOrDefault(forbidden);

    }
}
