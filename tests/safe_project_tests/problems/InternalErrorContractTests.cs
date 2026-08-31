using System;
using System.Linq;
using System.Reflection;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The carrier for a fault in the TOOL, as distinct from a finding about the project.
    ///
    /// <para><b>What these tests hold, and why each matters.</b></para>
    /// <list type="bullet">
    /// <item><description><b><c>Detail</c> is a captured string, never the exception.</b> If the record carried
    /// the <see cref="Exception"/>, a details dialog would read <c>Message</c>/<c>StackTrace</c>/<c>ToString</c>
    /// in the PRESENTATION layer — the exact leak the exception-message scan exists to pin, and it would have to
    /// grow an exemption for a presentation site. Capturing once at the raise site keeps the read count at one
    /// and hands presentation an opaque string it cannot misuse.</description></item>
    /// <item><description><b>No category, no severity, no refused operations, no location.</b> Every one of those
    /// is a statement about project CONTENT, and their absence is the entire point of the type: a crashed rule
    /// says nothing about the project it failed to examine.</description></item>
    /// <item><description><b><c>Origin</c> is declared, not derived.</b> A code's family separates SDK from host,
    /// but a platform fault reaches the application through a host code — so deriving would collapse
    /// <c>Platform</c> into <c>Host</c> and lose the one distinction a support query starts from.</description></item>
    /// <item><description><b>It is a VALUE.</b> The sink de-duplicates by code and detail, so two observations of
    /// the same fault have to compare equal on those without the timestamp being part of the identity.</description></item>
    /// </list>
    /// </summary>
    [TestFixture]
    public sealed class InternalErrorContractTests
    {
        private static InternalError Sample(string detail = "System.InvalidOperationException: boom") =>
            new(new ProblemCode("internal.unexpected"), "Uventet fejl", "the rule threw",
                InternalErrorOrigin.Sdk, detail, DateTimeOffset.UnixEpoch);

        [Test]
        public void NoMemberCarriesAnException()
        {
            var exceptionMembers = typeof(InternalError).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => typeof(Exception).IsAssignableFrom(p.PropertyType))
                .Select(p => p.Name)
                .ToList();

            Assert.That(exceptionMembers, Is.Empty,
                "an exception on this record puts Message/StackTrace/ToString within reach of the presentation "
                + "layer; the capture happens once, at the raise site, and presentation gets an opaque string");
        }

        [Test]
        public void NothingOnItDescribesTheProject()
        {
            var projectMembers = typeof(InternalError).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .Where(name => name is "Category" or "Severity" or "RefusedOperations" or "Location"
                                    or "TargetAttribute" or "Element")
                .ToList();

            Assert.That(projectMembers, Is.Empty,
                "a crashed rule says nothing about the project it failed to examine — the absence of every "
                + "content-describing member is what makes this type not a finding");
        }

        [Test]
        public void TheOriginVocabulary_NamesTheThreeSourcesAFaultCanComeFrom()
        {
            Assert.That(Enum.GetNames<InternalErrorOrigin>(),
                Is.EqualTo(new[] { "Sdk", "Host", "Platform" }).AsCollection,
                "the SDK, the application above it, and the machine underneath — a platform fault surfaces "
                + "through a host code, so it cannot be read back off the code");
        }

        [Test]
        public void TwoObservationsOfTheSameFault_CompareEqual()
        {
            InternalError observed = Sample();
            InternalError observedAgain = Sample();
            InternalError elsewhere = Sample("a different stack");

            Assert.Multiple(() =>
            {
                Assert.That(observedAgain, Is.EqualTo(observed), "it is a value, so the sink can group by it");
                Assert.That(elsewhere, Is.Not.EqualTo(observed),
                    "and the captured detail is part of that value — two different faults are not one row");
            });
        }

        [Test]
        public void ItCarriesBothLanguages_InSeparateSlots()
        {
            InternalError error = Sample();

            Assert.Multiple(() =>
            {
                Assert.That(error.Message, Is.EqualTo("Uventet fejl"), "the Danish the user reads, whole");
                Assert.That(error.Diagnostic, Is.EqualTo("the rule threw"), "the English the log reads");
                Assert.That(error.Origin, Is.EqualTo(InternalErrorOrigin.Sdk));
                Assert.That(error.Observed, Is.EqualTo(DateTimeOffset.UnixEpoch));
            });
        }

        /// <summary>The diagnostic is optional: some faults have a code and a Danish label and nothing more to
        /// say in English.</summary>
        [Test]
        public void TheEnglishDiagnostic_IsOptional()
        {
            var error = new InternalError(new ProblemCode("internal.unexpected"), "Uventet fejl", null,
                InternalErrorOrigin.Platform, "detail", DateTimeOffset.UnixEpoch);

            Assert.That(error.Diagnostic, Is.Null);
        }
    }
}
