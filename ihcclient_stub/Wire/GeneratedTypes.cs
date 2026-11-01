using System.Reflection;

namespace Ihc.Stubbing
{
    /// <summary>
    /// What the stub's reflection over the generated SOAP layer takes as generated, and as a value's writable
    /// members, stated once so the operation table, shape defaults and twin translation cannot disagree on either.
    /// </summary>
    internal static class GeneratedTypes
    {
        /// <summary>Whether <paramref name="type"/> is generated: each SOAP service generates its own namespace
        /// under <c>Ihc.Soap</c>.</summary>
        internal static bool IsGenerated(Type type) =>
            type.Namespace?.StartsWith("Ihc.Soap.", StringComparison.Ordinal) == true;

        /// <summary>The public properties a value of <paramref name="type"/> is written through, indexers aside.</summary>
        internal static IEnumerable<PropertyInfo> SettableProperties(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0);
    }
}
