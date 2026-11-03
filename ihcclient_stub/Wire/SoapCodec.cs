using System.Collections.Concurrent;
using System.Reflection;
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
            EmptyListsAsOneNilItem(body, new HashSet<object>(ReferenceEqualityComparer.Instance));
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

        /// <summary>
        /// Gives every empty list in a generated answer one null item, which the serializer writes as one nil
        /// <c>arrayItem</c>: a 3.3.44 controller answers every empty list so, 113 times across seven operations in its
        /// proxy logs, and an answer written as an empty element would hide from the SDK the form it must read. A list
        /// of a value type, such as the integer list whose "none" a controller states as a lone -1, has no nil item and
        /// is left as the answer built it.
        /// </summary>
        private static void EmptyListsAsOneNilItem(object? node, HashSet<object> seen)
        {
            if (node is null || node is string || node.GetType().IsValueType || !seen.Add(node))
            {
                return;
            }
            Type type = node.GetType();
            if (type.Namespace?.StartsWith("Ihc.Soap", StringComparison.Ordinal) != true)
            {
                return;
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                field.SetValue(node, WithOneNilItemIfEmpty(field.FieldType, field.GetValue(node), seen));
            }
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    property.SetValue(node, WithOneNilItemIfEmpty(property.PropertyType, property.GetValue(node), seen));
                }
            }
        }

        private static object? WithOneNilItemIfEmpty(Type declared, object? value, HashSet<object> seen)
        {
            if (value is Array list && declared.IsArray && declared.GetElementType() is { IsValueType: false } itemType)
            {
                if (list.Length == 0)
                {
                    return Array.CreateInstance(itemType, 1);
                }
                foreach (object? item in list)
                {
                    EmptyListsAsOneNilItem(item, seen);
                }
                return value;
            }
            EmptyListsAsOneNilItem(value, seen);
            return value;
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
