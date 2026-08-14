using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArchUnitNET.Domain;
using Ihc.App;
using Ihc.Vis;
using Ihc.Vis.Catalog;
using Ihc.Vis.Editing;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Programs;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;
using static Ihc.Tests.ArchRuleHelpers;
// ArchUnitNET.Loader also exports a `Type`; the reflection helpers in this fixture mean System.Type throughout.
using Type = System.Type;

namespace Ihc.Tests
{
    /// <summary>
    /// The SDK's (<c>ihcclient</c>) directional layering rules, enforced mechanically.
    ///
    /// These rules were previously stated only in comments and doc remarks, which is why they are worth pinning:
    /// a rule that nothing checks holds only for as long as everyone remembers it. ArchUnitNET reads IL (via
    /// Mono.Cecil), so it sees real dependencies (fields, signatures, base types, call targets) and correctly
    /// ignores the <c>&lt;see cref="..."/&gt;</c> doc references that point across these boundaries by design. The
    /// matching/assert mechanics live in <see cref="ArchRuleHelpers"/>; this fixture only states the SDK policy.
    ///
    /// Every layer namespace below is read off a representative <b>public</b> type via <c>typeof(T).Namespace</c>
    /// instead of a string literal, so a rule can never target a stale hand-typed namespace that quietly matches
    /// nothing and passes vacuously — the failure mode a hand-written namespace string is prone to. Removing or
    /// renaming an anchor type breaks <i>this file</i> at compile time; a namespace <i>rename</i> is followed
    /// automatically. The one move <c>typeof</c> cannot catch — an anchor type relocated into a different existing
    /// namespace, silently retargeting its rule — is pinned by <see cref="LayerAnchors_ResolveToTheirDocumentedNamespaces"/>.
    /// (safe_architecture_tests is deliberately not on ihcclient's <c>InternalsVisibleTo</c> list, so every anchor
    /// is a public type; that also keeps these rules pinned to the public contract.)
    /// </summary>
    [TestFixture]
    public class IhcClientArchitectureTests
    {
        // The SDK read into ArchUnitNET's model once for the whole fixture.
        private static readonly Architecture Sdk = ArchitectureModels.Sdk;

        // Layer namespaces, anchored to public types so a rename fails the compile, not the check silently.
        private static readonly string AppLayer = typeof(AppServiceBase).Namespace!;      // Ihc.App
        private static readonly string ApiRoot = typeof(AuthenticationService).Namespace!;// Ihc (controller API-service tier)
        private static readonly string VisRoot = typeof(ProjectAppService).Namespace!;    // Ihc.Vis (engine + its facade)
        private static readonly string Editing = typeof(ProjectEditor).Namespace!;        // Ihc.Vis.Editing
        private static readonly string Session = typeof(ProjectDocumentSession).Namespace!;// Ihc.Vis.Session (command runner)
        private static readonly string Io = typeof(ProjectSerializer).Namespace!;         // Ihc.Vis.Io
        // The report pipeline is internal (its public contract lives in root Ihc.Vis), so no public typeof anchor
        // exists. The string is kept honest by
        // ReportingPipelineTypes_AreInternal (the subtree must be populated and internal-only)
        // plus every consuming rule's non-empty vacuity guard.
        private const string Reporting = "Ihc.Vis.Reporting";
        private static readonly IReadOnlyCollection<string> ExpectedReportWriterTypeNames = new HashSet<string>
        {
            Reporting + ".HtmlReportWriter",
            Reporting + ".TextReportWriter",
        };
        private static readonly string Validation = typeof(ProjectValidationFinding).Namespace!; // Ihc.Vis.Validation
        private static readonly string Model = typeof(ProjectElement).Namespace!;         // Ihc.Vis.Model
        private static readonly string ProjectsNs = typeof(Project).Namespace!;           // Ihc.Vis.Projects

        private readonly record struct LayerAnchor(string Actual, string Expected, string Description);

        private static readonly IReadOnlyList<LayerAnchor> DefinitionLayerAnchors = new[]
        {
            new LayerAnchor(typeof(FunctionBlockDefinitionBuilder).Namespace!, "Ihc.Vis.FunctionBlocks", "function-block definitions"),
            new LayerAnchor(typeof(ProductDefinitionBuilder).Namespace!, "Ihc.Vis.Products", "product definitions"),
            new LayerAnchor(typeof(CatalogReader).Namespace!, "Ihc.Vis.Catalog", "catalog definitions"),
            new LayerAnchor(typeof(ProgramMethodCatalog).Namespace!, "Ihc.Vis.Programs", "program definitions"),
        };

        private static readonly IReadOnlyList<LayerAnchor> LayerAnchors = new[]
        {
            new LayerAnchor(AppLayer, "Ihc.App", "application-service tier"),
            new LayerAnchor(ApiRoot, "Ihc", "controller API-service tier"),
            new LayerAnchor(VisRoot, "Ihc.Vis", "offline engine root"),
            new LayerAnchor(Editing, "Ihc.Vis.Editing", "editing layer"),
            new LayerAnchor(Session, "Ihc.Vis.Session", "session command layer"),
            new LayerAnchor(Io, "Ihc.Vis.Io", "IO layer"),
            new LayerAnchor(Validation, "Ihc.Vis.Validation", "validation layer"),
            new LayerAnchor(Model, "Ihc.Vis.Model", "element model"),
            new LayerAnchor(ProjectsNs, "Ihc.Vis.Projects", "project model"),
            new LayerAnchor(SoapNs, "Ihc.Soap", "generated SOAP subtree"),
        }.Concat(DefinitionLayerAnchors).ToList();

        /// <summary>
        /// The whole catalog definition layer — every code-authoring/catalog namespace, not just one of them.
        /// Anchored to a representative public type per namespace so this list tracks renames automatically.
        /// </summary>
        private static IEnumerable<string> DefinitionLayerNamespaces() =>
            DefinitionLayerAnchors.Select(anchor => anchor.Actual);

        /// <summary>
        /// The one-way rule between the definition layer and the editing layer. <c>Editing</c> composes catalog
        /// definitions; the definition layer must not reach back into live-session editing types. This rule is the
        /// reason ProgramBuilder and FbProgramBuilder author the same graph twice — the duplication
        /// is the cost of keeping it, so the rule itself has to be real. It applies to the whole definition layer
        /// (products, function blocks, catalog, programs), not just one namespace.
        /// </summary>
        [TestCaseSource(nameof(DefinitionLayerNamespaces))]
        public void DefinitionLayer_DoesNotDependOn_Editing(string definitionNamespace) =>
            AssertNoDependency(Sdk, Subtree(definitionNamespace), Editing,
                "the definition layer composes catalog definitions but must not reach back into live-session editing types");

        /// <summary>
        /// The one sanctioned edge from the editing layer BACK UP to the session layer, pinned as an exact set.
        /// <para>The session layer sits above editing — its commands take a <see cref="ProjectEditor"/> — so the two
        /// are mutually dependent the moment editing names a session type, and no direction rule can be written
        /// between them. Exactly one such reference is intended: the require-or-throw resolver raises
        /// <see cref="EditRefusedException"/>, because a stale id met inside a command's <c>Execute</c> is an
        /// expected condition the session must map to <c>Refused</c>, and a plain exception would map to
        /// <c>Failed</c> instead. Anything else the editing layer starts reaching for up there is layering drift.</para>
        /// <para>Asserted as two-way set equality, not as a ban with an exemption: dropping the edge (by moving the
        /// exception down to a layer both can see, which is the alternative to keeping the cycle) must be a
        /// deliberate edit here, and adding a second one must fail. Nothing else in this fixture would notice
        /// either — the layering rules run downward, and this edge runs the other way.</para>
        /// </summary>
        [Test]
        public void EditingLayer_ReachesTheSessionLayer_OnlyForTheRefusalException()
        {
            var reached = DependencyEdges(Sdk, Editing)
                .Select(edge => edge.Target)
                .Where(target => target.StartsWith(Session + ".", StringComparison.Ordinal))
                .Select(OutermostTypeName)
                .Distinct()
                .ToList();

            Assert.That(reached, Is.EquivalentTo(new[] { typeof(EditRefusedException).FullName }),
                $"'{Editing}' may name exactly one type from '{Session}' — {nameof(EditRefusedException)}, which "
                + "makes a deep guard's stale-id miss a refusal instead of a failure. Any other edge is the "
                + "editing/session cycle widening unnoticed; if the edge is gone, move this assertion rather than delete it");
        }

        /// <summary>
        /// A command's <c>Execute</c> may raise exactly one exception type of its own: <see cref="EditRefusedException"/>.
        ///
        /// <para>The two statuses are decided by the exception type — the session maps
        /// <see cref="EditRefusedException"/> to <c>Refused</c> and everything else to <c>Failed</c> — and the two
        /// are read by different people in different languages: a refusal is a Danish sentence forwarded to the
        /// installer verbatim, a failure an English diagnostic for the log. So the choice of exception type inside
        /// an <c>Execute</c> body IS the choice of audience, which is far too easy to make by accident with a
        /// reflexive <c>throw new InvalidOperationException(...)</c>.</para>
        ///
        /// <para>What makes this reachable rather than theoretical is the composite: a <c>CompositeCommand</c>
        /// evaluates every part against the PRE-EDIT project, so a part invalidated by an earlier part passes its
        /// legality check and misses only here. The same installer mistake would then read as an engine bug when
        /// bundled and as a refusal when applied one part at a time. The rule covers the <c>Execute</c> body itself
        /// — deep helpers it calls are out of scope, which is where the engine's own genuine faults live.</para>
        ///
        /// <para>Armed by <see cref="ExecuteBodyExceptionScan_IsArmed"/>: the sanctioned construction must be
        /// observed, so a scan that stopped seeing <c>newobj</c> edges cannot read as compliance.</para>
        /// </summary>
        [Test]
        public void CommandExecuteBodies_RaiseOnlyTheRefusalException()
        {
            var raised = ExceptionsConstructedInExecuteBodies(Sdk, Session).ToList();

            Assert.That(raised.Select(edge => edge.TargetType), Does.Contain(typeof(EditRefusedException).FullName),
                "the scan must observe the sanctioned refusal raised from a command's Execute — otherwise it is "
                + "seeing no constructions at all and its verdict is meaningless");

            Assert.That(raised.Where(edge => edge.TargetType != typeof(EditRefusedException).FullName), Is.Empty,
                "an Execute body may only refuse: any other exception maps the miss to Failed, which answers the "
                + "installer with an English engine diagnostic instead of the Danish sentence the same mistake gets "
                + "when the command is applied on its own — offenders: "
                + string.Join("; ", raised
                    .Where(edge => edge.TargetType != typeof(EditRefusedException).FullName)
                    .Select(edge => $"{edge.Origin}.{edge.OriginMember} -> {edge.TargetType}")));
        }

        // Every exception constructed inside a member named Execute in the given namespace subtree.
        private static IEnumerable<(string Origin, string OriginMember, string TargetType)> ExceptionsConstructedInExecuteBodies(
            Architecture arch, string namespaceRoot) =>
            ConstructorCallEdgesWithOrigin(arch, namespaceRoot)
                .Where(edge => edge.OriginMember == "Execute" && IsExceptionType(edge.TargetType));

        // Resolved rather than matched on a "*Exception" name: the SDK's own types come from its assembly, the BCL's
        // resolve by full name, and the assignability check is then exact instead of conventional.
        private static bool IsExceptionType(string typeFullName) =>
            (typeof(ProjectAppService).Assembly.GetType(typeFullName) ?? Type.GetType(typeFullName)) is { } type
            && typeof(Exception).IsAssignableFrom(type);

        // Seeded violator for the control. Deliberately NOT a real ProjectCommand — this fixture is off ihcclient's
        // InternalsVisibleTo list on purpose, so the abstract Execute cannot be overridden here. The scan matches on
        // the member NAME, so a plain Execute body carries the same shape and proves the same detection.
        private static class SeededExecuteBody
        {
            public static void Execute() => throw new InvalidOperationException("seeded engine fault");
        }

        /// <summary>Positive control for <see cref="CommandExecuteBodies_RaiseOnlyTheRefusalException"/>: the same
        /// scan pointed at this assembly must report the seeded <c>InvalidOperationException</c> — proving it sees
        /// constructions inside an <c>Execute</c> body rather than passing because it sees none.</summary>
        [Test]
        public void ExecuteBodyExceptionScan_IsArmed() =>
            Assert.That(
                ExceptionsConstructedInExecuteBodies(ArchitectureModels.ArchitectureTests.Value,
                        typeof(IhcClientArchitectureTests).Namespace!)
                    .Select(edge => edge.TargetType),
                Does.Contain(typeof(InvalidOperationException).FullName),
                "the scan must report the seeded non-refusal exception raised from an Execute body");

        /// <summary>
        /// The mutating/IO layers the read-only reporting boundary forbids: the editing engine, the
        /// session command runner, and the IO serializer. Anchored to a representative public type per namespace.
        /// </summary>
        private static IEnumerable<string> MutatingAndIoLayers()
        {
            yield return typeof(ProjectEditor).Namespace!;          // Ihc.Vis.Editing
            yield return typeof(ProjectDocumentSession).Namespace!; // Ihc.Vis.Session
            yield return typeof(ProjectSerializer).Namespace!;      // Ihc.Vis.Io
        }

        /// <summary>
        /// The read-only reporting boundary: <c>Ihc.Vis.Reporting</c> reads the project and never mutates
        /// it or does IO, so it must not depend on the editing, session (command runner) or IO layers. Currently true —
        /// the report builder uses only the read side (Addressing/Model/Products/Projects) — so this is a born-green
        /// characterization that pins the boundary before the report builder grows. The shared vacuity guard in
        /// <see cref="ArchRuleHelpers.AssertNoDependency(Architecture,string,string,string)"/> keeps it armed: the
        /// <c>Ihc.Vis.Reporting</c> subtree must match at least one type, so the rule is seen to apply.
        /// </summary>
        [TestCaseSource(nameof(MutatingAndIoLayers))]
        public void Reporting_DoesNotDependOn_MutatingOrIoLayers(string mutatingOrIoNamespace) =>
            AssertNoDependency(Sdk, Subtree(Reporting), mutatingOrIoNamespace,
                "reports read the project and never mutate it or do IO — Ihc.Vis.Reporting must stay independent of the editing, session and IO layers");

        /// <summary>
        /// The report pipeline is an SDK implementation detail. Its namespace must remain populated and every type
        /// in it must stay non-public; public report contracts live in the root <c>Ihc.Vis</c> namespace instead.
        /// </summary>
        [Test]
        public void ReportingPipelineTypes_AreInternal()
        {
            var reportingTypes = typeof(ProjectAppService).Assembly.GetTypes()
                .Where(type => type.Namespace is { } ns
                               && (ns == Reporting || ns.StartsWith(Reporting + ".", StringComparison.Ordinal)))
                .ToList();

            Assert.That(reportingTypes, Is.Not.Empty,
                "the reporting namespace must contain the internal pipeline guarded by the reporting rules");
            Assert.That(reportingTypes.Where(type => type.IsPublic || type.IsNestedPublic), Is.Empty,
                "report builders, shapes and format writers are implementation details; only the root Ihc.Vis report contracts are public");
        }

        /// <summary>
        /// The single-content-path guarantee: the generic format writers render the
        /// shape document plus the icon contract and never read the project model — all content decisions
        /// live in the builders, so the two formats cannot drift by one writer reaching into the tree. The
        /// writer set is matched by the <c>*ReportWriter</c> naming convention (the writers are internal, so
        /// no <c>typeof</c> anchor is possible from this fixture); the guard below pins that the convention
        /// actually matches the real writers. Armed by <see cref="WriterModelScan_IsArmed"/>.
        /// </summary>
        [Test]
        public void ReportFormatWriters_DoNotDependOn_ProjectModel()
        {
            List<string> writers = ReportWriterTypeNames();
            Assert.That(writers, Is.EquivalentTo(ExpectedReportWriterTypeNames),
                "the stable *ReportWriter convention must identify the complete expected format-writer roster");

            Assert.That(ForbiddenModelEdges(writers), Is.Empty,
                "format writers render the shape document + icon contract only; project-model access belongs in the builders");
        }

        /// <summary>Positive control for <see cref="ReportFormatWriters_DoNotDependOn_ProjectModel"/>: the same
        /// edge scan pointed at the functions BUILDER — which reads the project model by design — must report
        /// forbidden edges, proving the scan sees model dependencies rather than passing because it sees none.</summary>
        [Test]
        public void WriterModelScan_IsArmed()
        {
            Assert.That(ForbiddenModelEdges(new List<string> { Reporting + ".FunctionsReportBuilder" }), Is.Not.Empty,
                "the builder depends on the project model by design; the scan must report those edges or the writer rule cannot be trusted");
            Assert.That(ReferencedTypeReachesProjectModel(typeof(ElementView).FullName!), Is.True,
                "the scan must see through a non-generic value wrapper such as ElementView to its Project and ProjectElement fields");
        }

        // The outermost authored types in Ihc.Vis.Reporting following the writer naming convention.
        private static List<string> ReportWriterTypeNames() =>
            Sdk.Types
                .Select(t => OutermostTypeName(t.FullName))
                .Where(name => name.StartsWith(Reporting + ".", StringComparison.Ordinal)
                               && name.EndsWith("ReportWriter", StringComparison.Ordinal))
                .Distinct()
                .ToList();

        // Every dependency edge from the given outermost types onto a type whose recursive value/signature closure
        // reaches the project model. This catches direct access and non-generic wrappers such as ElementView.
        private static List<string> ForbiddenModelEdges(List<string> outermostTypeNames) =>
            Sdk.Types
                .Where(t => outermostTypeNames.Contains(OutermostTypeName(t.FullName)))
                .SelectMany(t => t.Dependencies, (t, d) => (Origin: t.FullName, Target: d.Target.FullName))
                .Where(e => ReferencedTypeReachesProjectModel(e.Target))
                .Select(e => $"{e.Origin} -> {e.Target}")
                .Distinct()
                .ToList();

        private static bool ReferencedTypeReachesProjectModel(string targetTypeFullName) =>
            typeof(ProjectAppService).Assembly.GetType(targetTypeFullName) is { } target
            && TypeAndArguments(target).Any(type =>
                IsInNamespaceSubtree(type, Model) || IsInNamespaceSubtree(type, ProjectsNs));

        private static bool IsInNamespaceSubtree(Type type, string namespaceRoot) =>
            type.Namespace is { } ns
            && (ns == namespaceRoot || ns.StartsWith(namespaceRoot + ".", StringComparison.Ordinal));

        // A nested/compiler-generated type's authored outer type ("Outer+<>c" -> "Outer").
        /// <summary>
        /// The verification API is reusable outside
        /// reporting — reporting consumes validation findings, never the reverse. A validation type reaching
        /// into <c>Ihc.Vis.Reporting</c> would invert that and couple the save-gate path to report code.
        /// </summary>
        [Test]
        public void Validation_DoesNotDependOn_Reporting() =>
            AssertNoDependency(Sdk, Subtree(Validation), Reporting,
                "verification is independently reusable: reporting consumes validation, never the reverse");

        /// <summary>
        /// Public report contract types live in the root
        /// <c>Ihc.Vis</c> namespace next to the facade — not inside the internal pipeline namespace. The
        /// <c>typeof</c> references make this self-arming: deleting or moving a contract type breaks the
        /// compile or this assertion, never silently.
        /// </summary>
        [Test]
        public void ReportContractTypes_ResideInRootIhcVis() =>
            Assert.Multiple(() =>
            {
                Assert.That(typeof(ReportKind).Namespace, Is.EqualTo(VisRoot), $"{nameof(ReportKind)} is public API next to the facade");
                Assert.That(typeof(ReportMode).Namespace, Is.EqualTo(VisRoot), $"{nameof(ReportMode)} is public API next to the facade");
                Assert.That(typeof(ReportMimeTypes).Namespace, Is.EqualTo(VisRoot), $"{nameof(ReportMimeTypes)} is public API next to the facade");
                Assert.That(typeof(IReportIconProvider).Namespace, Is.EqualTo(VisRoot), $"{nameof(IReportIconProvider)} is public API next to the facade");
            });

        /// <summary>
        /// Port-surface purity: no <c>Ihc.Vis.Editing</c>/<c>Ihc.Vis.Io</c> type or concrete
        /// <c>ProjectDocumentSession</c> may appear anywhere in <see cref="IProjectDocument"/>'s inherited public
        /// signatures, including delegate payloads, generic arguments and constraints.
        ///
        /// The GUI is banned from depending on those two layers, and <see cref="IProjectDocument"/> is the one
        /// object it holds and drives everything through. So a single future member handing back (say) a
        /// <c>ProjectEditor</c> would deliver the banned layer to the GUI through the front door, legitimately,
        /// while every existing GUI-side rule stayed green — a dependency ban is powerless against a type the
        /// subject is *given*. This closes the port itself rather than policing its callers.
        ///
        /// Signature-level, not dependency-level, on purpose: the implementation may of course use the engine
        /// internally (it IS the session layer). What must not leak is the CONTRACT.
        /// Armed by <see cref="PortSurfaceScan_IsArmed"/>.
        /// </summary>
        [Test]
        public void DocumentPort_ExposesNoEngineTypes()
        {
            var surface = PortSurfaceTypes(typeof(IProjectDocument)).ToList();

            // Vacuity guard: the scan must actually observe the port's own contract types. Without this, a reflection
            // slip that returned nothing would read as a perfectly pure port.
            Assert.That(surface, Does.Contain(typeof(Ihc.Vis.Projects.Project)),
                "the port-surface scan must see the port's real signature types — otherwise its purity verdict is meaningless");

            Assert.That(EngineTypesOn(typeof(IProjectDocument)), Is.Empty,
                "IProjectDocument must not expose editing/IO types or its concrete ProjectDocumentSession implementation");
        }

        /// <summary>Positive control for <see cref="DocumentPort_ExposesNoEngineTypes"/>: the same checker, pointed at
        /// a synthetic port that leaks an editing type through a return and an IO type through a parameter, must
        /// report both — proving the scan inspects returns AND parameters rather than passing because it looked at
        /// neither.</summary>
        [Test]
        public void PortSurfaceScan_IsArmed()
        {
            Assert.That(EngineTypesOn(typeof(ISeededLeakyPort<>)),
                Is.SupersetOf(new[]
                {
                    typeof(ProjectEditor).FullName!,
                    typeof(ProjectSaveOptions).FullName!,
                    typeof(ProjectDocumentSession).FullName!,
                    typeof(global::Ihc.Vis.Editing.Seeded.INestedEngineContract).FullName!,
                }),
                "the port scan must traverse inherited interfaces, custom delegate signatures, generic constraints and nested engine namespaces");
        }

        // Synthetic violator for the control: the two leak shapes a real port regression would take. (The IO leak
        // uses ProjectSaveOptions rather than the ProjectSerializer anchor — the latter is a static class, which C#
        // permits neither as a parameter nor as a return type, so it cannot express a leak at all.)
        private delegate void SeededEngineEvent(ProjectEditor editor);

        private interface ISeededLeakyPort<T> : ISeededLeakyBasePort
            where T : global::Ihc.Vis.Editing.Seeded.INestedEngineContract
        {
            event SeededEngineEvent? Changed;
            global::Ihc.Vis.Editing.Seeded.NestedEngineType Detail { get; }
        }

        private interface ISeededLeakyBasePort
        {
            ProjectSaveOptions Options { get; }
            ProjectDocumentSession Session { get; }
        }

        // Every type named on the inherited public contract surface, recursively expanded by TypeAndArguments.
        private static IEnumerable<Type> PortSurfaceTypes(Type port) =>
            ContractHierarchy(port)
                .SelectMany(DeclaredContractTypes)
                .SelectMany(TypeAndArguments)
                .Distinct();

        private static IEnumerable<Type> ContractHierarchy(Type contract)
        {
            var seen = new HashSet<Type>();
            for (Type? current = contract; current is not null && seen.Add(current); current = current.BaseType)
                yield return current;
            foreach (Type inherited in contract.GetInterfaces())
                if (seen.Add(inherited))
                    yield return inherited;
        }

        private static IEnumerable<Type> DeclaredContractTypes(Type contract) =>
            contract.GetGenericArguments()
                .Where(argument => argument.IsGenericParameter)
                .SelectMany(argument => argument.GetGenericParameterConstraints())
                .Concat(contract.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                                             | BindingFlags.DeclaredOnly)
                    .SelectMany(DeclaredSignatureTypes))
                .Concat(contract.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                                                 | BindingFlags.DeclaredOnly)
                    .SelectMany(DeclaredSignatureTypes));

        private static IEnumerable<Type> DeclaredSignatureTypes(MemberInfo member) => member switch
        {
            PropertyInfo property => property.GetIndexParameters().Select(parameter => parameter.ParameterType)
                .Append(property.PropertyType),
            EventInfo declaredEvent => new[] { declaredEvent.EventHandlerType! },
            MethodInfo method => MethodSignatureTypes(method).Append(method.ReturnType),
            ConstructorInfo constructor => MethodSignatureTypes(constructor),
            FieldInfo field => new[] { field.FieldType },
            _ => Enumerable.Empty<Type>(),
        };

        private static IEnumerable<Type> MethodSignatureTypes(MethodBase method) =>
            method.GetParameters().Select(parameter => parameter.ParameterType)
                .Concat((method is MethodInfo genericMethod
                        ? genericMethod.GetGenericArguments()
                        : Array.Empty<Type>())
                    .Where(argument => argument.IsGenericParameter)
                    .SelectMany(argument => argument.GetGenericParameterConstraints()));

        private static IReadOnlyList<string> EngineTypesOn(Type port) =>
            PortSurfaceTypes(port)
                .Where(type => IsInNamespaceSubtree(type, Editing)
                               || IsInNamespaceSubtree(type, Io)
                               || type == typeof(ProjectDocumentSession))
                .Select(type => type.FullName!)
                .Distinct()
                .ToList();

        /// <summary>
        /// The <c>.vis</c> engine is a pure offline file engine: it must stay independent of the SOAP/controller
        /// stack so that project editing needs neither a controller nor the generated proxies.
        /// </summary>
        [Test]
        public void Vis_DoesNotDependOn_Soap() =>
            AssertNoDependency(Sdk, Subtree(VisRoot), SoapNs,
                "the offline .vis engine must not depend on the controller SOAP stack");

        /// <summary>
        /// The <c>.vis</c> engine sits below the application-service tier (ADR-002): it is reusable without
        /// <c>Ihc.App</c>. Only the <c>ProjectAppService</c> facade — which itself lives in the <c>Ihc.Vis</c> root
        /// namespace, not merely under it — may bridge up to <c>Ihc.App</c>. The rule therefore covers the whole
        /// <c>Ihc.Vis</c> subtree and exempts exactly that one facade <i>type</i>, not the entire root namespace:
        /// other root-namespace engine types (<c>ProjectCommands</c> — the authoring gateway — and
        /// <c>ProjectProjections</c>) must stay app-independent too, and a namespace-level exemption would silently
        /// let them acquire an <c>Ihc.App</c> dependency.
        /// </summary>
        [Test]
        public void VisEngine_DoesNotDependOn_AppLayer() =>
            AssertSubtreeExceptTypeHasNoDependency(Sdk, VisRoot, typeof(ProjectAppService), AppLayer,
                "only the ProjectAppService facade may bridge the offline engine up to the application-service tier");

        /// <summary>
        /// The application-service tier reaches the controller only through the <c>Ihc</c> API-service interfaces
        /// (their private <c>SoapImpl</c> adapters own the generated types); an application service must never
        /// touch the generated <c>Ihc.Soap.*</c> layer directly.
        /// </summary>
        [Test]
        public void AppLayer_DoesNotDependOn_Soap() =>
            AssertNoDependency(Sdk, Subtree(AppLayer), SoapNs,
                "application services must reach the controller through Ihc API-service interfaces, not the generated SOAP types");

        [Test]
        public void ControllerApiPublicContracts_ExposeNoSoapTypes()
        {
            var apiServices = typeof(IIHCApiService).Assembly.GetTypes()
                .Where(type => (type.IsPublic || type.IsNestedPublic)
                               && typeof(IIHCApiService).IsAssignableFrom(type))
                .ToList();
            Assert.That(apiServices, Is.Not.Empty,
                "the public-contract scan must find the high-level controller service interfaces and implementations");

            var offences = apiServices
                .SelectMany(service => SoapTypesOn(service)
                    .Select(soapType => $"{service.FullName} exposes {soapType}"))
                .Distinct()
                .ToList();

            Assert.That(offences, Is.Empty,
                "high-level controller APIs expose SDK models; generated Ihc.Soap artifacts belong only inside private SoapImpl adapters");
        }

        [Test]
        public void ControllerApiPublicContractScan_IsArmed() =>
            Assert.That(SoapTypesOn(typeof(ISeededSoapLeakyContract<>)),
                Does.Contain(typeof(global::Ihc.Soap.Authentication.AuthenticationService).FullName),
                "the public-contract scan must traverse a custom delegate and a generic constraint to a generated SOAP contract");

        private delegate void SeededSoapEvent(global::Ihc.Soap.Authentication.AuthenticationService service);

        private interface ISeededSoapLeakyContract<T>
            where T : global::Ihc.Soap.Authentication.AuthenticationService
        {
            event SeededSoapEvent? Changed;
        }

        private static IReadOnlyList<string> SoapTypesOn(Type contract) =>
            PortSurfaceTypes(contract)
                .Where(type => IsInNamespaceSubtree(type, SoapNs))
                .Select(type => type.FullName!)
                .Distinct()
                .ToList();

        /// <summary>
        /// The controller API-service tier — the <b>exact</b> <c>Ihc</c> root namespace (<c>AuthenticationService</c>,
        /// <c>ControllerService</c>, … and <c>IhcSettings</c>) — sits <i>below</i> the application-service tier: an
        /// API service composes SOAP adapters behind SDK models and must never reach up into an <c>Ihc.App</c>
        /// application service (ADR-002; "API services never know about application services"). The downward
        /// complement of <see cref="AppLayer_DoesNotDependOn_Soap"/>.
        /// </summary>
        [Test]
        public void ApiServiceLayer_DoesNotDependOn_AppLayer() =>
            AssertExactNamespaceHasNoDependency(Sdk, ApiRoot, AppLayer,
                "API services sit below the application-service tier and must not depend on it");

        /// <summary>
        /// The SDK is consumed by Avalonia apps but must never depend on a GUI framework — the layering that lets
        /// view-models stay headlessly testable starts here.
        /// </summary>
        [Test]
        public void Sdk_DoesNotDependOn_Avalonia() =>
            AssertAssemblyHasNoDependency(Sdk, AvaloniaNs,
                "the SDK must stay free of any GUI framework so view-models can be tested headlessly");

        /// <summary>
        /// Mechanical part of invariant 7: the SDK takes no <c>Microsoft.Extensions.Logging</c> dependency.
        /// Observability uses <c>ActivitySource</c>; package-reference policy covers other logging frameworks.
        /// </summary>
        [Test]
        public void Sdk_DoesNotDependOn_MicrosoftExtensionsLogging() =>
            AssertAssemblyHasNoDependency(Sdk, LoggingNs,
                "the SDK must not depend on Microsoft.Extensions.Logging; the host owns that logging stack");

        /// <summary>
        /// Pins the namespace topology the rules above are anchored to. Anchors read <c>typeof(T).Namespace</c>, so a
        /// namespace rename is followed automatically; the gap that leaves is an anchor type <i>moved</i> into a
        /// different existing namespace, silently retargeting its rule to the wrong layer. Asserting each anchor still
        /// resolves to its documented namespace turns that silent retarget into a named failure.
        /// </summary>
        [Test]
        public void LayerAnchors_ResolveToTheirDocumentedNamespaces() =>
            Assert.Multiple(() =>
            {
                foreach (LayerAnchor anchor in LayerAnchors)
                    Assert.That(anchor.Actual, Is.EqualTo(anchor.Expected),
                        $"{anchor.Description} must remain in its documented namespace");
            });

        /// <summary>
        /// A backstop against the whole suite being green because the mechanism is broken rather than because the
        /// rules hold. <c>ProjectAppService</c> depends on <c>Ihc.App</c> by design (it is the facade
        /// <see cref="VisEngine_DoesNotDependOn_AppLayer"/> exempts for exactly that reason), so a rule forbidding
        /// that dependency <b>must</b> be reported as violated. If this stops throwing, ArchUnitNET's dependency
        /// model or the <c>Check()</c> plumbing has silently stopped detecting violations and every green rule above
        /// is suspect.
        /// </summary>
        [Test]
        public void SdkFixture_DetectsKnownDependencyViolation() =>
            AssertDependencyIsDetected(Sdk, typeof(ProjectAppService), AppLayer,
                $"{nameof(ProjectAppService)} depends on {AppLayer} by design; a rule forbidding it must fail — otherwise the fitness function is not detecting violations");
    }
}
