using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Ihc.Tests
{
    /// <summary>
    /// Finds every place a <c>Task</c> is produced and thrown away — <c>_ = SomethingAsync()</c> — which in IL is a
    /// call to a task-returning method whose result is immediately popped.
    ///
    /// <para>A discarded task is never observed, so a fault inside it is raised on the finalizer thread through
    /// <c>TaskScheduler.UnobservedTaskException</c>, arbitrarily later, or not at all. The discard syntax exists
    /// precisely to silence the compiler warning that would otherwise say so, which is why the rule has to be
    /// structural: the code reads as deliberate whether or not anyone thought about the fault.</para>
    ///
    /// <para>Handing the task to a supervisor is NOT a discard: the task becomes an argument, so nothing is
    /// popped. The supervisor is still admitted by name below, so the rule stays true if that hand-off is ever
    /// written as a discard of the supervisor's own return.</para>
    /// </summary>
    internal static class TaskDiscardScan
    {
        /// <summary>The type and member that make a discard supervised.</summary>
        internal sealed record Supervisor(string TypeFullName, string Member);

        /// <summary>Every discarded task in <paramref name="assembly"/>, named by the member it was written in.
        /// Bodies are scanned wholesale — lambdas and async state machines included — because a discard inside a
        /// lambda is a discard.</summary>
        /// <remarks>
        /// MEMOISED per (assembly, supervisor). This is the costliest of the containment scans — a full IL decode
        /// of every member body, with a token resolve per call site — and every fixture over it asks several
        /// tests the same question about the same immutable assembly.
        /// </remarks>
        internal static IReadOnlyList<ContainmentSite> Sites(Assembly assembly, Supervisor supervisor) =>
            Cache.GetOrAdd((assembly, supervisor), key => Scan(key.Assembly, key.Supervisor));

        private static readonly
            ConcurrentDictionary<(Assembly Assembly, Supervisor Supervisor), IReadOnlyList<ContainmentSite>>
            Cache = new();

        private static IReadOnlyList<ContainmentSite> Scan(Assembly assembly, Supervisor supervisor) =>
            [.. AuthoredMembers.Of(assembly)
                .Where(m => DiscardsATask(m, supervisor))
                .Select(ContainmentSite.For)
                .Distinct()
                .OrderBy(site => site.ToString(), StringComparer.Ordinal)];

        internal static bool DiscardsATask(MethodBase method, Supervisor supervisor)
        {
            List<IlBody.Instruction> body = [.. IlBody.Instructions(method)];
            for (int at = 0; at < body.Count; at++)
            {
                if (body[at].Called is not { } called
                    || !IlBody.ReturnsTask(called)
                    || IsSupervised(called, supervisor))
                {
                    continue;
                }
                // A Debug build separates the call from its pop with a nop, so the pair is "the next instruction
                // that does something", not literally the next byte.
                int next = at + 1;
                while (next < body.Count && body[next].Op == OpCodes.Nop)
                {
                    next++;
                }
                if (next < body.Count && body[next].Op == OpCodes.Pop)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsSupervised(MethodBase called, Supervisor supervisor) =>
            called.Name == supervisor.Member && called.DeclaringType?.FullName == supervisor.TypeFullName;
    }
}
