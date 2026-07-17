using System.Linq;
using System.Reflection;
using NetArchTest.Rules;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The SDK's directional layering rules, enforced mechanically.
    ///
    /// These rules were previously stated only in comments and doc remarks, which is why they are worth pinning:
    /// a rule that nothing checks holds only for as long as everyone remembers it. NetArchTest reads IL, so it
    /// sees real dependencies (fields, signatures, base types, call targets) and correctly ignores the
    /// <c>&lt;see cref="..."/&gt;</c> doc references that point across these boundaries by design.
    /// </summary>
    [TestFixture]
    public class ArchitectureTests
    {
        private static readonly Assembly Sdk = typeof(IhcSettings).Assembly;

        private static void AssertNoDependency(string fromNamespace, string onNamespace, string because)
        {
            // Guard against a vacuous rule: a namespace that matches nothing (renamed, moved, mistyped) would make
            // the rule below pass without checking anything at all. The rule must be seen to apply to something.
            int subjects = Types.InAssembly(Sdk)
                .That().ResideInNamespaceStartingWith(fromNamespace)
                .GetTypes().Count();
            Assert.That(subjects, Is.GreaterThan(0),
                $"no types found in {fromNamespace} — this rule would pass vacuously; fix the namespace, not the assert");

            TestResult result = Types.InAssembly(Sdk)
                .That().ResideInNamespaceStartingWith(fromNamespace)
                .ShouldNot().HaveDependencyOn(onNamespace)
                .GetResult();

            string offenders = string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>());

            Assert.That(result.IsSuccessful, Is.True,
                $"{fromNamespace} must not depend on {onNamespace}: {because}. Offending types: {offenders}");
        }

        /// <summary>
        /// The one-way rule between the definition layer and the editing layer. <c>Editing</c> composes catalog
        /// definitions; the definition layer must not reach back into live-session editing types. This rule is the
        /// reason ProgramBuilder and FbProgramBuilder author the same graph twice (designfix R4) — the duplication
        /// is the cost of keeping it, so the rule itself has to be real.
        /// </summary>
        [Test]
        public void FunctionBlocks_DoesNotDependOn_Editing()
        {
            AssertNoDependency("Ihc.Vis.FunctionBlocks", "Ihc.Vis.Editing",
                "the definition layer must not reach back into live-session editing types");
        }

        /// <summary>
        /// The <c>.vis</c> engine is a pure offline file engine: it must stay independent of the SOAP/controller
        /// stack so that project editing needs neither a controller nor the generated proxies.
        /// </summary>
        [Test]
        public void Vis_DoesNotDependOn_Soap()
        {
            AssertNoDependency("Ihc.Vis", "Ihc.Soap",
                "the offline .vis engine must not depend on the controller SOAP stack");
        }

        /// <summary>
        /// The SDK is consumed by Avalonia apps but must never depend on a GUI framework — the layering that lets
        /// view-models stay headlessly testable starts here.
        /// </summary>
        [Test]
        public void Sdk_DoesNotDependOn_Avalonia()
        {
            TestResult result = Types.InAssembly(Sdk)
                .ShouldNot().HaveDependencyOn("Avalonia")
                .GetResult();

            Assert.That(result.IsSuccessful, Is.True,
                "the SDK must not depend on a GUI framework. Offending types: " +
                string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>()));
        }
    }
}
