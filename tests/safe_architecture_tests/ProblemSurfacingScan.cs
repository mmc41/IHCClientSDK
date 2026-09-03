using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ihc.Tests
{
    /// <summary>
    /// Finds every member that reaches one of the dialog port's problem-presenting members without going through
    /// the helper that tells the workflow's span first.
    ///
    /// <para><b>Why this is scoped by MEMBER and not by port.</b> The dialog port is the application's entire
    /// dialog surface — file pickers, save confirmations, every property editor, the about box. A rule reading
    /// "reaches the port without reaching the report" would indict the ordinary interactive UI along with the
    /// defect and buy an exemption roster longer than the population it polices, at which point the roster stops
    /// being evidence: exempting a file picker proves nothing about telemetry either way. The members that
    /// actually present a fault are a small, stable set whose only legitimate reachers are the two helpers, so
    /// every other site is the thing being looked for.</para>
    ///
    /// <para><b>Why REFERENCES and not calls.</b> At least one of these members is not called anywhere near
    /// where it is used: it is handed to another component as a method group and invoked later, from somewhere
    /// no scan would associate with a workflow. A scan matching invocations sees nothing there. Matching the
    /// member reference catches the hand-off at the point where the decision was actually made — which is also
    /// the only point at which anyone could route it through the report instead.</para>
    /// </summary>
    internal static class ProblemSurfacingScan
    {
        /// <summary>
        /// What the rule is scoped to.
        /// </summary>
        /// <param name="PortTypeName">The dialog port declaring the guarded members.</param>
        /// <param name="Members">The members that present a fault, by name. Overloads share a name on purpose:
        /// the rule is about the member, and every overload of it presents a fault.</param>
        /// <param name="AdmittedTypeNames">The helpers whose whole job is to reach these members after telling
        /// the span. Admitted BY NAME rather than exempted per site, because a new member on one of them is
        /// still the helper doing its job, while a new site anywhere else is the defect.</param>
        internal sealed record Anchors(string PortTypeName, string[] Members, string[] AdmittedTypeNames);

        /// <summary>Every site referencing a guarded member, named by the member it was written in.</summary>
        /// <remarks>
        /// MEMOISED per (assembly, anchors): a full IL decode with a token resolve per operand, asked the same
        /// question by several tests over the same immutable assembly.
        /// </remarks>
        internal static IReadOnlyList<ContainmentSite> Sites(Assembly assembly, Anchors anchors) =>
            Cache.GetOrAdd((assembly, anchors), key => Scan(key.Assembly, key.Anchors));

        private static readonly
            ConcurrentDictionary<(Assembly Assembly, Anchors Anchors), IReadOnlyList<ContainmentSite>>
            Cache = new();

        private static IReadOnlyList<ContainmentSite> Scan(Assembly assembly, Anchors anchors) =>
            [.. AuthoredMembers.Of(assembly)
                .Where(m => !IsAdmitted(m, anchors))
                .Where(m => ReferencesAGuardedMember(m, anchors))
                .Select(ContainmentSite.For)
                .Distinct()
                .OrderBy(site => site.ToString(), StringComparer.Ordinal)];

        /// <summary>True when the body names one of the guarded members, however it names it — a call, or a
        /// method group handed somewhere else. Both are an <c>InlineMethod</c> operand, which is what is read.</summary>
        internal static bool ReferencesAGuardedMember(MethodBase method, Anchors anchors) =>
            IlBody.Instructions(method)
                .Select(instruction => instruction.Called)
                .OfType<MethodBase>()
                .Any(called => anchors.Members.Contains(called.Name, StringComparer.Ordinal)
                               && called.DeclaringType?.FullName == anchors.PortTypeName);

        /// <summary>True for the helpers the rule admits, including anything a lambda inside one compiles to.</summary>
        internal static bool IsAdmitted(MethodBase method, Anchors anchors) =>
            method.DeclaringType?.FullName is { } declaring
            && anchors.AdmittedTypeNames.Contains(
                ArchRuleHelpers.OutermostTypeName(declaring), StringComparer.Ordinal);
    }
}
