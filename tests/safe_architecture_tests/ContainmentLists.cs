using System;
using System.Reflection;

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
                return name;
            }
            // A local function written inside a lambda emits the doubled form "<<Outer>b__0>g__Inner|1_0", so the
            // authored name starts after BOTH angles. Reading from offset 1 there yields "<Outer" — a site name
            // no list entry can ever match.
            int start = name.StartsWith("<<", StringComparison.Ordinal) ? 2 : 1;
            int close = name.IndexOf('>', start);
            return close > start ? name[start..close] + " (lambda)" : name;
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
}
