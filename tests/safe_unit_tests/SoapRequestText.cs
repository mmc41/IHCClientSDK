using System.Text.RegularExpressions;

using Ihc.Envelope;

namespace Ihc.Tests
{
    /// <summary>
    /// The request envelope a service actually put on the wire, as text to assert against.
    ///
    /// Shared by the mapping fixtures that pin an outbound message byte for byte. Each one drives a service
    /// over a faked SOAP layer, catches the generated message it was handed, and serializes THAT through the
    /// SDK's own serializer - so what is compared is the XML the controller would have received, not a
    /// re-description of it. Writing the envelope out per fixture spelled the message type twice per call and
    /// left every fixture with its own copy of the whitespace rule.
    /// </summary>
    internal static class SoapRequestText
    {
        /// <summary>The serialized request envelope carrying <paramref name="message"/>. The type argument is
        /// inferred, which is the point: written out it appears twice per call.</summary>
        internal static string Of<T>(T message) =>
            Serialization.SerializeXml<RequestEnvelope<T>>(new RequestEnvelope<T>(message));

        /// <summary>
        /// Whitespace removed, for a comparison against a literal indented to read. Kept separate from
        /// <see cref="Of{T}"/> because a fixture that READS the XML rather than comparing it whole wants the
        /// serializer's own output.
        /// </summary>
        /// <remarks>
        /// Whitespace is dropped rather than compared because it is the serializer's, not the contract's: the
        /// fixtures assert which ELEMENTS a message carries and what is in them. Element text in these
        /// messages is identifiers and numbers, so no assertion here turns on a space inside a value.
        /// </remarks>
        internal static string Normalized(string xml) => Regex.Replace(xml, @"\s", "");
    }
}
