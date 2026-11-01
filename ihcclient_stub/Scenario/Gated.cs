namespace Ihc.Stubbing
{
    /// <summary>
    /// Reads and writes one piece of the stub's state under its gate, so a scenario property states only what is
    /// particular to it, such as the check on a value it is given. Each call takes the gate with the <c>lock</c>
    /// statement, which the compiler refuses to hold across an <c>await</c>. A read-modify-write that spans several
    /// members takes the gate itself, so that one hold covers all of it.
    /// </summary>
    internal static class Gated
    {
        internal static T Read<T>(Lock gate, ref T location)
        {
            lock (gate)
            {
                return location;
            }
        }

        internal static void Write<T>(Lock gate, ref T location, T value)
        {
            lock (gate)
            {
                location = value;
            }
        }

        /// <summary>A copy of <paramref name="list"/>, which a caller reads while answers go on adding to it.</summary>
        internal static IReadOnlyList<T> Copy<T>(Lock gate, List<T> list)
        {
            lock (gate)
            {
                return [.. list];
            }
        }
    }
}
