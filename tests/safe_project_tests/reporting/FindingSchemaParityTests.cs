using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The published grammar against the declarations it describes.
    ///
    /// <para><b>Why this gate exists at all.</b> <c>ihc_project_findings.xsd</c> is validated against the oracle
    /// corpus at build time, and that catches a document the schema rejects. It cannot catch the opposite: a
    /// schema that has fallen BEHIND the SDK stays green for as long as no oracle happens to exercise the part
    /// that moved. A seventh operation head, or a new argument slot on a rule the corpus never fires, would
    /// produce a valid-looking build and a document no consumer could validate.</para>
    ///
    /// <para><b>Both vocabularies are closed and both are derived from one side.</b> The heads are enumerated in
    /// the schema because <c>CatalogInvariants</c> enforces the same closure, and the schema says so in its own
    /// prose — so the enumeration is a COPY of <see cref="OperationCodes.All"/> and has to be checked like one.
    /// The <c>arg_*</c> attributes are the same shape one level down: the schema lists every slot name the
    /// catalogue declares, and nothing else may appear on a finding.</para>
    ///
    /// <para><b>The slot union is read from the WHOLE catalogue</b>, not from the two finding sections. An export
    /// carries whatever the engine produced, and the engine's internal-fault row is an operation outcome — so
    /// narrowing the derivation to the finding sections would leave the schema free to drop a name that can
    /// still reach a file.</para>
    /// </summary>
    [TestFixture]
    public sealed class FindingSchemaParityTests
    {
        private static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";

        private const string ArgumentPrefix = "arg_";

        /// <summary>
        /// The schema, parsed ONCE. Both gates read the same immutable file, and a second round-trip through
        /// the disk could only produce the same document more slowly.
        /// </summary>
        private static XDocument Schema() => LazySchema.Value;

        private static readonly Lazy<XDocument> LazySchema = new(() =>
            XDocument.Load(Path.Combine(
                TestRepository.RequireRoot(), "ihcclient", "schemas", "ihc_project_findings.xsd")));

        /// <summary>
        /// The enumeration is compared IN ORDER, not as a set. It is read by eye beside
        /// <see cref="OperationCodes.All"/> whenever either moves, and a reordering that no assertion notices is
        /// how the two come to look like different lists to a reviewer while passing a set comparison.
        /// </summary>
        [Test]
        public void TheOperationHeadEnumerationIsTheSdksOwnSixHeadsInOrder()
        {
            ImmutableArray<string> published =
            [
                .. Schema().Descendants(Xs + "simpleType")
                    .Single(type => (string?)type.Attribute("name") == "operationHead")
                    .Descendants(Xs + "enumeration")
                    .Select(value => (string)value.Attribute("value")!),
            ];

            Assert.Multiple(() =>
            {
                Assert.That(published, Is.Not.Empty,
                    "the enumeration could not be read; an empty parse would satisfy nothing below it");
                Assert.That(published, Is.EqualTo(OperationCodes.All.Select(head => head.Value)).AsCollection,
                    "the schema's operationHead enumeration is a copy of OperationCodes.All. A head added to the "
                    + "SDK without being published here makes every export naming it unvalidatable, and a head "
                    + "published here that the SDK does not have documents an operation nothing can refuse");
            });
        }

        /// <summary>
        /// Every <c>arg_*</c> attribute the schema allows is a slot some catalogue entry declares, and every
        /// declared slot has an attribute. Compared as an exact SET both ways, because the two failures are
        /// different and both matter: a missing attribute makes a real export invalid, and a surplus one
        /// documents a slot no rule can fill.
        /// </summary>
        [Test]
        public void TheArgumentAttributesAreExactlyTheSlotsTheCatalogueDeclares()
        {
            ImmutableArray<string> published =
            [
                .. Schema().Descendants(Xs + "attribute")
                    .Select(attribute => (string?)attribute.Attribute("name"))
                    .Where(name => name is not null && name.StartsWith(ArgumentPrefix, StringComparison.Ordinal))
                    .Select(name => name![ArgumentPrefix.Length..])
                    .OrderBy(name => name, StringComparer.Ordinal),
            ];

            ImmutableArray<string> declared =
            [
                .. ProblemCatalog.Current.Entries
                    .SelectMany(entry => entry.Slots)
                    .Select(slot => slot.Name)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal),
            ];

            Assert.Multiple(() =>
            {
                Assert.That(published, Is.Not.Empty, "the schema's arg_ block could not be read");
                Assert.That(published, Is.Unique, "a name declared twice would make the comparison below lie");

                Assert.That(published, Is.EqualTo(declared).AsCollection,
                    "the arg_ vocabulary and the catalogue's declared slots must be the same set. Add the "
                    + "attribute when a rule declares a new slot; remove it when the last declaring row goes");
            });
        }
    }
}
