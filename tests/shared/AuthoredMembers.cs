using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ihc.Tests
{
    /// <summary>
    /// Every member an assembly declares — methods and constructors, public and private, on nested and
    /// compiler-generated types alike. Shared by the containment gates because all of them ask the same question
    /// of the same population, and each answering it its own way is how two gates come to disagree about what
    /// they cover.
    /// </summary>
    internal static class AuthoredMembers
    {
        private const BindingFlags All =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static IEnumerable<MethodBase> Of(Assembly assembly) => assembly.GetTypes().SelectMany(Of);

        /// <summary>The same, restricted to the namespace root an assembly's AUTHORED code lives under. The filter
        /// is what keeps injected build output out of a rule's population: the coverage collector's static
        /// instrumentation tracker, and a hot-reload weaver's markers, are authored by nobody here and land under
        /// roots of their own.</summary>
        internal static IEnumerable<MethodBase> Of(Assembly assembly, string authoredRoot) =>
            Types(assembly, authoredRoot).SelectMany(Of);

        /// <summary>The authored types themselves, under the same root and the same filter, for a rule that reads
        /// what a type DECLARES — its fields, its attributes — rather than what its members do. Derived from the
        /// members instead, a struct with fields and no constructor would have no member to be found through.</summary>
        internal static IEnumerable<Type> Types(Assembly assembly, string authoredRoot) =>
            assembly.GetTypes()
                .Where(t => t.FullName is { } name
                            && (name.StartsWith(authoredRoot + ".", StringComparison.Ordinal) || name == authoredRoot));

        private static IEnumerable<MethodBase> Of(Type type) =>
            type.GetMethods(All | BindingFlags.DeclaredOnly)
                .Cast<MethodBase>()
                .Concat(type.GetConstructors(All));
    }
}
