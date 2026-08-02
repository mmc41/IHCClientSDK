using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using static Ihc.Tests.ArchRuleHelpers;
// ArchUnitNET.Loader also exports a `Type`; the reflection helpers in this fixture mean System.Type throughout.
using Type = System.Type;
// ArchUnitNET.Domain likewise exports an `Assembly` and an `Attribute`; the reflection helpers mean the BCL ones.
using Assembly = System.Reflection.Assembly;
using Attribute = System.Attribute;

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

        // The GUI's report RENDERER layer (T021/T035), anchored to the renderer type so its DTO-only purity can be
        // scoped and pinned separately from the rest of Services. (The original justification — "the other Services
        // legitimately hold a Project" — was retired by the T007 audit: none of them does any more.)
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
        /// ONLY the public type graph reachable from <c>ProjectDocumentationReport</c>, including its legacy report
        /// DTOs and module-map data. Every other SDK type is forbidden, so the renderer cannot reach mutable project,
        /// generation, IO, or editing APIs and re-derive content the SDK owns. Scoped to the isolated renderer
        /// namespace, which now buys strictness rather than tolerance: since T007 no GUI type may RETAIN a
        /// <c>Project</c>, and this rule additionally forbids the renderer from even naming one. Armed by the shared name-based scan
        /// whose positive control is <see cref="CustomScans_DetectKnownFacadeEdges"/>.
        /// </summary>
        [Test]
        public void Renderer_SeesOnlyRenderReadyDtos() =>
            AssertNoDependencyOnTypeNames(Gui, Renderer, NonRenderReadySdkTypeNames(),
                "SDK types outside the public report DTO graph",
                "the report renderer must transform only the render-ready report DTO graph and never reach other SDK model, generation, IO, or editing types");

        /// <summary>
        /// Command execution belongs to the SDK: interactive code holds the <c>IProjectDocument</c> PORT from
        /// <c>ProjectAppService.OpenDocument</c> (crudarch D01), and the stateless <c>Apply/CanApply/Preview</c>
        /// facade serves one-shot callers — either way the GUI must never open the concrete
        /// <c>ProjectDocumentSession</c> engine runner itself. This bans the one engine TYPE by name,
        /// NEVER the <c>Ihc.Vis.Session</c> namespace: the command / outcome / change-set contract types live there
        /// and the GUI legitimately consumes them. (Armed by <see cref="CustomScans_DetectKnownFacadeEdges"/>, the
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

        // The stateless one-shot facade members interactive code must not call (crudarch T022) — the document
        // port (ProjectAppService.OpenDocument → IProjectDocument) is the interactive door.
        private static readonly IReadOnlyCollection<string> StatelessFacadeMemberNames =
            new[]
            {
                nameof(global::Ihc.Vis.ProjectAppService.Apply),
                nameof(global::Ihc.Vis.ProjectAppService.CanApply),
                nameof(global::Ihc.Vis.ProjectAppService.Preview),
            };

        /// <summary>
        /// crudarch T022, scope-confirmed by archtests T002: interactive edits go through the
        /// <c>IProjectDocument</c> port — <b>no type anywhere in the GUI assembly</b> may call the STATELESS
        /// one-shot facade (<c>ProjectAppService.Apply/Apply&lt;T&gt;/CanApply/Preview</c>), which would silently
        /// reinstate the per-call scratch-session cost the port removed (proposal G2) and bypass the document's
        /// undo history (G1). The allowlist is empty (D03a), and the likeliest regressors are precisely the
        /// NON-view-model types (<c>TreeDragDropController</c>, the <c>Services</c>), so the scan is rooted at
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
        /// archtests T002: every name-rooted boundary scan in this fixture judges "the GUI" by the
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
            var authored = typeof(global::ihc_openvisual.App).Assembly.GetTypes()
                .Where(t => !IsSynthesised(t) && !IsGeneratedBuildOutput(t))
                .Select(t => t.FullName!)
                .ToList();
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
        private static bool IsGeneratedBuildOutput(Type type) =>
            type.IsDefined(typeof(GeneratedCodeAttribute), inherit: false)
            || (type.FullName is { } name
                && (name.StartsWith("CompiledAvaloniaXaml", StringComparison.Ordinal)
                    || name.Contains('!', StringComparison.Ordinal)));

        // The document-lifecycle members exactly one GUI type may call (archtests T003 / D03b). Query and edit
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
        /// The document-lifecycle chokepoint (proposal 2.3-G1): <c>ProjectWorkflow</c> is the ONE GUI type that may
        /// open a document — <c>ProjectAppService.OpenDocument</c> — or drive its lifecycle
        /// (<c>Open</c>/<c>MarkSaved</c>/<c>Close</c>). The disease this prevents is a second document opened from a
        /// dialog or view-model: two documents over one file means two undo histories, so edits made through one are
        /// invisible to the other and are silently lost on save. Nothing behavioural catches that until a user's work
        /// disappears, which is precisely why it is pinned structurally. Armed by
        /// <see cref="LifecycleChokepointScan_IsArmed"/> over a seeded off-workflow caller in this test assembly.
        /// </summary>
        [Test]
        public void DocumentLifecycle_IsOwnedOnlyByTheWorkflow() =>
            Assert.Multiple(() =>
            {
                AssertMembersCalledOnlyFrom(Gui, GuiRoot, typeof(global::Ihc.Vis.ProjectAppService).FullName!,
                    new[] { nameof(global::Ihc.Vis.ProjectAppService.OpenDocument) }, LifecycleChokepoint,
                    "the document-opening door",
                    "only ProjectWorkflow may open a document — a second document over the same file splits the undo history and silently loses edits");

                AssertMembersCalledOnlyFrom(Gui, GuiRoot, typeof(global::Ihc.Vis.IProjectDocument).FullName!,
                    DocumentLifecycleMemberNames, LifecycleChokepoint,
                    "the document lifecycle members",
                    "only ProjectWorkflow may drive document lifecycle (Open/MarkSaved/Close) — query and edit members stay open to all");
            });

        // The MVVM-toolkit attributes are matched by NAME, not typeof: the toolkit is a transitive package here, and
        // naming it would add a compile dependency purely to spell two strings. The name binding is not taken on
        // trust — CommandEnablementAttributeScan_IsArmed asserts the real RelayCommandAttribute is actually found in
        // the GUI assembly, which simultaneously proves the names are right and (D04) that the toolkit's attributes
        // survive compilation into metadata at all.
        private const string RelayCommandAttributeName = "RelayCommandAttribute";
        private const string NotifyCanExecuteChangedForAttributeName = "NotifyCanExecuteChangedForAttribute";
        private const string CanExecuteArgumentName = "CanExecute";

        /// <summary>
        /// Command enablement has exactly ONE home — the registry row's Gate (crudarch D02/QC-02). This makes the
        /// main-backlog T015/T024 one-off greps permanent, banning the two toolkit mechanisms that would create a
        /// second, competing authority over whether a command is available:
        /// <c>[NotifyCanExecuteChangedFor]</c> (a property that re-queries some OTHER command's CanExecute, so
        /// invalidation no longer flows solely from <c>OnContextChanged</c>) and <c>[RelayCommand(CanExecute = …)]</c>
        /// (a per-command predicate competing with the row's Gate). A one-off grep proves today; this proves every day.
        /// Armed by <see cref="CommandEnablementAttributeScan_IsArmed"/>.
        /// </summary>
        [Test]
        public void Gui_DeclaresCommandEnablementOnlyThroughTheRegistryGate() =>
            Assert.That(EnablementAttributeOffences(typeof(global::ihc_openvisual.App).Assembly.GetTypes()), Is.Empty,
                "command availability is computed in exactly one place — the registry row's Gate — so no member may declare a competing enablement source");

        /// <summary>Positive control for <see cref="Gui_DeclaresCommandEnablementOnlyThroughTheRegistryGate"/>, and
        /// the D04 feasibility evidence in permanent form. Three claims: the real toolkit attributes DO survive into
        /// the compiled GUI metadata (otherwise the ban above would be unenforceable and would have to become a
        /// documented convention instead); the scan reports both forbidden shapes when they are present; and it does
        /// not report a plain <c>[RelayCommand]</c>, which is the sanctioned form the app uses 48 times.</summary>
        [Test]
        public void CommandEnablementAttributeScan_IsArmed() =>
            Assert.Multiple(() =>
            {
                var guiRelayCommands = typeof(global::ihc_openvisual.App).Assembly.GetTypes()
                    .SelectMany(DeclaredMembers)
                    .SelectMany(member => member.GetCustomAttributesData())
                    .Where(attribute => attribute.AttributeType.Name == RelayCommandAttributeName)
                    .ToList();

                Assert.That(guiRelayCommands, Is.Not.Empty,
                    "D04 feasibility: the toolkit's [RelayCommand] must be observable in the compiled GUI metadata — if it is ever stripped, this ban silently stops enforcing and must be replaced by a documented convention");

                Assert.That(EnablementAttributeOffences(new[] { typeof(SeededEnablementAttributeUser) }), Has.Count.EqualTo(2),
                    "the scan must report BOTH forbidden shapes: the NotifyCanExecuteChangedFor member and the CanExecute-carrying RelayCommand");
                Assert.That(EnablementAttributeOffences(new[] { typeof(SeededPlainRelayCommandUser) }), Is.Empty,
                    "a plain [RelayCommand] is the sanctioned form and must NOT be reported");
            });

        // Look-alike attributes for the controls. Matching is by attribute NAME, so these exercise the real predicate
        // without pulling the MVVM toolkit (and its source generator, which would demand partial ObservableObject
        // hosts and emit real commands) into this test assembly just to seed two violations.
        [AttributeUsage(AttributeTargets.All)]
        private sealed class RelayCommandAttribute : Attribute
        {
            public string? CanExecute { get; set; }
        }

        [AttributeUsage(AttributeTargets.All)]
        private sealed class NotifyCanExecuteChangedForAttribute(string commandName) : Attribute
        {
            public string CommandName { get; } = commandName;
        }

        private sealed class SeededEnablementAttributeUser
        {
            [NotifyCanExecuteChangedFor("SaveCommand")]
            public bool Dirty => false;

            [RelayCommand(CanExecute = nameof(Dirty))]
            public void Save() { }
        }

        private sealed class SeededPlainRelayCommandUser
        {
            [RelayCommand]
            public void Save() { }
        }

        // Every member a type declares itself, across visibilities — attributes can sit on the private field or the
        // partial property behind an [ObservableProperty] as readily as on a public method.
        private static IEnumerable<MemberInfo> DeclaredMembers(Type type) =>
            type.GetMembers(BindingFlags.Instance | BindingFlags.Static
                            | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        // The two competing-enablement declarations, reported as "Type.Member: shape".
        private static IReadOnlyList<string> EnablementAttributeOffences(IEnumerable<Type> types) =>
            types.SelectMany(type => DeclaredMembers(type).Select(member => (type, member)))
                .SelectMany(hit => hit.member.GetCustomAttributesData().Select(attribute => (hit.type, hit.member, attribute)))
                .Where(hit =>
                    hit.attribute.AttributeType.Name == NotifyCanExecuteChangedForAttributeName
                    || (hit.attribute.AttributeType.Name == RelayCommandAttributeName
                        && hit.attribute.NamedArguments.Any(argument => argument.MemberName == CanExecuteArgumentName)))
                .Select(hit => $"{hit.type.Name}.{hit.member.Name}: {hit.attribute.AttributeType.Name}")
                .ToList();

        // The immutable availability-context value zone (archtests T005 / proposal §3.2-3.3, review F4). These are
        // VALUE snapshots the registry evaluates against; a live reference in any of them would let a context in
        // hand drift while the tree mutates, which is stale enablement — the exact bug the explicit context model
        // replaced. CommandRegistry is deliberately NOT here: it is a live object with observable state, so it is
        // held to the narrower registry-purity rule below instead.
        private static IReadOnlyCollection<Type> ContextValueZone => new[]
        {
            typeof(global::ihc_openvisual.ViewModels.ShellContext),
            typeof(global::ihc_openvisual.ViewModels.NodeContext),
            typeof(global::ihc_openvisual.ViewModels.ClipboardContext),
            typeof(global::ihc_openvisual.ViewModels.Availability),
            typeof(global::ihc_openvisual.ViewModels.CommandSpec),
        };

        /// <summary>
        /// The context/registry purity zone (archtests T005).
        ///
        /// (a) is deliberately absent: "these types must not depend on Avalonia" (main-backlog D08, gesture-as-string)
        /// is ALREADY enforced for every one of them by <see cref="ViewModels_DoNotDependOn_Avalonia"/>, which is
        /// scoped to the whole <c>ViewModels</c> subtree — and all six types live there. A second Avalonia rule over
        /// a subset would be a pure duplicate, so this states only what that rule does not cover.
        ///
        /// (b) The context value types hold VALUES, never live objects: no <c>Project</c>, no <c>ProjectElement</c>,
        /// and no <c>INotifyPropertyChanged</c> implementor — the last being the mechanical proxy for "a live
        /// view-model" that catches <c>TreeNodeViewModel</c> without naming it, and catches its future siblings too.
        ///
        /// (b') <c>CommandRegistry</c> gets the narrower form: no <c>Project</c>/<c>ProjectElement</c>/
        /// <c>TreeNodeViewModel</c>. The INotifyPropertyChanged proxy cannot apply to it, because the commands it
        /// materializes are <c>IAsyncRelayCommand</c>, which itself extends INotifyPropertyChanged — holding those
        /// is the registry's whole job. What matters is that it evaluates rows against <c>ShellContext</c> and never
        /// reaches live tree state.
        ///
        /// (c) The context value types are immutable: instance fields readonly, properties get- or init-only.
        ///
        /// Delegate-typed members are opaque here, consistently with
        /// <see cref="Gui_PointsAtElementsByIdNotObjectReference"/>: a callback that OBTAINS the current
        /// snapshot is not a retained reference — indeed the registry's <c>Func&lt;ShellContext&gt;</c> exists
        /// precisely so gates read the current context instead of a captured one.
        /// Armed by <see cref="PurityZoneDetectors_AreArmed"/>.
        /// </summary>
        [Test]
        public void ContextAndRegistry_StayAPureValueZone() =>
            Assert.Multiple(() =>
            {
                Assert.That(ContextValueZone, Is.Not.Empty, "sanity: the context value zone is anchored");

                foreach (Type zoneType in ContextValueZone)
                {
                    Assert.That(LiveReferenceHeldBy(zoneType, IsLiveObject), Is.Empty,
                        $"{zoneType.Name} is a VALUE snapshot — holding a live object lets a context in hand drift while the tree mutates (review F4)");
                    Assert.That(MutableMembersOf(zoneType), Is.Empty,
                        $"{zoneType.Name} must be immutable — a mutable context snapshot can be edited after the availability it explains was computed");
                }

                Assert.That(
                    LiveReferenceHeldBy(typeof(global::ihc_openvisual.ViewModels.CommandRegistry), IsLiveTreeState),
                    Is.Empty,
                    "the registry evaluates rows against ShellContext and must not reach live tree state");
            });

        /// <summary>Positive control for <see cref="ContextAndRegistry_StayAPureValueZone"/>: both detectors must
        /// report against seeded violations, and must NOT report against the sanctioned shapes (a value context and
        /// a snapshot-obtaining callback) — otherwise they would be either blind or indiscriminate.</summary>
        [Test]
        public void PurityZoneDetectors_AreArmed() =>
            Assert.Multiple(() =>
            {
                Assert.That(LiveReferenceHeldBy(typeof(SeededImpureContext), IsLiveObject), Is.Not.Empty,
                    "the live-reference detector must flag a held view-model");
                Assert.That(LiveReferenceHeldBy(typeof(SeededImpureContext), IsLiveTreeState), Is.Not.Empty,
                    "the live-tree-state detector must flag a held TreeNodeViewModel");
                Assert.That(MutableMembersOf(typeof(SeededMutableContext)), Is.Not.Empty,
                    "the immutability detector must flag a settable property and a non-readonly field");

                Assert.That(LiveReferenceHeldBy(typeof(global::ihc_openvisual.ViewModels.ShellContext), IsLiveObject), Is.Empty,
                    "the real context must not trip the detector — otherwise the rule above is passing for the wrong reason");
                Assert.That(MutableMembersOf(typeof(global::ihc_openvisual.ViewModels.ShellContext)), Is.Empty,
                    "the real context is immutable");
            });

        // Seeded violators for the purity-zone controls.
        private sealed record SeededImpureContext(
            global::ihc_openvisual.ViewModels.TreeNodeViewModel Node,
            global::Ihc.Vis.Projects.Project Project);

        private sealed class SeededMutableContext
        {
            public bool Mutable { get; set; }
#pragma warning disable CS0649 // never assigned: the field exists only so the detector has a non-readonly one to find
            internal int Field;
#pragma warning restore CS0649
        }

        // A live object a value snapshot must not hold: the two mutable model roots, or anything that raises
        // property-change notifications (the mechanical stand-in for "a view-model").
        private static bool IsLiveObject(Type type) =>
            IsLiveTreeState(type) || typeof(INotifyPropertyChanged).IsAssignableFrom(type);

        private static bool IsLiveTreeState(Type type) =>
            type == typeof(global::Ihc.Vis.Projects.Project)
            || type == typeof(global::Ihc.Vis.Model.ProjectElement)
            || type == typeof(global::ihc_openvisual.ViewModels.TreeNodeViewModel);

        // Every member of the type that holds something matching the predicate, reported as "Type.Member : Held".
        private static IReadOnlyList<string> LiveReferenceHeldBy(Type owner, Func<Type, bool> forbidden) =>
            owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Select(f => (Member: f.Name, Held: FirstReferenced(f.FieldType, forbidden)))
                .Concat(owner.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Select(p => (Member: p.Name, Held: FirstReferenced(p.PropertyType, forbidden))))
                .Where(hit => hit.Held is not null)
                .Select(hit => $"{owner.Name}.{hit.Member} : {hit.Held!.Name}")
                .Distinct()
                .ToList();

        // Instance state that can change after construction: a non-readonly field, or a property with a setter that
        // is not init-only. Records satisfy this by construction; the rule exists so a later hand-written member
        // cannot quietly reintroduce mutability into a snapshot type.
        private static IReadOnlyList<string> MutableMembersOf(Type owner) =>
            owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(f => !f.IsInitOnly)
                .Select(f => $"{owner.Name}.{f.Name} (settable field)")
                .Concat(owner.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(p => p.SetMethod is { } setter && !IsInitOnly(setter))
                    .Select(p => $"{owner.Name}.{p.Name} (settable property)"))
                .ToList();

        private static bool IsInitOnly(MethodInfo setter) =>
            setter.ReturnParameter.GetRequiredCustomModifiers()
                .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");

        // The one GUI type sanctioned to call ConfigureAwait (D03c): its backup path only READS document.Current
        // and never mutates the document or handles its events, so leaving the UI thread there is safe. The type is
        // internal and so cannot be typeof-anchored from this assembly; the NAMESPACE is still anchored through a
        // public sibling, leaving only the type name as a literal. ConfigureAwaitScan_IsArmed asserts the name still
        // resolves, so the allowlist cannot silently go dead after a rename and quietly widen the ban's exemption.
        private static readonly string AutoBackupSchedulerName =
            typeof(global::ihc_openvisual.Services.ProjectWorkflow).Namespace + ".AutoBackupScheduler";

        private static IReadOnlyCollection<string> ConfigureAwaitAllowlist => new[] { AutoBackupSchedulerName };

        /// <summary>
        /// The GUI threading contract (crudarch D04(c)): no <c>ConfigureAwait</c> anywhere in the app except the
        /// <c>AutoBackupScheduler</c>'s read-only backup path. A single <c>ConfigureAwait(false)</c> upstream of a
        /// document mutation resumes the continuation on a pool thread, and the resulting Avalonia failure is partly
        /// SILENT — threading review WS-05/AP-02 records items simply going missing from an
        /// <c>ObservableCollection</c> rather than an exception being thrown. That is why this is a BLANKET call ban
        /// rather than an operand check: <c>ConfigureAwait(true)</c> is pointless in GUI code, so banning the call
        /// outright is both simpler and stricter, and nothing else automated watches future GUI code for this.
        /// The ban is by member NAME on any declaring type, since <c>ConfigureAwait</c> is declared separately on
        /// <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c>, <c>ValueTask&lt;T&gt;</c> and the async-enumerable
        /// extensions. Armed by <see cref="ConfigureAwaitScan_IsArmed"/>.
        /// </summary>
        [Test]
        public void Gui_DoesNotCallConfigureAwait() =>
            AssertDoesNotCallMembers(Gui, GuiRoot, targetTypeFullName: null,
                ConfigureAwaitMemberName, "ConfigureAwait outside the auto-backup writer",
                "GUI continuations must stay on the UI thread — a ConfigureAwait(false) upstream of a document mutation fails silently in Avalonia (WS-05/AP-02)",
                ConfigureAwaitAllowlist);

        private static readonly IReadOnlyCollection<string> ConfigureAwaitMemberName = new[] { "ConfigureAwait" };

        // Seeded violator for the ConfigureAwait ban's positive control: an unexempted type that genuinely calls it,
        // on both a Task and a ValueTask, so the name-based match is proven against more than one declaring type.
        private static class SeededConfigureAwaitCaller
        {
            public static async Task Call(Task work, ValueTask valueWork)
            {
                await work.ConfigureAwait(false);
                await valueWork.ConfigureAwait(false);
            }
        }

        /// <summary>The positive control for <see cref="Gui_DoesNotCallConfigureAwait"/>: run against this test
        /// assembly — where <see cref="SeededConfigureAwaitCaller"/> calls it on two different declaring types and is
        /// NOT allowlisted — the ban must report. Proves the scan detects real ConfigureAwait edges rather than
        /// passing because the GUI happens to make none outside the allowlist.</summary>
        [Test]
        public void ConfigureAwaitScan_IsArmed() =>
            Assert.Multiple(() =>
            {
                Assert.That(
                    () => AssertDoesNotCallMembers(OwnTestAssembly.Value, typeof(OpenVisualArchitectureTests).Namespace!,
                        targetTypeFullName: null, ConfigureAwaitMemberName, "seeded probe", "seeded probe",
                        exemptOriginTypeFullNames: null),
                    Throws.InstanceOf<AssertionException>(),
                    "the blanket ConfigureAwait scan must report the seeded Task and ValueTask calls");

                Assert.That(typeof(global::ihc_openvisual.App).Assembly.GetType(AutoBackupSchedulerName), Is.Not.Null,
                    $"the allowlisted '{AutoBackupSchedulerName}' must still exist — a rename would leave a dead allowlist entry that exempts nothing while reading as if it does");

                // The strongest control available: the same ban over the REAL GUI with an empty allowlist must fail.
                // That proves in one shot that the scan sees the GUI's genuine ConfigureAwait call (not merely the
                // seeded one in this test assembly) and that the allowlist — not an empty result set — is what makes
                // the rule green. If the auto-backup writer ever stops calling ConfigureAwait, delete the allowlist
                // and this assertion together; do not weaken the ban.
                Assert.That(
                    () => AssertDoesNotCallMembers(Gui, GuiRoot, targetTypeFullName: null,
                        ConfigureAwaitMemberName, "allowlist probe", "allowlist probe",
                        exemptOriginTypeFullNames: null),
                    Throws.InstanceOf<AssertionException>(),
                    "the GUI's one sanctioned ConfigureAwait call must be visible to the scan — otherwise the ban is green because it detects nothing");
            });

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

            Assert.Multiple(() =>
            {
                Assert.That(seededCalls, Is.EquivalentTo(DocumentLifecycleMemberNames),
                    "the chokepoint scan must detect every seeded Open/MarkSaved/Close call");
                Assert.That(
                    () => AssertMembersCalledOnlyFrom(OwnTestAssembly.Value, testRoot,
                        typeof(global::Ihc.Vis.ProjectAppService).FullName!,
                        new[] { nameof(global::Ihc.Vis.ProjectAppService.OpenDocument) }, LifecycleChokepoint,
                        "seeded probe", "seeded probe"),
                    Throws.InstanceOf<AssertionException>(),
                    "the scan must REPORT the seeded off-workflow OpenDocument call, not merely observe it");
            });
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
        private static readonly System.Lazy<Architecture> OwnTestAssembly = new(() =>
            new ArchLoader().LoadAssemblies(typeof(OpenVisualArchitectureTests).Assembly).Build());

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
                "the view layer must drive the model through its view-model, not IProjectDocument, ProjectWorkflow, ProjectAppService, or the ProjectCommands gateway directly");

        /// <summary>
        /// Identity that survives tree rebuilds (ARCHITECTURE.md Design Challenge 4): every edit rebuilds the
        /// immutable project tree, so any <see cref="Ihc.Vis.Projects.Project"/> / <see cref="Ihc.Vis.Model.ProjectElement"/>
        /// reference retained in bound UI state goes stale at once — the GUI therefore points at elements by
        /// <see cref="Ihc.Vis.Model.ElementId"/>, never by object reference. This checks the retained state directly:
        /// no instance field of any GUI type may retain <c>Project</c>, <c>ProjectElement</c>, or a live-editing
        /// handle (an <c>Ihc.Vis.Editing</c> type).
        ///
        /// Scope is the WHOLE assembly (archtests T007/D03d), not just view-models. It was view-model-scoped while
        /// the workflow kept snapshot stacks, and the exemption was justified as "Services legitimately hold a
        /// Project"; the crudarch document port deleted those stacks, and the T007 audit confirmed no GUI type
        /// outside the view-models holds one any more — so the exemption was retired rather than inherited.
        /// Holding a snapshot is what produces both stale references and the dual-history bug, so the ban belongs
        /// everywhere, and <c>Services</c> is precisely where a snapshot would most plausibly be hoarded again.
        ///
        /// <c>ProjectTreeProjector</c> is the one allowlisted survivor: it retains a snapshot only for the duration
        /// of a single projection pass and is never bound or stored as UI state. Parameters, returns and locals stay
        /// legal throughout — reading a project into a local, using it and dropping it is the sanctioned pattern.
        /// </summary>
        [Test]
        public void Gui_PointsAtElementsByIdNotObjectReference()
        {
            var stateOwners = typeof(global::ihc_openvisual.App).Assembly.GetTypes()
                .Where(IsViewModelStateOwner)
                .ToList();
            Assert.That(stateOwners, Is.Not.Empty, "sanity: the GUI exposes view-model state-owner types");

            var offences = new List<string>();
            foreach (Type stateOwner in stateOwners)
                foreach (FieldInfo field in RetainedFields(stateOwner))
                    if (RetainedModelType(field.FieldType) is { } stale)
                        offences.Add($"{stateOwner.Name}.{field.Name} : {stale.Name}");

            Assert.That(offences, Is.Empty,
                "GUI types must reference project elements by ElementId, not retain Project/ProjectElement/editing handles that go stale on the next edit: "
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
            string facade = typeof(global::Ihc.Vis.ProjectAppService).FullName!;
            Assert.Multiple(() =>
            {
                Assert.That(DependencyEdges(Gui, GuiRoot).Select(edge => edge.Target), Does.Contain(facade),
                    "the dependency scan must expose the GUI's real ProjectAppService dependency");
                Assert.That(ConstructorCallEdges(Gui, GuiRoot).Select(edge => edge.Target), Does.Contain(facade),
                    "the constructor scan must expose the GUI's real ProjectAppService construction");
            });
        }

        /// <summary>
        /// Backstop proving the reflection detector behind <see cref="Gui_PointsAtElementsByIdNotObjectReference"/>
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
                Assert.That(RetainedModelType(typeof(global::Ihc.Vis.Model.ElementId)), Is.Null,
                    "the detector must not flag ElementId — the sanctioned reference");
                Assert.That(RetainedModelType(typeof(global::Ihc.Vis.Model.ElementId?)), Is.Null,
                    "the detector must not flag ElementId?");
                Assert.That(RetainedModelType(typeof(Func<global::Ihc.Vis.Projects.Project?>)), Is.Null,
                    "a callback that obtains the current immutable snapshot does not retain that snapshot");
                Assert.That(IsViewModelStateOwner(typeof(global::ihc_openvisual.ViewModels.TreeNodeViewModel)), Is.True,
                    "TreeNodeViewModel is a bound view-model and in scope");
                Assert.That(IsViewModelStateOwner(typeof(global::ihc_openvisual.ViewModels.TreeDragDropController)), Is.True,
                    "long-lived controllers owned by a view-model are in scope");

                // T007 widened the scope from the view-model namespace to the whole GUI. These pin that the widening
                // actually took effect: the rule went green on first run, and without them that could equally mean it
                // had stopped looking at the layer the widening was FOR.
                Assert.That(IsViewModelStateOwner(typeof(global::ihc_openvisual.Services.ProjectWorkflow)), Is.True,
                    "the Services layer is in scope after T007 — it is where a snapshot would most plausibly be hoarded");
                Assert.That(IsViewModelStateOwner(typeof(global::ihc_openvisual.App)), Is.True,
                    "the composition root is in scope — the rule is assembly-wide, not layer-wide");
                Assert.That(RetainedFields(typeof(global::ihc_openvisual.Services.ProjectWorkflow)), Is.Not.Empty,
                    "the field walk must actually yield fields for a Services type — scope without a working walk inspects nothing");
                Assert.That(IsViewModelStateOwner(typeof(global::ihc_openvisual.ViewModels.ProjectTreeProjector)), Is.False,
                    "the per-projection helper is the explicit transient exemption");

                // The synthesised-type exclusion, armed against the real assembly: the registry's command bodies are
                // async lambdas, so the view-model namespace genuinely contains Roslyn state machines whose hoisted
                // locals would otherwise read as retained fields. Prove they exist, then prove they are out of scope.
                var synthesised = typeof(global::ihc_openvisual.App).Assembly.GetTypes()
                    .Where(t => t.Namespace == ViewModels && t.Name.Contains("b__", StringComparison.Ordinal))
                    .ToList();
                Assert.That(synthesised, Is.Not.Empty,
                    "sanity: the view-models really do compile lambdas into synthesised closure/state-machine types");
                Assert.That(synthesised.Where(IsViewModelStateOwner), Is.Empty,
                    "a synthesised closure/state machine is not a state owner — its <x>5__n members are hoisted locals of one operation");
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
            typeof(global::Ihc.Vis.IProjectDocument).FullName!,
        };

        /// <summary>The types the DTO-only report renderer must not depend on (T035): the mutable project tree
        /// (<c>Project</c>/<c>ProjectElement</c>), the report generator's private tree indexer (<c>TreeIndex</c>,
        /// reflected by name), and every live-session <c>Ihc.Vis.Editing</c> type (reflected from the SDK assembly).
        /// <c>ElementId</c> is deliberately NOT here — it is sanctioned switch data on the combined model.</summary>
        private static IReadOnlyCollection<string> NonRenderReadySdkTypeNames()
        {
            Assembly sdk = typeof(global::Ihc.Vis.ProjectDocumentationReport).Assembly;
            var allowed = new HashSet<Type>();
            var pending = new Queue<Type>();
            pending.Enqueue(typeof(global::Ihc.Vis.ProjectDocumentationReport));

            while (pending.TryDequeue(out Type? candidate))
            {
                foreach (Type type in TypeAndArguments(candidate))
                {
                    if (type.Assembly != sdk || !allowed.Add(type))
                        continue;

                    foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        pending.Enqueue(property.PropertyType);
                }
            }

            return sdk.GetTypes()
                .Where(type => !allowed.Contains(type))
                .Select(type => type.FullName!)
                .ToHashSet();
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

        private static bool IsViewModelStateOwner(Type type) =>
            type.FullName is { } name
            && name.StartsWith(GuiRoot + ".", StringComparison.Ordinal)
            && !IsSynthesised(type)
            && !IsGeneratedBuildOutput(type)
            && !IdentityRuleAllowlist.Contains(type);

        // Types allowed to hold a Project/ProjectElement, each for an audited reason (archtests T007 / D03d).
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

        // Instance fields declared by the view-model hierarchy up to (not including) ObservableObject — this covers
        // explicit fields and the compiler-generated backing fields of auto-properties and [ObservableProperty]s.
        private static IEnumerable<FieldInfo> RetainedFields(Type viewModel)
        {
            for (Type? t = viewModel; t is not null && t.FullName is { } name
                 && name.StartsWith(GuiRoot + ".", StringComparison.Ordinal); t = t.BaseType)
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    yield return f;
        }

        // The stale-able model type a field would retain — Project, ProjectElement, or a live-session editing handle
        // (any Ihc.Vis.Editing type) — reached through Nullable<T>, arrays and generic arguments; null if the field
        // holds nothing stale. ElementId (and collections of it) is the sanctioned reference and is never flagged.
        private static Type? RetainedModelType(Type fieldType) =>
            FirstReferenced(fieldType, candidate =>
                candidate == typeof(global::Ihc.Vis.Projects.Project)
                || candidate == typeof(global::Ihc.Vis.Model.ProjectElement)
                || candidate.Namespace == "Ihc.Vis.Editing");

        // The first type a member's declared type REACHES that matches the predicate — through Nullable&lt;T&gt;,
        // arrays and generic arguments. Delegates are opaque: a callback that obtains the current snapshot does not
        // retain it. Shared by the identity rule and the purity zone so both traverse types identically.
        private static Type? FirstReferenced(Type memberType, Func<Type, bool> forbidden) =>
            typeof(Delegate).IsAssignableFrom(memberType)
                ? null
                : TypeAndArguments(memberType).FirstOrDefault(forbidden);

    }
}
