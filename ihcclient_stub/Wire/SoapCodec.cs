using System.Collections.Concurrent;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Ihc.Envelope;

namespace Ihc.Stubbing
{
    /// <summary>
    /// Reads requests and writes answers exactly as the SDK writes and reads them. The SDK's serializer is internal
    /// to it, so its configuration is restated here, once: a plain serializer over an envelope does not put the
    /// generated body types in the <c>utcs</c> type namespace, and then neither request members nor response values
    /// match.
    /// </summary>
    internal static class SoapCodec
    {
        private const string SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

        /// <summary>One configured serializer per closed envelope type: the configuration is a per-type constant,
        /// and an <see cref="XmlSerializer"/> built over overrides is never cached by the framework.</summary>
        private static readonly ConcurrentDictionary<Type, XmlSerializer> Serializers = new();

        private static readonly XmlWriterSettings WriterSettings = new()
        {
            OmitXmlDeclaration = true,
            Indent = true,
            Encoding = new UTF8Encoding(false),
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
        };

        /// <summary>The body of a request envelope carrying a <paramref name="bodyType"/>.</summary>
        internal static object? ReadRequest(Type bodyType, string xml) =>
            BodyOf(typeof(RequestEnvelope<>).MakeGenericType(bodyType), xml);

        /// <summary>The body of a response envelope carrying a <paramref name="bodyType"/>.</summary>
        internal static object? ReadResponse(Type bodyType, string xml) =>
            BodyOf(typeof(ResponseEnvelope<>).MakeGenericType(bodyType), xml);

        /// <summary>A response envelope around <paramref name="body"/>, as a controller answers.</summary>
        internal static string WriteResponse(Type bodyType, object body)
        {
            Type envelopeType = typeof(ResponseEnvelope<>).MakeGenericType(bodyType);
            object envelope = Activator.CreateInstance(envelopeType, body)!;
            // The envelope declares its own prefixes; passing them is also what keeps the serializer from adding
            // its default xsi and xsd declarations a second time.
            var prefixes = (XmlSerializerNamespaces)envelopeType.GetProperty(nameof(ResponseEnvelope<object>.Xmlns))!.GetValue(envelope)!;

            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, WriterSettings))
            {
                SerializerFor(envelopeType).Serialize(writer, envelope, prefixes);
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>A SOAP 1.1 server fault carrying <paramref name="faultString"/>, escaped.</summary>
        internal static string Fault(string faultString)
        {
            XNamespace soap = SoapEnvelopeNamespace;
            return new XElement(soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "SOAP-ENV", SoapEnvelopeNamespace),
                new XElement(soap + "Body",
                    new XElement(soap + "Fault",
                        new XElement("faultcode", "SOAP-ENV:Server"),
                        new XElement("faultstring", faultString)))).ToString(SaveOptions.DisableFormatting);
        }

        private static object? BodyOf(Type envelopeType, string xml)
        {
            // Prohibiting DTDs and resolving nothing keeps a test-authored body from expanding entities or fetching.
            using var reader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(reader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            object? envelope = SerializerFor(envelopeType).Deserialize(xmlReader);
            return envelope is null ? null : envelopeType.GetField(nameof(RequestEnvelope<object>.Body))!.GetValue(envelope);
        }

        internal static XmlSerializer SerializerFor(Type envelopeType) =>
            Serializers.GetOrAdd(envelopeType, static type =>
            {
                var overrides = new XmlAttributeOverrides();
                var utcs = new XmlAttributes { XmlType = new XmlTypeAttribute { Namespace = "utcs" } };
                Type[] bodyTypes = type.GetGenericArguments();
                foreach (Type bodyType in bodyTypes)
                {
                    overrides.Add(bodyType, utcs);
                }
                return new XmlSerializer(type, overrides, bodyTypes, null, null);
            });
    }
}
