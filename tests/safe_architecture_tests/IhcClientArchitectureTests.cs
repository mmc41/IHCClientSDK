using System.Collections.Generic;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Ihc.App;
using Ihc.Vis;
using Ihc.Vis.Catalog;
using Ihc.Vis.Editing;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Products;
using Ihc.Vis.Programs;
using static Ihc.Tests.ArchRuleHelpers;

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
        private static readonly Architecture Sdk = new ArchLoader()
            .LoadAssemblies(typeof(IhcSettings).Assembly)
            .Build();

        // Layer namespaces, anchored to public types so a rename fails the compile, not the check silently.
        private static readonly string AppLayer = typeof(AppServiceBase).Namespace!;      // Ihc.App
        private static readonly string ApiRoot = typeof(AuthenticationService).Namespace!;// Ihc (controller API-service tier)
        private static readonly string VisRoot = typeof(ProjectAppService).Namespace!;    // Ihc.Vis (engine + its facade)
        private static readonly string Editing = typeof(ProjectEditor).Namespace!;        // Ihc.Vis.Editing

        /// <summary>
        /// The whole catalog definition layer — every code-authoring/catalog namespace, not just one of them.
        /// Anchored to a representative public type per namespace so this list tracks renames automatically.
        /// </summary>
        private static IEnumerable<string> DefinitionLayerNamespaces()
        {
            yield return typeof(FunctionBlockDefinitionBuilder).Namespace!; // Ihc.Vis.FunctionBlocks
            yield return typeof(ProductDefinitionBuilder).Namespace!;       // Ihc.Vis.Products
            yield return typeof(CatalogReader).Namespace!;                  // Ihc.Vis.Catalog
            yield return typeof(ProgramMethodCatalog).Namespace!;           // Ihc.Vis.Programs
        }

        /// <summary>
        /// The one-way rule between the definition layer and the editing layer. <c>Editing</c> composes catalog
        /// definitions; the definition layer must not reach back into live-session editing types. This rule is the
        /// reason ProgramBuilder and FbProgramBuilder author the same graph twice (designfix R4) — the duplication
        /// is the cost of keeping it, so the rule itself has to be real. It applies to the whole definition layer
        /// (products, function blocks, catalog, programs), not just one namespace.
        /// </summary>
        [TestCaseSource(nameof(DefinitionLayerNamespaces))]
        public void DefinitionLayer_DoesNotDependOn_Editing(string definitionNamespace) =>
            AssertNoDependency(Sdk, Subtree(definitionNamespace), Editing,
                "the definition layer composes catalog definitions but must not reach back into live-session editing types");

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
        /// Invariant 7: the SDK takes no logging dependency. Observability is tracing-only via the
        /// <c>ActivitySource</c> in <c>src/config/Telemetry.cs</c>; the host application chooses its logging stack.
        /// </summary>
        [Test]
        public void Sdk_DoesNotDependOn_Logging() =>
            AssertAssemblyHasNoDependency(Sdk, LoggingNs,
                "the SDK is logging-free (invariant 7); observability is tracing-only and the host owns logging");

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
                Assert.That(AppLayer, Is.EqualTo("Ihc.App"), $"{nameof(AppServiceBase)} anchors the application-service tier");
                Assert.That(ApiRoot, Is.EqualTo("Ihc"), $"{nameof(AuthenticationService)} anchors the API-service tier");
                Assert.That(VisRoot, Is.EqualTo("Ihc.Vis"), $"{nameof(ProjectAppService)} anchors the .vis engine root");
                Assert.That(Editing, Is.EqualTo("Ihc.Vis.Editing"), $"{nameof(ProjectEditor)} anchors the editing layer");
                Assert.That(SoapNs, Is.EqualTo("Ihc.Soap"), "the SOAP parent namespace spans the generated per-service namespaces");
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
        public void Fixture_DetectsAKnownViolation() =>
            AssertDependencyIsDetected(Sdk, typeof(ProjectAppService), AppLayer,
                $"{nameof(ProjectAppService)} depends on {AppLayer} by design; a rule forbidding it must fail — otherwise the fitness function is not detecting violations");
    }
}
