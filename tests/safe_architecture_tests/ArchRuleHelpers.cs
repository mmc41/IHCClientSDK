using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.NUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ihc.Tests
{
    /// <summary>
    /// The dependency-rule mechanics shared by the per-assembly architecture fixtures
    /// (<see cref="IhcClientArchitectureTests"/> for the SDK, <see cref="OpenVisualArchitectureTests"/> for the
    /// desktop GUI). Each fixture supplies its own loaded <see cref="Architecture"/> and the layer anchors specific
    /// to that assembly; the namespace-matching and forbidden-dependency logic lives here <b>once</b> so a fix to
    /// (say) the regex anchoring cannot silently rot in one copy while the other keeps the old behaviour.
    ///
    /// ArchUnitNET's <c>ResideInNamespaceMatching</c> is an un-anchored <c>Regex.IsMatch</c> over the namespace
    /// full name, so both matchers anchor with <c>^</c> and stop on a namespace boundary (<c>$</c> or a dot), giving
    /// "starts-with at a segment boundary" rather than a loose substring match (so <c>Ihc.Vis</c> never catches an
    /// unrelated <c>Ihc.Vistas</c>).
    ///
    /// Forbidden target sets are built with <c>Types(includeReferenced: true)</c> so they also span types an
    /// assembly <i>references</i> without the loader visiting their assembly — the Avalonia/Logging/SOAP/XML stubs
    /// that exist in the model at all only if a real dependency was introduced. Without that flag those rules could
    /// never fail. The source (constrained) sets stay on the loaded types only.
    /// </summary>
    internal static class ArchRuleHelpers
    {
        // The generated SOAP layer's parent namespace (Ihc.Soap), anchored to a stable generated service contract
        // then reduced to its parent so the forbidden set covers every per-service namespace (Ihc.Soap.Authentication,
        // ...). Both the SDK's app tier and the GUI must stay off this layer, so the anchor is shared here.
        public static readonly string SoapNs =
            ParentNamespace(typeof(global::Ihc.Soap.Authentication.AuthenticationService).Namespace!);

        // Namespaces the first-party assemblies must never pull in. These stay string literals on purpose: nothing
        // (and nothing that should) references these by design, so there is no type to anchor a typeof to — their
        // very absence is what the rules protect.
        public const string AvaloniaNs = "Avalonia";
        public const string LoggingNs = "Microsoft.Extensions.Logging";
        public const string SystemXmlNs = "System.Xml";

        public static string ParentNamespace(string ns)
        {
            int cut = ns.LastIndexOf('.');
            return cut < 0 ? ns : ns.Substring(0, cut);
        }

        /// <summary>The namespace itself and everything nested under it (the whole subtree).</summary>
        public static string Subtree(string ns) => "^" + Regex.Escape(ns) + @"($|\.)";

        /// <summary>The forbidden target set: every type in the <paramref name="namespace"/> subtree, referenced
        /// stubs included, tagged with a readable description for the failure message.</summary>
        public static IObjectProvider<IType> InNamespaceSubtree(string @namespace) =>
            Types(includeReferenced: true).That().ResideInNamespaceMatching(Subtree(@namespace)).As(@namespace);

        /// <summary>Forbidden dependency from one namespace pattern onto another namespace subtree, checked against
        /// <paramref name="arch"/>.</summary>
        public static void AssertNoDependency(Architecture arch, string fromPattern, string onNamespace, string because) =>
            AssertNoDependency(arch, Types().That().ResideInNamespaceMatching(fromPattern), $"pattern '{fromPattern}'",
                onNamespace, because);

        /// <summary>Forbidden dependency from an explicit source type-set onto a namespace subtree. The general form
        /// behind the other <c>AssertNoDependency</c> shapes: it carries the shared vacuity guard and the
        /// <c>NotDependOnAny</c>/<c>Check</c> mechanic, so a fix here reaches every rule. <paramref name="fromLabel"/>
        /// names the source set in the vacuity-guard message.</summary>
        public static void AssertNoDependency(Architecture arch, IObjectProvider<IType> from, string fromLabel, string onNamespace, string because)
        {
            // Guard against a vacuous rule: a source that matches nothing would make the rule below pass without
            // checking anything. (The target cannot rot the same way — it is typeof-anchored where possible, so a
            // rename breaks the compile instead.) The rule must be seen to apply to something.
            Assert.That(from.GetObjects(arch).Any(), Is.True,
                $"{fromLabel} matched no types in the loaded assembly — this rule would pass vacuously; fix the anchor, not the assert");

            Types().That().Are(from)
                .Should().NotDependOnAny(InNamespaceSubtree(onNamespace))
                .Because(because)
                .Check(arch);
        }

        /// <summary>Forbidden dependency from a whole namespace subtree, <b>except one exempt facade type</b>, onto
        /// another subtree. Covers the exact-root types a plain "nested namespaces only" pattern would silently skip
        /// (e.g. the <c>Ihc.Vis</c> root gateway types that are not the <c>ProjectAppService</c> facade).</summary>
        public static void AssertSubtreeExceptTypeHasNoDependency(Architecture arch, string fromSubtreeNs, System.Type exempt, string onNamespace, string because) =>
            AssertNoDependency(arch, Types().That().ResideInNamespaceMatching(Subtree(fromSubtreeNs)).And().AreNot(exempt),
                $"the '{fromSubtreeNs}' subtree except {exempt.Name}", onNamespace, because);

        /// <summary>Forbidden dependency from an <b>exact</b> namespace (its own types only, not the subtree beneath
        /// it) onto another subtree — for a tier that shares a namespace root with layers it is allowed to sit above.</summary>
        public static void AssertExactNamespaceHasNoDependency(Architecture arch, string fromExactNs, string onNamespace, string because) =>
            AssertNoDependency(arch, Types().That().ResideInNamespace(fromExactNs), $"the exact '{fromExactNs}' namespace",
                onNamespace, because);

        /// <summary>Asserts a <b>known-true</b> type→namespace dependency is reported as a violation, proving the
        /// fitness function can actually fail (guards against a suite that is green because the mechanism is broken).</summary>
        public static void AssertDependencyIsDetected(Architecture arch, System.Type from, string onNamespace, string message) =>
            Assert.That(() => Types().That().Are(from).Should().NotDependOnAny(InNamespaceSubtree(onNamespace)).Check(arch),
                Throws.Exception, message);

        /// <summary>Forbidden dependency from a whole loaded assembly onto an external namespace subtree.</summary>
        public static void AssertAssemblyHasNoDependency(Architecture arch, string onNamespace, string because)
        {
            // No vacuity guard: the subject is the whole assembly (never empty), and the target legitimately has
            // no types of its own here — its absence is the point, so a "target populated" check cannot apply.
            Types().Should().NotDependOnAny(InNamespaceSubtree(onNamespace))
                .Because(because)
                .Check(arch);
        }

        // ---- Name-based dependency scans -------------------------------------------------------------------------
        // The fluent rules above match their forbidden target set inside the loaded architecture. That is the wrong
        // shape when the forbidden types are ones the assembly must NOT reference: because the (correct) code does
        // not reference them, they are absent from its model, the fluent target set is empty, and the rule can only
        // ever pass — it would never begin to fail if a violation were introduced (a false negative). These two
        // scans instead take the forbidden types by full name (typically reflected from a referenced assembly the
        // subject must stay off) and match them against the dependency edges the loader DID record, so the rule is
        // armed even while its target set is legitimately absent from the subject's own model.

        /// <summary>Asserts no type in the <paramref name="fromNamespaceRoot"/> subtree has any dependency onto a
        /// type whose full name is in <paramref name="forbiddenFullNames"/>.</summary>
        public static void AssertNoDependencyOnTypeNames(Architecture arch, string fromNamespaceRoot,
            IReadOnlyCollection<string> forbiddenFullNames, string forbiddenLabel, string because)
        {
            Assert.That(forbiddenFullNames, Is.Not.Empty,
                $"{forbiddenLabel}: the forbidden name set is empty — this rule would pass vacuously; fix how the set is computed, not the assert");

            var offending = OwnTypes(arch, fromNamespaceRoot)
                .SelectMany(t => t.Dependencies, (t, d) => (Origin: t.FullName, Target: d.Target.FullName))
                .Where(e => forbiddenFullNames.Contains(e.Target))
                .Distinct()
                .ToList();

            Assert.That(offending, Is.Empty,
                because + " — offending edges: " + string.Join("; ", offending.Select(e => $"{e.Origin} -> {e.Target}")));
        }

        /// <summary>Asserts no type in the <paramref name="fromNamespaceRoot"/> subtree <b>constructs</b> (a
        /// <c>newobj</c> of) a type whose full name is in <paramref name="forbiddenCtorFullNames"/>. Distinct from
        /// <see cref="AssertNoDependencyOnTypeNames"/>: the subtree may still depend on those types every other way
        /// (receive one from a factory, hold it, pass it on) — only calling their constructor is forbidden, so this
        /// enforces "obtain these from their factory, never <c>new</c> them" without false-positiving on the
        /// legitimate factory-return dependency.</summary>
        public static void AssertDoesNotConstructTypeNames(Architecture arch, string fromNamespaceRoot,
            IReadOnlyCollection<string> forbiddenCtorFullNames, string forbiddenLabel, string because)
        {
            Assert.That(forbiddenCtorFullNames, Is.Not.Empty,
                $"{forbiddenLabel}: the forbidden name set is empty — this rule would pass vacuously; fix how the set is computed, not the assert");

            var constructed = OwnTypes(arch, fromNamespaceRoot)
                .SelectMany(t => t.Dependencies.OfType<MethodCallDependency>())
                .Select(c => c.TargetMember)
                .OfType<MethodMember>()
                .Where(m => m.MethodForm == MethodForm.Constructor)
                .Select(m => m.DeclaringType.FullName)
                .ToList();

            // Self-check: the subtree assuredly constructs SOMETHING, so the newobj detection must observe
            // constructor-call edges. If this ever empties, ArchUnitNET's constructor-call modelling has changed and
            // the assertion below would pass because it stopped seeing constructions, not because none are forbidden.
            Assert.That(constructed, Is.Not.Empty,
                $"{forbiddenLabel}: no constructor-call edges were seen at all — the newobj detection is not working, so this rule cannot be trusted");

            var offending = constructed.Where(forbiddenCtorFullNames.Contains).Distinct().ToList();
            Assert.That(offending, Is.Empty, because + " — constructed directly: " + string.Join(", ", offending));
        }

        // The loaded types the assembly OWNS — those in the given namespace root's subtree — as opposed to the
        // referenced framework/SDK stubs the loader also records. The boundary scans only judge the subject's own code.
        private static IEnumerable<IType> OwnTypes(Architecture arch, string fromNamespaceRoot) =>
            arch.Types.Where(t => t.FullName.StartsWith(fromNamespaceRoot + ".", StringComparison.Ordinal));
    }
}
