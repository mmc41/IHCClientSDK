using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;
using Ihc.App;
using Ihc.Vis;
using Ihc.Vis.Catalog;
using Ihc.Vis.Editing;
using Ihc.Vis.FunctionBlocks;
using Ihc.Vis.Products;
using Ihc.Vis.Programs;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ihc.Tests
{
    /// <summary>
    /// The SDK's directional layering rules, enforced mechanically.
    ///
    /// These rules were previously stated only in comments and doc remarks, which is why they are worth pinning:
    /// a rule that nothing checks holds only for as long as everyone remembers it. ArchUnitNET reads IL (via
    /// Mono.Cecil), so it sees real dependencies (fields, signatures, base types, call targets) and correctly
    /// ignores the <c>&lt;see cref="..."/&gt;</c> doc references that point across these boundaries by design.
    ///
    /// Every layer namespace below is read off a representative <b>public</b> type via <c>typeof(T).Namespace</c>
    /// instead of a string literal. Renaming or moving a layer then breaks <i>this file</i> at compile time,
    /// rather than letting the rule quietly match nothing and pass vacuously — the failure mode a hand-written
    /// namespace string is prone to. (safe_architecture_tests is deliberately not on ihcclient's
    /// <c>InternalsVisibleTo</c> list, so every anchor is a public type; that also keeps these rules pinned to
    /// the public contract.) The only string-literal targets are the third-party namespaces the SDK must never
    /// reference (Avalonia, Microsoft.Extensions.Logging): absent by design, so there is no type to anchor to.
    ///
    /// The assembly is loaded into ArchUnitNET's model once. A forbidden target set is built with
    /// <c>Types(includeReferenced: true)</c> so it also spans types the SDK <i>references</i> without the loader
    /// visiting their assembly — the Avalonia/Logging stubs that only exist at all if a real dependency was
    /// introduced. Without that flag those rules could never fail. The source (constrained) sets stay on the
    /// loaded SDK types only.
    /// </summary>
    [TestFixture]
    public class ArchitectureTests
    {
        // The SDK read into ArchUnitNET's model once for the whole fixture.
        private static readonly Architecture Sdk = new ArchLoader()
            .LoadAssemblies(typeof(IhcSettings).Assembly)
            .Build();

        // Layer namespaces, anchored to public types so a rename fails the compile, not the check silently.
        private static readonly string AppLayer = typeof(AppServiceBase).Namespace!;      // Ihc.App
        private static readonly string VisRoot = typeof(ProjectAppService).Namespace!;    // Ihc.Vis (engine + its facade)
        private static readonly string Editing = typeof(ProjectEditor).Namespace!;        // Ihc.Vis.Editing
        // Ihc.Soap — the parent of the per-service generated namespaces (Ihc.Soap.Authentication, ...). Anchored
        // to a stable generated service contract, then reduced to its parent so the rule covers every service.
        private static readonly string Soap =
            ParentNamespace(typeof(global::Ihc.Soap.Authentication.AuthenticationService).Namespace!);

        // Third-party namespaces the SDK must never pull in. These stay string literals on purpose: the SDK does
        // not (and must not) reference these assemblies, so there is no type to anchor a typeof to — their very
        // absence is what the rule protects.
        private const string AvaloniaNs = "Avalonia";
        private const string LoggingNs = "Microsoft.Extensions.Logging";

        private static string ParentNamespace(string ns)
        {
            int cut = ns.LastIndexOf('.');
            return cut < 0 ? ns : ns.Substring(0, cut);
        }

        // ArchUnitNET's ResideInNamespaceMatching is an un-anchored Regex.IsMatch over the namespace full name.
        // Both helpers therefore anchor with ^ and stop on a namespace boundary ($ or a dot), giving
        // "starts-with at a segment boundary" rather than a loose substring match (so Ihc.Vis never catches an
        // unrelated Ihc.Vistas).

        /// <summary>The namespace itself and everything nested under it (the whole subtree).</summary>
        private static string Subtree(string ns) => "^" + Regex.Escape(ns) + @"($|\.)";

        /// <summary>Only the namespaces nested under <paramref name="ns"/>; the exact root is excluded.</summary>
        private static string NestedNamespacesOf(string ns) => "^" + Regex.Escape(ns) + @"\.";

        /// <summary>The forbidden target set: every type in the <paramref name="namespace"/> subtree, referenced
        /// stubs included, tagged with a readable description for the failure message.</summary>
        private static IObjectProvider<IType> InNamespaceSubtree(string @namespace) =>
            Types(includeReferenced: true).That().ResideInNamespaceMatching(Subtree(@namespace)).As(@namespace);

        /// <summary>Forbidden dependency from one SDK namespace pattern onto another namespace subtree.</summary>
        private static void AssertNoDependency(string fromPattern, string onNamespace, string because)
        {
            IObjectProvider<IType> from = Types().That().ResideInNamespaceMatching(fromPattern);

            // Guard against a vacuous rule: a source pattern that matches nothing would make the rule below pass
            // without checking anything. (The target cannot rot the same way — it is typeof-anchored, so a rename
            // breaks the compile instead.) The rule must be seen to apply to something.
            Assert.That(from.GetObjects(Sdk).Any(), Is.True,
                $"pattern '{fromPattern}' matched no SDK types — this rule would pass vacuously; fix the anchor, not the assert");

            Types().That().Are(from)
                .Should().NotDependOnAny(InNamespaceSubtree(onNamespace))
                .Because(because)
                .Check(Sdk);
        }

        /// <summary>Forbidden dependency from the whole SDK assembly onto an external namespace.</summary>
        private static void AssertAssemblyHasNoDependency(string onNamespace, string because)
        {
            // No vacuity guard: the subject is the whole assembly (never empty), and the target legitimately has
            // no types of its own here — its absence is the point, so a "target populated" check cannot apply.
            Types().Should().NotDependOnAny(InNamespaceSubtree(onNamespace))
                .Because(because)
                .Check(Sdk);
        }

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
        public void DefinitionLayer_DoesNotDependOn_Editing(string definitionNamespace)
        {
            AssertNoDependency(Subtree(definitionNamespace), Editing,
                "the definition layer composes catalog definitions but must not reach back into live-session editing types");
        }

        /// <summary>
        /// The <c>.vis</c> engine is a pure offline file engine: it must stay independent of the SOAP/controller
        /// stack so that project editing needs neither a controller nor the generated proxies.
        /// </summary>
        [Test]
        public void Vis_DoesNotDependOn_Soap()
        {
            AssertNoDependency(Subtree(VisRoot), Soap,
                "the offline .vis engine must not depend on the controller SOAP stack");
        }

        /// <summary>
        /// The <c>.vis</c> engine sub-namespaces sit below the application-service tier (ADR-002): the engine is
        /// reusable without <c>Ihc.App</c>. Only the <c>ProjectAppService</c> facade in the <c>Ihc.Vis</c> root
        /// may bridge up to <c>Ihc.App</c>, which is why this rule targets the sub-namespaces, not the root.
        /// </summary>
        [Test]
        public void VisEngine_DoesNotDependOn_AppLayer()
        {
            AssertNoDependency(NestedNamespacesOf(VisRoot), AppLayer,
                "the offline engine sub-namespaces must not depend on the application-service tier");
        }

        /// <summary>
        /// The application-service tier reaches the controller only through the <c>Ihc</c> API-service interfaces
        /// (their private <c>SoapImpl</c> adapters own the generated types); an application service must never
        /// touch the generated <c>Ihc.Soap.*</c> layer directly.
        /// </summary>
        [Test]
        public void AppLayer_DoesNotDependOn_Soap()
        {
            AssertNoDependency(Subtree(AppLayer), Soap,
                "application services must reach the controller through Ihc API-service interfaces, not the generated SOAP types");
        }

        /// <summary>
        /// The SDK is consumed by Avalonia apps but must never depend on a GUI framework — the layering that lets
        /// view-models stay headlessly testable starts here.
        /// </summary>
        [Test]
        public void Sdk_DoesNotDependOn_Avalonia()
        {
            AssertAssemblyHasNoDependency(AvaloniaNs,
                "the SDK must stay free of any GUI framework so view-models can be tested headlessly");
        }

        /// <summary>
        /// Invariant 7: the SDK takes no logging dependency. Observability is tracing-only via the
        /// <c>ActivitySource</c> in <c>src/config/Telemetry.cs</c>; the host application chooses its logging stack.
        /// </summary>
        [Test]
        public void Sdk_DoesNotDependOn_Logging()
        {
            AssertAssemblyHasNoDependency(LoggingNs,
                "the SDK is logging-free (invariant 7); observability is tracing-only and the host owns logging");
        }
    }
}
