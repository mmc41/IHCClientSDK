using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>One interactive entry point, named the way a reader finds it in the source.</summary>
    internal readonly record struct ContainmentSite(string Type, string Member)
    {
        public override string ToString() => $"{Type}.{Member}";

        /// <summary>The site a reader would go to: the outermost type, and the method a lambda was written
        /// inside. Compiler-generated carriers -- display classes, async state machines -- are named by what they
        /// were written as, because that is what a list entry has to survive a recompile.</summary>
        internal static ContainmentSite For(MethodBase method) =>
            new(ArchRuleHelpers.OutermostTypeName(method.DeclaringType!.FullName!), Authored(method));

        private static string Authored(MethodBase method)
        {
            string name = method.Name;
            if (!name.StartsWith('<'))
            {
                return CarrierMethodName(method.DeclaringType) ?? name;
            }
            // A local function written inside a lambda emits the doubled form "<<Outer>b__0>g__Inner|1_0", so the
            // authored name starts after BOTH angles. Reading from offset 1 there yields "<Outer" — a site name
            // no list entry can ever match.
            int start = name.StartsWith("<<", StringComparison.Ordinal) ? 2 : 1;
            int close = name.IndexOf('>', start);
            return close > start ? name[start..close] + " (lambda)" : name;
        }

        /// <summary>The authored method an async or iterator body came from. Such a body is emitted as MoveNext
        /// on a carrier type named for its method -- "&lt;SendProject&gt;d__42" -- so reading the method name
        /// alone collapses every async body in a type onto one site called MoveNext, which no list entry can
        /// distinguish and no reader can navigate to. A lambda's carrier ("&lt;&gt;c") names no method and is
        /// left to the method-name path above, which is where a lambda's own name lives.</summary>
        private static string? CarrierMethodName(Type? declaring)
        {
            string? simple = declaring?.Name;
            if (simple is null || simple.Length == 0 || simple[0] != '<')
            {
                return null;
            }
            // The SAME doubled form the method-name path above documents: a lambda written inside an async
            // method carries both, as "<<Delete>b__0>d". Reading from offset 1 there yields "<Delete".
            bool lambda = simple.StartsWith("<<", StringComparison.Ordinal);
            int start = lambda ? 2 : 1;
            int close = simple.IndexOf('>', start);
            return close > start ? simple[start..close] + (lambda ? " (lambda)" : string.Empty) : null;
        }
    }

    /// <summary>
    /// A site that is DEBT: it reaches no floor today, a named task will put it on one, and the list may only
    /// shrink. A baseline is not an exemption and the two must never merge — an exemption is permanent and says
    /// "deliberate, because X", a baseline is temporary and says "debt, deleted by task T". Merging them is how a
    /// list stops being read: every entry then looks equally final.
    /// </summary>
    /// <param name="Site">The entry point still owing containment.</param>
    /// <param name="PaidBy">The task that removes this entry. An entry with no owner can never reach empty.</param>
    internal sealed record ContainmentDebt
    {
        internal ContainmentDebt(ContainmentSite site, string paidBy)
        {
            // Enforced here rather than by a test: a test over a list that is empty today would pass without
            // checking anything, and the moment it stops being empty is exactly when the rule has to hold.
            ArgumentException.ThrowIfNullOrWhiteSpace(paidBy);
            Site = site;
            PaidBy = paidBy;
        }

        internal ContainmentSite Site { get; }

        /// <summary>The task that removes this entry.</summary>
        internal string PaidBy { get; }
    }

    /// <summary>
    /// A site that is DELIBERATELY outside the rule, permanently, with the reason stated. Unlike a
    /// <see cref="ContainmentDebt"/> entry, nothing is expected to remove it.
    /// </summary>
    internal sealed record ContainmentExemption
    {
        internal ContainmentExemption(ContainmentSite site, string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            Site = site;
            Reason = reason;
        }

        internal ContainmentSite Site { get; }

        /// <summary>Why this site is outside the rule for good.</summary>
        internal string Reason { get; }
    }
    /// <summary>
    /// The honesty rule every containment gate carries, stated once. An entry naming a site the scan no longer
    /// finds is the list rotting: nobody removed the row after the site went, and the next reader trusts a list
    /// that has stopped describing the code. Shared because two gates asked it in identical words, and a third
    /// list kind will want the same question.
    /// </summary>
    internal static class ContainmentListHonesty
    {
        /// <summary>Asserts no exemption names a site the scan no longer reports.</summary>
        internal static void EveryExemptionStillNamesASite(
            IEnumerable<ContainmentExemption> exemptions, IReadOnlySet<ContainmentSite> sites) =>
            Assert.That(exemptions.Where(x => !sites.Contains(x.Site)).Select(x => x.Site.ToString()), Is.Empty,
                "an exemption for a site that no longer exists is noise that teaches the reader to skip the list");
    }
}
