using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace Ihc.Tests
{
    /// <summary>
    /// R17: the ownership partition is ENFORCED, not documented. Three of its four checks live here, because they
    /// are about assemblies; the fourth — a host family's phrasing against the SDK's standard — lives in the host's
    /// own language pin, where the sentences are.
    ///
    /// <list type="number">
    /// <item><description>The SDK assembly declares no <c>app.*</c> code.</description></item>
    /// <item><description>The GUI assembly declares no SDK-family code.</description></item>
    /// <item><description>A NON-GUI consumer renders an arbitrary SDK problem — code, Danish message, arguments —
    /// with no reference to the GUI assembly.</description></item>
    /// </list>
    ///
    /// <para>The first two keep the partition from eroding one convenient exception at a time. The third is the one
    /// that turns "the business-logic errors are the reusable bulk" from a claim into a measurement: if the SDK's
    /// problem contract could only be rendered by the shell, it would not be reusable, and nothing else in this
    /// suite would notice.</para>
    ///
    /// <para><b>Declared, not merely mentioned.</b> Ownership is about which assembly MINTS a code, so the scan
    /// reads static members that hold one — a <see cref="ProblemCode"/> or a <see cref="RefusalIdentity"/>, which
    /// carries two — and invokes them for their value. The GUI legitimately USES SDK codes (it forwards the SDK's
    /// own delete refusal, for one), and that is not a declaration; a scan that could not tell the two apart would
    /// have to ban the thing D7 exists to permit.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProblemOwnershipArchitectureTests
    {
        private static Assembly Sdk => typeof(ProblemCode).Assembly;

        private static Assembly Gui => typeof(global::ihc_openvisual.App).Assembly;

        private static Assembly Utility => typeof(global::Ihc.download_upload_example.ProblemConsoleFormat).Assembly;

        [Test]
        public void TheSdkAssemblyDeclaresNoHostCode()
        {
            IReadOnlyList<Declaration> hostOwned = [.. DeclaredCodes(Sdk).Where(d => d.Code.IsHostOwned)];

            Assert.Multiple(() =>
            {
                Assert.That(DeclaredCodes(Sdk), Is.Not.Empty, "sanity: the scan finds the SDK's declared codes");
                Assert.That(hostOwned, Is.Empty,
                    "the app.* family is RESERVED for a host: an SDK-declared app.* code would put the SDK inside "
                    + "the space it promised to keep out of");
                Assert.That(ProblemCatalog.Current.Entries.Where(e => e.Code.IsHostOwned), Is.Empty,
                    "and the SDK's catalogue carries no host row either");
            });
        }

        [Test]
        public void TheGuiAssemblyDeclaresNoSdkFamilyCode()
        {
            IReadOnlyList<Declaration> declared = [.. DeclaredCodes(Gui)];

            Assert.Multiple(() =>
            {
                Assert.That(declared, Is.Not.Empty, "sanity: the GUI does declare codes of its own");
                Assert.That(declared.Where(d => !d.Code.IsHostOwned), Is.Empty,
                    "a GUI-declared SDK-family code is a host writing in the SDK's vocabulary — the collision the "
                    + "family scheme exists to make impossible");
            });
        }

        /// <summary>
        /// Check three, in two halves: the utility renders the whole contract, and the utility's assembly does not
        /// reference the GUI's. Either half alone proves nothing — a renderer inside a GUI-referencing tool says
        /// nothing about reusability, and an isolated assembly that renders nothing says nothing at all.
        /// </summary>
        [Test]
        public void ANonGuiConsumerRendersAnArbitrarySdkProblem()
        {
            Problem problem = new(
                new ProblemCode("dataline-address-range"),
                "Klemmenummeret 42 ligger uden for datalinjens område",
                EquatableArray.Create<ProblemArgument>(
                [
                    new ProblemArgument("terminal", 42),
                    new ProblemArgument("dataline", 3),
                ]),
                Diagnostic: "terminal 42 is outside data line 3's range");

            string rendered = global::Ihc.download_upload_example.ProblemConsoleFormat.Describe(problem);

            Assert.Multiple(() =>
            {
                Assert.That(Utility.GetReferencedAssemblies().Select(a => a.Name), Does.Not.Contain(Gui.GetName().Name),
                    "the consumer must not reference the GUI assembly, or it proves nothing about reusability");
                Assert.That(rendered, Does.Contain(problem.Message), "the Danish message");
                Assert.That(rendered, Does.Contain("[dataline-address-range]"), "the code");
                Assert.That(rendered, Does.Contain("terminal=42").And.Contain("dataline=3"), "the declared arguments");
                Assert.That(rendered, Does.Not.Contain(problem.Diagnostic!),
                    "and not the English diagnostic, which belongs in a log line of its own");
                Assert.That(rendered, Does.StartWith(problem.Message),
                    "identity stays subordinate even here — a console reader gets the same rule, at no cost");
            });
        }

        /// <summary>
        /// The controls. Each check above passes when there is nothing to find, so each detector is run against a
        /// SEEDED violator declared in this test assembly and must flag it — otherwise a green result could mean
        /// the scan looked at nothing.
        /// </summary>
        [Test]
        public void TheOwnershipScansAreArmed()
        {
            IReadOnlyList<Declaration> seeded = [.. DeclaredCodes(typeof(SeededDeclarations))];

            Assert.Multiple(() =>
            {
                Assert.That(seeded.Select(d => d.Member), Is.EquivalentTo(
                        new[] { "HostCodeInTheWrongAssembly", "SdkCodeInTheWrongAssembly", "Identity", "Identity" }),
                    "the scan finds a code on a static property, a static field, and BOTH halves of a "
                    + "RefusalIdentity — the three shapes a declaration takes, and the identity carries two codes");
                Assert.That(seeded.Count(d => d.Code.IsHostOwned), Is.EqualTo(1),
                    "check one would have flagged the seeded app.* declaration");
                Assert.That(seeded.Count(d => !d.Code.IsHostOwned), Is.EqualTo(3),
                    "check two would have flagged the seeded SDK-family declarations, the identity's pair included");

                // And the third check's own arming: a renderer that dropped any part of the contract fails it.
                Problem bare = new(new ProblemCode("load-empty"), "Filen er tom", EquatableArray<ProblemArgument>.Empty);
                string rendered = global::Ihc.download_upload_example.ProblemConsoleFormat.Describe(bare);
                Assert.That(rendered, Is.EqualTo("Filen er tom [load-empty]"),
                    "a problem with no arguments renders as message plus code and nothing else");
            });
        }

        /// <summary>One declared code: which member holds it, and what it is.</summary>
        private readonly record struct Declaration(Type Owner, string Member, ProblemCode Code);

        /// <summary>Every code an assembly DECLARES, across every type it holds.</summary>
        private static IReadOnlyList<Declaration> DeclaredCodes(Assembly assembly) =>
            [.. assembly.GetTypes().SelectMany(DeclaredCodes)];

        /// <summary>
        /// Every code one type declares: static properties and fields typed <see cref="ProblemCode"/>, plus both
        /// halves of a static <see cref="RefusalIdentity"/>. Reading the VALUE rather than trusting the member name
        /// is what makes the check about the code space rather than about naming.
        /// </summary>
        private static IEnumerable<Declaration> DeclaredCodes(Type type)
        {
            const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            foreach (PropertyInfo property in type.GetProperties(Statics).Where(p => p.GetIndexParameters().Length == 0))
            {
                foreach (ProblemCode code in CodesIn(property.PropertyType, () => property.GetValue(null)))
                {
                    yield return new Declaration(type, property.Name, code);
                }
            }

            // Auto-property BACKING FIELDS are skipped: they hold the same code the property does, and counting
            // both would report every declaration twice — which showed up first in the armed control, not in the
            // pass/fail of the real scans, exactly the kind of blindness the control exists to surface.
            foreach (FieldInfo field in type.GetFields(Statics)
                .Where(f => !f.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)))
            {
                foreach (ProblemCode code in CodesIn(field.FieldType, () => field.GetValue(null)))
                {
                    yield return new Declaration(type, field.Name, code);
                }
            }
        }

        private static IEnumerable<ProblemCode> CodesIn(Type memberType, Func<object?> read)
        {
            if (memberType == typeof(ProblemCode))
            {
                if (read() is ProblemCode code && code.Value is { Length: > 0 })
                {
                    yield return code;
                }
            }
            else if (memberType == typeof(RefusalIdentity))
            {
                if (read() is RefusalIdentity identity)
                {
                    yield return identity.Operation;
                    yield return identity.Cause;
                }
            }
        }

        /// <summary>
        /// The seeded violators, in this test assembly where they can do no harm: one host code declared as if by
        /// the SDK, and two SDK-family codes declared as if by a host — one of them inside a
        /// <see cref="RefusalIdentity"/>, which is the shape a scan looking only for <see cref="ProblemCode"/>
        /// members would miss.
        /// </summary>
        private static class SeededDeclarations
        {
            public static ProblemCode HostCodeInTheWrongAssembly { get; } = new("app.openvisual.seeded");

            public static readonly ProblemCode SdkCodeInTheWrongAssembly = new("edit.seeded");

            public static RefusalIdentity Identity { get; } =
                new(OperationCodes.Load, OperationCodes.LoadLabel, new ProblemCode("load-empty"), "Filen er tom");
        }
    }
}
