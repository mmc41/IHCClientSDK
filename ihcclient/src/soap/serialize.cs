using System.Xml.Serialization;
using System;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Concurrent;

using Ihc;

namespace Ihc {
  /// <summary>
  /// Responsible for low-level serialization/deserialization of IHC SOAP requests/responses
  /// so we can communicate with the IHC server without any formal SOAP libraries involved
  /// as this is not (currently) supported by .NET Core.
  /// </summary>
  internal class Serialization {
    // Cache for XmlSerializer instances to prevent memory leak
    // Key is a combination of type and attribute overrides hash
    private static readonly ConcurrentDictionary<string, XmlSerializer> serializerCache = new ConcurrentDictionary<string, XmlSerializer>();

    private static string GetSerializerKey(Type type, XmlAttributeOverrides attrs, Type[]? extraTypes)
    {
        // Create a unique key based on the type and configuration
        var key = type.FullName ?? type.Name;
        if (attrs != null)
        {
            key += "_withAttrs";
        }
        if (extraTypes != null && extraTypes.Length > 0)
        {
            key += "_extraTypes_" + string.Join("_", extraTypes.Select(t => t.FullName ?? t.Name));
        }
        return key;
    }

    // Make sure XmlSerializers are reused to avoid memory leak. See also github issue #2
    private static XmlSerializer GetOrCreateSerializer(Type type, XmlAttributeOverrides attrs, Type[]? extraTypes = null)
    {
      var key = GetSerializerKey(type, attrs, extraTypes);

      return serializerCache.GetOrAdd(key, k =>
      {
        if (extraTypes != null && extraTypes.Length > 0)
        {
          return new XmlSerializer(type, attrs, extraTypes, null, null);
        }
        else if (attrs != null)
        {
          return new XmlSerializer(type, attrs);
        }
        else
        {
          return new XmlSerializer(type);
        }
      });
    }
    // The configured serializer per message type, keyed by the type itself rather than by a rendered string.
    private static readonly ConcurrentDictionary<Type, XmlSerializer> configuredSerializers = new ConcurrentDictionary<Type, XmlSerializer>();

    // The serializer for A, configured the ONE way both directions need. Reading and writing must agree on
    // the "utcs" type namespace for the round trip to hold at all, and a shared invariant kept as two copies
    // of its own setup is one edit away from being false.
    //
    // The override graph is a per-type constant, so it is built once and not per message: every SOAP call
    // reached this, and the key GetOrCreateSerializer builds records only THAT overrides were supplied, never
    // what they said - so the whole graph was being constructed and rendered into a ~200-character key only to
    // land on a serializer already cached. Keying on the Type collapses that to one reference-hash lookup.
    // GetOrCreateSerializer stays the builder underneath: it is the unit under test in SerializationUnitTest.
    private static XmlSerializer BuildSerializer<A>() where A : class {
      return configuredSerializers.GetOrAdd(typeof(A), static type => {
        var attrs = new XmlAttributeOverrides();
        var attr = new XmlAttributes();
        var typ = new XmlTypeAttribute();
        typ.Namespace = "utcs";

        attr.XmlType = typ;
        var genericTypes = type.GetGenericArguments();
        foreach(var genericType in genericTypes)
            attrs.Add(genericType, attr);

        return GetOrCreateSerializer(type, attrs, genericTypes);
      });
    }

    public static string SerializeXml<A>(A x) where A : class {
      try {
        // Retrive ns specifiation from object if it is there and use it
        // explictly for the serializer - as a side effect this will also
        // cause build-in xsi and xsd namespaces to be omitted by default 
        // as we would like for simple requests.
        var optNs = typeof(A).GetProperties()
                    .Where(p => Attribute.IsDefined(p, typeof(XmlNamespaceDeclarationsAttribute)))
                    .Take(1)
                    .Select(p => p.GetValue(x))
                    .FirstOrDefault(p => true) as XmlSerializerNamespaces;


        var xmlSerializer = BuildSerializer<A>();
        var settings = new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = true, Encoding = new UTF8Encoding(false), NamespaceHandling = NamespaceHandling.OmitDuplicates };

        using (var stream = new MemoryStream())
        using (var writer = XmlWriter.Create(stream, settings)) {
            if (optNs!=null) {
                xmlSerializer.Serialize(writer, x, optNs);
            } else {
                xmlSerializer.Serialize(writer, x);
            }

            writer.Flush();
            var result = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            return result;
        }
      } catch (Exception ex) {
        throw new ErrorWithCodeException(Errors.XML_SERIALIZE_ERROR, ex.Message, ex);
      }
    }

    // A parse of a response we do not control: DTD processing off (so no entity expansion or external subset) and no
    // resolver (so no outbound fetch).
    private static readonly XmlReaderSettings SafeReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

    private static XDocument loadSafe(string xml, LoadOptions options = LoadOptions.None) {
      using var reader = new StringReader(xml);
      using var xmlReader = XmlReader.Create(reader, SafeReaderSettings);
      return XDocument.Load(xmlReader, options);
    }

    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";

    /// <summary>
    /// <paramref name="xml"/> without its nil string elements. A controller sends an absent string as
    /// <c>xsi:nil="true"</c>, with a line break inside, also where its schema does not declare the element nillable,
    /// and the generated string members are not nillable, so the serializer would read that text as the value. An
    /// absent element reads as null instead. An array item stays, so an array keeps its count (OpenAPI pairs values by
    /// position, and a lone nil item is how a controller answers an empty list), and a nil member of any other type
    /// stays for the serializer to read as before.
    /// </summary>
    internal static string WithoutNilStrings(string xml) {
      // XML allows whitespace between an attribute's name and its '=', so the shortcut looks for the name alone.
      if (!xml.Contains(":nil", StringComparison.Ordinal))
        return xml;

      XDocument document = loadSafe(xml, LoadOptions.PreserveWhitespace);
      var nilStrings = document.Descendants()
          .Where(element => element.Name.LocalName != "arrayItem" && isNil(element) && isXsdString(element))
          .ToList();
      if (nilStrings.Count == 0)
        return xml;
      foreach (var element in nilStrings)
        element.Remove();
      return document.ToString(SaveOptions.DisableFormatting);
    }

    private static bool isNil(XElement element) =>
        (string?)element.Attribute(Xsi + "nil") is "true" or "1";

    private static bool isXsdString(XElement element) {
      if ((string?)element.Attribute(Xsi + "type") is not { } type)
        return false;
      int colon = type.IndexOf(':', StringComparison.Ordinal);
      XNamespace? typeNamespace = colon < 0 ? element.GetDefaultNamespace() : element.GetNamespaceOfPrefix(type[..colon]);
      return typeNamespace == Xsd && type[(colon + 1)..] == "string";
    }

    private static readonly XNamespace SoapEnvelope = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>
    /// The <c>faultcode</c> and <c>faultstring</c> of a SOAP 1.1 fault in <paramref name="xml"/>'s body, or null when
    /// the text is no SOAP envelope or its body holds no fault. Read without a public envelope type, since a fault is
    /// not a message the generated contracts declare.
    /// </summary>
    internal static (string Code, string Text)? SoapFaultOf(string xml) {
      if (!xml.Contains("Fault", StringComparison.Ordinal))
        return null;
      try {
        if (loadSafe(xml).Root is not { } root || root.Name != SoapEnvelope + "Envelope")
          return null;
        if (root.Element(SoapEnvelope + "Body")?.Element(SoapEnvelope + "Fault") is not { } fault)
          return null;
        // SOAP 1.1 leaves faultcode and faultstring unqualified; a stack that qualifies them is read the same way.
        string ChildText(string name) => fault.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value.Trim() ?? "";
        return (ChildText("faultcode"), ChildText("faultstring"));
      } catch (XmlException) {
        return null;
      }
    }

    public static A? DeserializeXml<A>(string xml) where A : class {
        try {
            var xmlSerializer = BuildSerializer<A>();
            // Read characters straight from the decoded string via a TextReader so no
            // byte re-encoding can corrupt the payload. (Previously this used
            // Encoding.ASCII.GetBytes, which flattened every non-ASCII character - e.g.
            // the Danish letters aeoe/AEOE - to '?'.) A TextReader also ignores any
            // encoding declaration in the XML, since the bytes are already characters.
            // Deserialize over a bare TextReader would leave DTD and resolver handling to the serializer's defaults.
            using (var reader = new StringReader(WithoutNilStrings(xml)))
            using (var xmlReader = XmlReader.Create(reader, SafeReaderSettings))
            {
                var result = xmlSerializer.Deserialize(xmlReader);
                return result as A;
            }
        } catch (Exception ex) {
            throw new ErrorWithCodeException(Errors.XML_DESERIALIZE_ERROR, ex.Message, ex);
        }
    }
  }
}