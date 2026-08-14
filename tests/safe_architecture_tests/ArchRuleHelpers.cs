using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public static void AssertDependencyIsDetected(Architecture arch, System.Type from, string onNamespace, string message)
        {
            var rule = Types().That().Are(from).Should().NotDependOnAny(InNamespaceSubtree(onNamespace));
            Assert.That(rule.HasNoViolations(arch), Is.False, message);
        }

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
            Assert.That(OwnTypes(arch, fromNamespaceRoot), Is.Not.Empty,
                $"'{fromNamespaceRoot}' matched no owned source types; the rule would pass vacuously");
            Assert.That(forbiddenFullNames, Is.Not.Empty,
                $"{forbiddenLabel}: the forbidden name set is empty — this rule would pass vacuously; fix how the set is computed, not the assert");

            var offending = DependencyEdges(arch, fromNamespaceRoot)
                .Where(e => forbiddenFullNames.Contains(e.Target))
                .ToList();

            Assert.That(offending, Is.Empty,
                because + " — offending edges: " + string.Join("; ", offending.Select(e => $"{e.Origin} -> {e.Target}")));
        }

        /// <summary>Asserts no type in the <paramref name="fromNamespaceRoot"/> subtree <b>constructs</b> (a
        /// <c>newobj</c> of) a type whose full name is in <paramref name="forbiddenCtorFullNames"/>. Distinct from
        /// <see cref="AssertNoDependencyOnTypeNames"/>: the subtree may still depend on those types every other way
        /// (receive one from a factory, hold it, pass it on) — only calling their constructor is forbidden, so this
        /// enforces "obtain these from their factory, never <c>new</c> them" without false-positiving on the
        /// legitimate factory-return dependency.
        ///
        /// <paramref name="exemptBaseConstructorEdges"/> contains exact sanctioned subclass-to-base-constructor
        /// pairs. This permits a replacement control's required base call without exempting other forbidden
        /// constructions written inside that replacement type.</summary>
        public static void AssertDoesNotConstructTypeNames(Architecture arch, string fromNamespaceRoot,
            IReadOnlyCollection<string> forbiddenCtorFullNames, string forbiddenLabel, string because,
            IReadOnlyCollection<ConstructorCallExemption>? exemptBaseConstructorEdges = null)
        {
            Assert.That(forbiddenCtorFullNames, Is.Not.Empty,
                $"{forbiddenLabel}: the forbidden name set is empty — this rule would pass vacuously; fix how the set is computed, not the assert");

            var constructed = ConstructorCallEdges(arch, fromNamespaceRoot)
                .ToList();

            // Self-check: the subtree assuredly constructs SOMETHING, so the newobj detection must observe
            // constructor-call edges. If this ever empties, ArchUnitNET's constructor-call modelling has changed and
            // the assertion below would pass because it stopped seeing constructions, not because none are forbidden.
            Assert.That(constructed, Is.Not.Empty,
                $"{forbiddenLabel}: no constructor-call edges were seen at all — the newobj detection is not working, so this rule cannot be trusted");

            var offending = constructed
                .Where(e => forbiddenCtorFullNames.Contains(e.Target)
                            && exemptBaseConstructorEdges?.Contains(
                                new ConstructorCallExemption(OutermostTypeName(e.Origin), e.Target)) != true)
                .ToList();
            Assert.That(offending, Is.Empty, because + " — constructed directly: "
                + string.Join("; ", offending.Select(e => $"{e.Origin} -> {e.Target}")));
        }

        /// <summary>Asserts no type in the <paramref name="fromNamespaceRoot"/> subtree CALLS one of the named
        /// members of <paramref name="targetTypeFullName"/>. Distinct from the two scans above:
        /// the subtree may legitimately depend on the target type every other way — hold it, call its OTHER
        /// members — and only the named member calls are forbidden (the stateless one-shot facade methods, once
        /// interactive edits must go through the document port).
        ///
        /// Pass <paramref name="targetTypeFullName"/> as <c>null</c> to ban a member NAME on any declaring type —
        /// the shape a blanket ban needs (<c>ConfigureAwait</c> is declared on Task, Task&lt;T&gt;, ValueTask,
        /// ValueTask&lt;T&gt; and the async-enumerable extensions, so enumerating target types would leave holes).
        /// <paramref name="exemptCallSites"/> contains exact authored type-and-member call sites. Async state-machine
        /// edges are mapped back to their authored method, so allowing one method does not exempt its whole type.
        ///
        /// Note the deliberate asymmetry with <see cref="AssertMembersCalledOnlyFrom"/>: that rule REQUIRES the
        /// members to be called (a chokepoint nobody routes through is not being enforced), whereas here zero calls
        /// is the ideal end state and must stay green — so the only vacuity guard is that call detection works at all.
        /// </summary>
        public static void AssertDoesNotCallMembers(Architecture arch, string fromNamespaceRoot,
            string? targetTypeFullName, IReadOnlyCollection<string> forbiddenMemberNames, string forbiddenLabel, string because,
            IReadOnlyCollection<MethodCallExemption>? exemptCallSites = null)
        {
            Assert.That(forbiddenMemberNames, Is.Not.Empty,
                $"{forbiddenLabel}: the forbidden member-name set is empty — this rule would pass vacuously; fix how the set is computed, not the assert");

            var calls = MethodCallEdges(arch, fromNamespaceRoot)
                .ToList();

            // Self-check mirror of the ctor scan: the subtree assuredly calls SOMETHING, so method-call edges must
            // be observed at all — otherwise the modelling changed and a green result would mean "saw nothing".
            Assert.That(calls, Is.Not.Empty,
                $"{forbiddenLabel}: no method-call edges were seen at all — the call detection is not working, so this rule cannot be trusted");

            var offending = calls
                .Where(e => (targetTypeFullName is null || e.TargetType == targetTypeFullName)
                            && forbiddenMemberNames.Contains(e.Member)
                            && exemptCallSites?.Contains(
                                new MethodCallExemption(OutermostTypeName(e.Origin), e.OriginMember)) != true)
                .ToList();

            Assert.That(offending, Is.Empty,
                because + " — offending calls: "
                + string.Join("; ", offending.Select(e =>
                    $"{e.Origin}.{e.OriginMember} -> {e.TargetType}.{e.Member}")));
        }

        /// <summary>A declared type and every type it reaches through indirection, arrays, generic arguments and
        /// constraints, custom delegate signatures, or fields of a user-defined value wrapper. Shared by the GUI
        /// retained-state/purity detectors and the SDK public-contract scans so their type closure cannot drift.</summary>
        public static IEnumerable<Type> TypeAndArguments(Type type)
        {
            var seen = new HashSet<Type>();
            return Expand(type, seen);

            static IEnumerable<Type> Expand(Type candidate, ISet<Type> visited)
            {
                if (candidate.IsByRef || candidate.IsPointer || candidate.IsArray)
                    candidate = candidate.GetElementType()!;
                candidate = Nullable.GetUnderlyingType(candidate) ?? candidate;
                if (!visited.Add(candidate))
                    yield break;

                yield return candidate;

                if (candidate.IsGenericParameter)
                    foreach (Type constraint in candidate.GetGenericParameterConstraints())
                        foreach (Type nested in Expand(constraint, visited))
                            yield return nested;

                if (candidate.IsGenericType)
                    foreach (Type argument in candidate.GetGenericArguments())
                        foreach (Type nested in Expand(argument, visited))
                            yield return nested;

                if (typeof(Delegate).IsAssignableFrom(candidate)
                    && candidate.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance) is { } invoke)
                {
                    foreach (Type signatureType in invoke.GetParameters().Select(parameter => parameter.ParameterType)
                                 .Append(invoke.ReturnType))
                        foreach (Type nested in Expand(signatureType, visited))
                            yield return nested;
                }

                // User-defined value types are copied by value, so retaining one retains the values wrapped inside
                // it. Follow their fields to catch non-generic handles such as ElementView(Project, ProjectElement).
                if (candidate.IsValueType && !candidate.IsPrimitive && !candidate.IsEnum
                    && candidate.Assembly != typeof(object).Assembly)
                    foreach (FieldInfo field in candidate.GetFields(BindingFlags.Instance | BindingFlags.Public
                                                                     | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        foreach (Type nested in Expand(field.FieldType, visited))
                            yield return nested;
            }
        }

        /// <summary>Asserts that inside the <paramref name="fromNamespaceRoot"/> subtree, the named members of
        /// <paramref name="targetTypeFullName"/> are called ONLY from the chokepoint types in
        /// <paramref name="chokepointTypeFullNames"/>. The complement of <see cref="AssertDoesNotCallMembers"/>:
        /// there the members are forbidden outright, here they are legal but must have exactly one caller — the
        /// shape a lifecycle invariant needs ("one owner opens/closes the document"), which no dependency-direction
        /// rule can express. Call origins are normalised to their outermost authored type, so a call made from a
        /// lambda or async body inside the chokepoint still counts as the chokepoint's own.</summary>
        public static void AssertMembersCalledOnlyFrom(Architecture arch, string fromNamespaceRoot,
            string targetTypeFullName, IReadOnlyCollection<string> chokepointMemberNames,
            IReadOnlyCollection<string> chokepointTypeFullNames, string forbiddenLabel, string because)
        {
            Assert.That(chokepointMemberNames, Is.Not.Empty,
                $"{forbiddenLabel}: the member-name set is empty — this rule would pass vacuously; fix how the set is computed, not the assert");
            Assert.That(chokepointTypeFullNames, Is.Not.Empty,
                $"{forbiddenLabel}: the chokepoint set is empty — every call would be an offence; fix how the set is computed, not the assert");

            var relevant = MethodCallEdges(arch, fromNamespaceRoot)
                .Where(e => e.TargetType == targetTypeFullName && chokepointMemberNames.Contains(e.Member))
                .ToList();

            // Vacuity guard: a chokepoint that nobody routes through is not being enforced, it is being ignored. If
            // these members stop being called at all the rule below would go green by seeing nothing — which is
            // exactly how a lifecycle rule rots after a refactor renames or inlines the call.
            Assert.That(relevant, Is.Not.Empty,
                $"{forbiddenLabel}: no calls to these members were seen anywhere in '{fromNamespaceRoot}' — the rule is watching nothing, so its green result is meaningless");

            var offending = relevant
                .Where(e => !chokepointTypeFullNames.Contains(OutermostTypeName(e.Origin)))
                .ToList();

            Assert.That(offending, Is.Empty, because + " — called from outside the chokepoint: "
                + string.Join("; ", offending.Select(e => $"{e.Origin} -> {e.Member}")));
        }

        // The authored type a (possibly compiler-generated) origin belongs to: ArchUnitNET renders nested types as
        // "Outer+Inner", and a call written inside an async body or lambda is emitted on a nested state machine /
        // display class, so the raw origin of a ProjectWorkflow call can read "…ProjectWorkflow+<StartAsync>d__12".
        public static string OutermostTypeName(string fullName)
        {
            int cut = fullName.IndexOfAny(new[] { '+', '/' });
            return cut < 0 ? fullName : fullName.Substring(0, cut);
        }

        // ArchUnitNET member names carry the signature ("Apply(Ihc.Vis.Projects.Project, ...)") and generic arity
        // ("Apply`1(...)"); reduce to the bare method name so callers forbid by the name a reader knows.
        public static IReadOnlyList<(string Origin, string Target)> DependencyEdges(
            Architecture arch, string fromNamespaceRoot) =>
            OwnTypes(arch, fromNamespaceRoot)
                .SelectMany(t => t.Dependencies,
                    (t, dependency) => (Origin: t.FullName, Target: dependency.Target.FullName))
                .Distinct()
                .ToList();

        public static IReadOnlyList<(string Origin, string Target)> ConstructorCallEdges(
            Architecture arch, string fromNamespaceRoot) =>
            ConstructorCallEdgesWithOrigin(arch, fromNamespaceRoot)
                .Select(edge => (edge.Origin, Target: edge.TargetType))
                .Distinct()
                .ToList();

        /// <summary>The same constructor edges, keeping the MEMBER the <c>newobj</c> was written in — for rules
        /// that hold inside one method rather than across a whole type. Origin members are normalised the same way
        /// <see cref="MethodCallEdges"/> normalises them, so a construction written inside an async body or lambda
        /// is still attributed to the method a reader sees.</summary>
        public static IReadOnlyList<(string Origin, string OriginMember, string TargetType)> ConstructorCallEdgesWithOrigin(
            Architecture arch, string fromNamespaceRoot) =>
            OwnTypes(arch, fromNamespaceRoot)
                .SelectMany(t => t.Dependencies.OfType<MethodCallDependency>(),
                    (t, call) => (Origin: t.FullName, OriginMember: call.OriginMember.Name, Member: call.TargetMember))
                .Where(edge => edge.Member is MethodMember { MethodForm: MethodForm.Constructor })
                .Select(edge => (edge.Origin,
                    OriginMember: AuthoredMemberName(edge.Origin, edge.OriginMember),
                    TargetType: edge.Member.DeclaringType.FullName))
                .Distinct()
                .ToList();

        public static IReadOnlyList<(string Origin, string OriginMember, string TargetType, string Member)> MethodCallEdges(
            Architecture arch, string fromNamespaceRoot) =>
            OwnTypes(arch, fromNamespaceRoot)
                .SelectMany(t => t.Dependencies.OfType<MethodCallDependency>(),
                    (t, call) => (Origin: t.FullName, OriginMember: call.OriginMember.Name,
                        TargetMember: call.TargetMember))
                .Select(edge => (edge.Origin,
                    OriginMember: AuthoredMemberName(edge.Origin, edge.OriginMember),
                    TargetType: edge.TargetMember.DeclaringType.FullName,
                    Member: BareMemberName(edge.TargetMember.Name)))
                .Distinct()
                .ToList();

        private static string AuthoredMemberName(string originTypeFullName, string emittedMemberName)
        {
            string nestedName = originTypeFullName.Substring(originTypeFullName.LastIndexOfAny(new[] { '+', '/' }) + 1);
            if (nestedName.StartsWith("<", StringComparison.Ordinal))
            {
                int nameStart = nestedName.StartsWith("<<", StringComparison.Ordinal) ? 2 : 1;
                int close = nestedName.IndexOf('>', nameStart);
                if (close > nameStart)
                    return nestedName.Substring(nameStart, close - nameStart);
            }

            return BareMemberName(emittedMemberName);
        }

        private static string BareMemberName(string memberName)
        {
            int cut = memberName.IndexOfAny(new[] { '(', '`' });
            return cut < 0 ? memberName : memberName.Substring(0, cut);
        }

        // The loaded types the assembly OWNS — those in the given namespace root's subtree — as opposed to the
        // referenced framework/SDK stubs the loader also records. The boundary scans only judge the subject's own code.
        private static IEnumerable<IType> OwnTypes(Architecture arch, string fromNamespaceRoot) =>
            arch.Types.Where(t => t.FullName.StartsWith(fromNamespaceRoot + ".", StringComparison.Ordinal));
    }

    internal readonly record struct ConstructorCallExemption(string OriginTypeFullName, string TargetTypeFullName);

    internal readonly record struct MethodCallExemption(string OriginTypeFullName, string OriginMemberName);
}
