using CsCheck;
using KellermanSoftware.CompareNetObjects;
using Ihc;
using Ihc.Envelope;
using Ihc.Soap.Authentication;

namespace Ihc.Tests
{
    /// <summary>
    /// Property-based tests for the custom SOAP serialization layer, using CsCheck.
    ///
    /// Unlike the example-based <see cref="SerializeTest"/> (which only uses ASCII
    /// literals), CsCheck generates a wide range of inputs and checks an algebraic
    /// invariant: Deserialize(Serialize(x)) == x. This guards against encoding
    /// regressions such as the UTF-8/ASCII mismatch that previously corrupted
    /// non-ASCII text (e.g. the Danish letters aeoe/AEOE) on the round-trip.
    /// </summary>
    [TestFixture]
    public class SerializationPropertyTests
    {
        // Curated, XML-legal charset: ASCII letters/digits + Danish/Latin-1 letters.
        // Deliberately EXCLUDES XML-illegal control characters and whitespace so the
        // ONLY thing that can break the round-trip is a genuine encoding defect, not
        // XML whitespace/entitization normalization. This is the "valid-input
        // generator" discipline that keeps property failures attributable.
        private const string Alphabet =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789æøåÆØÅéüßñ";

        private static readonly Gen<string> Text =
            Gen.OneOfConst(Alphabet.ToCharArray())
               .Array[1, 40]
               .Select(cs => new string(cs));

        /// <summary>
        /// Invariant: serializing a request and deserializing it back yields an
        /// equal object graph, for any XML-legal text payload.
        /// </summary>
        [Test]
        public void SerializeXml_DeserializeXml_RoundTripsText()
        {
            Text.Sample(username =>
            {
                var original = new RequestEnvelope<inputMessageName2>(
                    new inputMessageName2(new WSAuthenticationData
                    {
                        username = username,
                        password = "b",
                        application = "c"
                    }));

                var xml = Serialization.SerializeXml<RequestEnvelope<inputMessageName2>>(original);
                var roundTripped = Serialization.DeserializeXml<RequestEnvelope<inputMessageName2>>(xml);

                return new CompareLogic().Compare(roundTripped, original).AreEqual;
            });
        }
    }
}
