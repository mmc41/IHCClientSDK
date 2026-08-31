using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ihc.Tests
{
    /// <summary>
    /// Finds members that both HANDLE a fault and opt out of coverage measurement.
    ///
    /// <para>The containment surface is defined structurally: a member is on it when it, or the state machine the
    /// compiler generated for it, carries a <c>catch</c>. A named list would have to be maintained; a member with
    /// a catch IS containment, whoever wrote it and whenever they did.</para>
    /// </summary>
    internal static class CoverageOptOutScan
    {
        /// <summary>One assembly and the namespace root its AUTHORED code lives under. The root filter is what
        /// keeps injected build output out of the population — the coverage collector's own static instrumentation
        /// tracker, and a hot-reload weaver's markers, are neither authored here nor bound by this rule.</summary>
        internal sealed record Scope(Assembly Assembly, string AuthoredRoot);

        /// <summary>Members that handle a fault and are excluded from measurement — the shape that deletes the
        /// evidence a containment test depends on, silently and while improving the reported figure.</summary>
        /// <remarks>
        /// The opt-out is tested FIRST although the catch is the more interesting half: three
        /// <c>IsDefined</c> lookups reject nearly every member, whereas <see cref="HandlesAFault"/> reads a method
        /// body and an attribute's state-machine type. Ordered the other way, the expensive predicate runs over
        /// every member of every scoped assembly to feed a filter that then rejects almost all of them.
        /// </remarks>
        internal static IReadOnlyList<ContainmentSite> OptedOut(Scope scope) =>
            Cache.GetOrAdd(scope, Scan);

        private static readonly ConcurrentDictionary<Scope, IReadOnlyList<ContainmentSite>> Cache = new();

        private static IReadOnlyList<ContainmentSite> Scan(Scope scope) =>
            [.. Members(scope)
                .Where(IsExcludedFromCoverage)
                .Where(HandlesAFault)
                .Select(ContainmentSite.For)
                .Distinct()
                .OrderBy(site => site.ToString(), StringComparer.Ordinal)];

        /// <summary>Every authored member in the scope, constructors included.</summary>
        internal static IEnumerable<MethodBase> Members(Scope scope) =>
            AuthoredMembers.Of(scope.Assembly, scope.AuthoredRoot);

        /// <summary>True when the member catches something — directly, or in the state machine an <c>async</c>
        /// body was compiled into, where the catch a reader sees in the source actually lives.</summary>
        internal static bool HandlesAFault(MethodBase method) =>
            Catches(method) ||
            (method is MethodInfo info
             && info.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType is { } machine
             && machine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                is { } moveNext
             && Catches(moveNext));

        private static bool Catches(MethodBase method) =>
            method.GetMethodBody()?.ExceptionHandlingClauses
                .Any(c => c.Flags is ExceptionHandlingClauseOptions.Clause
                                  or ExceptionHandlingClauseOptions.Filter) == true;

        /// <summary>The attribute counts wherever it lands: on the member, on any type enclosing it, or on the
        /// assembly. Checking only the member would leave the cheapest spelling of the opt-out unseen.</summary>
        internal static bool IsExcludedFromCoverage(MethodBase method)
        {
            if (method.IsDefined(typeof(ExcludeFromCodeCoverageAttribute), inherit: false))
            {
                return true;
            }
            for (Type? type = method.DeclaringType; type is not null; type = type.DeclaringType)
            {
                if (type.IsDefined(typeof(ExcludeFromCodeCoverageAttribute), inherit: false))
                {
                    return true;
                }
            }
            return method.DeclaringType?.Assembly.IsDefined(typeof(ExcludeFromCodeCoverageAttribute), inherit: false)
                == true;
        }
    }
}
