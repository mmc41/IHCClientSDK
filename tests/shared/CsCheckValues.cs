using CsCheck;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// The value generators the randomized operation models share.
    ///
    /// <para>Only the VALUES are shared. Each law keeps its own set of operations, because one shared model
    /// would become the union of every law that touches it, and an operation added for one property would
    /// enlarge the state space of all the others. The values are the tedious part and are genuinely common;
    /// the operations are what each law is about.</para>
    /// </summary>
    internal static class CsCheckValues
    {
        /// <summary>
        /// A short name or label. The alphabet is chosen for what it can break rather than for coverage:
        /// Danish letters, because every name this application shows may carry them, and a space, because a
        /// label containing one is what separates a display name from a path segment.
        /// </summary>
        internal static readonly Gen<string> Name =
            Gen.OneOfConst("abcæø 09".ToCharArray()).Array[1, 5].Select(cs => new string(cs));

        /// <summary>
        /// An index into a collection whose size is unknown when the value is generated. It is allowed to run
        /// past the end on purpose, so each law has to say how it folds an out-of-range pick rather than only
        /// ever being handed a valid one.
        /// </summary>
        internal static readonly Gen<int> Pick = Gen.Int[0, 20];
    }
}
