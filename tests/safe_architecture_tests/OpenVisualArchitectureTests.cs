using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
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
    public class OpenVisualArchitectureTests
    {
        // The desktop GUI read into ArchUnitNET's model once for the whole fixture.
        private static readonly Architecture Gui = new ArchLoader()
            .LoadAssemblies(typeof(global::ihc_openvisual.App).Assembly)
            .Build();

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

        // The offline .vis IO layer, anchored to a public engine type so a rename fails the compile, not the check.
        private static readonly string VisIo =
            typeof(global::Ihc.Vis.Io.ProjectSerializer).Namespace!; // Ihc.Vis.Io

        // The live-session editing layer, anchored to a public type so a rename fails the compile, not the check.
        private static readonly string Editing =
            typeof(global::Ihc.Vis.Editing.ProjectEditor).Namespace!; // Ihc.Vis.Editing

        // The report-generation layer, anchored to a public type so a rename fails the compile, not the check.
        // (The render-ready report DTOs now live in Ihc.Vis; only the ReportBuilder generator remains here.)
        private static readonly string Reporting =
            typeof(global::Ihc.Vis.Reporting.ReportBuilder).Namespace!; // Ihc.Vis.Reporting

        // The GUI's report RENDERER layer (T021/T035), anchored to the renderer type so it is isolated from the other
        // Services (which legitimately hold a Project) and its DTO-only purity can be scoped and pinned.
        private static readonly string Renderer =
            typeof(global::ihc_openvisual.Services.Reporting.ReportHtmlRenderer).Namespace!; // ihc_openvisual.Services.Reporting

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
        public void Gui_DoesNotHandRollXml() =>
            AssertAssemblyHasNoDependency(Gui, SystemXmlNs,
                "the GUI must not parse or emit .vis XML itself; all XML IO belongs to the Ihc.Vis engine");

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
        /// Report <i>generation</i> belongs to the SDK: <c>ProjectAppService.Generate*Report</c> runs
        /// <c>ReportBuilder</c> and hands back the render-ready DTOs (which live in <c>Ihc.Vis</c>). The GUI renders
        /// those DTOs 1-to-1 into HTML but must never run <c>ReportBuilder</c> itself — that is report business
        /// logic, and re-deriving it in the UI would fork the report content from the SDK's single source of truth.
        /// </summary>
        [Test]
        public void Gui_DoesNotDependOn_Reporting() =>
            AssertAssemblyHasNoDependency(Gui, Reporting,
                "the GUI renders report DTOs but must generate them through ProjectAppService, not run ReportBuilder itself");

        /// <summary>
        /// The GUI's report RENDERER (<c>ReportHtmlRenderer</c>) is a pure 1-to-1 transform of the render-ready
        /// combined model into HTML (D14: "the app renders and applies switches, it computes nothing"). It must see
        /// ONLY the render-ready DTOs — never the mutable project tree (<c>Project</c>/<c>ProjectElement</c>), the
        /// report generator's tree indexer (<c>TreeIndex</c>) or any live-session <c>Ihc.Vis.Editing</c> type — so it
        /// cannot re-derive report content the SDK owns. <c>ElementId</c> stays sanctioned: the switch data on the
        /// combined model legitimately carries per-element ids. Scoped to the isolated renderer namespace (the other
        /// Services legitimately hold a <c>Project</c>). Armed by the shared <c>AssertNoDependencyOnTypeNames</c> scan
        /// whose positive control is <see cref="CustomScans_DetectKnownFacadeEdges"/>.
        /// </summary>
        [Test]
        public void Renderer_SeesOnlyRenderReadyDtos() =>
            AssertNoDependencyOnTypeNames(Gui, Renderer, RenderReadyPurityForbiddenTypeNames(),
                "the mutable project tree / tree indexer / editing layer",
                "the report renderer must transform the render-ready combined model 1-to-1 and never touch Project/ProjectElement/TreeIndex or a live-session editing type (ElementId stays sanctioned)");

        /// <summary>
        /// Applying a command belongs to the SDK: <c>ProjectAppService.Apply</c>/<c>CanApply</c>/<c>Preview</c> open a
        /// throwaway <c>ProjectDocumentSession</c> internally and run the command once. The GUI must not open that
        /// engine runner itself — <c>ProjectWorkflow</c> delegates to the facade and layers only its document
        /// lifecycle (Current, undo/redo, dirty/version, auto-backup) on top. This bans the one engine TYPE by name,
        /// NEVER the <c>Ihc.Vis.Session</c> namespace: the command / outcome / change-set contract types live there
        /// and the GUI legitimately consumes them. (Armed by <see cref="CustomScans_DetectKnownFacadeEdges"/>, the
        /// positive control over the same <c>AssertNoDependencyOnTypeNames</c> scan.)
        /// </summary>
        [Test]
        public void Gui_DoesNotDependOn_ProjectDocumentSession() =>
            AssertNoDependencyOnTypeNames(Gui, GuiRoot, ProjectDocumentSessionTypeName(), "the ProjectDocumentSession engine runner",
                "the GUI must apply commands through ProjectAppService.Apply/CanApply/Preview, not open a ProjectDocumentSession itself");

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
        /// The Humble Object direction: view-models depend on the UI-effect <b>ports</b> (<c>IDialogService</c>,
        /// <c>IThemeService</c>), never their Avalonia <b>adapters</b>. The adapters live in the same
        /// <c>Services</c> namespace as the Avalonia-free <c>ProjectWorkflow</c> the view-models legitimately use, so
        /// this cannot be a namespace ban; it forbids the two concrete adapter types by name. (The inert
        /// <c>NullDialogService</c>/<c>NullThemeService</c> used by the design-time constructor are deliberately not
        /// forbidden — they are the test/design seam, not Avalonia.)
        /// </summary>
        [Test]
        public void ViewModels_DoNotDependOn_AvaloniaAdapters() =>
            AssertNoDependencyOnTypeNames(Gui, ViewModels, AvaloniaAdapterTypeNames(), "the Avalonia UI-effect adapters",
                "view-models must depend on the IDialogService/IThemeService ports, not their Avalonia adapters (the Humble Object direction)");

        /// <summary>
        /// The mirror of the MVVM direction: the view layer drives the model only through its bound view-model, so a
        /// window/dialog must not reach the session, the SDK facade, or the command gateway itself. Scoped to the
        /// <c>Views</c> subtree — the composition root (<c>App</c>) is not under it and legitimately wires all three
        /// together. (A view binding to an edit-payload record that happens to live in <c>Ihc.Vis.Session</c> is
        /// fine; only the three driver types are forbidden.)
        /// </summary>
        [Test]
        public void ViewLayer_DoesNotDriveTheSessionDirectly() =>
            AssertNoDependencyOnTypeNames(Gui, Views, SessionDriverTypeNames(), "the session/facade/command drivers",
                "the view layer must drive the model through its view-model, not ProjectWorkflow, ProjectAppService or the ProjectCommands gateway directly");

        /// <summary>
        /// Identity that survives tree rebuilds (ARCHITECTURE.md Design Challenge 4): every edit rebuilds the
        /// immutable project tree, so any <see cref="Ihc.Vis.Projects.Project"/> / <see cref="Ihc.Vis.Model.ProjectElement"/>
        /// reference retained in bound UI state goes stale at once — the GUI therefore points at elements by
        /// <see cref="Ihc.Vis.Model.ElementId"/>, never by object reference. This checks the retained state directly:
        /// no field of any bound view-model type (an <c>ObservableObject</c>) may be a <c>Project</c>, a
        /// <c>ProjectElement</c>, or a live-editing handle (an <c>Ihc.Vis.Editing</c> type). Transient per-render
        /// helpers (the projector, the coordinators) are not <c>ObservableObject</c>s and legitimately hold a
        /// <c>Project</c> for the span of one projection, so they are out of scope by construction — the invariant is
        /// about long-lived bound state, not a method's working set.
        /// </summary>
        [Test]
        public void ViewModels_PointAtElementsByIdNotObjectReference()
        {
            var boundViewModels = typeof(global::ihc_openvisual.App).Assembly.GetTypes()
                .Where(IsObservableObject)
                .ToList();
            Assert.That(boundViewModels, Is.Not.Empty, "sanity: the GUI exposes bound (ObservableObject) view-model types");

            var offences = new List<string>();
            foreach (Type viewModel in boundViewModels)
                foreach (FieldInfo field in RetainedFields(viewModel))
                    if (RetainedModelType(field.FieldType) is { } stale)
                        offences.Add($"{viewModel.Name}.{field.Name} : {stale.Name}");

            Assert.That(offences, Is.Empty,
                "bound view-models must reference project elements by ElementId, not retain Project/ProjectElement/editing handles that go stale on the next edit: "
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
                Assert.That(VisIo, Is.EqualTo("Ihc.Vis.Io"), $"{nameof(global::Ihc.Vis.Io.ProjectSerializer)} anchors the offline IO engine");
                Assert.That(Editing, Is.EqualTo("Ihc.Vis.Editing"), $"{nameof(global::Ihc.Vis.Editing.ProjectEditor)} anchors the editing layer");
                Assert.That(Reporting, Is.EqualTo("Ihc.Vis.Reporting"), $"{nameof(global::Ihc.Vis.Reporting.ReportBuilder)} anchors the report generator");
                Assert.That(Renderer, Is.EqualTo("ihc_openvisual.Services.Reporting"), $"{nameof(global::ihc_openvisual.Services.Reporting.ReportHtmlRenderer)} anchors the GUI report renderer");
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
        public void Fixture_DetectsAKnownViolation() =>
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
        public void CustomScans_DetectKnownFacadeEdges()
        {
            var facade = new HashSet<string> { typeof(global::Ihc.Vis.ProjectAppService).FullName! };
            Assert.That(() => AssertNoDependencyOnTypeNames(Gui, GuiRoot, facade, "known facade dependency", "positive control"),
                Throws.Exception,
                "the name-based dependency scan did not detect the GUI's real dependency on ProjectAppService — the scan is not working");
            Assert.That(() => AssertDoesNotConstructTypeNames(Gui, GuiRoot, facade, "known facade construction", "positive control"),
                Throws.Exception,
                "the constructor scan did not detect the GUI's real construction of ProjectAppService — the newobj detection is not working");
        }

        /// <summary>
        /// Backstop proving the reflection detector behind <see cref="ViewModels_PointAtElementsByIdNotObjectReference"/>
        /// is armed: it must flag a retained <c>ProjectElement</c> (directly and through a collection) and must NOT
        /// flag an <c>ElementId</c>; and it must scope to bound <c>ObservableObject</c> types, excluding the transient
        /// projector. Without this, that rule could pass because the detector matches nothing, not because the state
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
                Assert.That(RetainedModelType(typeof(global::Ihc.Vis.Model.ElementId)), Is.Null,
                    "the detector must not flag ElementId — the sanctioned reference");
                Assert.That(RetainedModelType(typeof(global::Ihc.Vis.Model.ElementId?)), Is.Null,
                    "the detector must not flag ElementId?");
                Assert.That(IsObservableObject(typeof(global::ihc_openvisual.ViewModels.TreeNodeViewModel)), Is.True,
                    "TreeNodeViewModel is a bound view-model and in scope");
                Assert.That(IsObservableObject(typeof(global::ihc_openvisual.ViewModels.ProjectTreeProjector)), Is.False,
                    "the per-render projector is not an ObservableObject and is out of scope by construction");
            });

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

        /// <summary>The two concrete Avalonia UI-effect adapters, by full name — the types a view-model must reach
        /// only through their ports.</summary>
        private static IReadOnlyCollection<string> AvaloniaAdapterTypeNames() => new HashSet<string>
        {
            typeof(global::ihc_openvisual.Services.AvaloniaDialogService).FullName!,
            typeof(global::ihc_openvisual.Services.ThemeService).FullName!,
        };

        /// <summary>The session/facade/command driver types the view layer must not touch directly.</summary>
        private static IReadOnlyCollection<string> SessionDriverTypeNames() => new HashSet<string>
        {
            typeof(global::ihc_openvisual.Services.ProjectWorkflow).FullName!,
            typeof(global::Ihc.Vis.ProjectAppService).FullName!,
            typeof(global::Ihc.Vis.ProjectCommands).FullName!,
        };

        /// <summary>The types the DTO-only report renderer must not depend on (T035): the mutable project tree
        /// (<c>Project</c>/<c>ProjectElement</c>), the report generator's private tree indexer (<c>TreeIndex</c>,
        /// reflected by name), and every live-session <c>Ihc.Vis.Editing</c> type (reflected from the SDK assembly).
        /// <c>ElementId</c> is deliberately NOT here — it is sanctioned switch data on the combined model.</summary>
        private static IReadOnlyCollection<string> RenderReadyPurityForbiddenTypeNames()
        {
            var names = new HashSet<string>
            {
                typeof(global::Ihc.Vis.Projects.Project).FullName!,
                typeof(global::Ihc.Vis.Model.ProjectElement).FullName!,
            };
            if (typeof(global::Ihc.Vis.Reporting.ReportBuilder).GetNestedType("TreeIndex", BindingFlags.NonPublic)?.FullName is { } treeIndex)
                names.Add(treeIndex);
            foreach (Type t in typeof(global::Ihc.Vis.Editing.ProjectEditor).Assembly.GetTypes())
                if (t.Namespace == "Ihc.Vis.Editing")
                    names.Add(t.FullName!);
            return names;
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

        private const string ObservableObjectFullName = "CommunityToolkit.Mvvm.ComponentModel.ObservableObject";

        private static bool IsObservableObject(Type type)
        {
            for (Type? b = type.BaseType; b is not null; b = b.BaseType)
                if (b.FullName == ObservableObjectFullName)
                    return true;
            return false;
        }

        // Instance fields declared by the view-model hierarchy up to (not including) ObservableObject — this covers
        // explicit fields and the compiler-generated backing fields of auto-properties and [ObservableProperty]s.
        private static IEnumerable<FieldInfo> RetainedFields(Type viewModel)
        {
            for (Type? t = viewModel; t is not null && t.FullName != ObservableObjectFullName; t = t.BaseType)
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    yield return f;
        }

        // The stale-able model type a field would retain — Project, ProjectElement, or a live-session editing handle
        // (any Ihc.Vis.Editing type) — reached through Nullable<T>, arrays and generic arguments; null if the field
        // holds nothing stale. ElementId (and collections of it) is the sanctioned reference and is never flagged.
        private static Type? RetainedModelType(Type fieldType) =>
            TypeAndArguments(fieldType).FirstOrDefault(candidate =>
                candidate == typeof(global::Ihc.Vis.Projects.Project)
                || candidate == typeof(global::Ihc.Vis.Model.ProjectElement)
                || candidate.Namespace == "Ihc.Vis.Editing");

        private static IEnumerable<Type> TypeAndArguments(Type type)
        {
            if (type.IsByRef || type.IsPointer)
                type = type.GetElementType()!;
            Type core = Nullable.GetUnderlyingType(type) ?? type;
            yield return core;
            if (core.IsArray && core.GetElementType() is { } element)
                foreach (Type inner in TypeAndArguments(element))
                    yield return inner;
            if (core.IsGenericType)
                foreach (Type argument in core.GetGenericArguments())
                    foreach (Type inner in TypeAndArguments(argument))
                        yield return inner;
        }
    }
}
