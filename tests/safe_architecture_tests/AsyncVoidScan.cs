using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Ihc.Tests
{
    /// <summary>
    /// Finds every <c>async void</c> method in an assembly and reports which containment floor it reaches.
    ///
    /// <para><b>Why this reads IL directly rather than using the ArchUnitNET model.</b> That model carries no
    /// compiler-generated types at all, so a state machine's calls are attributed to the AUTHORED member they were
    /// written in — which is exactly right for a type-level rule and exactly wrong here: an <c>async void</c>
    /// LAMBDA's calls then merge with its enclosing method's, and the floor-1 arm below is a statement about the
    /// lambda's own body. The one floor-1 site in the tree today is such a lambda.</para>
    ///
    /// <para>The floor-1 arm is deliberately tight: the handler must AWAIT exactly one thing, and that one thing
    /// must itself be routed through the boundary. "It probably reaches a boundary eventually" is the reading this
    /// shape exists to refuse.</para>
    /// </summary>
    internal static class AsyncVoidScan
    {
        /// <summary>Where a handler's fault is contained, or <see cref="Floor.None"/> when nowhere.</summary>
        internal enum Floor
        {
            /// <summary>Unsupervised: a fault after the first await is raised on the synchronization context with
            /// no caller to catch it.</summary>
            None,

            /// <summary>The view-model's own error boundary, reached through a single awaited call. It reports to
            /// the user as well as recording the fault.</summary>
            ViewModelBoundary,

            /// <summary>The view layer's guard, for handlers with no view-model in reach.</summary>
            HandlerGuard,
        }

        /// <summary>The floors' anchors, so the same scan can be pointed at seeded controls in the test assembly
        /// to prove it detects what it claims to.</summary>
        /// <param name="GuardType">Full name of the view-layer guard type.</param>
        /// <param name="GuardMember">The guard's running member.</param>
        /// <param name="BoundaryType">Full name of the type owning the view-model error boundary.</param>
        /// <param name="BoundaryMember">The boundary member's name.</param>
        internal sealed record Anchors(string GuardType, string GuardMember, string BoundaryType, string BoundaryMember);

        /// <summary>Every <c>async void</c> in <paramref name="assembly"/>, lambdas included, named as a reader
        /// finds them: the outermost type, and the method a lambda was written inside.</summary>
        /// <remarks>
        /// MEMOISED per assembly. The scan is a pure function of an immutable assembly, and every fixture over it
        /// asks for the whole list in several tests — walking every type and decoding every state machine again
        /// each time answers a question whose answer cannot have changed.
        /// </remarks>
        internal static IReadOnlyList<(ContainmentSite Site, MethodInfo Method)> Sites(Assembly assembly) =>
            Cache.GetOrAdd(assembly, Scan);

        private static readonly ConcurrentDictionary<Assembly, IReadOnlyList<(ContainmentSite Site, MethodInfo Method)>>
            Cache = new();

        // Constructors cannot be async void, so restricting the shared population to methods loses nothing.
        private static IReadOnlyList<(ContainmentSite Site, MethodInfo Method)> Scan(Assembly assembly) =>
            [.. AuthoredMembers.Of(assembly)
                .OfType<MethodInfo>()
                .Where(IsAsyncVoid)
                .Select(m => (ContainmentSite.For(m), m))
                .OrderBy(x => x.Item1.ToString(), StringComparer.Ordinal)];

        internal static bool IsAsyncVoid(MethodInfo method) =>
            method.ReturnType == typeof(void) &&
            method.GetCustomAttribute<AsyncStateMachineAttribute>() is not null;

        internal static Floor FloorOf(MethodInfo asyncVoid, Anchors anchors)
        {
            Type stateMachine = asyncVoid.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
            MethodInfo moveNext = stateMachine.GetMethod("MoveNext",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            List<MethodBase> called = [.. IlBody.CalledMethods(moveNext)];

            if (called.Any(c => Is(c, anchors.GuardType, anchors.GuardMember)))
            {
                return Floor.HandlerGuard;
            }

            // Awaited calls only. A synchronous read written as an argument (a static property, say) is a call in
            // IL and is not the call that carries the work, so counting it would refuse a handler the floor model
            // accepts.
            List<MethodBase> awaited = [.. called
                .Where(c => c.DeclaringType?.Assembly == asyncVoid.DeclaringType!.Assembly)
                .Where(IlBody.ReturnsTask)
                .Distinct()];
            return awaited.Count == 1 && RoutesThroughBoundary(awaited[0], anchors)
                ? Floor.ViewModelBoundary
                : Floor.None;
        }

        private static bool RoutesThroughBoundary(MethodBase member, Anchors anchors) =>
            member is MethodInfo method &&
            IlBody.CalledMethods(method).Any(c => Is(c, anchors.BoundaryType, anchors.BoundaryMember));

        private static bool Is(MethodBase called, string typeFullName, string member) =>
            called.Name == member && called.DeclaringType?.FullName == typeFullName;

    }
}
