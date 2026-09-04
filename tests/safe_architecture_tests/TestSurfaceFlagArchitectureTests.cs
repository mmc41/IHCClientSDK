using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The boundary that makes the test surface safe, enforced rather than promised: the <c>--test</c> flag gates
    /// PUBLICATION and nothing else, so exactly two places in the application may read it — the entry point that
    /// parses it, and the composition root that hands it on as a value.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this is the real enforcement.</b> The behavioural comparison in <c>safe_project_tests</c>
    /// drives one route with the flag and without it and asserts the application reaches the same state. That is
    /// supporting evidence and cannot be more: no finite number of route comparisons rules out a branch on a route
    /// the comparison did not take. This rule is the one that scales, because it asks the opposite question —
    /// not "did behaviour differ here?" but "can anything below the root even SEE the flag?".</para>
    ///
    /// <para><b>Why an IL scan rather than a fluent rule.</b> The subject is a MEMBER, not a type: everything in
    /// the application legitimately depends on <c>Program</c> for the logger factory, the configuration and the
    /// start-up path. A type-level "must not depend on Program" rule would be false; what must not spread is a
    /// read of these three members.</para>
    ///
    /// <para><b>Why the parser and the setter are in the forbidden set too.</b> Anchoring only the getter would
    /// leave two other doors to the same fact — re-parsing the command line, or writing the flag from somewhere
    /// that is not <c>Main</c>. Both are reads of the same decision by another name.</para>
    /// </remarks>
    [TestFixture]
    public class TestSurfaceFlagArchitectureTests
    {
        /// <summary>
        /// The entry point. Reached by NAME rather than by <c>typeof</c> because it is <c>internal</c> and this
        /// suite is deliberately not an <c>InternalsVisibleTo</c> of the application — a rule about who may read a
        /// member should not be the reason a whole assembly's internals are opened to a test project. The throwing
        /// lookup is what keeps a rename loud rather than silently emptying the scan.
        /// </summary>
        private static Type ProgramType =>
            typeof(global::ihc_openvisual.App).Assembly.GetType("ihc_openvisual.Program", throwOnError: true)!;

        /// <summary>The types allowed to know whether the test surface is on.</summary>
        /// <remarks>
        /// <c>Program</c> parses and stores it; <c>App</c> reads it once and passes it on as a constructor
        /// argument. The publisher itself is NOT here, and that is the point — it takes a bool, so it cannot reach
        /// the flag either, and its two states are therefore constructible in a test.
        /// </remarks>
        private static IEnumerable<string> MayReadTheFlag =>
            [ProgramType.FullName!, typeof(global::ihc_openvisual.App).FullName!];

        private static IReadOnlyList<MethodBase> TheFlag()
        {
            const BindingFlags All = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type program = ProgramType;
            PropertyInfo enabled = program.GetProperty("TestSurfaceEnabled", All)
                ?? throw new InvalidOperationException("the test-surface flag was renamed; update this rule");
            MethodInfo parse = program.GetMethod("ParseTestSurfaceEnabled", All)
                ?? throw new InvalidOperationException("the test-surface parser was renamed; update this rule");

            return [enabled.GetGetMethod(nonPublic: true)!, enabled.GetSetMethod(nonPublic: true)!, parse];
        }

        /// <summary>The authored types in <paramref name="assembly"/> whose IL reaches any of those members.</summary>
        private static IReadOnlyList<string> Readers(Assembly assembly, string authoredRoot)
        {
            IReadOnlyList<MethodBase> flag = TheFlag();
            return [.. AuthoredMembers.Of(assembly, authoredRoot)
                .Where(member => IlBody.CalledMethods(member).Any(called => flag.Any(f => f == called)))
                .Select(member => member.DeclaringType?.FullName ?? string.Empty)
                .Distinct()
                .Order(StringComparer.Ordinal)];
        }

        /// <summary>
        /// Guards the guard. A scan that resolved no members, or found no reader at all, would pass for ever while
        /// checking nothing — and the two legitimate readers below are exactly what proves it does not.
        /// </summary>
        [Test]
        public void TheScanCanSeeAReadAtAll()
        {
            Assert.Multiple(() =>
            {
                Assert.That(TheFlag(), Has.Count.EqualTo(3), "the getter, the setter and the parser");
                Assert.That(Readers(ProgramType.Assembly, "ihc_openvisual"), Is.Not.Empty,
                    "the scan found no reader whatsoever, so it cannot fail either");
            });
        }

        [Test]
        public void NothingBelowTheCompositionRootCanSeeTheTestSurfaceFlag()
        {
            Assert.That(Readers(ProgramType.Assembly, "ihc_openvisual"),
                Is.EqualTo(MayReadTheFlag.Order(StringComparer.Ordinal)).AsCollection,
                "a type outside the entry point and the composition root reads the test-surface flag. The flag "
                + "gates PUBLICATION, never BEHAVIOUR: a reader below the root is how it becomes a test-mode "
                + "branch, which is out of scope for this surface and for the product");
        }
    }
}
