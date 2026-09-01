namespace Ihc.Vis.Problems
{
    /// <summary>
    /// Where a code is in its life. Declared before the first family ships, because retrofitting a lifecycle is
    /// impossible once a catalogue has consumers: a code that was published without a way to say "reserved" can
    /// only be un-published by breaking someone.
    /// <para>
    /// There is deliberately no member meaning REMOVED. That absence is the whole reservation mechanism: a code
    /// that stops being minted keeps its entry, the entry keeps the id occupied, and the catalogue's
    /// duplicate-code invariant is then what refuses to reuse it for a different condition. Reservation needs no
    /// separate reserved-id list precisely because nothing can be deleted.
    /// </para>
    /// <para>
    /// There is also no <c>Deprecated</c>. Deprecation is the courtesy of keeping something working while asking
    /// callers to move off it, and this SDK does not offer compatibility shims one level up either — a code is
    /// minted or it is not.
    /// </para>
    /// </summary>
    public enum ProblemCodeStatus
    {
        /// <summary>Minted and reported normally.</summary>
        Active,

        /// <summary>
        /// No longer minted; the id stays reserved and is never reused for a different condition. A speaking id
        /// that later under-describes its condition is SPLIT and the old id retired, never silently re-pointed —
        /// which is also what makes a rename distinguishable from a removal-plus-addition without a separate
        /// changelog artifact.
        /// </summary>
        Retired,

        /// <summary>
        /// INVESTIGATED, AND NEVER TO BE MINTED. Positive knowledge: the condition was examined and is not a
        /// defect, or the limit it assumes does not exist. The entry exists so nobody re-proposes it — deleting
        /// such a row loses the finding that it is not a finding, and the next person re-derives it.
        /// </summary>
        RuledOut,
    }
}
